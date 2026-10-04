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

"""The box of an OGI's instances, without Blender. The game takes the OGI's bounding box for its instances' own box (SetCollisionOgi):
their collision when the model has no hulls, which also places them in the scenery's tree, box shadows, physics bodies' sides and
cutscene framing. The game's tools made it around the meshes at rest with W 1 (417 of the PAL version's 508 models), but not every time
(the crates' pieces have a box their physics bodies take, and 10 models' boxes don't even hold their meshes): a model keeps its box
until its meshes change, and gets one around them when they then go past it."""

import typing

from . import tlm, tlm_math

Point = typing.Tuple[float, float, float]
Bounds = typing.Tuple[Point, Point]


def rest_bounds(file: tlm.TlmFile, root: typing.Dict[str, typing.Any]) -> typing.Optional[Bounds]:
    """The box of the model's skin, shape and rigid bodies at rest, in the model's space: a body is on its joint's bind pose, moved by
    its transform. None without meshes."""
    armature = tlm.find_child(root, "armature") or {}
    joints = {}
    for joint in armature.get("joints", []):
        bind = joint.get("bind", [])
        if len(bind) == 16:
            joints[int(joint.get("index", -1))] = tlm_math.compose(*tlm_math.rest_of(tlm_math.from_rows(bind)))

    points: typing.List[Point] = []
    for node in _nodes(root):
        kind = node.get("kind")
        if kind in ("skin", "shape"):
            points.extend(_positions(file, node.get("mesh")))
        elif kind == "body":
            local = tlm_math.compose(tuple(node.get("translation", (0.0, 0.0, 0.0))), tlm_math.xyzw_to_wxyz(node.get("rotation", (0.0, 0.0, 0.0, 1.0))),
                                     tuple(node.get("scale", (1.0, 1.0, 1.0))))
            joint = joints.get(int(node.get("joint", -1)))
            matrix = _multiply(joint, local) if joint is not None else local
            points.extend(_transformed(matrix, point) for point in _positions(file, node.get("mesh")))

    if not points:
        return None

    return tuple(min(point[axis] for point in points) for axis in range(3)), tuple(max(point[axis] for point in points) for axis in range(3))


def same_bounds(a: Bounds, b: Bounds) -> bool:
    """Whether two boxes are the same but for rounding errors: within a ten thousandth of the model's size."""
    tolerance = 1e-4 * max(1.0, max(a[1][axis] - a[0][axis] for axis in range(3)))
    return all(abs(x - y) <= tolerance for corner in range(2) for x, y in zip(a[corner], b[corner]))


def holds(box: Bounds, bounds: Bounds) -> bool:
    tolerance = 1e-4 * max(1.0, max(bounds[1][axis] - bounds[0][axis] for axis in range(3)))
    return all(box[0][axis] <= bounds[0][axis] + tolerance and box[1][axis] >= bounds[1][axis] - tolerance for axis in range(3))


def fitted_box(box: Bounds, bounds: typing.Optional[Bounds], imported: typing.Optional[Bounds]) -> typing.Optional[Bounds]:
    """The box the model gets, None to keep its own: the meshes' box when they changed since they were imported (or weren't, made in
    Blender) and went past the model's."""
    if bounds is None or imported is not None and same_bounds(bounds, imported) or holds(box, bounds):
        return None

    return bounds


def _nodes(node: typing.Dict[str, typing.Any]) -> typing.Iterator[typing.Dict[str, typing.Any]]:
    yield node
    for child in node.get("children", []) or []:
        yield from _nodes(child)


def _positions(file: tlm.TlmFile, mesh: typing.Optional[typing.Dict[str, typing.Any]]) -> typing.Iterator[Point]:
    for part in (mesh or {}).get("parts", []):
        values = file.read_view(part.get("position"), "f32")
        for index in range(0, len(values) - 2, 3):
            yield values[index], values[index + 1], values[index + 2]


def _multiply(a: tlm_math.Matrix, b: tlm_math.Matrix) -> tlm_math.Matrix:
    return [[sum(a[row][k] * b[k][column] for k in range(4)) for column in range(4)] for row in range(4)]


def _transformed(matrix: tlm_math.Matrix, point: Point) -> Point:
    return tuple(matrix[row][0] * point[0] + matrix[row][1] * point[1] + matrix[row][2] * point[2] + matrix[row][3] for row in range(3))
