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

"""Which joint of the game's model each bone of a new rig stands for, without Blender.

The rig's bones that stand for joints have to be the model's skeleton, literally a subtree of it: the rig's root is the model's root,
and every other paired bone is under the bone paired with its joint's parent. A joint left out leaves out everything under it, and a
bone under one standing for no joint stands for none either: such bones are new joints, at rest. Pairs that break that are refused,
nothing is retargeted with them. Certain pairs (a bone of the joint's index, or named like it) come first, the others are guessed by
which way the bones and what's under them rest from their parents, against the joints' (whatever size and wherever the rig is, a rig
of part of the model's skeleton too). Shape keys are paired by name, then in their order."""

import math
import typing

Vector = typing.Tuple[float, float, float]
# A bone: its name, its parent's name and its head where it rests, in the world
Bone = typing.Tuple[str, typing.Optional[str], Vector]
# Every bone of the rig and the joint it stands for, None for a new joint
Pairs = typing.Dict[str, typing.Optional[str]]

# How far a guess may turn from its joint at most: the angles between the ways they rest from their parents and the ways what's under them
# does, added up
GUESS_REACH = 0.9


class Skeleton:
    """The bones in the order they were given, which children keep."""

    def __init__(self, bones: typing.Sequence[Bone], indexes: typing.Optional[typing.Dict[str, int]] = None):
        self.names = [name for name, _, _ in bones]
        self.parent = {name: parent for name, parent, _ in bones}
        self.head = {name: tuple(float(value) for value in head) for name, _, head in bones}
        # The joint indexes of a model's skeleton, or the ones a rig's bones were given
        self.index = dict(indexes or {})
        self.children: typing.Dict[typing.Optional[str], typing.List[str]] = {}
        for name, parent, _ in bones:
            self.children.setdefault(parent if parent in self.parent else None, []).append(name)

    def roots(self) -> typing.List[str]:
        return self.children.get(None, [])

    def below(self, name: str) -> typing.List[str]:
        """The bone and every bone under it."""
        result = [name]
        for child in self.children.get(name, []):
            result.extend(self.below(child))

        return result

    def size(self) -> float:
        heads = list(self.head.values())
        if not heads:
            return 1.0

        return max(max(max(head[axis] for head in heads) - min(head[axis] for head in heads) for axis in range(3)), 1e-6)

    def way(self, start: str, points: typing.List[Vector]) -> typing.Optional[Vector]:
        """The way the points' middle is from the bone's head, none when it's there."""
        middle = tuple(sum(point[axis] for point in points) / len(points) for axis in range(3))
        offset = tuple(middle[axis] - self.head[start][axis] for axis in range(3))
        length = sum(value * value for value in offset) ** 0.5
        return tuple(value / length for value in offset) if length > self.size() * 1e-4 else None  # type: ignore[return-value]


def _angle(a: typing.Optional[Vector], b: typing.Optional[Vector]) -> float:
    if a is None or b is None:
        return 0.0 if a is None and b is None else math.pi

    return math.acos(max(-1.0, min(1.0, sum(x * y for x, y in zip(a, b)))))


def _certain(source: Skeleton, target: Skeleton, bone: str, joints: typing.List[str]) -> typing.Optional[str]:
    """The joint among the joints the bone certainly is: the one of its index, else the one of its name."""
    index = target.index.get(bone)
    if index is not None:
        found = next((joint for joint in joints if source.index.get(joint) == index), None)
        if found is not None:
            return found

    return bone if bone in joints else None


def suggest(source: Skeleton, target: Skeleton, reach: float = GUESS_REACH) -> Pairs:
    """The pairs to start the table with: the roots together, then every paired bone's children with its joint's children, certain pairs
    first, then the closest by where the bone and everything under it rest against the joint and everything under it, while they're
    within the reach. The rest are new joints."""
    pairs: Pairs = {name: None for name in target.names}
    roots, joint_roots = target.roots(), source.roots()
    if not roots or not joint_roots:
        return pairs

    def cost(parent: str, bone: str, parent_joint: str, joint: str) -> float:
        # The way the bone rests from its parent, and the way everything under it does
        bone_way = target.way(parent, [target.head[bone]])
        joint_way = source.way(parent_joint, [source.head[joint]])
        under_way = target.way(parent, [target.head[name] for name in target.below(bone)])
        joint_under_way = source.way(parent_joint, [source.head[name] for name in source.below(joint)])
        if bone_way is None and under_way is None:
            # A bone where its parent is, of nothing under it elsewhere: no way to tell
            return math.inf

        return _angle(bone_way, joint_way) + _angle(under_way, joint_under_way)

    pending = [(roots[0], joint_roots[0])]
    pairs[roots[0]] = joint_roots[0]
    while pending:
        bone, joint = pending.pop(0)
        children = list(target.children.get(bone, []))
        free = list(source.children.get(joint, []))
        for child in list(children):
            found = _certain(source, target, child, free)
            if found is not None:
                pairs[child] = found
                free.remove(found)
                children.remove(child)
                pending.append((child, found))

        # The closest of all that are left first
        candidates = sorted((cost(bone, child, joint, other), index, child, other) for index, child in enumerate(children) for other in free)
        for value, _, child, other in candidates:
            if value > reach or pairs[child] is not None or other not in free:
                continue

            pairs[child] = other
            free.remove(other)
            pending.append((child, other))

    return pairs


def problems(source: Skeleton, target: Skeleton, pairs: Pairs) -> typing.List[str]:
    """What keeps the pairs from being retargeted: the paired bones have to be the source's skeleton literally, a subtree of it from its
    root down, with no joint stood for twice."""
    result: typing.List[str] = []
    paired = {bone: joint for bone, joint in pairs.items() if joint is not None and bone in target.parent}
    if not paired:
        return ["No bone stands for a joint of the model: pair the rig's root with the model's root"]

    roots = target.roots()
    if len(roots) != 1:
        result.append("The rig has %d root bones (%s): a model's skeleton has one, joint 0" % (len(roots), ", ".join(roots)))

    joint_roots = source.roots()
    for bone in roots:
        joint = paired.get(bone)
        if joint is not None and joint not in joint_roots:
            result.append("%s is the rig's root and stands for %s, which isn't the model's root %s: the rig has to be the model's skeleton from its root down"
                          % (bone, joint, ", ".join(joint_roots)))
        elif joint is None:
            result.append("%s is the rig's root and stands for no joint: it has to stand for the model's root %s" % (bone, ", ".join(joint_roots)))

    for joint in sorted(set(paired.values())):
        bones = sorted(bone for bone, other in paired.items() if other == joint)
        if joint not in source.parent:
            result.append("%s stands for %s, which the model doesn't have" % (", ".join(bones), joint))
        elif len(bones) > 1:
            result.append("%s stand for the same joint %s: a joint is one bone" % (" and ".join(bones), joint))

    for bone in target.names:
        joint = paired.get(bone)
        parent = target.parent.get(bone)
        if joint is None or parent is None or joint not in source.parent:
            continue

        parent_joint = paired.get(parent)
        joint_parent = source.parent.get(joint)
        if parent_joint is None:
            result.append("%s stands for %s, but %s above it stands for no joint: what's under a new joint is new too" % (bone, joint, parent))
        elif parent_joint != joint_parent:
            result.append("%s stands for %s, whose parent is %s, but %s above it stands for %s: the rig has to be the model's skeleton, every bone under the "
                          "bone of its joint's parent" % (bone, joint, joint_parent or "none", parent, parent_joint))

    return result


def new_joints(target: Skeleton, pairs: Pairs) -> typing.List[str]:
    """The rig's bones standing for no joint, which become new joints at rest."""
    return [bone for bone in target.names if pairs.get(bone) is None]


def left_out(source: Skeleton, pairs: Pairs) -> typing.List[str]:
    """The model's joints no bone stands for: what follows them (exit points, hulls, joint IDs) has to go elsewhere."""
    stood = set(joint for joint in pairs.values() if joint is not None)
    return [joint for joint in source.names if joint not in stood]


def nearest_paired(source: Skeleton, joint: str, pairs: Pairs) -> typing.Optional[str]:
    """The bone standing for the joint, else for its nearest ancestor some bone stands for."""
    bone_of = {other: bone for bone, other in pairs.items() if other is not None}
    current: typing.Optional[str] = joint
    while current is not None:
        if current in bone_of:
            return bone_of[current]

        current = source.parent.get(current)

    return None


def pair_shapes(source: typing.Sequence[str], target: typing.Sequence[str]) -> typing.Dict[str, typing.Optional[str]]:
    """Every shape key of the rig's blend skin and the model's it plays the animations of: of the same name, then the free ones in order."""
    result: typing.Dict[str, typing.Optional[str]] = {}
    free = [name for name in source if name not in target]
    for name in target:
        if name in source:
            result[name] = name
        else:
            result[name] = free.pop(0) if free else None

    return result
