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

"""Matching a new armature's bones to the joints of the game's model and posing them like the model's animations, without Blender."""

import typing

from . import tlm_math

# A bone: its name, its parent's name and the joint index it already has
BoneInfo = typing.Tuple[str, typing.Optional[str], typing.Optional[int]]
# A bone's match: the joint index it gets, the original bone it stands for and how it was matched
Match = typing.Tuple[int, typing.Optional[str], str]
# A bone's rest: its translation and rotation in the space both skeletons share
Rest = typing.Tuple[tlm_math.Vector, tlm_math.Quaternion]
# A skeleton: every bone's parent and rest
Skeleton = typing.Dict[str, typing.Tuple[typing.Optional[str], Rest]]
# A pose bone's location, rotation and scale, relative to its rest in its own axes like Blender keeps them
Pose = typing.Tuple[tlm_math.Vector, tlm_math.Quaternion, tlm_math.Vector]

REST_POSE: Pose = ((0.0, 0.0, 0.0), (1.0, 0.0, 0.0, 0.0), (1.0, 1.0, 1.0))

BY_INDEX = "index"
BY_NAME = "name"
BY_ORDER = "order"
NEW = "new"
# A bone no joint is named like with MATCH_NAME_ONLY: a new joint keeping its name
KEPT = "kept"

# How the bones are matched: by their joint indexes while those are the original's (keeps_skeleton), or always, or never (names, then
# the hierarchy), or by their names alone
MATCH_AUTO = "AUTO"
MATCH_INDEX = "INDEX"
MATCH_NAME = "NAME"
MATCH_NAME_ONLY = "NAME_ONLY"


def keeps_skeleton(original: typing.Sequence[BoneInfo], incoming: typing.Sequence[BoneInfo]) -> bool:
    """Whether the incoming bones' joint indexes are the original's joints: every joint index both have has the same parent's index
    in both (bones added after the original's don't count). Exporting gives every bone of any rig an index, which only numbers it."""
    def parents(bones: typing.Sequence[BoneInfo], index_of: typing.Dict[str, int]) -> typing.Dict[int, int]:
        # -1 the root, -2 a parent of no index
        return {index_of[name]: -1 if parent is None else index_of.get(parent, -2) for name, parent, _ in bones if name in index_of}

    original_index = {name: index if index is not None else position for position, (name, _, index) in enumerate(original)}
    original_parents = parents(original, original_index)
    incoming_parents = parents(incoming, {name: index for name, _, index in incoming if index is not None})
    shared = original_parents.keys() & incoming_parents.keys()
    return bool(shared) and all(original_parents[index] == incoming_parents[index] for index in shared)


def match_joints(original: typing.Sequence[BoneInfo], incoming: typing.Sequence[BoneInfo], match: str = MATCH_AUTO) -> typing.Dict[str, Match]:
    """Which of the original's joints each incoming bone stands for. A bone that has one of the original's joint indexes keeps it
    (while the indexes are the original's joints, see keeps_skeleton, or always with MATCH_INDEX, never with MATCH_NAME), then bones
    named like the original's are those joints, then the hierarchies are walked together: the roots in order and the children of
    every pair of matched bones in order. What's left gets the next free joint indexes. With MATCH_NAME_ONLY only the names match,
    the bones named like none of the original's joints are new joints keeping their names (KEPT, see hierarchy_problems)."""
    original_index = {name: index if index is not None else position for position, (name, _, index) in enumerate(original)}
    original_children: typing.Dict[typing.Optional[str], typing.List[str]] = {}
    for name, parent, _ in original:
        original_children.setdefault(parent, []).append(name)

    incoming_children: typing.Dict[typing.Optional[str], typing.List[str]] = {}
    for name, parent, _ in incoming:
        incoming_children.setdefault(parent, []).append(name)

    original_by_index = {index: name for name, index in original_index.items()}
    result: typing.Dict[str, Match] = {}
    used: typing.Set[str] = set()

    def take(name: str, original_name: str, how: str) -> None:
        result[name] = (original_index[original_name], original_name, how)
        used.add(original_name)

    if match == MATCH_NAME_ONLY:
        next_index = max(original_index.values(), default=-1) + 1
        for name, _, _ in incoming:
            if name in original_index:
                take(name, name, BY_NAME)
            else:
                result[name] = (next_index, None, KEPT)
                next_index += 1

        return result

    if match == MATCH_INDEX or match == MATCH_AUTO and keeps_skeleton(original, incoming):
        for name, _, index in incoming:
            if index is not None and index in original_by_index and original_by_index[index] not in used:
                take(name, original_by_index[index], BY_INDEX)

    for name, _, _ in incoming:
        if name not in result and name in original_index and name not in used:
            take(name, name, BY_NAME)

    # The pairs whose children get matched by their order, starting from the bones matched so far and the roots
    pairs = [(original_name, name) for name, (_, original_name, _) in result.items() if original_name is not None]
    _pair_by_order(original_children.get(None, []), incoming_children.get(None, []), result, used, take, pairs)
    while pairs:
        original_name, name = pairs.pop(0)
        _pair_by_order(original_children.get(original_name, []), incoming_children.get(name, []), result, used, take, pairs)

    next_index = max(original_index.values(), default=-1) + 1
    for name, _, _ in incoming:
        if name not in result:
            result[name] = (next_index, None, NEW)
            next_index += 1

    return result


def _pair_by_order(original_names: typing.List[str], incoming_names: typing.List[str], result: typing.Dict[str, Match], used: typing.Set[str],
                   take: typing.Callable[[str, str, str], None], pairs: typing.List[typing.Tuple[str, str]]) -> None:
    free = [name for name in original_names if name not in used]
    for name in [name for name in incoming_names if name not in result]:
        if not free:
            return

        original_name = free.pop(0)
        take(name, original_name, BY_ORDER)
        pairs.append((original_name, name))


def joint_name(match: Match, name: str = "") -> str:
    """The name the bone gets: the original bone's, its own when it's kept as it is, or Joint N for a new joint."""
    if match[2] == KEPT:
        return name

    return match[1] if match[1] is not None else "Joint %d" % match[0]


def hierarchy_problems(original: typing.Sequence[BoneInfo], incoming: typing.Sequence[BoneInfo], matches: typing.Dict[str, Match]) -> typing.List[str]:
    """Where the matched bones aren't laid out like their joints: every matched bone has to be under the bone of the joint its joint
    is under, counting only matched ones (bones matching nothing may be in between, joints nothing matched may be left out). Empty when
    the matched joints form the same tree in both."""
    original_parents = {name: parent for name, parent, _ in original}
    incoming_parents = {name: parent for name, parent, _ in incoming}
    joint_of = {name: match[1] for name, match in matches.items() if match[1] is not None}
    bone_of = {joint: name for name, joint in joint_of.items()}

    def matched_above(name: str, parents: typing.Dict[str, typing.Optional[str]], matched: typing.Dict[str, str]) -> typing.Optional[str]:
        parent = parents.get(name)
        while parent is not None and parent not in matched:
            parent = parents.get(parent)

        return parent

    problems = []
    for name, _, _ in incoming:
        joint = joint_of.get(name)
        if joint is None:
            continue

        above = matched_above(name, incoming_parents, joint_of)
        joint_above = matched_above(joint, original_parents, bone_of)
        if (joint_of[above] if above is not None else None) == joint_above:
            continue

        where = "under %s" % above if above is not None else "above every matched bone"
        should = "under %s" % joint_above if joint_above is not None else "above every matched joint"
        problems.append("%s is %s, the source has %s %s" % (name, where, joint, should))

    return problems


def size_ratio(original: Skeleton, incoming: Skeleton, bones: typing.Dict[str, str]) -> float:
    """How much bigger the incoming skeleton is than the original: the largest extent of the matched bones' rests (incoming
    bone -> original bone) over the original's."""
    def extent(skeleton: Skeleton, names: typing.Iterable[str]) -> float:
        positions = [skeleton[name][1][0] for name in names if name in skeleton]
        if not positions:
            return 0.0

        return max(max(position[axis] for position in positions) - min(position[axis] for position in positions) for axis in range(3))

    original_extent = extent(original, bones.values())
    return extent(incoming, bones.keys()) / original_extent if original_extent > 1e-6 else 1.0


def world_deltas(skeleton: Skeleton, poses: typing.Dict[str, Pose]) -> typing.Dict[str, typing.Tuple[tlm_math.Quaternion, tlm_math.Vector]]:
    """Where a pose puts every bone: how much it's turned from its rest in the skeleton's space, and its position. Bones without a
    pose stay at rest under their parents. Scale is left out, so a joint hidden by scaling it away still turns its children.
    The poses' components can be arrays of one value per frame instead of floats, the results are then arrays too."""
    result: typing.Dict[str, typing.Tuple[tlm_math.Quaternion, tlm_math.Vector]] = {}

    def of(name: str) -> typing.Tuple[tlm_math.Quaternion, tlm_math.Vector]:
        if name in result:
            return result[name]

        parent, (rest_translation, rest_rotation) = skeleton[name]
        location, rotation, _ = poses.get(name, REST_POSE)
        if parent is not None and parent in skeleton:
            parent_delta, parent_position = of(parent)
            offset = tuple(value - parent_value for value, parent_value in zip(rest_translation, skeleton[parent][1][0]))
        else:
            parent_delta, parent_position, offset = (1.0, 0.0, 0.0, 0.0), (0.0, 0.0, 0.0), rest_translation

        # Blender poses a bone in its own rest axes, turned by what its parent's pose did to them
        axes = _multiply(parent_delta, rest_rotation)
        delta = _multiply(_multiply(axes, _normalized(rotation)), _inverse(rest_rotation))
        moved = _rotate(axes, location)
        turned_offset = _rotate(parent_delta, offset)  # type: ignore[arg-type]
        position = tuple(parent_position[axis] + turned_offset[axis] + moved[axis] for axis in range(3))
        result[name] = (delta, position)  # type: ignore[assignment]
        return result[name]

    for name in skeleton:
        of(name)

    return result


def retarget_pose(original: Skeleton, incoming: Skeleton, bones: typing.Dict[str, str], poses: typing.Dict[str, Pose], scale: float = 1.0) -> typing.Dict[str, Pose]:
    """The incoming bones' pose for the original's: every matched bone (incoming name -> original name) turns from its own rest as
    much as the original's turned from its, and goes as far beyond where its nearest matched ancestor's turn leaves its rest
    offset as the original's did, times the scale. Bones between matched ones stay at rest, so what's above a bone is the
    ancestor's turn whatever the incoming hierarchy has in between. Takes arrays of one value per frame like world_deltas."""
    deltas = world_deltas(original, poses)
    result: typing.Dict[str, Pose] = {}
    for name, original_name in bones.items():
        if name not in incoming or original_name not in deltas:
            continue

        ancestor = incoming[name][0]
        while ancestor is not None and ancestor not in bones:
            ancestor = incoming[ancestor][0] if ancestor in incoming else None

        original_rest = original[original_name][1]
        if ancestor is not None and bones[ancestor] in deltas:
            above_delta, above_position = deltas[bones[ancestor]]
            above_translation = original[bones[ancestor]][1][0]
        else:
            above_delta, above_position, above_translation = (1.0, 0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)

        delta, position = deltas[original_name]
        turned_offset = _rotate(above_delta, tuple(value - above for value, above in zip(original_rest[0], above_translation)))  # type: ignore[arg-type]
        extra = tuple((position[axis] - above_position[axis] - turned_offset[axis]) * scale for axis in range(3))
        rest_rotation = incoming[name][1][1]
        axes = _multiply(above_delta, rest_rotation)
        rotation = _multiply(_multiply(_inverse(axes), delta), rest_rotation)
        location = _rotate(_inverse(axes), extra)  # type: ignore[arg-type]
        result[name] = (location, _normalized(rotation), poses.get(original_name, REST_POSE)[2])  # type: ignore[assignment]

    return result


# Quaternion math on components that are floats or arrays of one value per frame, so a whole animation goes through at once

def _multiply(a: typing.Any, b: typing.Any) -> typing.Any:
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz,
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw)


def _normalized(q: typing.Any) -> typing.Any:
    w, x, y, z = q
    length = (w * w + x * x + y * y + z * z) ** 0.5
    return (w / length, x / length, y / length, z / length)


def _inverse(q: typing.Any) -> typing.Any:
    w, x, y, z = _normalized(q)
    return (w, -x, -y, -z)


def _rotate(q: typing.Any, v: typing.Any) -> typing.Any:
    w, x, y, z = _normalized(q)
    # v + 2w (q x v) + 2 q x (q x v), the q of the cross products being its vector part
    cx, cy, cz = y * v[2] - z * v[1], z * v[0] - x * v[2], x * v[1] - y * v[0]
    return (v[0] + 2.0 * (w * cx + y * cz - z * cy),
            v[1] + 2.0 * (w * cy + z * cx - x * cz),
            v[2] + 2.0 * (w * cz + x * cy - y * cx))
