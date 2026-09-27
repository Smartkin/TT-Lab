import array
import json
import math
import os
import random
import shutil
import tempfile
import unittest

from fakes import load_addon_module

tlm = load_addon_module("tlm")
tlm_math = load_addon_module("tlm_math")
tlm_mesh = load_addon_module("tlm_mesh")
project = load_addon_module("project")

FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "ogi.tlm")


class FileTests(unittest.TestCase):
    def test_views_come_back_with_their_type(self):
        file = tlm.TlmFile("Model", "Rock")
        floats = file.write_view([1.5, -0.0, 3.25], "f32")
        bytes_ = file.write_view([1, 2, 255], "u8")
        ints = file.write_view([-5, 7], "i32")

        read = tlm.TlmFile.from_bytes(file.to_bytes())

        self.assertEqual(list(read.read_view(floats, "f32")), [1.5, -0.0, 3.25])
        self.assertEqual(math.copysign(1, read.read_view(floats)[1]), -1)
        self.assertEqual(list(read.read_view(bytes_, "u8")), [1, 2, 255])
        self.assertEqual(list(read.read_view(ints, "i32")), [-5, 7])
        self.assertEqual((read.asset_type, read.name), ("Model", "Rock"))

    def test_views_start_on_4_bytes(self):
        file = tlm.TlmFile()
        file.write_view([1], "u8")

        self.assertEqual(file.write_view([2.0], "f32")["offset"] % 4, 0)

    def test_reading_a_view_as_another_type_fails(self):
        file = tlm.TlmFile()
        view = file.write_view([1.0], "f32")

        with self.assertRaises(tlm.TlmError):
            file.read_view(view, "u8")

    def test_views_past_the_data_fail(self):
        file = tlm.TlmFile()

        with self.assertRaises(tlm.TlmError):
            file.read_view({"offset": 0, "count": 4, "type": "f32"})

    def test_other_files_fail(self):
        with self.assertRaises(tlm.TlmError):
            tlm.TlmFile.from_bytes(b"glTF" + b"\0" * 16)

    # TT Lab writes the game's negative zeros as -0, which JSON reads as the integer 0
    def test_negative_zeros_stay(self):
        file = tlm.TlmFile()
        data = file.to_bytes().replace(b'"materials":[]', b'"materials":[],"root":{"data":{"Matrix":[-0,1]}}    ')
        data = data[:8] + (len(data) - 16).to_bytes(4, "little") + data[12:]

        read = tlm.TlmFile.from_bytes(data)

        self.assertEqual(math.copysign(1, read.root["data"]["Matrix"][0]), -1)

    def test_tt_labs_file_is_read(self):
        file = tlm.TlmFile.load(FIXTURE)

        self.assertEqual(file.asset_type, "Ogi")
        self.assertEqual([child["kind"] for child in tlm.children(file.root)], ["armature", "skin", "shape", "rigid_bodies", "exit_points"])
        armature = tlm.find_child(file.root, "armature")
        self.assertEqual([joint["parent"] for joint in armature["joints"]], [-1, 0, 1])
        self.assertEqual([animation["name"] for animation in armature["animations"]], ["Walk", "Wave"])
        self.assertTrue(all(len(file.read_view(animation["exact"], "u8")) > 0 for animation in armature["animations"]))
        self.assertTrue(file.materials[0]["uri"].startswith("res://"))

    def test_tt_labs_file_is_written_the_same(self):
        file = tlm.TlmFile.load(FIXTURE)

        read = tlm.TlmFile.from_bytes(file.to_bytes())

        self.assertEqual(read.json, file.json)
        self.assertEqual(bytes(read._binary), bytes(file._binary))


class MathTests(unittest.TestCase):
    def setUp(self):
        self.random = random.Random(7)

    def _rotation(self):
        return tlm_math.quat_normalized(tuple(self.random.uniform(-1, 1) for _ in range(4)))

    def _vector(self, scale=2.0):
        return tuple(self.random.uniform(-scale, scale) for _ in range(3))

    def assertClose(self, expected, actual, places=6):
        for a, b in zip(expected, actual):
            self.assertAlmostEqual(a, b, places)

    def assertSameRotation(self, expected, actual):
        self.assertAlmostEqual(abs(sum(a * b for a, b in zip(expected, actual))), 1.0, 6)

    def test_keys_come_back_from_poses(self):
        for _ in range(200):
            rest = (self._vector(), self._rotation())
            translation, rotation, scale = self._vector(), self._rotation(), self._vector()

            pose = tlm_math.key_to_pose(rest, translation, rotation, scale)
            key = tlm_math.pose_to_key(rest, *pose)

            self.assertClose(translation, key[0])
            self.assertSameRotation(rotation, key[1])
            self.assertEqual(scale, key[2])

    # The game hides joints by scaling them to nothing, their translation and rotation still come back
    def test_hidden_joints_come_back(self):
        rest = (self._vector(), self._rotation())
        rotation = self._rotation()

        key = tlm_math.pose_to_key(rest, *tlm_math.key_to_pose(rest, (1.0, 2.0, 3.0), rotation, (0.0, 0.0, 0.0)))

        self.assertClose((1.0, 2.0, 3.0), key[0])
        self.assertSameRotation(rotation, key[1])
        self.assertEqual((0.0, 0.0, 0.0), key[2])

    def test_rest_pose_is_no_pose(self):
        rest = (self._vector(), self._rotation())

        location, rotation, scale = tlm_math.key_to_pose(rest, rest[0], rest[1], (1.0, 1.0, 1.0))

        self.assertClose((0.0, 0.0, 0.0), location)
        self.assertSameRotation((1.0, 0.0, 0.0, 0.0), rotation)

    def test_bind_poses_lose_their_scale(self):
        translation, rotation = self._vector(), self._rotation()
        bind = tlm_math.compose(translation, rotation, (1.7, 1.7, 1.7))

        rest = tlm_math.rest_of(bind)

        self.assertClose(translation, rest[0])
        self.assertSameRotation(rotation, rest[1])

    def test_children_rest_relative_to_their_parent(self):
        parent = (self._vector(), self._rotation())
        local = (self._vector(), self._rotation())
        child = tlm_math.pose_to_key(parent, local[0], local[1], (1.0, 1.0, 1.0))

        relative = tlm_math.relative_rest(parent, (child[0], child[1]))

        self.assertClose(local[0], relative[0])
        self.assertSameRotation(local[1], relative[1])


def _corner_mesh(data, uv_edit=None, materials=None):
    """The mesh Blender makes of the data: UVs flipped per corner, normals per corner."""
    mesh = tlm_mesh.CornerMesh()
    mesh.positions = data.positions
    mesh.corner_vertices = list(data.triangles)
    mesh.triangle_parts = list(data.face_parts)
    mesh.triangle_materials = list(data.face_materials)
    mesh.vertex_parts = list(data.vertex_parts)
    mesh.corner_uvs = [value for vertex in data.triangles for value in (data.uvs[vertex * 2], 1.0 - data.uvs[vertex * 2 + 1])]
    if data.normals is not None:
        mesh.corner_normals = [value for vertex in data.triangles for value in data.normals[vertex * 3:vertex * 3 + 3]]

    mesh.vertex_colors = [value / 255.0 for value in data.colors]
    mesh.twin_normals = data.twin_normals
    mesh.twin_uvs = data.uvs
    mesh.twin_joints = [float(value) for value in data.twin_joints] if data.twin_joints is not None else None
    mesh.twin_weights = data.twin_weights
    mesh.shapes = data.shapes
    mesh.twin_shapes = list(data.shapes)
    mesh.parts = json.loads(json.dumps(data.parts))
    mesh.slot_materials = materials if materials is not None else [part["material"] for part in data.parts]
    if uv_edit is not None:
        uv_edit(mesh)

    return mesh


class MeshTests(unittest.TestCase):
    def setUp(self):
        self.file = tlm.TlmFile.load(FIXTURE)
        self.skin = tlm.find_child(self.file.root, "skin")["mesh"]

    def _values(self, file, part, key):
        return list(file.read_view(part[key])) if key in part else None

    def test_unedited_parts_come_back(self):
        data = tlm_mesh.from_parts(self.file, self.skin, True)
        target = tlm.TlmFile()

        written = tlm_mesh.to_parts(target, _corner_mesh(data), True)

        self.assertEqual(len(written["parts"]), len(self.skin["parts"]))
        for original, part in zip(self.skin["parts"], written["parts"]):
            self.assertEqual(part["vertices"], original["vertices"])
            for key in ("faces", "position", "twin_normal", "color", "joints", "weights"):
                self.assertEqual(self._values(target, part, key), self._values(self.file, original, key), key)

            uvs = self._values(target, part, "uv")
            for written_uv, uv in zip(uvs, self._values(self.file, original, "uv")):
                self.assertAlmostEqual(written_uv, uv, 6)

            self.assertEqual(target.read_view(part["strips"]["vertexes"]).tolist(), self.file.read_view(original["strips"]["vertexes"]).tolist())

    # A vertex whose corners got different UVs is written once more for the other UV
    def test_vertexes_with_different_uvs_split(self):
        data = tlm_mesh.from_parts(self.file, self.skin, True)

        corners = {}
        for corner, vertex in enumerate(data.triangles):
            corners.setdefault(vertex, []).append(corner)

        edited = next(vertex_corners[1] for vertex, vertex_corners in sorted(corners.items()) if len(vertex_corners) > 1)

        def move_one_corner(mesh):
            mesh.corner_uvs[edited * 2] += 0.25

        target = tlm.TlmFile()
        written = tlm_mesh.to_parts(target, _corner_mesh(data, move_one_corner), True)

        part = written["parts"][0]
        self.assertEqual(part["vertices"], self.skin["parts"][0]["vertices"] + 1)
        self.assertEqual(target.read_view(part["faces"])[edited], part["vertices"] - 1)

    def test_faces_given_another_material_are_a_part_of_their_own(self):
        data = tlm_mesh.from_parts(self.file, self.skin, True)

        def repaint(mesh):
            mesh.triangle_materials[0] = 1

        written = tlm_mesh.to_parts(tlm.TlmFile(), _corner_mesh(data, repaint, materials=[0, 3]), True)

        self.assertEqual(len(written["parts"]), 3)
        self.assertEqual([part["material"] for part in written["parts"]], [0, 3, 3])
        self.assertNotIn("strips", written["parts"][1])

    # Vertexes no face uses have no corner, they keep the game's UV
    def test_loose_vertexes_keep_their_uv(self):
        data = tlm_mesh.from_parts(self.file, self.skin, True)
        mesh = _corner_mesh(data)
        mesh.corner_vertices = mesh.corner_vertices[3:]
        mesh.corner_uvs = mesh.corner_uvs[6:]
        mesh.corner_normals = mesh.corner_normals[9:]
        mesh.triangle_parts = mesh.triangle_parts[1:]
        mesh.triangle_materials = mesh.triangle_materials[1:]
        target = tlm.TlmFile()

        written = tlm_mesh.to_parts(target, mesh, True)

        part = written["parts"][0]
        self.assertEqual(part["vertices"], self.skin["parts"][0]["vertices"])
        for written_uv, uv in zip(self._values(target, part, "uv"), self._values(self.file, self.skin["parts"][0], "uv")):
            self.assertAlmostEqual(written_uv, uv, 6)

    def test_parts_without_faces_stay(self):
        data = tlm_mesh.from_parts(self.file, self.skin, True)
        mesh = _corner_mesh(data)
        second = [index for index, part in enumerate(mesh.triangle_parts) if part == 1]
        mesh.corner_vertices = [vertex for triangle in range(len(mesh.triangle_parts)) if triangle not in second for vertex in mesh.corner_vertices[triangle * 3:triangle * 3 + 3]]
        mesh.corner_uvs = [value for triangle in range(len(mesh.triangle_parts)) if triangle not in second for value in mesh.corner_uvs[triangle * 6:triangle * 6 + 6]]
        mesh.corner_normals = [value for triangle in range(len(mesh.triangle_parts)) if triangle not in second for value in mesh.corner_normals[triangle * 9:triangle * 9 + 9]]
        mesh.triangle_materials = [material for triangle, material in enumerate(mesh.triangle_materials) if triangle not in second]
        mesh.triangle_parts = [part for part in mesh.triangle_parts if part != 1]

        written = tlm_mesh.to_parts(tlm.TlmFile(), mesh, True)

        self.assertEqual(len(written["parts"]), 2)

    def test_rigid_parts_keep_the_values_they_had(self):
        body = tlm.find_child(self.file.root, "rigid_bodies")["children"][0]["mesh"]
        data = tlm_mesh.from_parts(self.file, body, False)
        mesh = _corner_mesh(data)
        mesh.uv_q = data.uv_q
        mesh.alpha_flags = data.alpha_flags
        mesh.emit_colors = [value / 255.0 for value in data.emit_colors] if data.emit_colors is not None else None

        written = tlm_mesh.to_parts(tlm.TlmFile(), mesh, False)

        for original, part in zip(body["parts"], written["parts"]):
            for key in tlm_mesh.OPTIONAL_KEYS:
                self.assertEqual(key in part, key in original, key)


class ProjectTests(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp()
        with open(os.path.join(self.root, "Test.tson"), "w") as file:
            file.write("{}")

        package = os.path.join(self.root, "assets", "Global PS2_Test")
        os.makedirs(os.path.join(package, "Material"))
        os.makedirs(os.path.join(package, "Texture"))
        os.makedirs(os.path.join(package, "OGI"))
        self._write(os.path.join(package, "Texture", "Wood.json"), {"Type": "TT_Lab.Assets.Graphics.Texture, TT Lab", "Alias": "Wood",
                                                                   "URI": {"_uri": "res://Global PS2_Test/Texture/Wood"}})
        with open(os.path.join(package, "Texture", "Wood.png"), "wb") as file:
            file.write(b"\x89PNG")

        self._write(os.path.join(package, "Material", "Varnish.json"), {"Type": "TT_Lab.Assets.Graphics.Material, TT Lab", "Alias": "Varnish",
                                                                      "URI": {"_uri": "res://Global PS2_Test/Material/Varnish"},
                                                                      "Package": {"_uri": "res://Global PS2_Test"}})
        self._write(os.path.join(package, "Material", "Varnish.data"), {"Name": "varnish", "Shaders": [
            {"TextureId": {"_uri": "res://EMPTY"}}, {"TextureId": {"_uri": "res://Global PS2_Test/Texture/Wood"}}]})
        self.model = os.path.join(package, "OGI", "Crash.tlm")

    def tearDown(self):
        shutil.rmtree(self.root)

    @staticmethod
    def _write(path, value):
        with open(path, "w", encoding="utf-8") as file:
            json.dump(value, file)

    def test_models_find_their_project(self):
        self.assertEqual(project.find_root(self.model), self.root)
        self.assertEqual(project.package_of(self.root, self.model), "res://Global PS2_Test")

    def test_files_outside_of_projects_have_none(self):
        self.assertIsNone(project.find_root(os.path.join(tempfile.gettempdir(), "nowhere", "model.tlm")))

    def test_materials_are_drawn_with_their_texture(self):
        opened = project.Project(self.root)

        material = opened.materials["res://Global PS2_Test/Material/Varnish"]

        self.assertEqual(material.name, "Varnish")
        self.assertEqual(material.package, "res://Global PS2_Test")
        self.assertEqual(opened.texture_of(material).png_path, os.path.join(self.root, "assets", "Global PS2_Test", "Texture", "Wood.png"))

    def test_materials_say_which_chunk_they_are_of(self):
        package_material = project.ProjectMaterial("res://PS2_Test/Material/lambert1_10", "lambert1_10", "res://PS2_Test", "")
        scenery_material = project.ProjectMaterial("res://PS2_Test/levels/school/rooftop/roof01/Material/lambert1_10", "lambert1_10",
                                                   "res://PS2_Test", "")

        self.assertEqual(package_material.label, "lambert1_10")
        self.assertEqual(scenery_material.label, "lambert1_10 (levels/school/rooftop/roof01)")

    def test_packages_are_of_a_version_of_the_game(self):
        self.assertEqual(project.platform_of("res://Global XBOX_Test"), "XBOX")
        self.assertEqual(project.platform_of("res://PS2_Test"), "PS2")
        self.assertIsNone(project.platform_of("res://Test"))


if __name__ == "__main__":
    unittest.main()
