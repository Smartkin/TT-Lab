# Blender tools to work with TwinTech Lab(TT-Lab) files
# Copyright (C) 2025 Smyshliaev "Smartkin" Vladislav
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

"""The socket to a running TT Lab, without Blender: read on the main thread whenever messages are taken (the link's timer does it five times
a second), sent on the caller's thread. No thread of its own: Blender 4.5 crashed when another thread ran Python while the main thread ran
an operator in a temp_override (saving the blend file, an undo step). Answers go to the callbacks they were asked with."""

import select
import socket
import threading
import typing

from . import link_protocol

Callback = typing.Callable[[typing.Dict[str, typing.Any], bytes], None]


class Client:
    def __init__(self, port: int, timeout: float = 2.0, host: str = "127.0.0.1"):
        self._socket = socket.create_connection((host, port), timeout=timeout)
        self._socket.settimeout(None)
        self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self._lock = threading.Lock()
        self._decoder = link_protocol.Decoder()
        self._callbacks: typing.Dict[int, Callback] = {}
        self._next_id = 1
        self.closed = False
        self.error = ""

    def _read(self) -> typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        """The frames that came since the last look, without waiting for more."""
        frames: typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]] = []
        while not self.closed:
            try:
                readable, _, _ = select.select([self._socket], [], [], 0)
                if not readable:
                    break

                data = self._socket.recv(65536)
            except (BlockingIOError, InterruptedError):
                break
            except (OSError, ValueError) as error:
                self.error = str(error)
                self.closed = True
                break

            if not data:
                self.closed = True
                break

            try:
                frames.extend(self._decoder.feed(data))
            except link_protocol.ProtocolError as error:
                self.error = str(error)
                self.closed = True

        return frames

    def send(self, header: typing.Dict[str, typing.Any], payload: bytes = b"", answered: typing.Optional[Callback] = None) -> int:
        """Sends the message, its answer goes to the callback when the main thread takes it. Returns its id."""
        with self._lock:
            identifier = self._next_id
            self._next_id += 1
            if answered is not None:
                self._callbacks[identifier] = answered

        frame = link_protocol.encode(dict(header, id=identifier), payload)
        try:
            with self._lock:
                self._socket.sendall(frame)
        except OSError as error:
            self.error = str(error)
            self.closed = True

        return identifier

    def take(self) -> typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        """The messages that came that answer nothing, the answers handed to their callbacks."""
        pushed = []
        for header, payload in self._read():
            answered = header.get("re")
            with self._lock:
                callback = self._callbacks.pop(answered, None) if isinstance(answered, int) else None

            if callback is not None:
                callback(header, payload)
            else:
                pushed.append((header, payload))

        return pushed

    def ask(self, header: typing.Dict[str, typing.Any], payload: bytes = b"", timeout: float = 10.0) -> typing.Optional[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        """Sends the message and waits for its answer, none when it doesn't come in time. Only where nothing else takes the answers."""
        import time

        answers = []
        self.send(header, payload, answered=lambda answer, answer_payload: answers.append((answer, answer_payload)))
        end = time.time() + timeout
        while not answers and not self.closed and time.time() < end:
            self.take()
            time.sleep(0.02)

        self.take()
        return answers[0] if answers else None

    def pending(self) -> int:
        """How many answers haven't come yet."""
        with self._lock:
            return len(self._callbacks)

    def close(self) -> None:
        self.closed = True
        try:
            self._socket.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass

        self._socket.close()
