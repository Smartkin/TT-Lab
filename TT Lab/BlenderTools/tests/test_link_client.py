"""The socket to TT Lab against a server standing in for it, without Blender."""

import socket
import threading
import time
import unittest

from fakes import load_addon_module

link_protocol = load_addon_module("link_protocol")
link_client = load_addon_module("link_client")


class FakeTTLab:
    """Answers every request with its type and payload's length, pushes a ping first."""

    def __init__(self):
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.bind(("127.0.0.1", 0))
        self.server.listen(1)
        self.port = self.server.getsockname()[1]
        self.received = []
        self.thread = threading.Thread(target=self._serve, daemon=True)
        self.thread.start()

    def _serve(self):
        connection, _ = self.server.accept()
        connection.sendall(link_protocol.encode({"type": "ping", "id": 1}))
        decoder = link_protocol.Decoder()
        while True:
            data = connection.recv(65536)
            if not data:
                break

            for header, payload in decoder.feed(data):
                self.received.append((header, payload))
                if header["type"] == "bye":
                    connection.close()
                    return

                connection.sendall(link_protocol.encode({"type": "ok", "id": 2, "re": header["id"], "asked": header["type"]}, payload[:3]))

    def close(self):
        self.server.close()


def _wait(condition, seconds=5.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        time.sleep(0.01)


class ClientTests(unittest.TestCase):
    def test_answers_go_to_their_callbacks_and_pushes_are_taken(self):
        fake = FakeTTLab()
        client = link_client.Client(fake.port)
        answers = []
        client.send({"type": "hello", "token": "t"}, answered=lambda header, payload: answers.append((header["asked"], payload)))
        client.send({"type": "update_picture", "uri": "res://x"}, b"\x89PNG....", answered=lambda header, payload: answers.append((header["asked"], payload)))
        pushed = []
        _wait(lambda: pushed.extend(client.take()) or len(answers) == 2)

        self.assertEqual([("hello", b""), ("update_picture", b"\x89PN")], answers)
        self.assertEqual(["ping"], [header["type"] for header, _ in pushed])
        self.assertEqual([1, 2], [header["id"] for header, _ in fake.received])

        # Read when it's taken, by the main thread: the socket closing too
        client.send({"type": "bye"})
        _wait(lambda: client.take() is not None and client.closed)
        self.assertTrue(client.closed)
        client.close()
        fake.close()

    def test_nothing_is_read_on_another_thread(self):
        fake = FakeTTLab()
        client = link_client.Client(fake.port)
        before = threading.active_count()
        answers = []
        client.send({"type": "hello"}, answered=lambda header, payload: answers.append(header))
        time.sleep(0.2)
        # Unread until taken
        self.assertEqual([], answers)
        self.assertEqual(before, threading.active_count())
        _wait(lambda: client.take() is not None and answers)
        self.assertEqual(["hello"], [answer["asked"] for answer in answers])
        client.close()
        fake.close()

    def test_a_tt_lab_that_isnt_there_is_no_client(self):
        server = socket.socket()
        server.bind(("127.0.0.1", 0))
        port = server.getsockname()[1]
        server.close()

        with self.assertRaises(OSError):
            link_client.Client(port, timeout=0.5)


if __name__ == "__main__":
    unittest.main()
