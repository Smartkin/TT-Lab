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

"""Collision hulls the game can use, without Blender. The game collides convex hulls: it tests two hulls along their faces' normals and
the cross products of their edges (HullsIntersect) and a point or a sphere against every face's plane (IsPointInsideHull, SphereInPlanes),
so a hull that isn't convex collides like the space behind all of its faces' planes. HullsIntersect copies a hull's vertexes into 64
places on its stack and SphereInPlanes keeps a distance for each plane in 64, with no check: a hull has 64 vertexes and 64 faces at most
(the game's own have 24 and 33 at most)."""

import typing

MAX_VERTICES = 64
MAX_FACES = 64

Point = typing.Sequence[float]


def is_convex(points: typing.Sequence[Point], faces: typing.Sequence[typing.Sequence[int]]) -> bool:
    """Whether every face has the whole hull on one side of its plane, like TT Lab works the planes out (a thousandth of the hull's size
    of play)."""
    if not points:
        return True

    tolerance = 1e-3 * max(1.0, max(abs(value) for point in points for value in point[:3]))
    for face in faces:
        normal = _newell_normal([points[index] for index in face])
        length = sum(value * value for value in normal) ** 0.5
        if length == 0.0:
            continue

        normal = [value / length for value in normal]
        first = points[face[0]]
        distances = [sum(normal[axis] * (point[axis] - first[axis]) for axis in range(3)) for point in points]
        if max(distances) > tolerance and min(distances) < -tolerance:
            return False

    return True


def check_hull(name: str, vertices: int, faces: int) -> None:
    if vertices > MAX_VERTICES or faces > MAX_FACES:
        raise ValueError("%s has %d vertexes and %d faces and the game collides with hulls of %d of each at most (more overwrite its memory): simplify "
                         "it or split it into several hulls" % (name, vertices, faces, MAX_VERTICES))


def _newell_normal(corners: typing.Sequence[Point]) -> typing.List[float]:
    normal = [0.0, 0.0, 0.0]
    for index, current in enumerate(corners):
        following = corners[(index + 1) % len(corners)]
        normal[0] += (current[1] - following[1]) * (current[2] + following[2])
        normal[1] += (current[2] - following[2]) * (current[0] + following[0])
        normal[2] += (current[0] - following[0]) * (current[1] + following[1])

    return normal
