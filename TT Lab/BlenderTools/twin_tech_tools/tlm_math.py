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

"""Transforms of joints without Blender's mathutils, so they can be tested without it.

Quaternions are (w, x, y, z) like Blender's, TT Lab's files have them as x, y, z, w. Matrices transform column vectors and are
lists of 4 rows, like the bind poses in TT Lab's files and Blender's matrices.

Bones rest where the joints' bind poses put them, without the scale bones can't have. An animation key is the joint's transform
relative to its parent joint, a pose bone's values are relative to its rest: key = rest @ basis, with the rest relative to the
parent bone's rest. Both are made of a translation, a rotation and a scale, so they're converted part by part and a joint the
game hides with a scale of 0 converts like any other.
"""

import math
import typing

Vector = typing.Tuple[float, float, float]
Quaternion = typing.Tuple[float, float, float, float]
Matrix = typing.List[typing.List[float]]


def quat_multiply(a: Quaternion, b: Quaternion) -> Quaternion:
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz,
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw)


def quat_normalized(q: Quaternion) -> Quaternion:
    length = math.sqrt(sum(value * value for value in q))
    return tuple(value / length for value in q) if length > 1e-12 else (1.0, 0.0, 0.0, 0.0)  # type: ignore[return-value]


def quat_inverse(q: Quaternion) -> Quaternion:
    w, x, y, z = quat_normalized(q)
    return (w, -x, -y, -z)


def quat_rotate(q: Quaternion, v: Vector) -> Vector:
    w, x, y, z = quat_normalized(q)
    _, rx, ry, rz = quat_multiply(quat_multiply((w, x, y, z), (0.0, v[0], v[1], v[2])), (w, -x, -y, -z))
    return (rx, ry, rz)


def matrix_to_quat(m: Matrix) -> Quaternion:
    """The rotation of a matrix without scale or shear."""
    trace = m[0][0] + m[1][1] + m[2][2]
    if trace > 0:
        s = math.sqrt(trace + 1.0) * 2
        return quat_normalized((0.25 * s, (m[2][1] - m[1][2]) / s, (m[0][2] - m[2][0]) / s, (m[1][0] - m[0][1]) / s))

    if m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        s = math.sqrt(1.0 + m[0][0] - m[1][1] - m[2][2]) * 2
        return quat_normalized(((m[2][1] - m[1][2]) / s, 0.25 * s, (m[0][1] + m[1][0]) / s, (m[0][2] + m[2][0]) / s))

    if m[1][1] > m[2][2]:
        s = math.sqrt(1.0 + m[1][1] - m[0][0] - m[2][2]) * 2
        return quat_normalized(((m[0][2] - m[2][0]) / s, (m[0][1] + m[1][0]) / s, 0.25 * s, (m[1][2] + m[2][1]) / s))

    s = math.sqrt(1.0 + m[2][2] - m[0][0] - m[1][1]) * 2
    return quat_normalized(((m[1][0] - m[0][1]) / s, (m[0][2] + m[2][0]) / s, (m[1][2] + m[2][1]) / s, 0.25 * s))


def decompose(m: Matrix) -> typing.Tuple[Vector, Quaternion, Vector]:
    """Translation, rotation and scale of a matrix."""
    translation = (m[0][3], m[1][3], m[2][3])
    scale = tuple(math.sqrt(m[0][column] ** 2 + m[1][column] ** 2 + m[2][column] ** 2) for column in range(3))
    rotation_matrix = [[m[row][column] / scale[column] if scale[column] > 1e-12 else (1.0 if row == column else 0.0) for column in range(3)] for row in range(3)]
    # A mirroring matrix turns the other way around one axis
    determinant = (rotation_matrix[0][0] * (rotation_matrix[1][1] * rotation_matrix[2][2] - rotation_matrix[1][2] * rotation_matrix[2][1])
                   - rotation_matrix[0][1] * (rotation_matrix[1][0] * rotation_matrix[2][2] - rotation_matrix[1][2] * rotation_matrix[2][0])
                   + rotation_matrix[0][2] * (rotation_matrix[1][0] * rotation_matrix[2][1] - rotation_matrix[1][1] * rotation_matrix[2][0]))
    if determinant < 0:
        scale = (-scale[0], scale[1], scale[2])
        for row in range(3):
            rotation_matrix[row][0] = -rotation_matrix[row][0]

    return translation, matrix_to_quat(rotation_matrix), scale  # type: ignore[return-value]


def compose(translation: Vector, rotation: Quaternion, scale: Vector = (1.0, 1.0, 1.0)) -> Matrix:
    w, x, y, z = quat_normalized(rotation)
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
         [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
         [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)]]
    return [[r[row][0] * scale[0], r[row][1] * scale[1], r[row][2] * scale[2], translation[row]] for row in range(3)] + [[0.0, 0.0, 0.0, 1.0]]


def from_rows(values: typing.Sequence[float]) -> Matrix:
    return [list(values[row * 4:row * 4 + 4]) for row in range(4)]


def to_rows(m: Matrix) -> typing.List[float]:
    return [float(value) for row in m for value in row]


def rest_of(bind: Matrix) -> typing.Tuple[Vector, Quaternion]:
    """Where a bone rests for a joint's bind pose, which can have a scale bones can't."""
    translation, rotation, _ = decompose(bind)
    return translation, rotation


def relative_rest(parent: typing.Optional[typing.Tuple[Vector, Quaternion]], child: typing.Tuple[Vector, Quaternion]) -> typing.Tuple[Vector, Quaternion]:
    """The child's rest relative to its parent's."""
    if parent is None:
        return child

    inverse = quat_inverse(parent[1])
    offset = tuple(c - p for c, p in zip(child[0], parent[0]))
    return quat_rotate(inverse, offset), quat_multiply(inverse, child[1])  # type: ignore[arg-type]


def key_to_pose(rest: typing.Tuple[Vector, Quaternion], translation: Vector, rotation: Quaternion, scale: Vector) -> typing.Tuple[Vector, Quaternion, Vector]:
    """A pose bone's location, rotation and scale that put the joint at the key."""
    inverse = quat_inverse(rest[1])
    location = quat_rotate(inverse, tuple(t - r for t, r in zip(translation, rest[0])))  # type: ignore[arg-type]
    return location, quat_multiply(inverse, rotation), scale


def pose_to_key(rest: typing.Tuple[Vector, Quaternion], location: Vector, rotation: Quaternion, scale: Vector) -> typing.Tuple[Vector, Quaternion, Vector]:
    """The key a pose bone's location, rotation and scale put the joint at."""
    moved = quat_rotate(rest[1], location)
    return tuple(r + m for r, m in zip(rest[0], moved)), quat_multiply(rest[1], rotation), scale  # type: ignore[return-value]


def xyzw_to_wxyz(values: typing.Sequence[float]) -> Quaternion:
    return (values[3], values[0], values[1], values[2])


def wxyz_to_xyzw(q: Quaternion) -> typing.Tuple[float, float, float, float]:
    return (q[1], q[2], q[3], q[0])
