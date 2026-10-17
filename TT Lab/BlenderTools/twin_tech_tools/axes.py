# Blender tools to work with TwinTech Lab(TT-Lab) files
# Copyright (C) 2025 Smyshliaev "Smartkin" Vladislav
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

"""The game's axes and Blender's, without Blender.

The game is Y up and drawn the other way round from how a renderer of Blender's handedness draws it (TT Lab shows its frame mirrored).
A point of the game (x, y, z) is Blender's (-x, -z, y): Z up and a reflection along Blender's X, so Blender shows a model the way the
console does (Crash faces -Y with his left at +X, like rigs made in Blender) and nothing needs a turned root. Every conversion picks
and negates components, which is exact, negative zeros included, so a file comes back as it was.

Things in the model's space convert by conjugation: points by the reflection M, a transform T by M T M^-1, which is a rotation again.
A joint's frame is the reflection of the game's within it as well (D, X negated): a bone rests at M R D C, C its own axes picked of
the joint's to point down its limb (aim_of). What follows a joint is relative to its bone: a body or a hull by C^-1 D T D C, its own
points by C^-1 D, an exit point (no points, its axes are what the game turns things it holds by) by C^-1 D T D, keeping the joint's
frame whatever bone it's moved to. Lights keep their Z axis along the game's (their arrow): their frame is M G D, relative to their parent
M L D, like an exit point on no joint. Those and an exit point's rotation on a bone whose axes aren't its joint's or a half turn about one
of them are the only conversions of a rotation that aren't exact (TT Lab keeps the direction a light has while its node points along
it, and an exit point's matrix while its transform stands for it).

Faces of both reflections face the other way, triangles and faces get their corners turned around (the first corner kept).
"""

import typing

Vector = typing.Tuple[float, float, float]
# (x, y, z, w) like TT Lab's files
Quaternion = typing.Tuple[float, float, float, float]
Rows = typing.List[typing.List[float]]

# Blender's axis i is the game's axis _AXES[i] times _SIGNS[i]: (-x, -z, y). The axes are swapped in pairs, so the picks undo themselves
_AXES = (0, 2, 1)
_SIGNS = (-1.0, -1.0, 1.0)
# The reflection within a joint's frame
_JOINT_SIGNS = (-1.0, 1.0, 1.0)


def _negated(value: float, sign: float) -> float:
    # Files have whole numbers as JSON integers, whose 0 has no sign to turn around
    value = float(value)
    return -value if sign < 0 else value


# The model's space

def point(game: typing.Sequence[float]) -> Vector:
    """A point or direction of the game in Blender."""
    return (_negated(game[_AXES[0]], _SIGNS[0]), _negated(game[_AXES[1]], _SIGNS[1]), _negated(game[_AXES[2]], _SIGNS[2]))


def point_back(blender: typing.Sequence[float]) -> Vector:
    """A point or direction of Blender in the game."""
    result = [0.0, 0.0, 0.0]
    for axis in range(3):
        result[_AXES[axis]] = _negated(blender[axis], _SIGNS[axis])

    return (result[0], result[1], result[2])


def points(game: typing.Sequence[float]) -> typing.List[float]:
    """Flat x, y, z values of the game in Blender."""
    result = [0.0] * len(game)
    for start in range(0, len(game) - 2, 3):
        result[start:start + 3] = point(game[start:start + 3])

    return result


def points_back(blender: typing.Sequence[float]) -> typing.List[float]:
    result = [0.0] * len(blender)
    for start in range(0, len(blender) - 2, 3):
        result[start:start + 3] = point_back(blender[start:start + 3])

    return result


def _vector_part(x: float, y: float, z: float, signs: typing.Sequence[float], axes: typing.Sequence[int]) -> typing.List[float]:
    # A reflection R turns q = (w, v) into (w, -R v): the axis is reflected and the turn goes the other way round it
    values = (x, y, z)
    return [_negated(values[axes[axis]], -signs[axis]) for axis in range(3)]


def rotation(game: typing.Sequence[float]) -> Quaternion:
    """A rotation of the game (x, y, z, w) as Blender's in the model's space, M R M^-1."""
    x, y, z = _vector_part(game[0], game[1], game[2], _SIGNS, _AXES)
    return (x, y, z, game[3])


def rotation_back(blender: typing.Sequence[float]) -> Quaternion:
    result = [0.0, 0.0, 0.0]
    for axis in range(3):
        result[_AXES[axis]] = _negated(blender[axis], -_SIGNS[axis])

    return (result[0], result[1], result[2], blender[3])


def scale(game: typing.Sequence[float]) -> Vector:
    """A scale along the game's axes along Blender's. The signs go with the axes they're along."""
    return (game[_AXES[0]], game[_AXES[1]], game[_AXES[2]])


def scale_back(blender: typing.Sequence[float]) -> Vector:
    result = [1.0, 1.0, 1.0]
    for axis in range(3):
        result[_AXES[axis]] = blender[axis]

    return (result[0], result[1], result[2])


# A joint's frame, which the reflection D undoes

def joint_point(value: typing.Sequence[float]) -> Vector:
    """A point or direction in a joint's frame of the game in its bone's frame, and back."""
    return (_negated(value[0], _JOINT_SIGNS[0]), _negated(value[1], _JOINT_SIGNS[1]), _negated(value[2], _JOINT_SIGNS[2]))


def joint_points(values: typing.Sequence[float]) -> typing.List[float]:
    result = [0.0] * len(values)
    for start in range(0, len(values) - 2, 3):
        result[start:start + 3] = joint_point(values[start:start + 3])

    return result


def joint_rotation(value: typing.Sequence[float]) -> Quaternion:
    """A rotation (x, y, z, w) in a joint's frame of the game in its bone's frame, D R D, and back."""
    x, y, z = _vector_part(value[0], value[1], value[2], _JOINT_SIGNS, (0, 1, 2))
    return (x, y, z, value[3])


def joint_rotation_wxyz(value: typing.Sequence[float]) -> typing.Tuple[float, float, float, float]:
    """joint_rotation of a quaternion (w, x, y, z) like Blender's and tlm_math's."""
    x, y, z, w = joint_rotation((value[1], value[2], value[3], value[0]))
    return (w, x, y, z)


# Matrices as rows, transforming column vectors

def _square(rows: typing.Sequence[typing.Sequence[float]]) -> Rows:
    return [list(rows[row][:4]) for row in range(4)]


def matrix(game: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """A transform of the game in the model's space as Blender's, M T M^-1."""
    game = _square(game)
    result = [[0.0] * 4 for _ in range(3)] + [list(game[3])]
    for row in range(3):
        for column in range(3):
            # Both signs at once, exact
            result[row][column] = _negated(game[_AXES[row]][_AXES[column]], _SIGNS[row] * _SIGNS[column])

        result[row][3] = _negated(game[_AXES[row]][3], _SIGNS[row])

    return result


def matrix_back(blender: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """matrix's inverse, M^-1 T M: the picks and their signs are the same both ways."""
    blender = _square(blender)
    result = [[0.0] * 4 for _ in range(3)] + [list(blender[3])]
    for row in range(3):
        for column in range(3):
            result[_AXES[row]][_AXES[column]] = _negated(blender[row][column], _SIGNS[row] * _SIGNS[column])

        result[_AXES[row]][3] = _negated(blender[row][3], _SIGNS[row])

    return result


def bone_matrix(game: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """A joint's bind pose of the game, in the model's space, as its bone's rest: M T D."""
    game = _square(game)
    result = [[0.0] * 4 for _ in range(3)] + [list(game[3])]
    for row in range(3):
        for column in range(3):
            result[row][column] = _negated(game[_AXES[row]][column], _SIGNS[row] * _JOINT_SIGNS[column])

        result[row][3] = _negated(game[_AXES[row]][3], _SIGNS[row])

    return result


def bone_matrix_back(blender: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """bone_matrix's inverse, M^-1 A D."""
    blender = _square(blender)
    result = [[0.0] * 4 for _ in range(3)] + [list(blender[3])]
    for row in range(3):
        for column in range(3):
            result[_AXES[row]][column] = _negated(blender[row][column], _SIGNS[row] * _JOINT_SIGNS[column])

        result[_AXES[row]][3] = _negated(blender[row][3], _SIGNS[row])

    return result


def joint_matrix(value: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """A transform relative to a joint of the game relative to its bone, D T D, and back."""
    value = _square(value)
    result = [[0.0] * 4 for _ in range(3)] + [list(value[3])]
    for row in range(3):
        for column in range(3):
            result[row][column] = _negated(value[row][column], _JOINT_SIGNS[row] * _JOINT_SIGNS[column])

        result[row][3] = _negated(value[row][3], _JOINT_SIGNS[row])

    return result


def joint_frame_back(blender: typing.Sequence[typing.Sequence[float]]) -> Rows:
    """The game's transform, in the model's space, of an object whose frame is a joint's (which follows a joint, or a light): M^-1 O D."""
    return bone_matrix_back(blender)


def _multiply(a: Quaternion, b: Quaternion) -> Quaternion:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


# M D: the quarter turn about X the light's frame is turned by from the model's
_HALF_SQRT2 = 0.5 ** 0.5
# M D, a quarter turn about X
_FRAME_TURN: Quaternion = (_HALF_SQRT2, 0.0, 0.0, _HALF_SQRT2)
_FRAME_TURN_BACK: Quaternion = (-_HALF_SQRT2, 0.0, 0.0, _HALF_SQRT2)


def frame_rotation(game: typing.Sequence[float]) -> Quaternion:
    """The rotation (x, y, z, w) of a frame of the game relative to its parent in the model's space as Blender's, M R D, its axes the
    game's through the joints' reflection: a light's (its Z, the arrow, along the game's) and an exit point's on no joint."""
    return _multiply(rotation(game), _FRAME_TURN)


def frame_rotation_back(blender: typing.Sequence[float]) -> Quaternion:
    return rotation_back(_multiply(tuple(blender), _FRAME_TURN_BACK))  # type: ignore[arg-type]


def box(minimum: typing.Sequence[float], maximum: typing.Sequence[float]) -> typing.Tuple[typing.Tuple[float, ...], typing.Tuple[float, ...]]:
    """A box's corners of the game, in the model's space, as Blender's: an axis turned around swaps them. A fourth value stays."""
    low, high = list(map(float, minimum)), list(map(float, maximum))
    for axis in range(3):
        game_axis = _AXES[axis]
        if _SIGNS[axis] < 0:
            low[axis], high[axis] = -float(maximum[game_axis]), -float(minimum[game_axis])
        else:
            low[axis], high[axis] = float(minimum[game_axis]), float(maximum[game_axis])

    return tuple(low), tuple(high)


def box_back(minimum: typing.Sequence[float], maximum: typing.Sequence[float]) -> typing.Tuple[typing.Tuple[float, ...], typing.Tuple[float, ...]]:
    low, high = list(map(float, minimum)), list(map(float, maximum))
    for axis in range(3):
        game_axis = _AXES[axis]
        if _SIGNS[axis] < 0:
            low[game_axis], high[game_axis] = -float(maximum[axis]), -float(minimum[axis])
        else:
            low[game_axis], high[game_axis] = float(minimum[axis]), float(maximum[axis])

    return tuple(low), tuple(high)


# Faces

def triangles(corners: typing.Sequence[int]) -> typing.List[int]:
    """Triangles of 3 corners each with their second and third corners swapped: the faces of a reflection face the other way. The
    swap undoes itself."""
    result = list(corners)
    for start in range(0, len(result) - 2, 3):
        result[start + 1], result[start + 2] = result[start + 2], result[start + 1]

    return result


def corner_values(values: typing.Sequence[typing.Any], size: int) -> typing.List[typing.Any]:
    """Values of every triangle's corners, size of them a corner, in the order triangles() puts the corners in."""
    result = list(values)
    step = size * 3
    for start in range(0, len(result) - step + 1, step):
        second, third = start + size, start + size * 2
        result[second:second + size], result[third:third + size] = result[third:third + size], result[second:second + size]

    return result


def face(corners: typing.Sequence[int]) -> typing.List[int]:
    """A face of any number of corners turned around, the first corner kept: undoes itself."""
    return [corners[0]] + list(reversed(corners[1:])) if len(corners) > 2 else list(corners)


# A bone's own axes

# Blender draws a bone along its Y axis. The game's joints point down their limb along one of their axes (the tools aimed them at a
# child), the right side's often mirrored with every axis turned around: bones along their joints' Y stood across their limbs and the
# two sides' pointed opposite ways. A bone's frame is its joint's with the axes picked and turned around, which is exact: its Y the axis
# closest to the child it points at. A bone carrying on its parent's chain (pointing within 45 degrees of it, on its side of the model or
# from the middle line onto a side) takes the roll closest to its parent's carried along, so a chain's bones turn about the same axes;
# other bones (roots, a limb branching off) the roll scoring highest by its X toward the model's left (counting double), its Z up and
# then forward: a spine's or a leg's X to the left like Blender's roll 0, an arm's Z up until it hangs down 40 degrees.
# Mirroring the model doesn't change the rule, so twins whose frames and children are mirror images get Blender's mirrored frames (Y and Z
# mirrored, X mirrored and turned around, an X across pointing left on both sides) and bones on the middle line X to the left.
# An aim is the joint's axes the bone's X, Y and Z are, one more than the joint's axis index, negative when turned around: (1, 2, 3) is
# the joint's own frame, what bones made in Blender have.
Aim = typing.Tuple[int, int, int]
IDENTITY_AIM: Aim = (1, 2, 3)
# The orders of the axes of a frame of the same handedness
_EVEN = ((0, 1, 2), (1, 2, 0), (2, 0, 1))


def _picks(aim: Aim) -> typing.Tuple[typing.Tuple[int, int], ...]:
    # The bone's axes as (the joint's axis, sign)
    return tuple((abs(pick) - 1, 1 if pick > 0 else -1) for pick in aim)


def is_aim(value: typing.Any) -> bool:
    """Whether a value is an aim: the joint's three axes, each once, a frame of the same handedness."""
    try:
        picks = [int(component) for component in value]
    except (TypeError, ValueError):
        return False

    if len(picks) != 3 or sorted(abs(pick) for pick in picks) != [1, 2, 3]:
        return False

    sign = 1 if tuple(abs(pick) - 1 for pick in picks) in _EVEN else -1
    for pick in picks:
        sign = sign if pick > 0 else -sign

    return sign > 0


# Values closer than this are ties, which go to the choice the model's mirror image makes too
_CLOSE = 1e-4
# A bone pointing within 45 degrees of its parent carries on its chain and takes its roll from it
_CARRIED = 0.5 ** 0.5


def _most(candidates: typing.List[typing.Any], *scores: typing.Callable[[typing.Any], float]) -> typing.Any:
    # The candidate scoring highest by the first score, of those as high the one scoring highest by the next and so on, the first of equals
    for score in scores:
        values = [score(candidate) for candidate in candidates]
        top = max(values)
        candidates = [candidate for candidate, value in zip(candidates, values) if value >= top - _CLOSE]

    return candidates[0]


def _lean(value: typing.Sequence[float]) -> float:
    # How much a direction of the model's space points up, then forward (-Y), and not across: mirroring the model only turns x around
    return 2.0 * value[2] - value[1] - 4.0 * abs(value[0])


def _unit(value: typing.Sequence[float]) -> typing.Optional[Vector]:
    length = (value[0] * value[0] + value[1] * value[1] + value[2] * value[2]) ** 0.5
    return (value[0] / length, value[1] / length, value[2] / length) if length > 1e-12 else None


def _axis_of(frame: typing.Sequence[typing.Sequence[float]], axis: int, sign: int) -> Vector:
    return (sign * frame[0][axis], sign * frame[1][axis], sign * frame[2][axis])


def _cross(a: typing.Sequence[float], b: typing.Sequence[float]) -> Vector:
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _roll_score(y: Vector, z: Vector) -> float:
    # How much the bone's X (Y x Z) points to the model's left, counting double, its Z up and then forward: mirroring the model keeps it
    x = _cross(y, z)
    return 2.0 * x[0] + z[2] - 0.5 * z[1]


def _carried(value: Vector, start: Vector, end: Vector) -> Vector:
    # The vector turned by the shortest turn taking the unit vector start to end (less than a half turn apart)
    k = _cross(start, end)
    c = sum(a * b for a, b in zip(start, end))
    k_value = _cross(k, value)
    k_k_value = _cross(k, k_value)
    return tuple(value[i] + k_value[i] + k_k_value[i] / (1.0 + c) for i in range(3))  # type: ignore[return-value]


def best_way(frame: typing.Sequence[typing.Sequence[float]], ways: typing.Sequence[typing.Sequence[float]], side: float = 0.0) -> typing.Optional[int]:
    """Which of the ways a joint's bone can point (to its children, in their order) lies closest to one of the frame's axes; None when
    none has a length. The tools aimed a joint at one of its children, the mean of their ways points nowhere (a hip's legs pull it the
    spine's other way). Of ways as close the one most up, then forward and least across, then the one pointing away from the middle of
    the model (side: which side of it the joint is on, 1, -1 or 0), then the most up and the nearest, so the mirror image of a joint
    picks the mirror image of its way whatever order its children are in."""
    units = {index: _unit(way) for index, way in enumerate(ways)}
    candidates = [index for index, unit in units.items() if unit is not None]
    if not candidates:
        return None

    def alignment(index: int) -> float:
        return max(abs(sum(frame[row][axis] * units[index][row] for row in range(3))) for axis in range(3))

    return _most(candidates, alignment, lambda index: _lean(units[index]), lambda index: units[index][0] * side, lambda index: units[index][2],
                 lambda index: -_length(ways[index]))


def aim_of(frame: typing.Sequence[typing.Sequence[float]], direction: typing.Optional[typing.Sequence[float]], side: float = 0.0,
           parent: typing.Optional[typing.Tuple[Vector, Vector]] = None) -> Aim:
    """The bone axes of a joint's frame (3x3 or 4x4 rows, its columns the axes, in the model's space) pointing along the direction (in
    the same space): Y the axis closest to it. Z, for a bone pointing within 45 degrees of its parent's bone (parent: the parent bone's Y
    and Z), the axis closest to the parent's Z turned along with its Y onto this one's; else the one pointing X most to the model's left
    (counting double), Z most up, then forward. Ties go the way best_way's do. Mirror images of a frame, a direction and a parent get
    mirror images of the axes. A bone with no direction (None or of no length) follows its parent's bone, a root without one points up."""
    unit = _unit(direction) if direction is not None else None
    follows = unit is None and parent is not None
    if unit is None:
        unit = parent[0] if parent is not None else (0.0, 0.0, 1.0)

    signed = [(axis, sign) for axis in range(3) for sign in (1, -1)]
    # Ties go to what the model's mirror image picks too, never to the order of the joint's axes
    ties = [lambda pick: _axis_of(frame, *pick)[0] * side, lambda pick: _axis_of(frame, *pick)[2], lambda pick: -_axis_of(frame, *pick)[1]]
    y_axis, y_sign = _most(signed, lambda pick: sum(a * b for a, b in zip(_axis_of(frame, *pick), unit)),
                           lambda pick: _lean(_axis_of(frame, *pick)), *ties)
    y = _axis_of(frame, y_axis, y_sign)
    candidates = [pick for pick in signed if pick[0] != y_axis]
    scores = [lambda pick: _roll_score(y, _axis_of(frame, *pick))] + ties
    if parent is not None and (follows or sum(a * b for a, b in zip(parent[0], y)) >= _CARRIED):
        carried = _carried(parent[1], parent[0], y)
        scores.insert(0, lambda pick: sum(a * b for a, b in zip(_axis_of(frame, *pick), carried)))

    z_axis, z_sign = _most(candidates, *scores)
    x_axis = 3 - y_axis - z_axis
    # X = Y x Z
    x_sign = y_sign * z_sign * (1 if (y_axis, z_axis, x_axis) in _EVEN else -1)
    return (x_sign * (x_axis + 1), y_sign * (y_axis + 1), z_sign * (z_axis + 1))


def _onto_side(bone: typing.Tuple[Vector, Vector], side: float) -> typing.Tuple[Vector, Vector]:
    # A bone on the middle line (its Y and Z) as a limb on the side branching off it carries on from it: as it is when it points to that
    # side (by its Y, else, while its Y lies across by less than a tenth of its Z, by its Z), else its mirror image. Twins branching off it
    # carry on from mirror images.
    y, z = bone
    across = y[0] if abs(y[0]) > max(_CLOSE, 0.1 * abs(z[0])) else (z[0] if abs(z[0]) > _CLOSE else 0.0)
    if across * side >= 0.0:
        return bone

    return (-y[0], y[1], y[2]), (-z[0], z[1], z[2])


def _difference(a: typing.Sequence[float], b: typing.Sequence[float]) -> Vector:
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _length(value: typing.Sequence[float]) -> float:
    return (value[0] * value[0] + value[1] * value[1] + value[2] * value[2]) ** 0.5


def bone_frames(rests: typing.Dict[int, typing.Sequence[typing.Sequence[float]]], parents: typing.Dict[int, int],
                stored: typing.Optional[typing.Dict[int, typing.Tuple[Aim, float]]] = None) -> typing.Dict[int, typing.Tuple[Aim, float]]:
    """Every joint's bone axes (aim_of) and the length to show it with, from the joints' bones at rest (bone_matrix, in the model's space)
    and their parents (-1 none), but the joints stored has (what their bones were in Blender). A bone points at the child lying closest
    to one of its axes, looking past children at its place to theirs, and reaches it; a joint with nothing below it elsewhere points the
    way from the nearest joint above it elsewhere and is half as long as that way, one with every joint above it at its place too carries on
    along its parent's bone (a root points up). A bone's roll follows its parent's down a chain on its side of the model, on the middle
    line and from the middle line onto a side, the parents' bones are worked out first. Blender drops bones of no length and keeps short
    ones less precisely, every bone has some length."""
    stored = stored or {}
    heads = {index: (rest[0][3], rest[1][3], rest[2][3]) for index, rest in rests.items()}
    if not heads:
        return {}

    first = heads[min(heads)]
    size = max(_length(_difference(head, first)) for head in heads.values())
    same_place = max(1e-6, size * 1e-4)
    shortest = max(0.005, size * 0.025)
    children: typing.Dict[int, typing.List[int]] = {index: [] for index in heads}
    for index in sorted(heads):
        parent = parents.get(index, -1)
        if parent in children and parent != index:
            children[parent].append(index)

    # Parents first, from the roots down
    order: typing.List[int] = []
    pending = [index for index in sorted(heads, reverse=True) if parents.get(index, -1) not in children or parents.get(index, -1) == index]
    while pending:
        index = pending.pop()
        if index not in order:
            order.append(index)
            pending.extend(reversed(children[index]))

    order.extend(index for index in sorted(heads) if index not in order)
    sides = {index: 0.0 if abs(head[0]) <= same_place else (1.0 if head[0] > 0.0 else -1.0) for index, head in heads.items()}
    result: typing.Dict[int, typing.Tuple[Aim, float]] = {}
    for index in order:
        head = heads[index]
        if index in stored:
            result[index] = stored[index]
            continue

        frame = rests[index]
        side = sides[index]
        above = parents.get(index, -1)
        parent = None
        # A chain carries on on its side of the model, and from the middle line onto a side as _onto_side has it, not across the middle. A
        # parent ten times nearer the middle line than the bone counts as on it: twins under a middle joint a hair off it stay twins
        middle = above in heads and (sides[above] == 0.0 or abs(heads[above][0]) * 10.0 <= abs(head[0]))
        if above in result and above != index and (sides[above] == side or middle):
            parent_picks = _picks(result[above][0])
            parent = (_axis_of(rests[above], *parent_picks[1]), _axis_of(rests[above], *parent_picks[2]))
            if middle and side != 0.0:
                parent = _onto_side(parent, side)

        ways = []
        pending = list(reversed(children[index]))
        seen = {index}
        while pending:
            child = pending.pop()
            if child in seen:
                continue

            seen.add(child)
            way = _difference(heads[child], head)
            if _length(way) > same_place:
                ways.append(way)
            else:
                pending.extend(reversed(children[child]))

        chosen = best_way(frame, ways, side)
        if chosen is not None:
            aim = aim_of(frame, ways[chosen], side, parent)
            axis, sign = _picks(aim)[1]
            along = sum(_negated(frame[row][axis], sign) * ways[chosen][row] for row in range(3))
            result[index] = (aim, max(along, shortest))
            continue

        visited = {index}
        while above in heads and above not in visited and _length(_difference(head, heads[above])) <= same_place:
            visited.add(above)
            above = parents.get(above, -1)

        if above in heads and above not in visited:
            way = _difference(head, heads[above])
            result[index] = (aim_of(frame, way, side, parent), max(_length(way) * 0.5, shortest))
        else:
            # Every joint above it at its place too: it carries on along its parent's bone, a root points up
            result[index] = (aim_of(frame, None, side, parent), shortest * 2.0)

    return result


def aimed(rest: typing.Sequence[typing.Sequence[float]], aim: Aim) -> Rows:
    """A bone's rest (4x4 rows) with its axes picked for the aim: rest C, C the picks. The translation stays."""
    rest = _square(rest)
    result = [list(row) for row in rest]
    for column, (axis, sign) in enumerate(_picks(aim)):
        for row in range(3):
            result[row][column] = _negated(rest[row][axis], sign)

    return result


def unaimed(rest: typing.Sequence[typing.Sequence[float]], aim: Aim) -> Rows:
    """aimed's inverse: the joint's frame of an aimed bone's rest, rest C^-1."""
    rest = _square(rest)
    result = [list(row) for row in rest]
    for column, (axis, sign) in enumerate(_picks(aim)):
        for row in range(3):
            result[row][axis] = _negated(rest[row][column], sign)

    return result


def aimed_offset(value: typing.Sequence[typing.Sequence[float]], aim: Aim) -> Rows:
    """A transform into a joint bone's frame (what follows the bone) into the aimed bone's, C^-1 T: the rows picked."""
    value = _square(value)
    result = [list(row) for row in value]
    for row, (axis, sign) in enumerate(_picks(aim)):
        result[row] = [_negated(component, sign) for component in value[axis]]

    return result


def aimed_vector(value: typing.Sequence[float], aim: Aim) -> Vector:
    """A direction or point in a joint's bone frame in the aimed bone's frame, C^-1 v: a location, a rotation's axis."""
    return tuple(_negated(value[axis], sign) for axis, sign in _picks(aim))  # type: ignore[return-value]


def unaimed_vector(value: typing.Sequence[float], aim: Aim) -> Vector:
    result = [0.0, 0.0, 0.0]
    for column, (axis, sign) in enumerate(_picks(aim)):
        result[axis] = _negated(value[column], sign)

    return (result[0], result[1], result[2])


def aimed_points(values: typing.Sequence[float], aim: Aim) -> typing.List[float]:
    """aimed_vector of every point of a flat list (x, y, z, x, y, z...): a mesh in its bone's frame in its aimed bone's."""
    if tuple(aim) == IDENTITY_AIM:
        return list(values)

    picks = _picks(aim)
    result: typing.List[float] = []
    for start in range(0, len(values) - 2, 3):
        result.extend(_negated(values[start + axis], sign) for axis, sign in picks)

    return result


def unaimed_points(values: typing.Sequence[float], aim: Aim) -> typing.List[float]:
    if tuple(aim) == IDENTITY_AIM:
        return list(values)

    picks = _picks(aim)
    result = [0.0] * (len(values) // 3 * 3)
    for start in range(0, len(result), 3):
        for column, (axis, sign) in enumerate(picks):
            result[start + axis] = _negated(values[start + column], sign)

    return result


def aimed_rotation_wxyz(value: typing.Sequence[float], aim: Aim) -> typing.Tuple[float, float, float, float]:
    """A pose rotation (w, x, y, z) of a joint's bone in the aimed bone's frame, C^-1 R C: the same turn about the axis in the new frame."""
    x, y, z = aimed_vector(value[1:4], aim)
    return (value[0], x, y, z)


def unaimed_rotation_wxyz(value: typing.Sequence[float], aim: Aim) -> typing.Tuple[float, float, float, float]:
    x, y, z = unaimed_vector(value[1:4], aim)
    return (value[0], x, y, z)


def aim_rotation(aim: Aim) -> Quaternion:
    """The turn (x, y, z, w) of an aim's picks, C: the bone's own axes in the joint's frame."""
    picks = _picks(aim)
    m = [[0.0] * 3 for _ in range(3)]
    for column, (axis, sign) in enumerate(picks):
        m[axis][column] = float(sign)

    trace = m[0][0] + m[1][1] + m[2][2]
    if trace > 0.0:
        s = (trace + 1.0) ** 0.5 * 2.0
        return ((m[2][1] - m[1][2]) / s, (m[0][2] - m[2][0]) / s, (m[1][0] - m[0][1]) / s, 0.25 * s)

    if m[0][0] >= m[1][1] and m[0][0] >= m[2][2]:
        s = (1.0 + m[0][0] - m[1][1] - m[2][2]) ** 0.5 * 2.0
        return (0.25 * s, (m[0][1] + m[1][0]) / s, (m[0][2] + m[2][0]) / s, (m[2][1] - m[1][2]) / s)

    if m[1][1] >= m[2][2]:
        s = (1.0 + m[1][1] - m[0][0] - m[2][2]) ** 0.5 * 2.0
        return ((m[0][1] + m[1][0]) / s, 0.25 * s, (m[1][2] + m[2][1]) / s, (m[0][2] - m[2][0]) / s)

    s = (1.0 + m[2][2] - m[0][0] - m[1][1]) ** 0.5 * 2.0
    return ((m[0][2] + m[2][0]) / s, (m[1][2] + m[2][1]) / s, 0.25 * s, (m[1][0] - m[0][1]) / s)


def exit_rotation_wxyz(value: typing.Sequence[float], aim: Aim) -> typing.Tuple[float, float, float, float]:
    """An exit point's rotation (w, x, y, z) relative to its joint's bone (joint_rotation_wxyz) relative to the aimed bone, C^-1 R: its
    axes stay the joint frame's whichever its bone's own are. Exact only for a bone whose axes are its joint's or a half turn about one of
    them (the other aims' turns have halves and square roots of a half in them)."""
    x, y, z, w = aim_rotation(aim)
    x, y, z, w = _multiply((-x, -y, -z, w), (value[1], value[2], value[3], value[0]))
    return (w, x, y, z)


def aimed_scale(value: typing.Sequence[float], aim: Aim) -> Vector:
    """A scale along a joint bone's axes along the aimed bone's: the axes picked, their signs don't turn a scale around."""
    return tuple(float(value[axis]) for axis, _ in _picks(aim))  # type: ignore[return-value]


def unaimed_scale(value: typing.Sequence[float], aim: Aim) -> Vector:
    result = [1.0, 1.0, 1.0]
    for column, (axis, _) in enumerate(_picks(aim)):
        result[axis] = float(value[column])

    return (result[0], result[1], result[2])


def aimed_matrix(value: typing.Sequence[typing.Sequence[float]], aim: Aim) -> Rows:
    """A transform in a joint bone's frame (a pose) in the aimed bone's frame, C^-1 T C."""
    value = _square(value)
    picks = _picks(aim)
    result = [[0.0] * 4 for _ in range(3)] + [list(value[3])]
    for row, (row_axis, row_sign) in enumerate(picks):
        for column, (column_axis, column_sign) in enumerate(picks):
            result[row][column] = _negated(value[row_axis][column_axis], row_sign * column_sign)

        result[row][3] = _negated(value[row_axis][3], row_sign)

    return result


def unaimed_matrix(value: typing.Sequence[typing.Sequence[float]], aim: Aim) -> Rows:
    value = _square(value)
    picks = _picks(aim)
    result = [[0.0] * 4 for _ in range(3)] + [list(value[3])]
    for row, (row_axis, row_sign) in enumerate(picks):
        for column, (column_axis, column_sign) in enumerate(picks):
            result[row_axis][column_axis] = _negated(value[row][column], row_sign * column_sign)

        result[row_axis][3] = _negated(value[row][3], row_sign)

    return result


# Euler angles of the game's dynamic scenery, X then Y then Z

BLENDER_EULER_ORDER = "XZY"


def euler(game: typing.Sequence[float]) -> Vector:
    """The game's angles (X turned first, then Y, then Z) as Blender's rotation_euler in BLENDER_EULER_ORDER: M Rz Ry Rx M^-1 is a turn
    about X by the same angle, then about Z by the other way round the Y angle, then about Y by the Z angle."""
    return (game[0], game[2], -game[1])


def euler_back(blender: typing.Sequence[float]) -> Vector:
    return (blender[0], -blender[2], blender[1])
