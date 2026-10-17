"""What the objects under a model's root are: their types first, then Blender's rigging, with what's refused."""

import unittest

from fakes import load_addon_module

parts = load_addon_module("parts")
Info = parts.ObjectInfo


class OgiRoleTests(unittest.TestCase):
    def test_ordinary_rigging_decides(self):
        objects = [
            Info("Armature", "ARMATURE"),
            # Ctrl+P > With Automatic Weights: parented to the armature and deformed by it
            Info("Body Mesh", "MESH", parent="Armature", deformed_by=["Armature"]),
            Info("Face", "MESH", parent="Armature", deformed_by=["Armature"], shape_keys=3),
            # Ctrl+P > Bone
            Info("Hat", "MESH", parent="Armature", parent_type="BONE", parent_bone="Head"),
            Info("Sword", "MESH", follows=("Armature", "Hand")),
            Info("Base", "MESH"),
            Info("Props", "EMPTY"),
        ]

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual({"Armature": parts.ARMATURE, "Body Mesh": parts.SKIN, "Face": parts.SHAPE, "Hat": parts.BODY, "Sword": parts.BODY,
                          "Base": parts.BODY, "Props": parts.FOLDER}, roles)
        self.assertEqual([], problems)
        self.assertEqual("Head", parts.joint_bone(objects[3], "Armature"))
        self.assertEqual("Hand", parts.joint_bone(objects[4], "Armature"))
        self.assertIsNone(parts.joint_bone(objects[5], "Armature"))

    def test_types_and_holders_win_over_the_rigging(self):
        objects = [
            Info("Armature", "ARMATURE"),
            Info("Exit Points", "EMPTY", kind="exit_points"),
            Info("Hand", "EMPTY", parent="Exit Points"),
            Info("Collision Hulls", "EMPTY", kind="collision_hulls"),
            Info("Box Hull", "MESH", parent="Collision Hulls"),
            Info("Typed Hull", "MESH", twin_type="CollisionHull", follows=("Armature", "Spine")),
            Info("Imported Skin", "MESH", kind="skin", deformed_by=["Armature"]),
            Info("Point", "EMPTY", twin_type="ExitPoint"),
        ]

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual(parts.EXIT_POINT, roles["Hand"])
        self.assertEqual(parts.EXIT_POINT, roles["Point"])
        self.assertEqual(parts.HULL, roles["Box Hull"])
        self.assertEqual(parts.HULL, roles["Typed Hull"])
        self.assertEqual(parts.SKIN, roles["Imported Skin"])
        self.assertEqual(parts.FOLDER, roles["Exit Points"])
        self.assertEqual([], problems)

    def test_what_is_under_a_part_on_a_bone_follows_that_bone(self):
        objects = [
            Info("Armature", "ARMATURE"),
            Info("Gun", "MESH", follows=("Armature", "Hand")),
            # Ctrl+P > Object onto the gun: a muzzle, a sight on a holder under it, a scope on a bone parented part
            Info("Muzzle", "EMPTY", parent="Gun", twin_type="ExitPoint"),
            Info("Sight Holder", "EMPTY", parent="Gun"),
            Info("Sight", "MESH", parent="Sight Holder"),
            Info("Helmet", "MESH", parent="Armature", parent_type="BONE", parent_bone="Head"),
            Info("Visor", "MESH", parent="Helmet"),
            Info("Loose", "MESH", parent="Armature"),
        ]
        by_name = {info.name: info for info in objects}

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual([], problems)
        self.assertEqual(parts.EXIT_POINT, roles["Muzzle"])
        self.assertEqual({"Muzzle": "Hand", "Sight": "Hand", "Visor": "Head", "Loose": None},
                         {name: parts.joint_bone(by_name[name], "Armature", by_name) for name in ("Muzzle", "Sight", "Visor", "Loose")})

    def test_a_part_following_a_bone_by_its_child_of_and_by_its_parent_is_refused(self):
        objects = [
            Info("Armature", "ARMATURE"),
            # An imported exit point parented to another bone with Ctrl+P > Bone, keeping its Child Of
            Info("Hand Point", "EMPTY", parent="Armature", parent_type="BONE", parent_bone="Finger", twin_type="ExitPoint", follows=("Armature", "Hand")),
            Info("Gun", "MESH", follows=("Armature", "Hand")),
            # An imported hull parented to the gun, keeping its Child Of
            Info("Gun Hull", "MESH", parent="Gun", twin_type="CollisionHull", follows=("Armature", "Spine")),
        ]

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual([["Hand Point"], ["Gun Hull"]], [problem.subjects for problem in problems])
        self.assertTrue(all(problem.refused and problem.fix == "ttt.remove_child_of" for problem in problems))
        self.assertIn("Hand by a Child Of and Finger", problems[0].text)
        self.assertIn("Spine by a Child Of and Hand", problems[1].text)
        # The other way out keeps it under the model, which clearing its parent wouldn't
        self.assertIn("Ctrl+P > Object onto its holder or the model's root", problems[0].text)
        self.assertNotIn("Clear", problems[0].text)

    def test_parts_following_vertexes_are_refused(self):
        objects = [
            Info("Armature", "ARMATURE"),
            Info("Gun", "MESH", follows=("Armature", "Hand")),
            # Ctrl+P > Vertex onto the gun, an imported exit point keeping its Child Of, and a sight on a holder under it
            Info("Muzzle", "EMPTY", parent="Gun", parent_type="VERTEX", twin_type="ExitPoint"),
            Info("Eye Point", "EMPTY", parent="Gun", parent_type="VERTEX_3", twin_type="ExitPoint", follows=("Armature", "Eye")),
            Info("Holder", "EMPTY", parent="Gun", parent_type="VERTEX"),
            Info("Sight", "MESH", parent="Holder"),
        ]
        by_name = {info.name: info for info in objects}

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual([["Muzzle"], ["Eye Point"], ["Sight"]], [problem.subjects for problem in problems])
        self.assertTrue(all(problem.refused and "follows vertexes of" in problem.text for problem in problems))
        self.assertIsNone(parts.joint_bone(by_name["Muzzle"], "Armature", by_name))
        self.assertIsNone(parts.joint_bone(by_name["Sight"], "Armature", by_name))

    def test_a_second_armature_and_another_armatures_meshes_are_refused(self):
        objects = [
            Info("Rig", "ARMATURE"),
            Info("Old Rig", "ARMATURE"),
            Info("Old Skin", "MESH", deformed_by=["Old Rig"]),
            Info("Old Sword", "MESH", follows=("Old Rig", "Hand")),
        ]

        roles, problems = parts.ogi_roles(objects)

        self.assertTrue(all(problem.refused for problem in problems))
        self.assertEqual([["Old Rig"], ["Old Skin"], ["Old Sword"]], [problem.subjects for problem in problems])
        self.assertIn("2 armatures", problems[0].text)

    def test_other_kinds_of_objects_are_refused_with_how_to_fix_them(self):
        objects = [Info("Armature", "ARMATURE"), Info("Logo", "FONT"), Info("Lamp", "LIGHT")]

        roles, problems = parts.ogi_roles(objects)

        self.assertEqual(["Logo", "Lamp"], [problem.subjects[0] for problem in problems])
        self.assertTrue(all(problem.refused and "Convert > Mesh" in problem.text for problem in problems))
        self.assertNotIn("Logo", roles)

    def test_a_typed_skin_with_shape_keys_is_refused(self):
        roles, problems = parts.ogi_roles([Info("Armature", "ARMATURE"), Info("Skin", "MESH", twin_type="Skin", deformed_by=["Armature"], shape_keys=1)])

        self.assertEqual(parts.SKIN, roles["Skin"])
        self.assertEqual("ttt.make_blend_skin", problems[0].fix)


class ShapeKeyTests(unittest.TestCase):
    def test_the_blend_skins_meshes_share_their_shape_keys(self):
        self.assertEqual([], parts.shape_key_problems({"Face": ["Smile", "Blink"], "Teeth": ["Smile", "Blink"]}, 15))

        problems = parts.shape_key_problems({"Face": ["Smile", "Blink"], "Teeth": ["Smile"]}, 15)

        self.assertEqual(1, len(problems))
        self.assertEqual(["Face", "Teeth"], problems[0].subjects)

    def test_more_shape_keys_than_the_game_blends_are_refused(self):
        problems = parts.shape_key_problems({"Face": ["Shape %d" % index for index in range(17)]}, 15)

        self.assertEqual(1, len(problems))
        self.assertIn("17 shape keys", problems[0].text)


if __name__ == "__main__":
    unittest.main()
