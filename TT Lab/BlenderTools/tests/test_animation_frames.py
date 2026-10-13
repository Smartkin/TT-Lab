import math
import struct
import unittest

from fakes import load_addon_module

animation_frames = load_addon_module("animation_frames")
tlm_math = load_addon_module("tlm_math")


def shortest_turn(start, end):
    cross = (start[1] * end[2] - start[2] * end[1], start[2] * end[0] - start[0] * end[2], start[0] * end[1] - start[1] * end[0])
    return tlm_math.quat_normalized((1.0 + sum(a * b for a, b in zip(start, end)),) + cross)


def unit(vector):
    length = math.sqrt(sum(value * value for value in vector))
    return tuple(value / length for value in vector)


def along(start, end, share):
    angle = math.acos(max(-1.0, min(1.0, sum(a * b for a, b in zip(start, end)))))
    return tuple((math.sin((1 - share) * angle) * a + math.sin(share * angle) * b) / math.sin(angle) for a, b in zip(start, end))


def passing_gimbal_lock():
    """A thigh's run cycle, made up: its X axis passes 5 degrees from where gimbal lock turns it to (Z) between two frames and comes back
    far from it, the bone turned from X the shortest way."""
    closest = (math.sin(math.radians(5)), 0.0, math.cos(math.radians(5)))
    directions = [tuple(c * math.cos(math.radians(degrees)) + u * math.sin(math.radians(degrees)) for c, u in zip(closest, (0.0, 1.0, 0.0)))
                  for degrees in (-60, -35, -12, 12, 35, 60)]
    far = unit((0.9, 0.0, 0.44))
    start, end = directions[-1], directions[0]
    for step in range(1, 7):
        share = step / 7 * 2
        directions.append(along(start, far, share) if share <= 1 else along(far, end, share - 1))

    return [shortest_turn((1.0, 0.0, 0.0), direction) for direction in directions]


def from_angles(x, y, z):
    """The game's rotation of Euler angles in 4096ths of a turn: about X, then Y, then Z."""
    turn = lambda axis, raw: tuple([math.cos(raw / 4096 * math.pi)] + [math.sin(raw / 4096 * math.pi) if index == axis else 0.0 for index in range(3)])
    return tlm_math.quat_multiply(turn(2, z), tlm_math.quat_multiply(turn(1, y), turn(0, x)))


def animation_bytes(frames, joints):
    """An animation as the game stores it: every joint's channels (translation, rotation, scale) as (values, static), its additional
    rotation flag"""
    statics, animated, settings = [], [], []
    per_frame = [[] for _ in range(frames)]
    for flag, channels in joints:
        choices, static_index, animated_index = 0, len(statics), len(per_frame[0]) if frames else 0
        for channel, (values, static) in enumerate(channels):
            if static:
                choices |= 1 << channel
                statics.append(values[0])
            else:
                for frame in range(frames):
                    per_frame[frame].append(values[frame])

        settings.append(struct.pack("<4H", (9 << 8) | (flag << 12), choices, static_index, animated_index))

    packed = len(joints) | (len(statics) * 2 & 0xFFE) << 10 | len(per_frame[0]) << 22
    return (struct.pack("<IH", packed, frames) + b"".join(settings) + struct.pack("<%dh" % len(statics), *statics) +
            b"".join(struct.pack("<%dh" % len(values), *values) for values in per_frame))


class FrameTests(unittest.TestCase):
    # The game blends a joint angle by angle: near gimbal lock the angles of nearby rotations are far apart and its blends strayed from
    # the turns, the run's thigh by a quarter of the leg. Turned away from it they follow them
    def test_joints_near_gimbal_lock_get_a_frame_away_from_it(self):
        track = passing_gimbal_lock()

        frame = animation_frames.choose_frame([track], animation_frames.IDENTITY)

        self.assertGreater(animation_frames.largest_deviation([track], animation_frames.IDENTITY), 5.0)
        self.assertLess(animation_frames.largest_deviation([track], frame), 1.0)

    def test_joints_blending_well_keep_their_frame(self):
        smooth = [tlm_math.quat_normalized((1.0, 0.1 * math.sin(frame / 3), 0.05 * math.cos(frame / 5), 0.002 * frame)) for frame in range(20)]
        current = tlm_math.quat_normalized((0.9, 0.1, 0.2, 0.3))

        self.assertEqual(animation_frames.IDENTITY, animation_frames.choose_frame([smooth], animation_frames.IDENTITY))
        self.assertEqual(current, animation_frames.choose_frame([smooth], current))
        self.assertEqual(current, animation_frames.choose_frame([[current] * 5], current))

    # Turning the angles' axes about gimbal lock moves the last angle of every frame by as much, the blends stay the same
    def test_only_where_gimbal_lock_is_matters(self):
        track = passing_gimbal_lock()
        frame = animation_frames.choose_frame([track], animation_frames.IDENTITY)
        about_lock = tlm_math.quat_rotate(frame, (0.0, 0.0, 1.0))
        turned = tlm_math.quat_multiply((math.cos(0.6), *(value * math.sin(0.6) for value in about_lock)), frame)

        self.assertAlmostEqual(animation_frames.largest_deviation([track], frame), animation_frames.largest_deviation([track], turned), places=6)

    @unittest.skipIf(animation_frames.numpy is None, "without numpy only the frame by frame costs are there")
    def test_costs_are_the_same_frame_by_frame_and_on_arrays(self):
        tracks = [passing_gimbal_lock(), [tlm_math.quat_normalized((1.0, 0.3 * math.sin(frame), 0.2, 0.1 * frame)) for frame in range(9)]]
        frames = [animation_frames.IDENTITY, tlm_math.quat_normalized((0.5, 0.5, -0.5, 0.2))]
        arrays = animation_frames._costs(animation_frames._Prepared(tracks), frames)
        numpy = animation_frames.numpy
        try:
            animation_frames.numpy = None
            floats = animation_frames._costs(animation_frames._Prepared(tracks), frames)
        finally:
            animation_frames.numpy = numpy

        for array_cost, float_cost in zip(arrays, floats):
            self.assertAlmostEqual(array_cost[0], float_cost[0], places=12)
            self.assertAlmostEqual(array_cost[1], float_cost[1], places=12)


class GameAngleTests(unittest.TestCase):
    def test_angles_are_read_as_the_game_stores_them(self):
        exact = animation_bytes(3, [
            (False, [([0], True), ([0], True), ([0], True), ([10, 20, 30], False), ([5], True), ([-40, -50, -60], False),
                     ([4096], True), ([4096], True), ([4096], True)]),
            (True, [([1], True), ([2], True), ([3], True), ([100], True), ([200], True), ([300], True), ([4096], True), ([4096], True), ([4096], True)]),
        ])

        angles = [(turned, [tuple(int(value) for value in frame) for frame in frames]) for turned, frames in animation_frames.game_angles(exact)]

        self.assertEqual([(False, [(10, 5, -40), (20, 5, -50), (30, 5, -60)]), (True, [(100, 200, 300)] * 3)], angles)

    # TT Lab keeps the game's angles of frames whose keys still hold them, the additional rotation an animation turns them by can't change
    def test_frames_holding_the_games_angles_are_found(self):
        additional = tlm_math.quat_normalized((0.9, 0.3, -0.2, 0.1))
        game = [(100, 200, 300), (150, 250, 350)]
        keys = [tlm_math.quat_multiply(additional, from_angles(*angles)) for angles in game]
        nudged = [tlm_math.quat_multiply(key, (math.cos(math.radians(0.5)), math.sin(math.radians(0.5)), 0.0, 0.0)) for key in keys]

        self.assertTrue(animation_frames.holds_any(game, keys, additional))
        self.assertTrue(animation_frames.holds_any(game, [nudged[0], keys[1]], additional))
        self.assertFalse(animation_frames.holds_any(game, nudged, additional))
        self.assertFalse(animation_frames.holds_any(game, keys, None))


if __name__ == "__main__":
    unittest.main()
