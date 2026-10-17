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

"""The link to a running TT Lab, without Blender: how its messages go over the socket and where TT Lab says how to reach it.

A message is a frame: the header's length and the payload's length (unsigned 32 bit little endian), the header (JSON in UTF-8: its
`type`, its `id` and, for an answer, the `re` of the message it answers) and the payload (a model file, a picture), see LINK.md. A
running TT Lab writes a discovery file per process into its state folder: the port and token to connect with and the project it has
open."""

import functools
import json
import ntpath
import os
import posixpath
import struct
import sys
import typing

PROTOCOL = 1
# A header past this is no header of TT Lab's
MOST_HEADER = 16 * 1024 * 1024
MOST_PAYLOAD = 1024 * 1024 * 1024
_LENGTHS = struct.Struct("<II")


class ProtocolError(Exception):
    pass


def encode(header: typing.Dict[str, typing.Any], payload: bytes = b"") -> bytes:
    text = json.dumps(header, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return _LENGTHS.pack(len(text), len(payload)) + text + bytes(payload)


class Decoder:
    """Frames out of the bytes as they come."""

    def __init__(self) -> None:
        self._buffer = bytearray()

    def feed(self, data: bytes) -> typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        self._buffer.extend(data)
        frames = []
        while len(self._buffer) >= _LENGTHS.size:
            header_length, payload_length = _LENGTHS.unpack_from(self._buffer, 0)
            if header_length > MOST_HEADER or payload_length > MOST_PAYLOAD:
                raise ProtocolError("a frame of a %d byte header and a %d byte payload isn't TT Lab's" % (header_length, payload_length))

            end = _LENGTHS.size + header_length + payload_length
            if len(self._buffer) < end:
                break

            try:
                header = json.loads(bytes(self._buffer[_LENGTHS.size:_LENGTHS.size + header_length]).decode("utf-8"))
            except (UnicodeDecodeError, ValueError) as error:
                raise ProtocolError("a header that isn't JSON: %s" % error) from error

            if not isinstance(header, dict) or not isinstance(header.get("type"), str):
                raise ProtocolError("a header without a type")

            frames.append((header, bytes(self._buffer[_LENGTHS.size + header_length:end])))
            del self._buffer[:end]

        return frames


def state_folders(environment: typing.Optional[typing.Mapping[str, str]] = None, platform: str = sys.platform) -> typing.List[str]:
    """The folders TT Lab may keep its discovery files in: its state folder's blender folder, as paths of the platform. A Blender of the
    Flatpak has its own XDG_STATE_HOME, TT Lab's is the host's."""
    environment = os.environ if environment is None else environment
    if platform.startswith("win"):
        local = environment.get("LOCALAPPDATA") or ntpath.join(os.path.expanduser("~"), "AppData", "Local")
        return [ntpath.join(local, "TT Lab", "Blender")]

    home = environment.get("HOME") or os.path.expanduser("~")
    folders = []
    state = environment.get("XDG_STATE_HOME", "")
    if state and posixpath.isabs(state) and "/.var/app/" not in state:
        folders.append(posixpath.join(state, "tt-lab", "blender"))

    folders.append(posixpath.join(home, ".local", "state", "tt-lab", "blender"))
    return list(dict.fromkeys(folders))


def discovery_files(folders: typing.Sequence[str]) -> typing.List[typing.Dict[str, typing.Any]]:
    """What the running TT Labs say about themselves, the newest first. A file of a process that's gone is left out."""
    found = []
    for folder in folders:
        try:
            names = os.listdir(folder)
        except OSError:
            continue

        for name in names:
            if not (name.startswith("link-") and name.endswith(".json")):
                continue

            path = os.path.join(folder, name)
            try:
                with open(path, encoding="utf-8") as file:
                    data = json.load(file)

                modified = os.path.getmtime(path)
            except (OSError, ValueError):
                continue

            if isinstance(data, dict) and data.get("protocol") == PROTOCOL and _is_running(data.get("pid")):
                data["_modified"] = modified
                found.append(data)

    return sorted(found, key=lambda data: data["_modified"], reverse=True)


def _is_running(pid: typing.Any) -> bool:
    if not isinstance(pid, int) or pid <= 0:
        return False

    if sys.platform.startswith("win"):
        return _is_running_on_windows(pid)

    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False

    return True


# OpenProcess's right to read a process's exit code, the exit code of a process still running and the error of another user's process
_PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
_STILL_ACTIVE = 259
_ERROR_ACCESS_DENIED = 5


@functools.lru_cache(maxsize=None)
def _kernel32() -> typing.Any:
    import ctypes
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.GetExitCodeProcess.argtypes = (wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD))
    kernel32.GetExitCodeProcess.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = (wintypes.HANDLE,)
    kernel32.CloseHandle.restype = wintypes.BOOL
    return kernel32


def _is_running_on_windows(pid: int) -> bool:
    """Whether the process is there, asked for its exit code: Windows has no signal 0, os.kill ends the process with any other. A TT Lab
    that crashed left its discovery file, which was taken for a running TT Lab's."""
    import ctypes
    from ctypes import wintypes

    if pid > 0xFFFFFFFF:
        return False

    kernel32 = _kernel32()
    handle = kernel32.OpenProcess(_PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        # Another user's process is there, a process id of none isn't
        return ctypes.get_last_error() == _ERROR_ACCESS_DENIED

    try:
        code = wintypes.DWORD()
        return bool(kernel32.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == _STILL_ACTIVE
    finally:
        kernel32.CloseHandle(handle)


# What starts TT Lab, a record TT Lab leaves in its state folder when it runs (BlenderLinkServer.ExecutableFile)
EXECUTABLE_FILE = "tt-lab.json"


def any_tt_lab(folders: typing.Optional[typing.Sequence[str]] = None) -> typing.Optional[typing.Dict[str, typing.Any]]:
    """A running TT Lab whatever project it has open, the newest."""
    found = discovery_files(folders if folders is not None else state_folders())
    return found[0] if found else None


def tt_lab_command(folders: typing.Optional[typing.Sequence[str]] = None, inside_flatpak: typing.Optional[bool] = None) -> typing.Optional[typing.List[str]]:
    """What starts the TT Lab that ran last, none when no TT Lab ever ran (or its executable is gone). A Blender of the Flatpak starts it on
    the host."""
    if inside_flatpak is None:
        inside_flatpak = os.path.exists("/.flatpak-info")

    for folder in folders if folders is not None else state_folders():
        try:
            with open(os.path.join(folder, EXECUTABLE_FILE), encoding="utf-8") as file:
                data = json.load(file)
        except (OSError, ValueError):
            continue

        command = data.get("command") if isinstance(data, dict) else None
        if not isinstance(command, list) or not command or not all(isinstance(part, str) and part for part in command):
            continue

        if inside_flatpak:
            return ["flatpak-spawn", "--host"] + command

        if os.path.exists(command[0]):
            return command

    return None


def tt_lab_of(project_root: str, project_uuid: str, folders: typing.Optional[typing.Sequence[str]] = None) -> typing.Optional[typing.Dict[str, typing.Any]]:
    """The running TT Lab with the project open: by its UUID, else by its folder."""
    wanted = os.path.normcase(os.path.abspath(project_root)) if project_root else ""
    for data in discovery_files(folders if folders is not None else state_folders()):
        project = data.get("project")
        if not isinstance(project, dict):
            continue

        if project_uuid and project.get("uuid") == project_uuid:
            return data

        path = project.get("path")
        if wanted and isinstance(path, str) and os.path.normcase(os.path.abspath(path)) == wanted:
            return data

    return None
