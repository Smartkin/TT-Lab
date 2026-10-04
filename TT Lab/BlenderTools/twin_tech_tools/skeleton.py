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

"""The skeletons the game can have, without Blender. The game builds a model's skeleton in joint order, every joint under its parent's,
and takes every joint without a parent for its root (SetJointAnimations). It works out the joints' matrices walking the skeleton from the
root down, a joint's children in the order of their indexes, each with everything under it (TransformJoints), and draws the joint of an
index with the matrix of that place in the walk (DrawOgi): a skeleton has one root, joint 0, and its joints are numbered in that walk's
order, like every model of the game. A joint has 12 children at most (the game has none for a 13th) and a skin is drawn with 63 joints
at most (their count goes into 6 bits of the VIF packet)."""

import typing

# A bone: its name, its parent's name and the joint index its Joint type gives it
BoneInfo = typing.Tuple[str, typing.Optional[str], typing.Optional[int]]


def root_bones(bones: typing.Sequence[BoneInfo]) -> typing.List[str]:
    return [name for name, parent, _ in bones if parent is None]


MAX_CHILD_JOINTS = 12
MAX_SKINNED_JOINTS = 63


def check_skeleton(bones: typing.Sequence[BoneInfo], skinned: bool = False) -> None:
    """Refuses a skeleton the game can't have: several roots (the game would move all of them as its root joint), a bone of more
    children than a joint has, more bones than a skin is drawn with."""
    roots = root_bones(bones)
    if len(roots) > 1:
        raise ValueError("the armature has %d root bones (%s) and the game's skeletons have one, joint 0, which every other bone is under: "
                         "parent the others to the bone that should be the root" % (len(roots), ", ".join(roots)))

    children: typing.Dict[str, int] = {}
    for _, parent, _ in bones:
        if parent is not None:
            children[parent] = children.get(parent, 0) + 1

    crowded = next((name for name, _, _ in bones if children.get(name, 0) > MAX_CHILD_JOINTS), None)
    if crowded is not None:
        raise ValueError("%s has %d child bones and the game gives a joint %d at most" % (crowded, children[crowded], MAX_CHILD_JOINTS))

    if skinned and len(bones) > MAX_SKINNED_JOINTS:
        raise ValueError("the armature has %d bones and the game draws a skin with %d at most: remove the bones nothing is weighted to"
                         % (len(bones), MAX_SKINNED_JOINTS))


def animation_joint_count(stored: int, model_joints: int, bones: int) -> int:
    """How many joints an imported animation has track settings for. Every joint of the model reads the settings of its index from the
    animation playing (AnimateJoint, with no check), so a joint past them reads whatever follows them. The game's animations have more or
    fewer joints than their model now and then, and keep that while the model has no more joints than it had when they were imported;
    once bones got added they cover every bone."""
    return stored if bones <= model_joints else max(stored, bones)


# Animations made in Blender get IDs from here up, past the game's
FIRST_NEW_ANIMATION_ID = 0x8000


def animation_ids(stored: typing.Sequence[typing.Optional[int]]) -> typing.List[int]:
    """The ID of every animation of a model in their order: their own, the next free one from 0x8000 up for animations without one
    (made in Blender) and copies repeating one before them. Game objects play an animation by its ID, so an animation keeps the ID it
    got once."""
    taken = {animation_id for animation_id in stored if animation_id is not None}
    next_id = max([FIRST_NEW_ANIMATION_ID] + [animation_id + 1 for animation_id in taken])
    seen: typing.Set[int] = set()
    result = []
    for animation_id in stored:
        if animation_id is None or animation_id in seen:
            animation_id = next_id
            next_id += 1

        seen.add(animation_id)
        result.append(animation_id)

    return result


def number_joints(bones: typing.Sequence[BoneInfo]) -> typing.Tuple[typing.Dict[str, int], bool]:
    """The joint of every bone: the index its Joint type gives it, the next free ones for bones without one or with one a bone before
    it has. When that isn't the order the game walks the skeleton in, every bone gets numbered in it, the children of a bone in the order
    of the indexes they had. Returns the joints and whether they were numbered in the walk's order."""
    result: typing.Dict[str, int] = {}
    taken = set()
    for name, _, index in bones:
        if index is not None and index not in taken:
            result[name] = index
            taken.add(index)

    next_index = max(taken, default=-1) + 1
    for name, _, _ in bones:
        if name not in result:
            result[name] = next_index
            next_index += 1

    if _is_in_game_order(bones, result):
        return result, False

    return _numbered_from_roots(bones), True


def _is_in_game_order(bones: typing.Sequence[BoneInfo], joints: typing.Dict[str, int]) -> bool:
    if not bones:
        return True

    children: typing.Dict[str, typing.List[str]] = {}
    roots = []
    for name, parent, _ in bones:
        if parent in joints:
            children.setdefault(parent, []).append(name)
        else:
            roots.append(name)

    if len(roots) != 1 or sorted(joints.values()) != list(range(len(bones))):
        return False

    walk = []
    pending = [roots[0]]
    while pending:
        name = pending.pop()
        walk.append(name)
        pending.extend(sorted(children.get(name, []), key=joints.get, reverse=True))

    return all(joints[name] == place for place, name in enumerate(walk))


def _numbered_from_roots(bones: typing.Sequence[BoneInfo]) -> typing.Dict[str, int]:
    position = {name: order for order, (name, _, _) in enumerate(bones)}
    stored = {name: index for name, _, index in bones}
    children: typing.Dict[typing.Optional[str], typing.List[str]] = {}
    for name, parent, _ in bones:
        children.setdefault(parent if parent in position else None, []).append(name)

    def numbered_first(name: str) -> typing.Tuple[bool, int, int]:
        return stored[name] is None, stored[name] or 0, position[name]

    result: typing.Dict[str, int] = {}
    pending = sorted(children.get(None, []), key=numbered_first, reverse=True)
    while pending:
        name = pending.pop()
        result[name] = len(result)
        pending.extend(sorted(children.get(name, []), key=numbered_first, reverse=True))

    return result
