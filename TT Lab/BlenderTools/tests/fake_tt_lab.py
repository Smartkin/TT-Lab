"""A TT Lab stood in for by a process of its own, for the add-on's Blender tests (blender_link.py, blender_use_blend.py): it answers the
link the way TT Lab does, keeps what it got and can hold, refuse and push answers. The tests drive it through FakeTTLab, over its standard
input and output.

A process of its own, like TT Lab: Blender 4.5's Python (3.11) takes another thread running Python for the main thread holding the
interpreter, so operators run under temp_override called into Python without it while the fake's threads answered, and Blender crashed
about one run in ten."""
import base64
import hashlib
import importlib.util
import json
import os
import shutil
import socket
import struct
import subprocess
import sys
import threading
import typing
import zlib

LINK_PROTOCOL = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "twin_tech_tools", "link_protocol.py")
TOKEN = "secret"


class FakeTTLab:
    """The tests' end: starts the fake TT Lab, which writes its discovery file into the state folder, and asks it what it got."""

    def __init__(self, state: str, project_root: str, uuid: str, package: str, addon_version: str, link_revision: str = "r0",
                 link_materials: typing.Optional[typing.List[typing.Dict[str, typing.Any]]] = None, write_revision: typing.Optional[str] = None):
        self.material = (link_materials or [{}])[0].get("data")
        config = {"state": state, "project_root": project_root, "uuid": uuid, "package": package, "addon_version": addon_version,
                  "link_revision": link_revision, "link_materials": link_materials or [], "write_revision": write_revision}
        python = sys.executable if "python" in os.path.basename(sys.executable).lower() else shutil.which("python3")
        self._process = subprocess.Popen([python, "-I", os.path.abspath(__file__), json.dumps(config)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                         text=True, bufsize=1)
        self.port = json.loads(self._process.stdout.readline())["port"]
        self._received: typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]] = []

    def _ask(self, command: typing.Dict[str, typing.Any]) -> typing.Dict[str, typing.Any]:
        self._process.stdin.write(json.dumps(command) + "\n")
        self._process.stdin.flush()
        line = self._process.stdout.readline()
        if not line:
            raise RuntimeError("The fake TT Lab quit")

        return json.loads(line)

    def _sync(self) -> None:
        for header, payload in self._ask({"cmd": "since", "from": len(self._received)})["messages"]:
            self._received.append((header, base64.b64decode(payload)))

    @property
    def received(self) -> typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        self._sync()
        return self._received

    def of(self, kind: str) -> typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        return [(header, payload) for header, payload in self.received if header["type"] == kind]

    def push(self, header: typing.Dict[str, typing.Any], payload: bytes = b"") -> None:
        self._ask({"cmd": "push", "header": header, "payload": base64.b64encode(payload).decode("ascii")})

    def hold(self, kind: str) -> None:
        """Answers of the kind wait until they're released."""
        self._ask({"cmd": "hold", "kind": kind})

    def release(self, kind: str) -> None:
        self._ask({"cmd": "release", "kind": kind})

    def refuse_next_writes(self, count: int) -> None:
        self._ask({"cmd": "refuse", "count": count})

    def conflict_next_writes(self, count: int) -> None:
        """The next writes not forced are answered: TT Lab changed the model since."""
        self._ask({"cmd": "conflict", "count": count})

    def answer_link(self, path: str, materials: typing.List[typing.Dict[str, typing.Any]], pictures: typing.List[typing.Dict[str, typing.Any]]) -> None:
        """TT Lab's model of the file draws the materials ({uri, name, data}) and pictures ({uri, name, hash}): linking it answers them, like
        TT Lab answers its model's. Other files are answered the materials the fake was made with and no pictures."""
        self._ask({"cmd": "answer_link", "path": path, "materials": materials, "pictures": pictures})

    def revise(self, path: str, revision: str) -> None:
        """TT Lab changed the model of the file: its revision from now on, what Blender read of the file isn't it."""
        self._ask({"cmd": "revise", "path": path, "revision": revision})

    def close_on(self, kind: str) -> None:
        """The next message of the kind closes the link instead of being answered (TT Lab quitting meanwhile)."""
        self._ask({"cmd": "close_on", "kind": kind})

    def add_unknown(self, path: str) -> None:
        """The project has no model of the data file: linking it is an error."""
        self._ask({"cmd": "unknown", "path": path, "known": False})

    def discard_unknown(self, path: str) -> None:
        self._ask({"cmd": "unknown", "path": path, "known": True})

    def close(self) -> None:
        if self._process.poll() is None:
            self._ask({"cmd": "close"})
            self._process.wait(10)


class _Server:
    """The fake TT Lab's process: a listening socket on the loopback, its discovery file, a thread per connection."""

    def __init__(self, config: typing.Dict[str, typing.Any]):
        spec = importlib.util.spec_from_file_location("ttt_fake_link_protocol", LINK_PROTOCOL)
        self.protocol = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.protocol)
        self.config = config
        self.lock = threading.Lock()
        self.received: typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]] = []
        self.holding: typing.Set[str] = set()
        self.held: typing.Dict[str, typing.List[typing.Any]] = {}
        self.refuse_writes = 0
        self.conflict_writes = 0
        self.unknown: typing.Set[str] = set()
        self.link_answers: typing.Dict[str, typing.Tuple[typing.List[typing.Any], typing.List[typing.Any]]] = {}
        self.revision = 0
        # TT Lab's revision of each model by its file (the one it was made with until a write or a conflict), and the models TT Lab changed
        # since their files: what Blender read of a file isn't TT Lab's model then
        self.revisions: typing.Dict[str, str] = {}
        self.changed: typing.Set[str] = set()
        self.closing_on: typing.Set[str] = set()
        self.connection: typing.Optional[socket.socket] = None
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.bind(("127.0.0.1", 0))
        self.server.listen(4)
        self.port = self.server.getsockname()[1]
        folder = os.path.join(config["state"], "tt-lab", "blender")
        os.makedirs(folder, exist_ok=True)
        self.discovery = os.path.join(folder, "link-%d.json" % os.getpid())
        with open(self.discovery, "w", encoding="utf-8") as file:
            json.dump({"protocol": 1, "port": self.port, "token": TOKEN, "pid": os.getpid(), "version": "2.0.0", "addon_version": config["addon_version"],
                       "project": {"uuid": config["uuid"], "path": config["project_root"], "name": "Test"}}, file)

        threading.Thread(target=self._serve, daemon=True).start()

    def _serve(self) -> None:
        while True:
            try:
                connection, _ = self.server.accept()
            except OSError:
                return

            self.connection = connection
            threading.Thread(target=self._read, args=(connection,), daemon=True).start()

    def _read(self, connection: socket.socket) -> None:
        decoder = self.protocol.Decoder()
        while True:
            try:
                data = connection.recv(65536)
            except OSError:
                return

            if not data:
                return

            for header, payload in decoder.feed(data):
                with self.lock:
                    self.received.append((header, payload))
                    if header["type"] in self.closing_on:
                        self.closing_on.discard(header["type"])
                        connection.close()
                        return

                    answer = self._answer(header, payload)
                    if answer is None:
                        continue

                    if header["type"] in self.holding:
                        self.held.setdefault(header["type"], []).append((connection, header, answer))
                        continue

                self._send(connection, header, answer)

    def _send(self, connection: socket.socket, header: typing.Dict[str, typing.Any], answer: typing.Tuple[typing.Dict[str, typing.Any], bytes]) -> None:
        reply, payload = answer
        reply = dict(reply, re=header["id"], id=1000 + len(self.received))
        try:
            connection.sendall(self.protocol.encode(reply, payload))
        except OSError:
            pass

    def _uri_of(self, path: str) -> str:
        return "res://%s/OGI/%s" % (self.config["package"], os.path.splitext(os.path.basename(path))[0])

    def _path_of(self, uri: str) -> str:
        return os.path.join(self.config["project_root"], "assets", self.config["package"], "OGI", uri.rsplit("/", 1)[-1] + ".tlm")

    @staticmethod
    def _key(path: str) -> str:
        return os.path.normcase(os.path.abspath(path))

    def _current(self, path: str) -> str:
        return self.revisions.get(self._key(path), self.config["link_revision"])

    def _is_current(self, path: str, revision: str) -> bool:
        """TT Lab's IsCurrent (LINK.md): what Blender has of the model is TT Lab's model. None kept is the model as saved, a file's revision
        the file while TT Lab didn't change the model since."""
        if not revision:
            return self._key(path) not in self.changed

        for prefix in ("file:", "exported:"):
            if revision.startswith(prefix):
                try:
                    with open(path, "rb") as file:
                        digest = hashlib.sha256(file.read()).hexdigest()
                except OSError:
                    return False

                return self._key(path) not in self.changed and digest == revision[len(prefix):]

        return revision == self._current(path)

    def _answer(self, header: typing.Dict[str, typing.Any], payload: bytes = b"") -> typing.Optional[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
        kind = header["type"]
        if kind == "hello":
            if header.get("token") != TOKEN:
                return {"type": "error", "text": "token"}, b""

            return {"type": "welcome", "project": {"uuid": self.config["uuid"]}}, b""

        if kind == "link_model":
            if header["path"] in self.unknown:
                return {"type": "error", "text": "The project has no model of the file %s" % header["path"]}, b""

            # Another project's are what the project doesn't have
            missing = [uri for uri in header.get("materials", []) + header.get("pictures", []) if uri.startswith("res://Elsewhere/")]
            materials, pictures = self.link_answers.get(self._key(header["path"]), (self.config["link_materials"], []))
            return {"type": "model", "uri": self._uri_of(header["path"]), "revision": self._current(header["path"]),
                    "changed": not self._is_current(header["path"], header.get("revision", "")), "materials": materials, "pictures": pictures, "missing": missing}, b""

        if kind in ("create_picture", "create_material"):
            folder = "Texture" if kind == "create_picture" else "Material"
            return {"type": "created", "uri": "res://%s/%s/%s" % (self.config["package"], folder, header["name"])}, b""

        if kind == "new_model_targets":
            return {"type": "targets", "folders": [{"uri": "res://%s/OGI" % self.config["package"], "label": "OGI"}]}, b""

        if kind == "create_model":
            # The package's model of the name, its file what Blender sent
            uri = "res://%s/OGI/%s" % (self.config["package"], header["name"])
            path = self._path_of(uri)
            with open(path, "wb") as file:
                file.write(payload)

            return {"type": "created", "uri": uri, "path": path}, b""

        if kind == "get_picture":
            return {"type": "picture", "name": header["uri"].rsplit("/", 1)[-1]}, _png(4, 4, (200, 100, 50, 255))

        if kind == "get_model":
            # The model as TT Lab has it: its file here, TT Lab's revision of it from then on
            path = self._path_of(header["uri"])
            self.revisions[self._key(path)] = "r-tt-lab"
            with open(path, "rb") as file:
                return {"type": "model", "uri": header["uri"], "revision": "r-tt-lab"}, file.read()

        if kind == "write_model":
            path = self._path_of(header["uri"])
            if self.conflict_writes > 0 and not header.get("force"):
                # TT Lab changed the model since
                self.conflict_writes -= 1
                self.revisions[self._key(path)] = "r-changed"
                self.changed.add(self._key(path))
                return {"type": "conflict", "revision": "r-changed", "changed": True}, b""

            if self.refuse_writes > 0:
                self.refuse_writes -= 1
                return {"type": "refused", "text": "a character drawing it needs 11 exit points", "problems": [{"text": "needs 11 exit points", "refused": True}]}, b""

            if self.config["write_revision"]:
                revision = self.config["write_revision"]
            else:
                self.revision += 1
                revision = "r%d" % self.revision

            self.revisions[self._key(path)] = revision
            self.changed.discard(self._key(path))
            return {"type": "ok", "revision": revision, "problems": []}, b""

        return {"type": "ok"}, b""

    def command(self, command: typing.Dict[str, typing.Any]) -> typing.Dict[str, typing.Any]:
        kind = command["cmd"]
        if kind == "since":
            with self.lock:
                messages = self.received[command["from"]:]

            return {"messages": [[header, base64.b64encode(payload).decode("ascii")] for header, payload in messages]}

        if kind == "push":
            with self.lock:
                header = dict(command["header"], id=5000 + len(self.received))

            if self.connection is not None:
                self.connection.sendall(self.protocol.encode(header, base64.b64decode(command["payload"])))

            return {}

        if kind == "hold":
            with self.lock:
                self.holding.add(command["kind"])

            return {}

        if kind == "release":
            with self.lock:
                self.holding.discard(command["kind"])
                held = self.held.pop(command["kind"], [])

            for connection, header, answer in held:
                self._send(connection, header, answer)

            return {}

        if kind == "refuse":
            with self.lock:
                self.refuse_writes = int(command["count"])

            return {}

        if kind == "conflict":
            with self.lock:
                self.conflict_writes = int(command["count"])

            return {}

        if kind == "answer_link":
            with self.lock:
                self.link_answers[self._key(command["path"])] = (command["materials"], command["pictures"])

            return {}

        if kind == "revise":
            with self.lock:
                self.revisions[self._key(command["path"])] = command["revision"]
                self.changed.add(self._key(command["path"]))

            return {}

        if kind == "close_on":
            with self.lock:
                self.closing_on.add(command["kind"])

            return {}

        if kind == "unknown":
            with self.lock:
                if command["known"]:
                    self.unknown.discard(command["path"])
                else:
                    self.unknown.add(command["path"])

            return {}

        raise ValueError(kind)

    def close(self) -> None:
        try:
            os.remove(self.discovery)
        except OSError:
            pass

        self.server.close()
        if self.connection is not None:
            self.connection.close()


def _png(width: int, height: int, color: typing.Tuple[int, int, int, int]) -> bytes:
    """A picture of one color, the PNG TT Lab sends of a texture."""
    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    rows = b"".join(b"\x00" + bytes(color) * width for _ in range(height))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def main() -> None:
    server = _Server(json.loads(sys.argv[1]))
    print(json.dumps({"port": server.port}), flush=True)
    try:
        for line in sys.stdin:
            command = json.loads(line)
            if command["cmd"] == "close":
                print(json.dumps({}), flush=True)
                break

            print(json.dumps(server.command(command)), flush=True)
    finally:
        server.close()


if __name__ == "__main__":
    main()
