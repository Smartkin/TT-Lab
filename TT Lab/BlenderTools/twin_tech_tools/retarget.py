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

"""Posing a new armature's bones like the game's model's animations pose the joints they stand for (pairing), without Blender."""

import typing

from . import tlm_math

# A bone: its name, its parent's name and the joint index it already has
BoneInfo = typing.Tuple[str, typing.Optional[str], typing.Optional[int]]
# A bone's rest: its translation and rotation in the space both skeletons share
Rest = typing.Tuple[tlm_math.Vector, tlm_math.Quaternion]
# A skeleton: every bone's parent and rest
Skeleton = typing.Dict[str, typing.Tuple[typing.Optional[str], Rest]]
# A pose bone's location, rotation and scale, relative to its rest in its own axes like Blender keeps them
Pose = typing.Tuple[tlm_math.Vector, tlm_math.Quaternion, tlm_math.Vector]

REST_POSE: Pose = ((0.0, 0.0, 0.0), (1.0, 0.0, 0.0, 0.0), (1.0, 1.0, 1.0))

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
        stretch = poses.get(original_name, REST_POSE)[2]
        result[name] = (location, _normalized(rotation), tuple(stretch[axis] for axis in _matching_axes(original_rest[1], rest_rotation)))  # type: ignore[assignment]

    return result


def _matching_axes(original: tlm_math.Quaternion, incoming: tlm_math.Quaternion) -> typing.List[int]:
    """For every axis of the incoming bone, the original bone's axis lying closest to it at rest: a stretch along one goes along the other
    (bones' own axes can be picked another way, axes.aim_of)"""
    units = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)]
    original_axes = [_rotate(original, unit) for unit in units]
    result = []
    for unit in units:
        axis = _rotate(incoming, unit)
        result.append(max(range(3), key=lambda index: abs(sum(a * b for a, b in zip(axis, original_axes[index])))))

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
