"""Pairing a rig's bones with the game's model's joints: guesses top down, and the rig's paired bones literally a subtree of the skeleton."""

import unittest

from fakes import load_addon_module

pairing = load_addon_module("pairing")

# A model's skeleton like Crash's, two spine joints over the hips, in Blender's axes (his left at +X, up Z), in joint index order
CRASH = [
    ("hips", None, (0.0, 0.0, 1.0)), ("spine1", "hips", (0.0, 0.0, 1.15)), ("spine2", "spine1", (0.0, 0.0, 1.3)),
    ("neck", "spine2", (0.0, 0.0, 1.5)), ("head", "neck", (0.0, 0.0, 1.6)),
    ("arm.L", "spine2", (0.2, 0.0, 1.45)), ("forearm.L", "arm.L", (0.45, 0.0, 1.45)), ("hand.L", "forearm.L", (0.7, 0.0, 1.45)),
    ("arm.R", "spine2", (-0.2, 0.0, 1.45)), ("forearm.R", "arm.R", (-0.45, 0.0, 1.45)), ("hand.R", "forearm.R", (-0.7, 0.0, 1.45)),
    ("thigh.L", "hips", (0.12, 0.0, 0.95)), ("shin.L", "thigh.L", (0.12, 0.0, 0.5)), ("foot.L", "shin.L", (0.12, -0.05, 0.08)),
    ("thigh.R", "hips", (-0.12, 0.0, 0.95)), ("shin.R", "thigh.R", (-0.12, 0.0, 0.5)), ("foot.R", "shin.R", (-0.12, -0.05, 0.08)),
]


def crash():
    return pairing.Skeleton(CRASH, {name: index for index, (name, _, _) in enumerate(CRASH)})


def rig(bones, scale=1.0, offset=(0.0, 0.0, 0.0), indexes=None):
    return pairing.Skeleton([(name, parent, tuple(value * scale + shift for value, shift in zip(head, offset))) for name, parent, head in bones], indexes)


# Another artist's human of the same build, named their way, twice as big and somewhere else
HUMAN = [
    ("pelvis", None, (0.0, 0.0, 1.0)), ("spine_01", "pelvis", (0.0, 0.0, 1.15)), ("spine_02", "spine_01", (0.0, 0.0, 1.3)),
    ("neck_01", "spine_02", (0.0, 0.0, 1.5)), ("head", "neck_01", (0.0, 0.0, 1.62)),
    ("upperarm_l", "spine_02", (0.21, 0.0, 1.44)), ("lowerarm_l", "upperarm_l", (0.46, 0.0, 1.44)), ("hand_l", "lowerarm_l", (0.71, 0.0, 1.44)),
    ("upperarm_r", "spine_02", (-0.21, 0.0, 1.44)), ("lowerarm_r", "upperarm_r", (-0.46, 0.0, 1.44)), ("hand_r", "lowerarm_r", (-0.71, 0.0, 1.44)),
    ("thigh_l", "pelvis", (0.11, 0.0, 0.96)), ("calf_l", "thigh_l", (0.11, 0.0, 0.51)), ("foot_l", "calf_l", (0.11, -0.06, 0.07)),
    ("thigh_r", "pelvis", (-0.11, 0.0, 0.96)), ("calf_r", "thigh_r", (-0.11, 0.0, 0.51)), ("foot_r", "calf_r", (-0.11, -0.06, 0.07)),
]


class SuggestTests(unittest.TestCase):
    def test_a_rig_of_the_same_build_pairs_by_where_its_bones_rest(self):
        human = rig(HUMAN, 2.0, (5.0, 3.0, 0.0))

        pairs = pairing.suggest(crash(), human)

        self.assertEqual({"pelvis": "hips", "spine_01": "spine1", "spine_02": "spine2", "neck_01": "neck", "head": "head", "upperarm_l": "arm.L",
                          "lowerarm_l": "forearm.L", "hand_l": "hand.L", "upperarm_r": "arm.R", "lowerarm_r": "forearm.R", "hand_r": "hand.R",
                          "thigh_l": "thigh.L", "calf_l": "shin.L", "foot_l": "foot.L", "thigh_r": "thigh.R", "calf_r": "shin.R", "foot_r": "foot.R"}, pairs)
        self.assertEqual([], pairing.problems(crash(), human, pairs))

    def test_bones_of_the_joints_index_or_name_are_certain(self):
        # The model's own skeleton imported again, its bones moved around: they keep being their joints
        moved = pairing.Skeleton([(name, parent, (head[0] + 3.0, head[1], head[2] * 0.2)) for name, parent, head in CRASH],
                                 {name: index for index, (name, _, _) in enumerate(CRASH)})

        pairs = pairing.suggest(crash(), moved)

        self.assertEqual({name: name for name, _, _ in CRASH}, pairs)

    def test_a_bone_added_where_no_joint_is_a_new_joint(self):
        # The report's first rig: Add > Twin Tech > OGI Model's joint 0 and a bone added at the cursor under it (Shift+A), which swung like
        # Crash's left thigh
        template = rig([("Joint 0", None, (0.0, 0.0, 0.0)), ("leg", "Joint 0", (0.0, 0.0, 0.0))], indexes={"Joint 0": 0})

        pairs = pairing.suggest(crash(), template)

        self.assertEqual({"Joint 0": "hips", "leg": None}, pairs)
        self.assertEqual(["leg"], pairing.new_joints(template, pairs))
        self.assertEqual([], pairing.problems(crash(), template, pairs))

    def test_bones_only_pair_with_the_children_of_their_parents_joint(self):
        # The report's second and third rigs: thighs and a spine under the bone of Crash's first spine joint. The spine is his second one,
        # his thighs are under his hips: the rig's are new joints
        rigged = rig([("Joint 0", None, (0.0, 0.0, 1.0)), ("Joint 1", "Joint 0", (0.0, 0.0, 1.15)), ("spine", "Joint 1", (0.0, 0.0, 1.3)),
                      ("thigh.L", "Joint 1", (0.12, 0.0, 0.95)), ("thigh.R", "Joint 1", (-0.12, 0.0, 0.95))], indexes={"Joint 0": 0, "Joint 1": 1})

        pairs = pairing.suggest(crash(), rigged)

        self.assertEqual({"Joint 0": "hips", "Joint 1": "spine1", "spine": "spine2", "thigh.L": None, "thigh.R": None}, pairs)
        self.assertEqual([], pairing.problems(crash(), rigged, pairs))

    def test_every_guess_is_a_subtree(self):
        # A rig of one spine bone, its arms and neck on it
        one_spine = [(name, "spine_01" if parent == "spine_02" else parent, head) for name, parent, head in HUMAN if name != "spine_02"]
        for bones in (HUMAN, HUMAN[:5], one_spine):
            target = rig(bones, 0.5)
            self.assertEqual([], pairing.problems(crash(), target, pairing.suggest(crash(), target)), bones)


class ProblemTests(unittest.TestCase):
    def test_one_spine_bone_where_the_model_has_two_is_refused(self):
        # The arms and neck hang off the rig's only spine bone, Crash's off his second
        target = rig([("pelvis", None, (0.0, 0.0, 1.0)), ("spine", "pelvis", (0.0, 0.0, 1.2)), ("neck", "spine", (0.0, 0.0, 1.5)),
                      ("upperarm_l", "spine", (0.2, 0.0, 1.45))])
        pairs = {"pelvis": "hips", "spine": "spine1", "neck": "neck", "upperarm_l": "arm.L"}

        problems = pairing.problems(crash(), target, pairs)

        self.assertEqual(2, len(problems))
        self.assertIn("neck stands for neck, whose parent is spine2, but spine above it stands for spine1", problems[0])
        self.assertIn("upperarm_l stands for arm.L, whose parent is spine2", problems[1])

    def test_what_is_under_a_new_joint_is_new(self):
        target = rig([("pelvis", None, (0.0, 0.0, 1.0)), ("tail", "pelvis", (0.0, 0.3, 1.0)), ("tip", "tail", (0.0, 0.6, 1.0))])

        problems = pairing.problems(crash(), target, {"pelvis": "hips", "tail": None, "tip": "spine1"})

        self.assertEqual(["tip stands for spine1, but tail above it stands for no joint: what's under a new joint is new too"], problems)

    def test_the_root_is_the_models_root_and_joints_are_one_bone(self):
        target = rig([("pelvis", None, (0.0, 0.0, 1.0)), ("a", "pelvis", (0.1, 0.0, 1.0)), ("b", "pelvis", (-0.1, 0.0, 1.0))])

        self.assertIn("isn't the model's root", pairing.problems(crash(), target, {"pelvis": "spine1"})[0])
        self.assertTrue(any("stand for the same joint thigh.L" in problem for problem in pairing.problems(crash(), target, {"pelvis": "hips", "a": "thigh.L", "b": "thigh.L"})))
        self.assertIn("No bone stands for a joint", pairing.problems(crash(), target, {})[0])

    def test_what_follows_a_left_out_joint_goes_to_its_nearest_paired_ancestor(self):
        target = rig(HUMAN[:5])
        pairs = pairing.suggest(crash(), target)

        self.assertEqual("spine_02", pairing.nearest_paired(crash(), "hand.L", pairs))
        self.assertIn("hand.L", pairing.left_out(crash(), pairs))


class ShapeTests(unittest.TestCase):
    def test_shape_keys_pair_by_name_then_in_order(self):
        self.assertEqual({"Blink": "Blink", "Frown": "Smile", "Wink": "Angry", "Extra": None},
                         pairing.pair_shapes(["Smile", "Blink", "Angry"], ["Blink", "Frown", "Wink", "Extra"]))


if __name__ == "__main__":
    unittest.main()
