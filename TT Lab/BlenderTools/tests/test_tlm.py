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
save_icon = load_addon_module("save_icon")

FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "ogi.tlm")
SAVE_ICON_FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "save_icon.tlm")


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
        self.assertEqual([child["kind"] for child in tlm.children(file.root)], ["armature", "skin", "shape", "rigid_bodies", "exit_points", "collision_hulls"])
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


class SaveIconTests(unittest.TestCase):
    """The save icon's one part keeps its shapes, which are written for what's shaped though it's no skin."""

    def setUp(self):
        self.file = tlm.TlmFile.load(SAVE_ICON_FIXTURE)
        self.part = self.file.root["mesh"]["parts"][0]

    def test_the_icon_is_a_shaped_mesh_with_its_texture(self):
        self.assertEqual(self.file.root["kind"], "save_icon")
        self.assertEqual(len(self.part["shapes"]), 1)
        self.assertIn("png", self.file.materials[self.part["material"]]["image"])
        self.assertEqual([frame["shape"] for frame in self.file.root["animation"]["frames"]], [0, 1])

    def test_unedited_icons_come_back_with_their_shapes(self):
        data = tlm_mesh.from_parts(self.file, self.file.root["mesh"], False)
        target = tlm.TlmFile()

        written = tlm_mesh.to_parts(target, _corner_mesh(data), False, shaped=True)

        part = written["parts"][0]
        for key in ("faces", "position", "twin_normal", "color"):
            self.assertEqual(list(target.read_view(part[key])), list(self.file.read_view(self.part[key])), key)

        self.assertEqual(list(target.read_view(part["shapes"][0])), list(self.file.read_view(self.part["shapes"][0])))
        self.assertNotIn("shapes", tlm_mesh.to_parts(tlm.TlmFile(), _corner_mesh(data), False)["parts"][0])


class SaveIconAnimationTests(unittest.TestCase):
    """The first shape's weight is what the shape keys leave over, so the console's blend of the shapes is what Blender shows."""

    def test_weights_go_in_straight_lines_between_the_keys(self):
        keys = [0.0, 0.0, 30.0, 1.0, 60.0, 0.0]
        self.assertEqual(save_icon.weight_at(keys, -5.0), 0.0)
        self.assertEqual(save_icon.weight_at(keys, 15.0), 0.5)
        self.assertEqual(save_icon.weight_at(keys, 30.0), 1.0)
        self.assertEqual(save_icon.weight_at(keys, 90.0), 0.0)
        self.assertEqual(save_icon.weight_at([5.0, 0.25], 0.0), 0.25)
        self.assertEqual(save_icon.weight_at([], 10.0), 0.0)

    def test_the_first_shape_takes_what_the_others_leave(self):
        self.assertEqual(save_icon.basis_keys([[0.0, 0.0, 30.0, 0.5, 60.0, 0.0]]), [0.0, 1.0, 30.0, 0.5, 60.0, 1.0])
        self.assertEqual(save_icon.basis_keys([]), [0.0, 1.0])

    def test_the_console_draws_what_blender_shows(self):
        first = [0.0, 0.0, 20.0, 1.0]
        second = [10.0, 0.0, 40.0, 0.5]
        basis = save_icon.basis_keys([first, second])
        self.assertEqual(basis[0::2], [0.0, 10.0, 20.0, 40.0])
        # A coordinate of a vertex in the three shapes: the console takes every shape times its weight over the weights' sum, Blender
        # the shape keys' offsets from the first times their values
        shapes = [0.0, 3.0, -2.0]
        for time in (-1.0, 0.0, 5.0, 12.5, 20.0, 33.0, 50.0):
            weights = [save_icon.weight_at(keys, time) for keys in (basis, first, second)]
            console = sum(weight * shape for weight, shape in zip(weights, shapes)) / sum(weights)
            blender = shapes[0] + weights[1] * (shapes[1] - shapes[0]) + weights[2] * (shapes[2] - shapes[0])
            self.assertAlmostEqual(console, blender, msg=str(time))


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


class RetargetTests(unittest.TestCase):
    """A new armature's bones find the original's joints by index, name, then their place in the hierarchy."""

    def setUp(self):
        self.retarget = load_addon_module("retarget")
        # The original: a root joint with a spine and a leg, the spine holds a head
        self.original = [("Joint 0", None, 0), ("Joint 1", "Joint 0", 1), ("Joint 2", "Joint 1", 2), ("Joint 3", "Joint 0", 3)]

    def test_bones_named_like_the_original_are_its_joints(self):
        matches = self.retarget.match_joints(self.original, [("Joint 3", "Hips", None), ("Hips", None, None), ("Joint 1", "Hips", None)])

        self.assertEqual((3, "Joint 3", "name"), matches["Joint 3"])
        self.assertEqual((1, "Joint 1", "name"), matches["Joint 1"])
        self.assertEqual((0, "Joint 0", "order"), matches["Hips"])

    def test_bones_follow_the_hierarchy_in_order_and_the_rest_are_new(self):
        incoming = [("Hips", None, None), ("Spine", "Hips", None), ("Head", "Spine", None), ("Leg", "Hips", None), ("Tail", "Hips", None), ("Hair", "Head", None)]

        matches = self.retarget.match_joints(self.original, incoming)

        self.assertEqual({"Hips": (0, "Joint 0", "order"), "Spine": (1, "Joint 1", "order"), "Head": (2, "Joint 2", "order"), "Leg": (3, "Joint 3", "order"),
                          "Tail": (4, None, "new"), "Hair": (5, None, "new")}, matches)
        self.assertEqual("Joint 0", self.retarget.joint_name(matches["Hips"]))
        self.assertEqual("Joint 4", self.retarget.joint_name(matches["Tail"]))

    def test_a_joint_index_a_bone_already_has_wins_over_names_and_order(self):
        incoming = [("Joint 1", None, 2), ("Neck", "Joint 1", None)]

        matches = self.retarget.match_joints(self.original, incoming, self.retarget.MATCH_INDEX)

        self.assertEqual((2, "Joint 2", "index"), matches["Joint 1"])
        # The original's Joint 2 has no children, the neck is new
        self.assertEqual((4, None, "new"), matches["Neck"])

    def test_indexes_another_rig_got_from_its_export_are_left_out(self):
        # A rig exported from Blender has every bone numbered in its own order: its leg, named to be the original's Joint 3, is its joint 1
        incoming = [("Hips", None, 0), ("Joint 3", "Hips", 1), ("Spine", "Hips", 2), ("Head", "Spine", 3)]

        matches = self.retarget.match_joints(self.original, incoming)

        self.assertFalse(self.retarget.keeps_skeleton(self.original, incoming))
        self.assertEqual({"Hips": (0, "Joint 0", "order"), "Joint 3": (3, "Joint 3", "name"), "Spine": (1, "Joint 1", "order"), "Head": (2, "Joint 2", "order")}, matches)

    def test_a_copy_of_the_skeleton_keeps_its_indexes(self):
        # Bones renamed and one added, every joint the two share under the same parent
        incoming = [("Root", None, 0), ("Body", "Root", 1), ("Head", "Body", 2), ("Leg", "Root", 3), ("Tail", "Root", 4)]

        matches = self.retarget.match_joints(self.original, incoming)

        self.assertTrue(self.retarget.keeps_skeleton(self.original, incoming))
        self.assertEqual({"Root": (0, "Joint 0", "index"), "Body": (1, "Joint 1", "index"), "Head": (2, "Joint 2", "index"), "Leg": (3, "Joint 3", "index"),
                          "Tail": (4, None, "new")}, matches)
        # Names and the hierarchy alone, when asked
        self.assertEqual("order", self.retarget.match_joints(self.original, incoming, self.retarget.MATCH_NAME)["Body"][2])

    def test_by_name_only_the_other_bones_stay_as_they_are(self):
        # Joint 1's bone has the original's Joint 3 as its index and the place of Joint 0: neither counts by name only
        incoming = [("Hips", None, 3), ("Joint 1", "Hips", 3), ("Twist", "Joint 1", None), ("Joint 2", "Twist", None)]

        matches = self.retarget.match_joints(self.original, incoming, self.retarget.MATCH_NAME_ONLY)

        self.assertEqual({"Hips": (4, None, "kept"), "Joint 1": (1, "Joint 1", "name"), "Twist": (5, None, "kept"), "Joint 2": (2, "Joint 2", "name")}, matches)
        self.assertEqual("Twist", self.retarget.joint_name(matches["Twist"], "Twist"))
        self.assertEqual("Joint 1", self.retarget.joint_name(matches["Joint 1"], "Joint 1"))
        # Bones in between and joints left out are fine: the matched ones are under each other like their joints
        self.assertEqual([], self.retarget.hierarchy_problems(self.original, incoming, matches))

    def test_by_name_only_the_named_bones_have_to_be_laid_out_like_their_joints(self):
        # Joint 2 is under Joint 1 in the original, Joint 3 under Joint 0
        incoming = [("Joint 0", None, None), ("Joint 3", "Joint 0", None), ("Joint 2", "Joint 3", None), ("Joint 1", "Joint 0", None)]

        matches = self.retarget.match_joints(self.original, incoming, self.retarget.MATCH_NAME_ONLY)
        problems = self.retarget.hierarchy_problems(self.original, incoming, matches)

        self.assertEqual(["Joint 2 is under Joint 3, the source has Joint 2 under Joint 1"], problems)

    def test_by_name_only_a_matched_bone_out_from_under_the_rest(self):
        incoming = [("Joint 0", None, None), ("Joint 1", None, None)]

        matches = self.retarget.match_joints(self.original, incoming, self.retarget.MATCH_NAME_ONLY)

        self.assertEqual(["Joint 1 is above every matched bone, the source has Joint 1 under Joint 0"],
                         self.retarget.hierarchy_problems(self.original, incoming, matches))
        # And the other way round
        upside_down = [("Joint 1", None, None), ("Joint 0", "Joint 1", None)]
        self.assertEqual(["Joint 1 is above every matched bone, the source has Joint 1 under Joint 0", "Joint 0 is under Joint 1, the source has Joint 0 above every matched joint"],
                         self.retarget.hierarchy_problems(self.original, upside_down, self.retarget.match_joints(self.original, upside_down, self.retarget.MATCH_NAME_ONLY)))

    def test_original_bones_without_indexes_count_by_position(self):
        matches = self.retarget.match_joints([("Root", None, None), ("Arm", "Root", None)], [("Base", None, None), ("Arm", "Base", None)])

        self.assertEqual({"Base": (0, "Root", "order"), "Arm": (1, "Arm", "name")}, matches)


class SkeletonTests(unittest.TestCase):
    """The game's skeletons have one root, joint 0, their joints numbered in the order the game walks them from the root down (a joint's
    children by their indexes), 12 children a joint and 63 joints a skin at most: exporting refuses what can't be numbered so and numbers
    the joints in the walk's order when the bones' own indexes don't follow it."""

    def setUp(self):
        self.skeleton = load_addon_module("skeleton")
        # An imported model: a root joint with a spine and a leg, the spine holds a head
        self.imported = [("Joint 0", None, 0), ("Joint 1", "Joint 0", 1), ("Joint 2", "Joint 1", 2), ("Joint 3", "Joint 0", 3)]

    def test_several_roots_are_refused_with_their_names(self):
        bones = [("Joint 0", None, 0), ("Joint 1", None, 0), ("Joint 2", "Joint 1", 0), ("Joint 7", None, 0)]

        with self.assertRaises(ValueError) as raised:
            self.skeleton.check_skeleton(bones)

        self.assertIn("3 root bones (Joint 0, Joint 1, Joint 7)", str(raised.exception))
        self.skeleton.check_skeleton(self.imported)

    def test_animations_made_in_blender_keep_the_ids_they_get(self):
        self.assertEqual([16, 17, 0x8000], self.skeleton.animation_ids([16, 17, None]))
        # A copy repeating an ID gets one of its own, past every ID the model has
        self.assertEqual([16, 0x8001, 0x8000, 0x8002], self.skeleton.animation_ids([16, 16, 0x8000, None]))

    def test_animations_cover_the_bones_added_to_their_model(self):
        # Every joint reads the settings of its index from the animation playing: the game's animations keep the count they were imported
        # with (Crash has animations of 62 joints and one of 2) until the model gets more joints than it had
        self.assertEqual(2, self.skeleton.animation_joint_count(2, 51, 51))
        self.assertEqual(62, self.skeleton.animation_joint_count(62, 51, 51))
        self.assertEqual(51, self.skeleton.animation_joint_count(51, 51, 50))
        self.assertEqual(52, self.skeleton.animation_joint_count(2, 51, 52))
        self.assertEqual(52, self.skeleton.animation_joint_count(51, 51, 52))
        self.assertEqual(62, self.skeleton.animation_joint_count(62, 51, 52))
        self.assertEqual(63, self.skeleton.animation_joint_count(62, 51, 63))

    def test_bones_past_the_games_limits_are_refused(self):
        crowded = [("Root", None, None)] + [("Finger %d" % i, "Root", None) for i in range(13)]
        with self.assertRaises(ValueError) as raised:
            self.skeleton.check_skeleton(crowded)
        self.assertIn("Root has 13 child bones", str(raised.exception))
        self.skeleton.check_skeleton(crowded[:-1])

        chain = [("Bone 0", None, None)] + [("Bone %d" % i, "Bone %d" % (i - 1), None) for i in range(1, 64)]
        with self.assertRaises(ValueError) as raised:
            self.skeleton.check_skeleton(chain, skinned=True)
        self.assertIn("64 bones and the game draws a skin with 63 at most", str(raised.exception))
        # A model without a skin has no such limit
        self.skeleton.check_skeleton(chain)
        self.skeleton.check_skeleton(chain[:-1], skinned=True)

    def test_the_indexes_of_an_imported_model_stay(self):
        bones = self.imported + [("Tail", "Joint 3", None)]

        joints, renumbered = self.skeleton.number_joints(bones)

        self.assertFalse(renumbered)
        self.assertEqual({"Joint 0": 0, "Joint 1": 1, "Joint 2": 2, "Joint 3": 3, "Tail": 4}, joints)

    def test_a_bone_added_inside_a_model_takes_its_place_in_the_walk(self):
        # Every bone after its parent isn't enough: the game would draw the leg with the hair's matrix
        bones = self.imported + [("Hair", "Joint 2", None)]

        joints, renumbered = self.skeleton.number_joints(bones)

        self.assertTrue(renumbered)
        self.assertEqual({"Joint 0": 0, "Joint 1": 1, "Joint 2": 2, "Hair": 3, "Joint 3": 4}, joints)

    def test_bones_all_left_at_index_0_go_in_hierarchy_order(self):
        bones = [("Hips", None, 0), ("Spine", "Hips", 0), ("Head", "Spine", 0), ("Leg", "Hips", 0)]

        joints, renumbered = self.skeleton.number_joints(bones)

        self.assertFalse(renumbered)
        self.assertEqual({"Hips": 0, "Spine": 1, "Head": 2, "Leg": 3}, joints)

    def test_a_root_added_above_a_model_numbers_it_from_the_root(self):
        bones = [("Origin", None, None)] + [(name, parent or "Origin", index) for name, parent, index in self.imported]

        joints, renumbered = self.skeleton.number_joints(bones)

        self.assertTrue(renumbered)
        self.assertEqual({"Origin": 0, "Joint 0": 1, "Joint 1": 2, "Joint 2": 3, "Joint 3": 4}, joints)

    def test_a_bone_numbered_before_its_parent_numbers_them_from_the_root(self):
        # The leg's children come in the order of the indexes they had
        bones = [("Root", None, 0), ("Leg", "Root", 5), ("Foot", "Leg", 2), ("Toe", "Leg", 1)]

        joints, renumbered = self.skeleton.number_joints(bones)

        self.assertTrue(renumbered)
        self.assertEqual({"Root": 0, "Leg": 1, "Toe": 2, "Foot": 3}, joints)

    def test_indexes_left_out_are_numbered_again(self):
        # A bone deleted from an imported model left its index out, the game's skeletons number their joints one after the other
        joints, renumbered = self.skeleton.number_joints([("Root", None, 0), ("Arm", "Root", 2)])

        self.assertTrue(renumbered)
        self.assertEqual({"Root": 0, "Arm": 1}, joints)


class ModelBoundsTests(unittest.TestCase):
    """The game takes an OGI's bounding box for its instances' collision without hulls, shadows and physics. The tools made it around the
    meshes at rest but not every time, so a model keeps its box until its meshes change and go past it."""

    def setUp(self):
        self.model_bounds = load_addon_module("model_bounds")
        self.tlm = load_addon_module("tlm")
        self.file = self.tlm.TlmFile("OGI", "Test")
        # A joint resting 2 up, turned a quarter about Z
        bind = [0.0, -1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 2.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0]
        self.root = {"kind": "ogi", "children": [
            {"kind": "armature", "joints": [{"index": 0, "parent": -1, "bind": bind}]},
            {"kind": "skin", "mesh": {"parts": [{"position": self.file.write_view([-1.0, 0.0, -1.0, 1.0, 1.5, 1.0], "f32")}]}},
            {"kind": "rigid_bodies", "children": [
                {"kind": "body", "joint": 0, "translation": [1.0, 0.0, 0.0], "rotation": [0.0, 0.0, 0.0, 1.0],
                 "mesh": {"parts": [{"position": self.file.write_view([0.0, 0.0, 0.0, 0.5, 0.0, 0.0], "f32")}]}},
            ]},
        ]}

    def test_bodies_are_on_their_joints_at_rest(self):
        # The body's points are 1 and 1.5 along the joint's X, which the joint turns to Y above its 2
        self.assertEqual(((-1.0, 0.0, -1.0), (1.0, 3.5, 1.0)), self.rounded(self.model_bounds.rest_bounds(self.file, self.root)))

    def test_a_box_stays_until_the_meshes_change_past_it(self):
        bounds = self.model_bounds.rest_bounds(self.file, self.root)
        # The game's box of a model that doesn't hold its meshes stays while they're as imported
        small = ((-0.5, 0.0, -0.5), (0.5, 1.0, 0.5))
        self.assertIsNone(self.model_bounds.fitted_box(small, bounds, bounds))
        moved = ((-1.0, 0.0, -1.0), (1.0, 4.0, 1.0))
        self.assertEqual(moved, self.model_bounds.fitted_box(small, moved, bounds))
        # A box holding the changed meshes stays, and a model made in Blender has nothing imported
        self.assertIsNone(self.model_bounds.fitted_box(((-2.0, -1.0, -2.0), (2.0, 5.0, 2.0)), moved, bounds))
        self.assertEqual(bounds, self.model_bounds.fitted_box(small, bounds, None))
        self.assertIsNone(self.model_bounds.fitted_box(small, None, None))

    def test_rounding_errors_are_the_same_bounds(self):
        bounds = ((-1.0, 0.0, -1.0), (1.0, 3.5, 1.0))
        self.assertTrue(self.model_bounds.same_bounds(bounds, ((-1.0000001, 0.0, -1.0), (1.0, 3.5000002, 1.0))))
        self.assertFalse(self.model_bounds.same_bounds(bounds, ((-1.0, 0.0, -1.0), (1.0, 3.51, 1.0))))

    @staticmethod
    def rounded(bounds):
        return tuple(tuple(round(value, 6) + 0.0 for value in corner) for corner in bounds)


class HullTests(unittest.TestCase):
    """The game collides convex hulls of 64 vertexes and 64 faces at most."""

    def setUp(self):
        self.hulls = load_addon_module("hulls")
        self.cube = [(x, y, z) for x in (0.0, 1.0) for y in (0.0, 1.0) for z in (0.0, 1.0)]
        self.cube_faces = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]

    def test_a_box_is_convex_whichever_way_its_faces_turn(self):
        self.assertTrue(self.hulls.is_convex(self.cube, self.cube_faces))
        self.assertTrue(self.hulls.is_convex(self.cube, [list(reversed(face)) for face in self.cube_faces]))

    def test_a_dent_is_not_convex(self):
        dented = list(self.cube)
        # The top's corner pushed down below the top's other corners
        dented[7] = (1.0, 1.0, 0.5)
        self.assertFalse(self.hulls.is_convex(dented, self.cube_faces))

    def test_hulls_past_the_games_limits_are_refused(self):
        self.hulls.check_hull("Hull", 64, 64)
        with self.assertRaises(ValueError) as raised:
            self.hulls.check_hull("Hull", 65, 30)
        self.assertIn("Hull has 65 vertexes and 30 faces", str(raised.exception))
        with self.assertRaises(ValueError):
            self.hulls.check_hull("Hull", 20, 65)


class RetargetPoseTests(unittest.TestCase):
    """A retargeted pose turns every matched bone from its own rest as much as the original's turned, whatever way its axes point,
    and moves it as far, scaled to the skeleton's size."""

    def setUp(self):
        self.retarget = load_addon_module("retarget")
        identity = (1.0, 0.0, 0.0, 0.0)
        # The original: a root, a spine above it and a head above that, all with the same axes
        self.original = {"J0": (None, ((0.0, 0.0, 0.0), identity)), "J1": ("J0", ((0.0, 1.0, 0.0), identity)), "J2": ("J1", ((0.0, 2.0, 0.0), _axis_angle((1.0, 0.0, 0.0), 30.0)))}
        # The new skeleton: twice as big, every bone with other axes, and an unmatched bone between the spine and the head
        self.incoming = {"A": (None, ((0.0, 0.0, 0.0), _axis_angle((0.0, 0.0, 1.0), 90.0))), "T": ("A", ((0.0, 1.0, 0.0), _axis_angle((0.0, 1.0, 0.0), 40.0))),
                         "B": ("T", ((0.0, 2.0, 0.0), _axis_angle((0.0, 0.0, 1.0), 90.0))), "C": ("B", ((0.0, 4.0, 0.0), _axis_angle((1.0, 1.0, 0.0), 70.0)))}
        self.bones = {"A": "J0", "B": "J1", "C": "J2"}
        self.poses = {"J0": ((0.3, 0.0, 0.0), _axis_angle((1.0, 0.0, 0.0), 90.0), (1.0, 1.0, 1.0)), "J1": ((0.0, 0.5, 0.0), _axis_angle((0.0, 1.0, 0.0), 45.0), (1.0, 1.0, 1.0)),
                      "J2": ((0.0, 0.0, 0.0), _axis_angle((0.0, 0.0, 1.0), 20.0), (2.0, 2.0, 2.0))}

    def test_the_size_ratio_is_the_extent_of_the_matched_bones(self):
        self.assertAlmostEqual(2.0, self.retarget.size_ratio(self.original, self.incoming, self.bones))
        self.assertAlmostEqual(1.0, self.retarget.size_ratio(self.original, self.incoming, {"A": "J0"}))

    def test_the_bones_turn_like_the_originals_in_the_world(self):
        result = self.retarget.retarget_pose(self.original, self.incoming, self.bones, self.poses, 2.0)

        original_deltas = self.retarget.world_deltas(self.original, self.poses)
        deltas = self.retarget.world_deltas(self.incoming, result)
        for name, original_name in self.bones.items():
            dot = sum(a * b for a, b in zip(original_deltas[original_name][0], deltas[name][0]))
            self.assertAlmostEqual(1.0, abs(dot), places=9, msg=name)

        self.assertEqual((2.0, 2.0, 2.0), result["C"][2])
        self.assertNotIn("T", result)

    def test_the_bones_move_as_far_as_the_originals_scaled(self):
        result = self.retarget.retarget_pose(self.original, self.incoming, self.bones, self.poses, 2.0)

        positions = {name: delta[1] for name, delta in self.retarget.world_deltas(self.incoming, result).items()}
        # The root moved 0.3 along X, twice as far here
        self.assertEqual([0.6, 0.0, 0.0], [round(value, 6) for value in positions["A"]])
        # The spine's offset turned up by the root's 90 degrees about X, plus the 0.5 it rose by (1.5 above where the original's
        # root put it, 1 for its rest offset), twice as far
        self.assertEqual([0.6, 0.0, 3.0], [round(value, 6) for value in positions["B"]])
        # The head keeps its rest offset from the spine, turned by the spine's turn, whatever the bone between them rests like
        self.assertEqual([0.6, 0.0, 5.0], [round(value, 6) for value in positions["C"]])

    def test_a_pose_of_the_same_skeleton_comes_back_as_it_is(self):
        result = self.retarget.retarget_pose(self.original, self.original, {name: name for name in self.original}, self.poses, 1.0)

        for name, pose in self.poses.items():
            for expected, actual in zip(pose, result[name]):
                self.assertEqual([round(value, 6) for value in expected], [round(value, 6) for value in actual], name)

    def test_a_bone_the_original_lacks_stays_at_rest_and_turns_what_is_under_it(self):
        # The original's spine isn't in the new skeleton, the head is a root there
        incoming = {"C": (None, ((0.0, 2.0, 0.0), _axis_angle((1.0, 1.0, 0.0), 70.0)))}

        result = self.retarget.retarget_pose(self.original, incoming, {"C": "J2"}, self.poses, 1.0)

        original_deltas = self.retarget.world_deltas(self.original, self.poses)
        deltas = self.retarget.world_deltas(incoming, result)
        self.assertAlmostEqual(1.0, abs(sum(a * b for a, b in zip(original_deltas["J2"][0], deltas["C"][0]))), places=9)
        self.assertEqual([round(value, 6) for value in original_deltas["J2"][1]], [round(value, 6) for value in deltas["C"][1]])


def _axis_angle(axis, degrees):
    half = math.radians(degrees) / 2.0
    length = math.sqrt(sum(value * value for value in axis))
    return (math.cos(half),) + tuple(value / length * math.sin(half) for value in axis)
