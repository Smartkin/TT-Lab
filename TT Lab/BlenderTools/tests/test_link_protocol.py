"""The link's frames and TT Lab's discovery files, without Blender."""

import json
import os
import shutil
import tempfile
import unittest

from fakes import load_addon_module

link_protocol = load_addon_module("link_protocol")


class FrameTests(unittest.TestCase):
    def test_frames_come_out_as_they_went_in_whatever_the_pieces(self):
        frames = [({"type": "hello", "id": 1, "token": "t"}, b""), ({"type": "save", "id": 2, "re": 9, "name": "Шапка"}, bytes(range(256)) * 40)]
        data = b"".join(link_protocol.encode(header, payload) for header, payload in frames)

        for size in (1, 7, len(data)):
            decoder = link_protocol.Decoder()
            received = []
            for start in range(0, len(data), size):
                received.extend(decoder.feed(data[start:start + size]))

            self.assertEqual(frames, received)

    def test_frames_that_arent_tt_labs_are_refused(self):
        with self.assertRaises(link_protocol.ProtocolError):
            link_protocol.Decoder().feed((2 ** 31).to_bytes(4, "little") + bytes(4))

        with self.assertRaises(link_protocol.ProtocolError):
            text = b'{"id": 1}'
            link_protocol.Decoder().feed(len(text).to_bytes(4, "little") + bytes(4) + text)


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.folder)

    def _write(self, name, data):
        with open(os.path.join(self.folder, name), "w", encoding="utf-8") as file:
            json.dump(data, file)

    def test_the_tt_lab_with_the_project_open_is_found_by_its_uuid_or_folder(self):
        alive = os.getpid()
        self._write("link-%d.json" % alive, {"protocol": 1, "port": 5000, "token": "a", "pid": alive,
                                             "project": {"uuid": "u-1", "path": "/projects/Mod", "name": "Mod"}})
        # A TT Lab that's gone left its file behind
        self._write("link-999999999.json", {"protocol": 1, "port": 5001, "token": "b", "pid": 999999999,
                                            "project": {"uuid": "u-2", "path": "/projects/Other", "name": "Other"}})

        self.assertEqual(5000, link_protocol.tt_lab_of("", "u-1", [self.folder])["port"])
        self.assertEqual(5000, link_protocol.tt_lab_of("/projects/Mod", "", [self.folder])["port"])
        self.assertIsNone(link_protocol.tt_lab_of("/projects/Other", "u-2", [self.folder]))

    def test_any_running_tt_lab_opens_another_project(self):
        self.assertIsNone(link_protocol.any_tt_lab([self.folder]))
        alive = os.getpid()
        self._write("link-%d.json" % alive, {"protocol": 1, "port": 5000, "token": "a", "pid": alive, "project": None})

        self.assertEqual(5000, link_protocol.any_tt_lab([self.folder])["port"])

    def test_tt_lab_starts_with_what_it_left_in_its_record(self):
        self.assertIsNone(link_protocol.tt_lab_command([self.folder], inside_flatpak=False))
        executable = os.path.join(self.folder, "TT Lab")
        with open(executable, "w", encoding="utf-8") as file:
            file.write("")

        self._write("tt-lab.json", {"command": [executable], "version": "2.0.0"})
        self.assertEqual([executable], link_protocol.tt_lab_command([self.folder], inside_flatpak=False))
        # Inside the Flatpak's sandbox the host's executable is out of sight, the host starts it
        self.assertEqual(["flatpak-spawn", "--host", executable], link_protocol.tt_lab_command([self.folder], inside_flatpak=True))
        os.remove(executable)
        self.assertIsNone(link_protocol.tt_lab_command([self.folder], inside_flatpak=False))

    def test_a_flatpak_blender_reads_the_hosts_state_folder(self):
        folders = link_protocol.state_folders({"HOME": "/home/me", "XDG_STATE_HOME": "/home/me/.var/app/org.blender.Blender/.local/state"}, "linux")

        self.assertEqual(["/home/me/.local/state/tt-lab/blender"], folders)
        self.assertEqual(["/state/tt-lab/blender", "/home/me/.local/state/tt-lab/blender"], link_protocol.state_folders({"HOME": "/home/me", "XDG_STATE_HOME": "/state"}, "linux"))
        self.assertEqual([os.path.join("C:\\Users\\me\\AppData\\Local", "TT Lab", "Blender")],
                         link_protocol.state_folders({"LOCALAPPDATA": "C:\\Users\\me\\AppData\\Local"}, "win32"))


if __name__ == "__main__":
    unittest.main()
