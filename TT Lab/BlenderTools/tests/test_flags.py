import types
import unittest

from fakes import Action, Bone, load_addon_module

flags = load_addon_module("flags")


class BoneFlagsTests(unittest.TestCase):
    def setUp(self):
        self.walk = Action("Walk")
        self.run = Action("Run")

    def _imported(self, bone, action, independent_scaling):
        entry = flags.get_or_add_flags(bone, action)
        entry.independent_scaling = independent_scaling
        entry.imported = True
        entry.imported_independent_scaling = independent_scaling
        return entry

    def test_every_action_has_one_entry(self):
        bone = Bone("BONE_1")

        first = flags.get_or_add_flags(bone, self.walk)
        second = flags.get_or_add_flags(bone, self.walk)

        self.assertIs(first, second)
        self.assertEqual(len(bone.ttt_animation_flags), 1)

    # Resetting puts back what the file had, the settings added in Blender go
    def test_resetting_goes_back_to_the_imported_flags(self):
        bone = Bone("BONE_1")
        self._imported(bone, self.walk, True).independent_scaling = False
        flags.get_or_add_flags(bone, self.run).uses_additional_rotation = True

        flags.reset_to_imported(bone)

        self.assertEqual([entry.action for entry in bone.ttt_animation_flags], [self.walk])
        self.assertTrue(flags.find_flags(bone, "Walk").independent_scaling)

    def test_entries_of_deleted_actions_are_skipped(self):
        bone = Bone("BONE_1")
        flags.get_or_add_flags(bone, self.walk)
        bone.ttt_animation_flags[0].action = None

        self.assertIsNone(flags.find_flags(bone, "Walk"))


class InheritScaleTests(unittest.TestCase):
    def setUp(self):
        self.walk = Action("Walk")
        self.run = Action("Run")
        self.scaled = Bone("BONE_1")
        flags.get_or_add_flags(self.scaled, self.walk).independent_scaling = True
        flags.get_or_add_flags(self.scaled, self.run).independent_scaling = False
        self.other = Bone("BONE_2")

    def armature(self, action):
        return types.SimpleNamespace(
            animation_data=types.SimpleNamespace(action=action),
            data=types.SimpleNamespace(bones=[self.scaled, self.other]),
        )

    def test_independent_scaling_turns_scale_inheritance_off(self):
        flags.apply_inherit_scale(self.armature(self.walk))

        self.assertEqual(self.scaled.inherit_scale, "NONE")
        self.assertEqual(self.other.inherit_scale, "FULL")

    def test_switching_animations_turns_it_back_on(self):
        flags.apply_inherit_scale(self.armature(self.walk))
        flags.apply_inherit_scale(self.armature(self.run))

        self.assertEqual(self.scaled.inherit_scale, "FULL")

    def test_no_animation_inherits_scale(self):
        flags.apply_inherit_scale(self.armature(None))
        self.assertEqual(self.scaled.inherit_scale, "FULL")

        armature = self.armature(None)
        armature.animation_data = None
        flags.apply_inherit_scale(armature)
        self.assertEqual(self.scaled.inherit_scale, "FULL")
