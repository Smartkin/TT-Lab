"""The game's axes in Blender: exact both ways, negative zeros included, and the same as the reflections they stand for."""

import itertools
import math
import random
import struct
import unittest

from fakes import load_addon_module

axes = load_addon_module("axes")
tlm_math = load_addon_module("tlm_math")

# The game's (x, y, z) is Blender's (-x, -z, y)
M = [[-1.0, 0.0, 0.0], [0.0, 0.0, -1.0], [0.0, 1.0, 0.0]]
D = [[-1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]


def _bits(values):
    return struct.pack("<%df" % len(values), *values)


def _flat(rows):
    return [value for row in rows for value in row]


def _multiply(a, b):
    return [[sum(a[row][k] * b[k][column] for k in range(len(b))) for column in range(len(b[0]))] for row in range(len(a))]


def _transposed(a):
    return [list(column) for column in zip(*a)]


def _homogeneous(linear):
    return [list(row) + [0.0] for row in linear] + [[0.0, 0.0, 0.0, 1.0]]


def _close(test, a, b, tolerance=1e-6):
    test.assertEqual(len(a), len(b))
    for x, y in zip(a, b):
        test.assertAlmostEqual(x, y, delta=tolerance)


def _random_values(generator, count):
    """Floats as files have them (single precision), with zeros of either sign among them."""
    values = []
    for _ in range(count):
        kind = generator.random()
        if kind < 0.1:
            values.append(-0.0)
        elif kind < 0.2:
            values.append(0.0)
        else:
            values.append(struct.unpack("<f", struct.pack("<f", generator.uniform(-1000.0, 1000.0)))[0])

    return values


def _random_rotation(generator):
    q = [generator.gauss(0.0, 1.0) for _ in range(4)]
    length = math.sqrt(sum(value * value for value in q))
    return tuple(value / length for value in q)


def _rotation_rows(q_xyzw):
    return [row[:3] for row in tlm_math.compose((0.0, 0.0, 0.0), tlm_math.xyzw_to_wxyz(q_xyzw))[:3]]


def _euler_rows(angles, order):
    """Blender's rotation of Euler angles in the order they're applied, the first turned first."""
    turns = {
        "X": lambda a: [[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]],
        "Y": lambda a: [[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]],
        "Z": lambda a: [[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]],
    }
    result = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    for axis in order:
        result = _multiply(turns[axis](angles["XYZ".index(axis)]), result)

    return result


class ExactTests(unittest.TestCase):
    def setUp(self):
        self.generator = random.Random(4)

    def test_points_come_back_to_the_bit(self):
        values = _random_values(self.generator, 300)
        self.assertEqual(_bits(values), _bits(axes.points_back(axes.points(values))))
        self.assertEqual(_bits(values), _bits(axes.joint_points(axes.joint_points(values))))
        for start in range(0, 300, 3):
            point = values[start:start + 3]
            self.assertEqual(_bits(point), _bits(axes.point_back(axes.point(point))))
            self.assertEqual(_bits(point), _bits(axes.scale_back(axes.scale(point))))
            self.assertEqual(_bits(point), _bits(axes.euler_back(axes.euler(point))))

    def test_rotations_come_back_to_the_bit(self):
        values = _random_values(self.generator, 400)
        for start in range(0, 400, 4):
            q = values[start:start + 4]
            self.assertEqual(_bits(q), _bits(axes.rotation_back(axes.rotation(q))))
            self.assertEqual(_bits(q), _bits(axes.joint_rotation(axes.joint_rotation(q))))

    def test_matrices_come_back_to_the_bit(self):
        for _ in range(20):
            rows = [_random_values(self.generator, 4) for _ in range(3)] + [[0.0, 0.0, 0.0, 1.0]]
            self.assertEqual(_bits(_flat(rows)), _bits(_flat(axes.matrix_back(axes.matrix(rows)))))
            self.assertEqual(_bits(_flat(rows)), _bits(_flat(axes.bone_matrix_back(axes.bone_matrix(rows)))))
            self.assertEqual(_bits(_flat(rows)), _bits(_flat(axes.joint_matrix(axes.joint_matrix(rows)))))

    def test_negative_zeros_stay(self):
        self.assertEqual(_bits([-0.0, 0.0, -0.0]), _bits(axes.point_back(axes.point([-0.0, 0.0, -0.0]))))
        self.assertEqual(_bits([0.0, -0.0, 0.0, -0.0]), _bits(axes.rotation_back(axes.rotation([0.0, -0.0, 0.0, -0.0]))))

    def test_boxes_come_back_to_the_bit(self):
        for _ in range(20):
            low = _random_values(self.generator, 4)
            high = [value + abs(extra) for value, extra in zip(low, _random_values(self.generator, 4))]
            converted = axes.box(low, high)
            self.assertEqual(_bits(low + high), _bits(list(axes.box_back(*converted)[0]) + list(axes.box_back(*converted)[1])))
            self.assertTrue(all(a <= b for a, b in zip(*converted)))

    def test_corners_turn_around_and_back(self):
        corners = list(range(12))
        turned = axes.triangles(corners)
        self.assertEqual([0, 2, 1, 3, 5, 4, 6, 8, 7, 9, 11, 10], turned)
        self.assertEqual(corners, axes.triangles(turned))
        uvs = [float(value) for value in range(24)]
        self.assertEqual([0.0, 1.0, 4.0, 5.0, 2.0, 3.0], axes.corner_values(uvs, 2)[:6])
        self.assertEqual(uvs, axes.corner_values(axes.corner_values(uvs, 2), 2))
        self.assertEqual([4, 7, 6, 5], axes.face([4, 5, 6, 7]))
        self.assertEqual([4, 5, 6, 7], axes.face(axes.face([4, 5, 6, 7])))


class ReflectionTests(unittest.TestCase):
    def setUp(self):
        self.generator = random.Random(9)

    def test_points_are_the_reflection(self):
        for _ in range(10):
            point = [self.generator.uniform(-10.0, 10.0) for _ in range(3)]
            _close(self, [row[0] for row in _multiply(M, [[value] for value in point])], axes.point(point))
            _close(self, [row[0] for row in _multiply(D, [[value] for value in point])], axes.joint_point(point))

    def test_boxes_hold_their_corners(self):
        low, high = axes.box((-1.0, 0.0, -2.0), (3.0, 4.0, 5.0))
        self.assertEqual(((-3.0, -5.0, 0.0), (1.0, 2.0, 4.0)), (low, high))
        for corner in ((-1.0, 0.0, -2.0), (3.0, 4.0, 5.0), (3.0, 0.0, -2.0)):
            point = axes.point(corner)
            self.assertTrue(all(a <= value <= b for a, value, b in zip(low, point, high)))

    def test_crash_faces_minus_y_with_his_left_at_plus_x(self):
        # The game's models face +z and are Y up; Crash's left foot is at -x
        self.assertEqual((0.0, -1.0, 0.0), axes.point((0.0, 0.0, 1.0)))
        self.assertEqual((0.0, 0.0, 1.0), axes.point((0.0, 1.0, 0.0)))
        self.assertEqual((0.2, 0.0, 0.0), axes.point((-0.2, 0.0, 0.0)))

    def test_transforms_are_conjugated(self):
        for _ in range(10):
            q = _random_rotation(self.generator)
            translation = [self.generator.uniform(-5.0, 5.0) for _ in range(3)]
            rows = tlm_math.compose(translation, tlm_math.xyzw_to_wxyz(q))
            m4, d4 = _homogeneous(M), _homogeneous(D)
            _close(self, _flat(_multiply(_multiply(m4, rows), _transposed(m4))), _flat(axes.matrix(rows)))
            _close(self, _flat(_multiply(_multiply(m4, rows), d4)), _flat(axes.bone_matrix(rows)))
            _close(self, _flat(_multiply(_multiply(d4, rows), d4)), _flat(axes.joint_matrix(rows)))

    def test_rotations_are_the_transforms_rotations(self):
        for _ in range(10):
            q = _random_rotation(self.generator)
            rotated = _rotation_rows(q)
            _close(self, _flat(_multiply(_multiply(M, rotated), _transposed(M))), _flat(_rotation_rows(axes.rotation(q))))
            _close(self, _flat(_multiply(_multiply(D, rotated), D)), _flat(_rotation_rows(axes.joint_rotation(q))))
            # A light keeps its Z axis along the game's
            _close(self, _flat(_multiply(_multiply(M, rotated), D)), _flat(_rotation_rows(axes.frame_rotation(q))))
            _close(self, q if q[3] * axes.frame_rotation_back(axes.frame_rotation(q))[3] >= 0 else [-value for value in q],
                   axes.frame_rotation_back(axes.frame_rotation(q)))

    def test_scales_go_along_their_axes(self):
        scale = (2.0, 3.0, 5.0)
        rows = tlm_math.compose((0.0, 0.0, 0.0), (1.0, 0.0, 0.0, 0.0), scale)
        converted = axes.matrix(rows)
        self.assertEqual(axes.scale(scale), (converted[0][0], converted[1][1], converted[2][2]))

    def test_dynamic_sceneries_euler_angles_turn_the_same(self):
        for _ in range(10):
            angles = [self.generator.uniform(-math.pi, math.pi) for _ in range(3)]
            game = _euler_rows(angles, "XYZ")
            blender = _euler_rows(axes.euler(angles), axes.BLENDER_EULER_ORDER)
            _close(self, _flat(_multiply(_multiply(M, game), _transposed(M))), _flat(blender))


def _signed_permutations():
    for order in itertools.permutations(range(3)):
        for signs in itertools.product((1, -1), repeat=3):
            yield tuple(sign * (axis + 1) for axis, sign in zip(order, signs))


# Every aim: the 24 signed permutations of the axes that keep the frame's handedness
AIMS = [aim for aim in _signed_permutations() if axes.is_aim(aim)]
S = [[-1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]


def _aim_matrix(aim):
    identity = [[1.0 if row == column else 0.0 for column in range(4)] for row in range(4)]
    return [row[:3] for row in axes.aimed(identity, aim)[:3]]


def _determinant(m):
    return (m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1]) - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
            + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]))


def _column(m, index):
    return [m[row][index] for row in range(3)]


def _bone(frame, aim):
    return [row[:3] for row in axes.aimed(_homogeneous(frame), aim)[:3]]


class AimTests(unittest.TestCase):
    """A bone's own axes: picked of its joint's to point down its limb, exact both ways, twins mirrored the way Blender mirrors them."""

    def setUp(self):
        self.generator = random.Random(4)

    def test_aims_are_the_turns_among_the_signed_permutations(self):
        self.assertEqual(len(AIMS), 24)
        self.assertEqual(axes.IDENTITY_AIM, (1, 2, 3))
        self.assertEqual(_aim_matrix(axes.IDENTITY_AIM), [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]])
        for aim in _signed_permutations():
            self.assertEqual(axes.is_aim(aim), _determinant(_aim_matrix(aim)) > 0, aim)

        for value in [(1, 2), (1, 2, 2), (0, 1, 2), (1, 2, 4), (-1, 2, 3), "abc", None]:
            self.assertFalse(axes.is_aim(value), value)

    def test_rests_come_back_to_the_bit(self):
        for aim in AIMS:
            for _ in range(10):
                rest = [_random_values(self.generator, 4) for _ in range(3)] + [[0.0, 0.0, 0.0, 1.0]]
                self.assertEqual(_bits(_flat(axes.unaimed(axes.aimed(rest, aim), aim))), _bits(_flat(rest)))
                matrix = [_random_values(self.generator, 4) for _ in range(3)] + [[0.0, 0.0, 0.0, 1.0]]
                self.assertEqual(_bits(_flat(axes.unaimed_matrix(axes.aimed_matrix(matrix, aim), aim))), _bits(_flat(matrix)))
                vector = _random_values(self.generator, 3)
                self.assertEqual(_bits(axes.unaimed_vector(axes.aimed_vector(vector, aim), aim)), _bits(vector))
                self.assertEqual(_bits(axes.unaimed_scale(axes.aimed_scale(vector, aim), aim)), _bits(vector))
                rotation = (1.0,) + tuple(vector)
                self.assertEqual(_bits(axes.unaimed_rotation_wxyz(axes.aimed_rotation_wxyz(rotation, aim), aim)), _bits(rotation))
                points = _random_values(self.generator, 12)
                aimed = axes.aimed_points(points, aim)
                self.assertEqual(_bits(aimed), _bits([value for start in range(0, 12, 3) for value in axes.aimed_vector(points[start:start + 3], aim)]))
                self.assertEqual(_bits(axes.unaimed_points(aimed, aim)), _bits(points))

    def test_the_conversions_are_the_picks(self):
        for aim in AIMS:
            c = _aim_matrix(aim)
            for _ in range(5):
                q = _random_rotation(self.generator)
                rest = _homogeneous(_rotation_rows(q))
                _close(self, _flat(axes.aimed(rest, aim)), _flat(_homogeneous(_multiply(_rotation_rows(q), c))))
                _close(self, _flat(axes.aimed_offset(rest, aim)), _flat(_homogeneous(_multiply(_transposed(c), _rotation_rows(q)))))
                turn = _rotation_rows(_random_rotation(self.generator))
                conjugated = _multiply(_multiply(_transposed(c), turn), c)
                _close(self, _flat(axes.aimed_matrix(_homogeneous(turn), aim)), _flat(_homogeneous(conjugated)))
                # The quaternion of C^-1 R C is R's turn about the axis in the new frame
                aimed = axes.aimed_rotation_wxyz(tlm_math.xyzw_to_wxyz(q), aim)
                _close(self, _flat(_rotation_rows(tlm_math.wxyz_to_xyzw(aimed))), _flat(_multiply(_multiply(_transposed(c), _rotation_rows(q)), c)))
                vector = [self.generator.uniform(-5.0, 5.0) for _ in range(3)]
                _close(self, list(axes.aimed_vector(vector, aim)), [sum(c[k][row] * vector[k] for k in range(3)) for row in range(3)])
                scale = [self.generator.uniform(0.1, 3.0) for _ in range(3)]
                scaled = _multiply(_multiply(_transposed(c), [[scale[0], 0, 0], [0, scale[1], 0], [0, 0, scale[2]]]), c)
                _close(self, list(axes.aimed_scale(scale, aim)), [scaled[0][0], scaled[1][1], scaled[2][2]])

    def test_an_exit_point_keeps_its_joints_frame_whichever_its_bones_axes(self):
        for aim in AIMS:
            c = _aim_matrix(aim)
            _close(self, _flat(_rotation_rows(axes.aim_rotation(aim))), _flat(c), 1e-12)
            for _ in range(5):
                q = _random_rotation(self.generator)
                # Relative to the aimed bone C^-1 R, so that the bone's C puts it back at the joint's R
                turned = tlm_math.wxyz_to_xyzw(axes.exit_rotation_wxyz(tlm_math.xyzw_to_wxyz(q), aim))
                _close(self, _flat(_multiply(c, _rotation_rows(turned))), _flat(_rotation_rows(q)), 1e-12)

    def test_a_bone_runs_along_its_way_with_z_up_then_forward(self):
        identity = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
        # An arm out to the side: no axis left across, Z up, X forward (-Y)
        self.assertEqual(axes.aim_of(identity, (1.0, 0.1, 0.0)), (-2, 1, 3))
        # A leg down: X across to the left, Z back, Blender's roll 0
        self.assertEqual(axes.aim_of(identity, (0.1, 0.0, -1.0)), (1, -3, 2))
        # A spine up: X left, Z forward
        self.assertEqual(axes.aim_of(identity, (0.0, -0.1, 1.0)), (1, 3, -2))
        # A foot forward: X left, Z down
        self.assertEqual(axes.aim_of(identity, (0.0, -1.0, 0.2)), (1, -2, -3))
        # No way to point: up like Blender's new bones, or along its parent's bone with its roll
        self.assertEqual(axes.aim_of(identity, (0.0, 0.0, 0.0)), (1, 3, -2))
        self.assertEqual(axes.aim_of(identity, None, parent=((1.0, 0.0, 0.0), (0.0, 0.0, 1.0))), (-2, 1, 3))
        frame = _rotation_rows(_random_rotation(self.generator))
        for axis in range(3):
            for sign in (1, -1):
                direction = [sign * frame[row][axis] * 3.0 + 0.1 * frame[row][(axis + 1) % 3] for row in range(3)]
                aim = axes.aim_of(frame, direction)
                self.assertTrue(axes.is_aim(aim))
                self.assertEqual(aim[1], sign * (axis + 1))

    def test_the_child_closest_to_an_axis_is_the_way(self):
        identity = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
        # A hip: the spine straight up, the legs down and out, their mean down
        ways = [(0.3, 0.0, -0.9), (0.0, 0.0, 1.0), (-0.3, 0.0, -0.9)]
        self.assertEqual(axes.best_way(identity, ways), 1)
        self.assertEqual(axes.best_way(identity, [(0.0, 0.0, 1.0), (1.0, 0.0, 0.0)]), 0)
        self.assertIsNone(axes.best_way(identity, [(0.0, 0.0, 0.0)]))
        self.assertIsNone(axes.best_way(identity, []))

    def test_mirror_images_get_blenders_mirrored_axes(self):
        mirrors = [aim for aim in _signed_permutations() if not axes.is_aim(aim)]
        for trial in range(400):
            left = _rotation_rows(_random_rotation(self.generator))
            # Twins of the game: mirror images with the axes picked and turned around somehow, a turn again
            q = _aim_matrix(mirrors[trial % len(mirrors)])
            right = _multiply(_multiply(S, left), q)
            direction = [self.generator.uniform(-1.0, 1.0) for _ in range(3)]
            left_bone = _bone(left, axes.aim_of(left, direction))
            right_bone = _bone(right, axes.aim_of(right, [-direction[0], direction[1], direction[2]]))
            mirrored = _multiply(S, left_bone)
            _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
            _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
            _close(self, _column(right_bone, 0), [-value for value in _column(mirrored, 0)], 1e-12)

    def test_mirror_images_break_deeper_ties_alike(self):
        mirrors = [aim for aim in _signed_permutations() if not axes.is_aim(aim)]
        # Two axes as close to the way and leaning alike, the third straight across: only how far up they point tells them apart
        turn = math.atan(3.0)
        u, v = (0.0, math.cos(turn), math.sin(turn)), (0.0, -math.sin(turn), math.cos(turn))
        frame = _transposed([list(u), list(v), [1.0, 0.0, 0.0]])
        if _determinant(frame) < 0:
            frame = _transposed([list(u), list(v), [-1.0, 0.0, 0.0]])

        way = [a + b for a, b in zip(u, v)]
        # Two rolls of the same score leaning across and up alike: only how far forward they point tells them apart
        x, s = 1.0 / (4.0 * math.sqrt(2.0)), 1.0 / math.sqrt(2.0)
        z = math.sqrt(0.5 - x * x)
        along = (math.sqrt(2.0) * z, 0.0, -math.sqrt(2.0) * x)
        rolled = _transposed([list(along), [x, s, z], [x, -s, z]])
        self.assertGreater(_determinant(rolled), 0.0)
        for left, direction in ((frame, way), (rolled, along)):
            left_bone = _bone(left, axes.aim_of(left, direction, 1.0))
            mirrored = _multiply(S, left_bone)
            for mirror in mirrors:
                right = _multiply(_multiply(S, left), _aim_matrix(mirror))
                right_bone = _bone(right, axes.aim_of(right, [-direction[0], direction[1], direction[2]], -1.0))
                _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-9)
                _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-9)

    def test_bones_on_the_middle_line_get_x_across(self):
        for trial in range(200):
            angle = self.generator.uniform(-math.pi, math.pi)
            across = [1.0 if trial % 2 == 0 else -1.0, 0.0, 0.0]
            up = [0.0, math.cos(angle), math.sin(angle)]
            forward = [0.0, -math.sin(angle), math.cos(angle)]
            columns = [across, up, forward]
            order = list(itertools.permutations(range(3)))[trial % 6]
            picked = [columns[index] for index in order]
            if _determinant(_transposed(picked)) < 0:
                picked[2] = [-value for value in picked[2]]

            frame = _transposed(picked)
            way = self.generator.uniform(-math.pi, math.pi)
            bone = _bone(frame, axes.aim_of(frame, (0.0, math.cos(way), math.sin(way))))
            # To the model's left, Blender's roll 0 of a bone standing up or hanging down
            self.assertAlmostEqual(bone[0][0], 1.0, delta=1e-12)
            self.assertAlmostEqual(bone[0][1], 0.0, delta=1e-12)
            self.assertAlmostEqual(bone[0][2], 0.0, delta=1e-12)

    def test_a_posed_bone_deforms_the_same_whichever_its_axes(self):
        for aim in AIMS:
            rest = _homogeneous(_rotation_rows(_random_rotation(self.generator)))
            for row in range(3):
                rest[row][3] = self.generator.uniform(-2.0, 2.0)

            pose = _homogeneous(_rotation_rows(_random_rotation(self.generator)))
            for row in range(3):
                pose[row][3] = self.generator.uniform(-1.0, 1.0)

            aimed_rest, aimed_pose = axes.aimed(rest, aim), axes.aimed_matrix(pose, aim)
            # What the armature moves a vertex by: the posed frame times the rest's inverse
            inverse = _homogeneous(_transposed([row[:3] for row in rest[:3]]))
            aimed_inverse = _homogeneous(_transposed([row[:3] for row in aimed_rest[:3]]))
            for row in range(3):
                inverse[row][3] = -sum(inverse[row][k] * rest[k][3] for k in range(3))
                aimed_inverse[row][3] = -sum(aimed_inverse[row][k] * aimed_rest[k][3] for k in range(3))

            _close(self, _flat(_multiply(_multiply(rest, pose), inverse)), _flat(_multiply(_multiply(aimed_rest, aimed_pose), aimed_inverse)))

    def test_what_follows_a_bone_stays_where_it_was(self):
        for aim in AIMS:
            rest = _homogeneous(_rotation_rows(_random_rotation(self.generator)))
            follower = _homogeneous(_rotation_rows(_random_rotation(self.generator)))
            for row in range(3):
                follower[row][3] = self.generator.uniform(-1.0, 1.0)

            # Following the aimed bone through C^-1 in between
            _close(self, _flat(_multiply(axes.aimed(rest, aim), axes.aimed_offset(follower, aim))), _flat(_multiply(rest, follower)))


def _rest_at(head, frame=None):
    frame = frame or [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
    return [list(frame[row]) + [head[row]] for row in range(3)] + [[0.0, 0.0, 0.0, 1.0]]


class BoneFrameTests(unittest.TestCase):
    """Which way a skeleton's bones point and how long they're shown."""

    def test_a_bone_points_at_its_child_and_reaches_it(self):
        frames = axes.bone_frames({0: _rest_at((0.0, 0.0, 0.0)), 1: _rest_at((2.0, 0.0, 0.0))}, {0: -1, 1: 0})
        self.assertEqual(frames[0][0][1], 1)
        self.assertAlmostEqual(frames[0][1], 2.0)
        # A leaf points on from its parent, half as long as the way
        self.assertEqual(frames[1][0][1], 1)
        self.assertAlmostEqual(frames[1][1], 1.0)

    def test_a_hip_points_up_its_spine_whatever_its_legs(self):
        rests = {0: _rest_at((0.0, 0.0, 1.0)), 1: _rest_at((0.3, 0.0, 0.1)), 2: _rest_at((0.0, 0.0, 2.0)), 3: _rest_at((-0.3, 0.0, 0.1))}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 0, 3: 0})
        self.assertEqual(frames[0][0][1], 3)
        self.assertAlmostEqual(frames[0][1], 1.0)

    def test_children_at_the_joints_place_are_looked_past(self):
        rests = {0: _rest_at((0.0, 0.0, 0.0)), 1: _rest_at((0.0, 0.0, 0.0)), 2: _rest_at((0.0, -3.0, 0.0))}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1})
        self.assertEqual(frames[0][0][1], -2)
        self.assertEqual(frames[1][0][1], -2)
        # Below nothing, at its parent's place: from the nearest joint above it elsewhere
        rests = {0: _rest_at((0.0, 0.0, 0.0)), 1: _rest_at((0.0, 0.0, 4.0)), 2: _rest_at((0.0, 0.0, 4.0))}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1})
        self.assertEqual(frames[2][0][1], 3)
        self.assertAlmostEqual(frames[2][1], 2.0)

    def test_joints_all_at_one_place_point_up_together(self):
        # The root's joint unturned, the others turned a quarter turn about X and about Z
        frames_of = {0: [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]], 1: [[1.0, 0.0, 0.0], [0.0, 0.0, -1.0], [0.0, 1.0, 0.0]],
                     2: [[0.0, -1.0, 0.0], [1.0, 0.0, 0.0], [0.0, 0.0, 1.0]]}
        rests = {index: _rest_at((1.0, 2.0, 3.0), frame) for index, frame in frames_of.items()}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1})
        for index, (aim, length) in frames.items():
            bone = _bone(frames_of[index], aim)
            _close(self, _column(bone, 1), [0.0, 0.0, 1.0], 1e-12)
            _close(self, _column(bone, 2), [0.0, -1.0, 0.0], 1e-12)
            self.assertGreater(length, 1e-3)

    def test_a_bone_at_its_parents_place_takes_its_parents_roll_however_its_joint_is_turned(self):
        # The joint turned so that its axis closest to the parent's bone is 51 degrees off it, past where a chain carries a roll on
        frame = _euler_rows((math.radians(40.0), math.radians(35.0), 0.0), "XYZ")
        # The parent's bone up, rolled with its Z to the left as a rig might have it
        rests = {0: _rest_at((0.0, 0.0, 1.0)), 1: _rest_at((0.0, 0.0, 1.0), frame)}
        frames = axes.bone_frames(rests, {0: -1, 1: 0}, {0: ((2, 3, 1), 1.0)})
        bone = _bone(frame, frames[1][0])
        self.assertLess(_column(bone, 1)[2], math.cos(math.radians(45.0)))
        # Its Z the axis closest to the left turned along with the parent's up onto the bone
        y = _column(bone, 1)
        carried = _turned((0.0, 0.0, 1.0), y, (1.0, 0.0, 0.0))
        candidates = [[sign * frame[row][axis] for row in range(3)] for axis in range(3) for sign in (1, -1)]
        candidates = [axis for axis in candidates if abs(sum(a * b for a, b in zip(axis, y))) < 0.5]
        _close(self, _column(bone, 2), max(candidates, key=lambda axis: sum(a * b for a, b in zip(axis, carried))), 1e-12)

    def test_no_bone_is_shorter_than_a_fortieth_of_the_skeleton(self):
        rests = {0: _rest_at((0.0, 0.0, 0.0)), 1: _rest_at((0.0, 0.0, 4.0)), 2: _rest_at((0.0, 0.0, 4.001))}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1})
        self.assertAlmostEqual(frames[1][1], 4.001 * 0.025)

    def test_twin_arms_get_mirrored_bones(self):
        generator = random.Random(7)
        left = _rotation_rows(_random_rotation(generator))
        # The tools' mirrored joint: every axis of the mirror image turned around
        right = [[-value for value in row] for row in _multiply(S, left)]
        rests = {0: _rest_at((0.0, 0.0, 1.0)), 1: _rest_at((0.5, 0.1, 1.2), left), 2: _rest_at((1.0, 0.2, 1.1), left),
                 3: _rest_at((-0.5, 0.1, 1.2), right), 4: _rest_at((-1.0, 0.2, 1.1), right)}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1, 3: 0, 4: 3})
        for a, b in ((1, 3), (2, 4)):
            left_bone, right_bone = _bone([row[:3] for row in rests[a][:3]], frames[a][0]), _bone([row[:3] for row in rests[b][:3]], frames[b][0])
            mirrored = _multiply(S, left_bone)
            _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
            _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
            _close(self, _column(right_bone, 0), [-value for value in _column(mirrored, 0)], 1e-12)
            self.assertAlmostEqual(frames[a][1], frames[b][1])


    def test_twin_claws_pick_mirrored_fingers(self):
        # Four fingers as close to the claw's axes as each other (FinalBoss's claws), the other claw's listed in another order
        half = 0.5 ** 0.5
        left = [[half, -half, 0.0], [half, half, 0.0], [0.0, 0.0, 1.0]]
        right = [[-value for value in row] for row in _multiply(S, left)]
        rests = {0: _rest_at((0.0, 0.0, 1.0)), 1: _rest_at((2.0, 0.0, 1.0), left), 2: _rest_at((-2.0, 0.0, 1.0), right)}
        parents = {0: -1, 1: 0, 2: 0}
        fingers = [(1.0, 1.0, -0.7), (1.0, -1.0, -0.7), (-1.0, -1.0, -0.7), (-1.0, 1.0, -0.7)]
        for joint, (claw, sign, order) in enumerate(((1, 1.0, (0, 1, 2, 3)), (2, -1.0, (2, 0, 3, 1))), start=3):
            for finger in order:
                index = joint * 10 + finger
                x, y, z = fingers[finger]
                head = rests[claw]
                rests[index] = _rest_at((head[0][3] + sign * x, head[1][3] + y, head[2][3] + z))
                parents[index] = claw

        frames = axes.bone_frames(rests, parents)
        left_bone, right_bone = _bone(left, frames[1][0]), _bone(right, frames[2][0])
        mirrored = _multiply(S, left_bone)
        _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
        _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
        _close(self, _column(right_bone, 0), [-value for value in _column(mirrored, 0)], 1e-12)
        # The finger pointing forward and away from the middle
        self.assertGreater(left_bone[0][1], 0.0)
        self.assertLess(left_bone[1][1], 0.0)


    def test_an_arm_keeps_z_up_until_it_hangs_down(self):
        # A joint aimed down its arm (its X), its Z up, the arm drooping
        for degrees, expected_up in ((0.0, True), (20.0, True), (30.0, True), (60.0, False), (80.0, False)):
            droop = math.radians(degrees)
            along, up = (math.cos(droop), 0.0, -math.sin(droop)), (math.sin(droop), 0.0, math.cos(droop))
            frame = _transposed([list(along), [0.0, 1.0, 0.0], list(up)])
            frames = axes.bone_frames({0: _rest_at((0.0, 0.0, 0.0)), 1: _rest_at((0.5, 0.0, 1.0), frame), 2: _rest_at(tuple(0.5 + a for a in along[:1]) + (0.0, 1.0 + along[2]), frame)},
                                      {0: -1, 1: 0, 2: 1})
            bone = _bone(frame, frames[1][0])
            if expected_up:
                self.assertGreater(bone[2][2], 0.8, "Z of an arm drooping %g degrees" % degrees)
            else:
                # Hanging down, its X goes to the model's left like a leg's
                self.assertGreater(bone[0][0], 0.8, "X of an arm drooping %g degrees" % degrees)

    def test_a_chain_takes_its_parents_roll(self):
        identity = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
        # The child's joint turned 60 degrees about its limb: Z most up would be another axis than the one its parent's Z turns into
        twist = math.radians(60.0)
        child = [[1.0, 0.0, 0.0], [0.0, math.cos(twist), -math.sin(twist)], [0.0, math.sin(twist), math.cos(twist)]]
        rests = {0: _rest_at((1.0, 0.0, 1.0), identity), 1: _rest_at((2.0, 0.0, 1.0), child), 2: _rest_at((3.0, 0.2, 1.0), child)}
        # The parent's bone along X with Z forward, as a rig might have it
        stored = {0: ((3, 1, -2), 1.0)}
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1}, stored)
        bone = _bone(child, frames[1][0])
        self.assertGreater(-bone[1][2], 0.85, "the child's Z %s isn't the parent's forward carried along" % (_column(bone, 2),))
        alone = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1})
        self.assertNotEqual(alone[1][0], frames[1][0])

    def test_a_drooping_chain_keeps_its_roll(self):
        # Arm bones drooping more down the chain, past where the X across the model would take over: their Z stay together
        rests, parents = {0: _rest_at((0.0, 0.0, 1.0))}, {0: -1}
        position = [0.3, 0.0, 1.0]
        for index, degrees in enumerate((38.0, 44.0, 47.0, 52.0, 58.0), start=1):
            droop = math.radians(degrees)
            along = (math.cos(droop), 0.0, -math.sin(droop))
            frame = _transposed([list(along), [0.0, 1.0, 0.0], [math.sin(droop), 0.0, math.cos(droop)]])
            rests[index] = _rest_at(tuple(position), frame)
            parents[index] = index - 1
            position = [position[axis] + 0.3 * along[axis] for axis in range(3)]

        rests[6] = _rest_at(tuple(position), [row[:3] for row in rests[5][:3]])
        parents[6] = 5
        frames = axes.bone_frames(rests, parents)
        zs = [_column(_bone([row[:3] for row in rests[index][:3]], frames[index][0]), 2) for index in range(1, 7)]
        for a, b in zip(zs, zs[1:]):
            self.assertGreater(sum(x * y for x, y in zip(a, b)), 0.7, "neighbouring Z %s and %s" % (a, b))

    def test_limbs_branching_off_the_middle_line_carry_on_its_roll(self):
        identity = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
        # Joints turned 60 degrees about their limb: the roll most up of the score's isn't the one the middle's Z turns into
        twist = math.radians(60.0)
        left = [[1.0, 0.0, 0.0], [0.0, math.cos(twist), -math.sin(twist)], [0.0, math.sin(twist), math.cos(twist)]]
        right = [[-value for value in row] for row in _multiply(S, left)]
        # The middle joint on the middle line or three times as far off it as joints count as at one place: still the middle's
        for first, middle in itertools.product(("left", "right"), (0.0, 3e-4, -3e-4)):
            # The middle's bone points at whichever twin comes first, along X with Z up
            limbs = {"left": (left, 1.0), "right": (right, -1.0)}
            order = [first, "right" if first == "left" else "left"]
            rests, parents = {0: _rest_at((middle, 0.0, 1.0), identity)}, {0: -1}
            for number, name in enumerate(order):
                frame, sign = limbs[name]
                rests[1 + 2 * number] = _rest_at((sign * 0.5, 0.0, 1.0), frame)
                rests[2 + 2 * number] = _rest_at((sign * 1.0, 0.0, 1.0), frame)
                parents[1 + 2 * number], parents[2 + 2 * number] = 0, 1 + 2 * number

            frames = axes.bone_frames(rests, parents)
            left_index = 1 if first == "left" else 3
            right_index = 4 - left_index
            left_bone, right_bone = _bone(left, frames[left_index][0]), _bone(right, frames[right_index][0])
            self.assertGreater(left_bone[2][2], 0.85, "the left limb's Z %s isn't the middle's up carried on" % (_column(left_bone, 2),))
            mirrored = _multiply(S, left_bone)
            _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
            _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
            _close(self, _column(right_bone, 0), [-value for value in _column(mirrored, 0)], 1e-12)

    def test_limbs_carry_the_same_roll_off_a_middle_bone_a_hair_off_upright(self):
        # A middle bone pointing up and rolled 40 degrees, its limbs 30 degrees off upright: tilted a hundredth of a degree either way
        # (float noise in a game's binds) its limbs carry the same roll on, its Y across by far less than its Z
        roll, spread = math.radians(40.0), math.radians(30.0)
        twist = math.radians(20.0)
        aims = []
        for tilt in (0.0, 0.01, -0.01):
            turn = _multiply(_euler_rows((0.0, math.radians(tilt), 0.0), "XYZ"), _euler_rows((0.0, 0.0, roll), "XYZ"))
            # The joint's Y up, X and Z across and back
            middle = _multiply(turn, [[1.0, 0.0, 0.0], [0.0, 0.0, -1.0], [0.0, 1.0, 0.0]])
            way = (math.sin(spread), 0.0, math.cos(spread))
            left = _multiply(_euler_rows((0.0, spread, 0.0), "XYZ"), _euler_rows((0.0, 0.0, twist), "XYZ"))
            left = _multiply(left, [[1.0, 0.0, 0.0], [0.0, 0.0, -1.0], [0.0, 1.0, 0.0]])
            right = [[-value for value in row] for row in _multiply(S, left)]
            rests = {0: _rest_at((0.0, 0.0, 0.0), middle), 1: _rest_at((0.0, 0.0, 1.0)),
                     2: _rest_at((0.1, 0.0, 0.1), left), 3: _rest_at((0.1 + way[0], 0.0, 0.1 + way[2]), left),
                     4: _rest_at((-0.1, 0.0, 0.1), right), 5: _rest_at((-0.1 - way[0], 0.0, 0.1 + way[2]), right)}
            frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 0, 3: 2, 4: 0, 5: 4})
            left_bone, right_bone = _bone(left, frames[2][0]), _bone(right, frames[4][0])
            mirrored = _multiply(S, left_bone)
            _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
            aims.append((frames[2][0], frames[4][0]))

        self.assertEqual(aims[1], aims[0])
        self.assertEqual(aims[2], aims[0])

    def test_a_carried_roll_turns_along_with_the_bone(self):
        # An arm along the parent's bone (X) raised 40 degrees, the parent's Z leaning 48 degrees from forward to up: turned along the
        # swing it lies nearer the raised arm's up (42 degrees) than forward (48), as it is it's nearer forward
        raised, lean = math.radians(40.0), math.radians(48.0)
        y = (math.cos(raised), 0.0, math.sin(raised))
        up = (-math.sin(raised), 0.0, math.cos(raised))
        parent = ((1.0, 0.0, 0.0), (0.0, -math.cos(lean), math.sin(lean)))
        frame = [[y[row], (0.0, -1.0, 0.0)[row], -up[row]] for row in range(3)]
        self.assertEqual(axes.aim_of(frame, y, 1.0, parent), (2, 1, -3))

    def test_a_roll_is_carried_within_45_degrees_of_the_parents_bone(self):
        # The parent's bone along X with its Z up, the arm raised: carried, its Z is the parent's up turned along (the joint's -Z); past 45
        # degrees the score's forward (the joint's Y)
        parent = ((1.0, 0.0, 0.0), (0.0, 0.0, 1.0))
        for degrees, expected in ((40.0, (2, 1, -3)), (50.0, (3, 1, 2))):
            raised = math.radians(degrees)
            y = (math.cos(raised), 0.0, math.sin(raised))
            frame = [[y[row], (0.0, -1.0, 0.0)[row], (math.sin(raised), 0.0, -math.cos(raised))[row]] for row in range(3)]
            self.assertEqual(axes.aim_of(frame, y, 1.0, parent), expected, "raised %g degrees" % degrees)

    def test_a_middle_bone_carries_onto_a_side_as_it_points_or_as_its_mirror_image(self):
        # Across by its Y, else, while that's less than a tenth of its Z's, by its Z
        up, rolled = (0.0, 0.0, 1.0), (0.6, -0.8, 0.0)
        self.assertEqual(axes._onto_side((up, rolled), 1.0), (up, rolled))
        self.assertEqual(axes._onto_side((up, rolled), -1.0), ((-0.0, 0.0, 1.0), (-0.6, -0.8, 0.0)))
        tilted = (-1e-3, 0.0, 1.0)
        self.assertEqual(axes._onto_side((tilted, rolled), -1.0), ((1e-3, 0.0, 1.0), (-0.6, -0.8, 0.0)))
        leaning = (-0.3, 0.0, 0.95)
        self.assertEqual(axes._onto_side((leaning, rolled), -1.0), (leaning, rolled))

    def test_a_chain_crossing_the_middle_line_takes_no_roll_across_it(self):
        identity = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
        rests = {0: _rest_at((0.5, 0.0, 1.0), identity), 1: _rest_at((-0.5, 0.0, 1.0), identity), 2: _rest_at((-1.5, 0.0, 1.0), identity)}
        # The left bone pointing right with its Z forward, as a rig might have it: the right bone below it takes the score's up
        self.assertTrue(axes.is_aim((3, -1, -2)))
        frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1}, {0: ((3, -1, -2), 1.0)})
        _close(self, _column(_bone(identity, frames[1][0]), 2), [0.0, 0.0, 1.0], 1e-12)

    def test_twins_listing_their_children_in_another_order_pick_mirrored_ways(self):
        generator = random.Random(5)
        left = _rotation_rows(_random_rotation(generator))
        right = [[-value for value in row] for row in _multiply(S, left)]
        # Children the same way at two distances, and ways the other way round that lean alike: the nearest and the one most up
        cases = {"along": [(0.3, 0.1, 0.2), (0.6, 0.2, 0.4)], "opposite": [(0.0, 2.0 * 0.2, 0.2), (0.0, -2.0 * 0.2, -0.2)]}
        for name, ways in cases.items():
            rests, parents = {0: _rest_at((0.0, 0.0, 1.0))}, {0: -1}
            for twin, (frame, sign, order) in enumerate(((left, 1.0, (0, 1)), (right, -1.0, (1, 0))), start=1):
                head = (sign * 0.5, 0.0, 1.0)
                rests[twin], parents[twin] = _rest_at(head, frame), 0
                for number, way in enumerate(order):
                    index = 10 * twin + number
                    x, y, z = ways[way]
                    rests[index], parents[index] = _rest_at((head[0] + sign * x, y, head[2] + z)), twin

            frames = axes.bone_frames(rests, parents)
            left_bone, right_bone = _bone(left, frames[1][0]), _bone(right, frames[2][0])
            mirrored = _multiply(S, left_bone)
            _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
            _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)
            self.assertAlmostEqual(frames[1][1], frames[2][1], msg=name)

    def test_twins_of_swapped_axes_break_roll_ties_alike(self):
        # A bone whose other two axes lie 45 degrees above and below the horizontal: two rolls score the same and lean across the same,
        # the twin's joint has those axes the other way round
        along = (math.cos(math.radians(-30.0)), math.sin(math.radians(-30.0)), 0.0)
        flat = (-along[1], along[0], 0.0)
        half = 0.5 ** 0.5
        above = [half * a + half * b for a, b in zip(flat, (0.0, 0.0, 1.0))]
        below = [half * a - half * b for a, b in zip(flat, (0.0, 0.0, 1.0))]
        left = _transposed([list(along), above, below])
        if _determinant(left) < 0:
            left = _transposed([list(along), below, above])

        swap = [[0.0, 0.0, 1.0], [0.0, 1.0, 0.0], [1.0, 0.0, 0.0]]
        right = _multiply(_multiply(S, left), swap)
        left_bone = _bone(left, axes.aim_of(left, along, 1.0))
        right_bone = _bone(right, axes.aim_of(right, (-along[0], along[1], along[2]), -1.0))
        mirrored = _multiply(S, left_bone)
        _close(self, _column(right_bone, 1), _column(mirrored, 1), 1e-12)
        _close(self, _column(right_bone, 2), _column(mirrored, 2), 1e-12)

    def test_twins_a_little_off_each_others_mirror_image_stay_mirrored(self):
        # Twin limbs whose joints are a few degrees off each other's mirror image (most of the game's are within one) get mirrored bones, unless their
        # joints lie about as close to two rolls, which no asymmetry is safe from: turned about 45 degrees about the limb, or an arm drooping
        # about as far as where its roll goes over to a leg's (40 degrees, 55 with its joint turned 18 degrees about it)
        generator = random.Random(9)
        for degrees in (0.0, 15.0, 25.0, 70.0, 90.0):
            droop = math.radians(degrees)
            along = (math.cos(droop), 0.0, -math.sin(droop))
            for trial in range(40):
                twist = math.radians(generator.uniform(-15.0, 15.0))
                up = (math.sin(droop), 0.0, math.cos(droop))
                back = (0.0, 1.0, 0.0)
                turned_up = [math.cos(twist) * u + math.sin(twist) * b for u, b in zip(up, back)]
                turned_back = [math.cos(twist) * b - math.sin(twist) * u for u, b in zip(up, back)]
                limb = _transposed([list(along), turned_back, turned_up])
                # The joint's own axes in any order
                left = _multiply(limb, _transposed(_aim_matrix(AIMS[generator.randrange(len(AIMS))])))
                nudge = _axis_angle_rows((generator.uniform(-1, 1), generator.uniform(-1, 1), generator.uniform(-1, 1)), math.radians(5.0))
                right = _multiply(nudge, [[-value for value in row] for row in _multiply(S, left)])
                end = [0.3 + 0.5 * along[0], 0.0, 1.0 + 0.5 * along[2]]
                rests = {0: _rest_at((0.0, 0.0, 1.0)), 1: _rest_at((0.3, 0.0, 1.0), left), 2: _rest_at(tuple(end), left),
                         3: _rest_at((-0.3, 0.0, 1.0), right), 4: _rest_at((-end[0], end[1], end[2]), right)}
                frames = axes.bone_frames(rests, {0: -1, 1: 0, 2: 1, 3: 0, 4: 3})
                left_bone, right_bone = _bone(left, frames[1][0]), _bone(right, frames[3][0])
                mirrored = _multiply(S, left_bone)
                for axis in (1, 2):
                    self.assertGreater(sum(a * b for a, b in zip(_column(right_bone, axis), _column(mirrored, axis))), math.cos(math.radians(10.0)),
                                       "axis %d drooping %g degrees, trial %d" % (axis, degrees, trial))


def _turned(start, end, value):
    # The value turned by the shortest turn taking the unit vector start to end
    axis = [start[1] * end[2] - start[2] * end[1], start[2] * end[0] - start[0] * end[2], start[0] * end[1] - start[1] * end[0]]
    angle = math.atan2(math.sqrt(sum(a * a for a in axis)), sum(a * b for a, b in zip(start, end)))
    return [sum(row[k] * value[k] for k in range(3)) for row in _axis_angle_rows(axis, angle)]


def _axis_angle_rows(axis, angle):
    length = math.sqrt(sum(value * value for value in axis))
    x, y, z = (value / length for value in axis)
    c, s, t = math.cos(angle), math.sin(angle), 1.0 - math.cos(angle)
    return [[t * x * x + c, t * x * y - s * z, t * x * z + s * y], [t * x * y + s * z, t * y * y + c, t * y * z - s * x], [t * x * z - s * y, t * y * z + s * x, t * z * z + c]]


if __name__ == "__main__":
    unittest.main()
