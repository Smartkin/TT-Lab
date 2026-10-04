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

"""Collision made from the triangles of meshes, without Blender.

Corners closer than the weld distance become one vertex (the collision's own vertexes stay as they are, new corners join them),
triangles that come out flat are dropped and ones the collision already has are skipped. The game winds most of its collision so a
triangle's right-handed normal points into the solid (a floor's down), the other way from the meshes drawn on it, so the triangles
are turned around unless told otherwise.

The game's collision is much coarser than its meshes (a platform's mesh of 167 triangles stands on a box of 12), and it has to be:
the player only collides with 32 triangles at a time (the decomp's GatherTriangleContacts). With more in the box around Crash it
cuts his motion down to a fifth and still keeps only the first 32 it finds, so on collision as fine as a mesh he crawls and gets
stuck. The triangles are made coarser by collapsing their edges while every corner stays within the tolerance of the planes of the
triangles it took the place of (Ronfard and Rossignac's measure, the collapses in the order of Garland and Heckbert's quadrics).
"""

import heapq
import math
import typing

Vector3 = typing.Tuple[float, float, float]
Triangle = typing.Tuple[int, int, int]

# A triangle whose area is this share of its longest edge squared or less is a line
FLAT = 1e-6
# How far, in the game's units, the collision may be from the meshes it's made of
TOLERANCE = 0.1
# A plane whose normal's Y is this or more is a floor (Y is up in the game), floors stay within the tolerance when a mesh gets made
# coarser: the player stands on them
FLOOR = 0.7
# A mesh becomes its convex hull while every point of the hull is within this distance of it: gaps narrower than about twice it get
# filled, Crash is 1.2 units across
HULL_DISTANCE = 0.5
# The most triangles the game collides the player with at a time
MOST_TRIANGLES = 32
# The box the game gathers them in around the player: Crash's hull's box (half his width to either side, his height up from his
# feet) grown by 0.3 and a frame of running
REACH_SIDE = 0.62 + 0.3 + 0.15
REACH_BELOW = 0.3 + 0.15
REACH_ABOVE = 1.95 + 0.3 + 0.15


class Result(typing.NamedTuple):
    positions: typing.List[Vector3]
    """The new vertexes, numbered after the collision's own"""
    triangles: typing.List[Triangle]
    """The new triangles, by the collision's vertexes and the new ones"""
    skipped: int
    """Triangles the collision already had"""
    dropped: int
    """Triangles that were flat once their corners were welded"""
    sources: int = 0
    """Triangles of the meshes that went in, before they were made coarser"""
    hulls: int = 0
    """Meshes and parts of meshes made into their convex hulls"""
    coarsened: int = 0
    """Meshes made coarser than the tolerance and hull distance because the player would touch too many of their triangles"""
    covered: int = 0
    """Parts of meshes left out because they lie on others (drop_covered)"""


class Crowding(typing.NamedTuple):
    places: int
    """Floor triangles where the player would touch the limit of triangles or more"""
    most: int
    """The most triangles the player would touch on any of them"""
    worst: typing.Optional[Vector3] = None
    """Where on the floors the player would touch the most, none without floors"""


class _Welder:
    def __init__(self, distance: float, positions: typing.Sequence[Vector3]):
        self.distance = max(distance, 0.0)
        self.cell = max(self.distance, 1e-6)
        self.positions: typing.List[Vector3] = []
        self.cells: typing.Dict[typing.Tuple[int, int, int], typing.List[int]] = {}
        for position in positions:
            self._add(tuple(float(value) for value in position))

    def _key(self, position: Vector3) -> typing.Tuple[int, int, int]:
        return tuple(int(math.floor(value / self.cell)) for value in position)

    def _add(self, position: Vector3) -> int:
        index = len(self.positions)
        self.positions.append(position)
        self.cells.setdefault(self._key(position), []).append(index)
        return index

    def index_of(self, position: Vector3) -> int:
        """The vertex within the weld distance of the position, a new one when there's none"""
        position = tuple(float(value) for value in position)
        key = self._key(position)
        nearest, best = -1, self.distance * self.distance
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for index in self.cells.get((key[0] + dx, key[1] + dy, key[2] + dz), ()):
                        other = self.positions[index]
                        distance = sum((a - b) * (a - b) for a, b in zip(position, other))
                        if distance <= best:
                            nearest, best = index, distance

        return nearest if nearest >= 0 else self._add(position)

    def forget_after(self, count: int) -> None:
        """Takes back the vertexes added after the first count"""
        for index in range(len(self.positions) - 1, count - 1, -1):
            self.cells[self._key(self.positions[index])].remove(index)

        del self.positions[count:]


def _sub(a: Vector3, b: Vector3) -> Vector3:
    return a[0] - b[0], a[1] - b[1], a[2] - b[2]


def _cross(a: Vector3, b: Vector3) -> Vector3:
    return a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]


def _dot(a: Vector3, b: Vector3) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def is_flat(a: Vector3, b: Vector3, c: Vector3) -> bool:
    ab = _sub(b, a)
    ac = _sub(c, a)
    bc = _sub(c, b)
    area = math.sqrt(_dot(_cross(ab, ac), _cross(ab, ac)))
    longest = max(_dot(ab, ab), _dot(ac, ac), _dot(bc, bc))
    return area <= FLAT * longest or area == 0.0


def _plane_through(normal: Vector3, point: Vector3) -> typing.Optional[typing.Tuple[float, float, float, float]]:
    length = math.sqrt(_dot(normal, normal))
    if length == 0.0:
        return None

    nx, ny, nz = normal[0] / length, normal[1] / length, normal[2] / length
    return nx, ny, nz, -(nx * point[0] + ny * point[1] + nz * point[2])


def _add_quadric(quadric: typing.List[float], plane: typing.Tuple[float, float, float, float], weight: float) -> None:
    a, b, c, d = plane
    quadric[0] += weight * a * a
    quadric[1] += weight * a * b
    quadric[2] += weight * a * c
    quadric[3] += weight * a * d
    quadric[4] += weight * b * b
    quadric[5] += weight * b * c
    quadric[6] += weight * b * d
    quadric[7] += weight * c * c
    quadric[8] += weight * c * d
    quadric[9] += weight * d * d
    quadric[10] += weight


def _quadric_error(q: typing.Sequence[float], p: Vector3) -> float:
    x, y, z = p
    return (q[0] * x * x + 2.0 * q[1] * x * y + 2.0 * q[2] * x * z + 2.0 * q[3] * x + q[4] * y * y + 2.0 * q[5] * y * z + 2.0 * q[6] * y
            + q[7] * z * z + 2.0 * q[8] * z + q[9])


def _quadric_minimum(q: typing.Sequence[float]) -> typing.Optional[Vector3]:
    """Where the quadric is smallest, none when that's a line or a plane (fewer than three planes that aren't parallel)"""
    a, b, c, e, f, i = q[0], q[1], q[2], q[4], q[5], q[7]
    det = a * (e * i - f * f) - b * (b * i - f * c) + c * (b * f - e * c)
    trace = a + e + i
    if trace <= 0.0 or abs(det) <= 1e-9 * trace * trace * trace:
        return None

    rx, ry, rz = -q[3], -q[6], -q[8]
    x = (rx * (e * i - f * f) - b * (ry * i - f * rz) + c * (ry * f - e * rz)) / det
    y = (a * (ry * i - f * rz) - rx * (b * i - f * c) + c * (b * rz - ry * c)) / det
    z = (a * (e * rz - ry * f) - b * (b * rz - ry * c) + rx * (b * f - e * c)) / det
    return x, y, z


def _per_vertex(value: typing.Union[float, typing.Sequence[float]], count: int) -> typing.List[float]:
    return [float(value)] * count if isinstance(value, (int, float)) else [float(item) for item in value]


class _Simplifier:
    """Collapses edges of the triangles, cheapest first, while every corner stays within the tolerance of the planes of the
    triangles it took the place of, a vertex's own or the one all vertexes have, and within the floor tolerance of floors. The first
    `fixed` vertexes (the collision's own) don't move"""

    def __init__(self, positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle], fixed: int,
                 tolerance: typing.Union[float, typing.Sequence[float]], floor_tolerance: typing.Union[None, float, typing.Sequence[float]] = None):
        self.points: typing.List[Vector3] = [tuple(position) for position in positions]
        self.faces: typing.List[typing.Optional[Triangle]] = [tuple(triangle) for triangle in triangles]
        self.fixed = fixed
        count = len(self.points)
        self.tolerances = _per_vertex(tolerance, count)
        self.floor_tolerances = _per_vertex(float("inf") if floor_tolerance is None else floor_tolerance, count)
        self.vertex_faces: typing.List[typing.Set[int]] = [set() for _ in range(count)]
        self.quadrics = [[0.0] * 11 for _ in range(count)]
        # Planes by a rounded key, most of a flat area's triangles are one plane
        self.planes: typing.List[typing.Dict[tuple, tuple]] = [{} for _ in range(count)]
        self.versions = [0] * count
        self.heap: typing.List[tuple] = []
        edges: typing.Dict[typing.Tuple[int, int], typing.List[int]] = {}
        for index, (a, b, c) in enumerate(self.faces):
            for vertex in (a, b, c):
                self.vertex_faces[vertex].add(index)

            normal = self._normal(index)
            plane = _plane_through(normal, self.points[a])
            area = 0.5 * math.sqrt(_dot(normal, normal))
            for vertex in (a, b, c):
                self._add_plane(vertex, plane, area)

            for p, q in ((a, b), (b, c), (c, a)):
                edges.setdefault((p, q) if p < q else (q, p), []).append(index)

        # A plane through a boundary edge standing on its triangle keeps the edge where it is
        for (p, q), faces in edges.items():
            if len(faces) != 1:
                continue

            edge = _sub(self.points[q], self.points[p])
            plane = _plane_through(_cross(edge, self._normal(faces[0])), self.points[p])
            if plane is not None:
                for vertex in (p, q):
                    self._add_plane(vertex, plane, _dot(edge, edge))

        for p, q in edges:
            self._push(p, q)

    def _normal(self, face: int) -> Vector3:
        a, b, c = (self.points[vertex] for vertex in self.faces[face])
        return _cross(_sub(b, a), _sub(c, a))

    def _add_plane(self, vertex: int, plane, weight: float) -> None:
        if plane is None:
            return

        key = (round(plane[0] * 1e4), round(plane[1] * 1e4), round(plane[2] * 1e4), round(plane[3] * 1e3))
        self.planes[vertex].setdefault(key, plane + (plane[1] >= FLOOR,))
        _add_quadric(self.quadrics[vertex], plane, weight)

    def _neighbours(self, vertex: int) -> typing.Set[int]:
        found = set()
        for face in self.vertex_faces[vertex]:
            found.update(self.faces[face])

        found.discard(vertex)
        return found

    def _candidates(self, u: int, v: int) -> typing.List[typing.Tuple[float, Vector3]]:
        """Where the collapsed vertex can go with each place's mean squared distance from the planes, the best first"""
        tolerance = min(self.tolerances[u], self.tolerances[v])
        if u < self.fixed:
            places = [self.points[u]]
        elif v < self.fixed:
            places = [self.points[v]]
        else:
            pu, pv = self.points[u], self.points[v]
            places = [pu, pv]
            quadric = [x + y for x, y in zip(self.quadrics[u], self.quadrics[v])]
            edge = _sub(pv, pu)
            # The best place along the edge
            ax = quadric[0] * edge[0] + quadric[1] * edge[1] + quadric[2] * edge[2]
            ay = quadric[1] * edge[0] + quadric[4] * edge[1] + quadric[5] * edge[2]
            az = quadric[2] * edge[0] + quadric[5] * edge[1] + quadric[7] * edge[2]
            alpha = ax * edge[0] + ay * edge[1] + az * edge[2]
            if alpha > 1e-12:
                gx = quadric[0] * pu[0] + quadric[1] * pu[1] + quadric[2] * pu[2] + quadric[3]
                gy = quadric[1] * pu[0] + quadric[4] * pu[1] + quadric[5] * pu[2] + quadric[6]
                gz = quadric[2] * pu[0] + quadric[5] * pu[1] + quadric[7] * pu[2] + quadric[8]
                t = -(gx * edge[0] + gy * edge[1] + gz * edge[2]) / alpha
                if 0.0 < t < 1.0:
                    places.append((pu[0] + t * edge[0], pu[1] + t * edge[1], pu[2] + t * edge[2]))

            # The best place of all, while it's near the edge: with planes almost parallel it runs away
            best = _quadric_minimum(quadric)
            if best is not None:
                middle = (0.5 * (pu[0] + pv[0]), 0.5 * (pu[1] + pv[1]), 0.5 * (pu[2] + pv[2]))
                away = _sub(best, middle)
                if _dot(away, away) <= max(_dot(edge, edge), tolerance * tolerance):
                    places.append(best)

        quadric = [x + y for x, y in zip(self.quadrics[u], self.quadrics[v])]
        weight = quadric[10] if quadric[10] > 0.0 else 1.0
        scored = [(max(_quadric_error(quadric, place), 0.0) / weight, order, place) for order, place in enumerate(places)]
        scored.sort()
        return [(cost, place) for cost, _, place in scored]

    def _push(self, u: int, v: int) -> None:
        if u < self.fixed and v < self.fixed:
            return

        candidates = self._candidates(u, v)
        edge = _sub(self.points[u], self.points[v])
        heapq.heappush(self.heap, (candidates[0][0], _dot(edge, edge), u, v, self.versions[u], self.versions[v], candidates))

    def _is_close(self, u: int, v: int, place: Vector3, tolerance: float) -> bool:
        x, y, z = place
        floor_tolerance = min(tolerance, self.floor_tolerances[u], self.floor_tolerances[v])
        for planes in (self.planes[u], self.planes[v]):
            for a, b, c, d, floor in planes.values():
                if abs(a * x + b * y + c * z + d) > (floor_tolerance if floor else tolerance):
                    return False

        return True

    def _on_boundary(self, vertex: int) -> bool:
        counts: typing.Dict[int, int] = {}
        for face in self.vertex_faces[vertex]:
            for other in self.faces[face]:
                if other != vertex:
                    counts[other] = counts.get(other, 0) + 1

        return any(count == 1 for count in counts.values())

    def _can_collapse(self, u: int, v: int, place: Vector3) -> bool:
        shared = self.vertex_faces[u] & self.vertex_faces[v]
        if not shared or len(shared) > 2:
            return False

        # Their common neighbours have to be the corners across the edge, or the collapse pinches the surface
        across = set()
        for face in shared:
            across.update(self.faces[face])

        across.discard(u)
        across.discard(v)
        if self._neighbours(u) & self._neighbours(v) != across:
            return False

        if len(shared) == 2 and self._on_boundary(u) and self._on_boundary(v):
            return False

        made = set()
        for face in (self.vertex_faces[u] | self.vertex_faces[v]) - shared:
            corners = tuple(u if vertex == v else vertex for vertex in self.faces[face])
            key = tuple(sorted(corners))
            if key in made:
                return False

            made.add(key)
            a, b, c = (place if vertex == u else self.points[vertex] for vertex in corners)
            if is_flat(a, b, c):
                return False

            # A triangle turning over folds the surface
            if _dot(_cross(_sub(b, a), _sub(c, a)), self._normal(face)) <= 0.0:
                return False

        return True

    def _collapse(self, u: int, v: int, place: Vector3) -> None:
        if v < self.fixed:
            u, v = v, u

        for face in self.vertex_faces[u] & self.vertex_faces[v]:
            for vertex in self.faces[face]:
                self.vertex_faces[vertex].discard(face)

            self.faces[face] = None

        for face in self.vertex_faces[v]:
            self.faces[face] = tuple(u if vertex == v else vertex for vertex in self.faces[face])
            self.vertex_faces[u].add(face)

        self.vertex_faces[v] = set()
        self.points[u] = place
        self.tolerances[u] = min(self.tolerances[u], self.tolerances[v])
        self.floor_tolerances[u] = min(self.floor_tolerances[u], self.floor_tolerances[v])
        self.quadrics[u] = [x + y for x, y in zip(self.quadrics[u], self.quadrics[v])]
        for key, plane in self.planes[v].items():
            self.planes[u].setdefault(key, plane)

        self.planes[v] = {}
        self.versions[u] += 1
        self.versions[v] = -1
        for other in self._neighbours(u):
            self._push(u, other)

    def run(self) -> None:
        most = max(self.tolerances[self.fixed:], default=0.0) ** 2
        while self.heap:
            cost, _, u, v, version_u, version_v, candidates = heapq.heappop(self.heap)
            if cost > most:
                break

            if self.versions[u] != version_u or self.versions[v] != version_v:
                continue

            tolerance = min(self.tolerances[u], self.tolerances[v])
            for cost, place in candidates:
                if cost > tolerance * tolerance:
                    break

                if self._is_close(u, v, place, tolerance) and self._can_collapse(u, v, place):
                    self._collapse(u, v, place)
                    break


def _compact(positions: typing.Sequence[Vector3], triangles: typing.Iterable[typing.Optional[Triangle]],
             fixed: int) -> typing.Tuple[typing.List[Vector3], typing.List[Triangle]]:
    """The first `fixed` positions as they are, the ones the triangles use after them in the order they're first used"""
    renumbered: typing.Dict[int, int] = {}
    kept_positions = [tuple(position) for position in positions[:fixed]]
    kept_triangles: typing.List[Triangle] = []
    for triangle in triangles:
        if triangle is None:
            continue

        corners = []
        for vertex in triangle:
            if vertex < fixed:
                corners.append(vertex)
                continue

            if vertex not in renumbered:
                renumbered[vertex] = len(kept_positions)
                kept_positions.append(tuple(positions[vertex]))

            corners.append(renumbered[vertex])

        kept_triangles.append(tuple(corners))

    return kept_positions, kept_triangles


def simplify(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle],
             tolerance: typing.Union[float, typing.Sequence[float]] = TOLERANCE, fixed: int = 0,
             floor_tolerance: typing.Union[None, float, typing.Sequence[float]] = None) -> typing.Tuple[typing.List[Vector3], typing.List[Triangle]]:
    """The triangles made coarser: their edges collapsed while every corner stays within the tolerance (one for all, or each
    position's own) of the planes of the triangles it took the place of, of floors' planes within the floor tolerance (likewise)
    when it's smaller. The first `fixed` positions stay where and what they are, the others are numbered again after them and the
    ones no triangle uses any more are left out"""
    simplifier = _Simplifier(positions, triangles, fixed, tolerance, floor_tolerance)
    simplifier.run()
    return _compact(simplifier.points, simplifier.faces, fixed)


def convex_hull(points: typing.Sequence[Vector3], flat: float = 1e-3) -> typing.Optional[typing.Tuple[typing.List[Vector3], typing.List[Triangle]]]:
    """The convex hull of the points (quickhull), its triangles counter-clockwise seen from outside, none when the points are within
    the flat distance of a plane"""
    points = [tuple(float(value) for value in point) for point in points]
    if len(points) < 4:
        return None

    low = [min(point[axis] for point in points) for axis in range(3)]
    high = [max(point[axis] for point in points) for axis in range(3)]
    axis = max(range(3), key=lambda index: high[index] - low[index])
    epsilon = 1e-7 * max(max(abs(value) for value in low + high), high[axis] - low[axis], 1.0)
    first = min(range(len(points)), key=lambda index: points[index][axis])
    second = max(range(len(points)), key=lambda index: points[index][axis])
    line = _sub(points[second], points[first])
    third = max(range(len(points)), key=lambda index: _dot(_cross(_sub(points[index], points[first]), line), _cross(_sub(points[index], points[first]), line)))
    normal = _cross(line, _sub(points[third], points[first]))
    length = math.sqrt(_dot(normal, normal))
    if length == 0.0:
        return None

    fourth = max(range(len(points)), key=lambda index: abs(_dot(_sub(points[index], points[first]), normal)))
    if abs(_dot(_sub(points[fourth], points[first]), normal)) / length <= flat:
        return None

    faces: typing.Dict[int, list] = {}
    edges: typing.Dict[typing.Tuple[int, int], int] = {}
    pending: typing.List[int] = []
    made = [0]

    def add_face(a: int, b: int, c: int) -> int:
        plane = _plane_through(_cross(_sub(points[b], points[a]), _sub(points[c], points[a])), points[a]) or (0.0, 0.0, 0.0, 0.0)
        face = made[0]
        made[0] += 1
        faces[face] = [(a, b, c), plane, []]
        for edge in ((a, b), (b, c), (c, a)):
            edges[edge] = face

        return face

    def height(face: int, index: int) -> float:
        a, b, c, d = faces[face][1]
        point = points[index]
        return a * point[0] + b * point[1] + c * point[2] + d

    simplex = (first, second, third, fourth)
    centre = tuple(sum(points[index][axis] for index in simplex) / 4.0 for axis in range(3))
    for a, b, c in ((first, second, third), (first, second, fourth), (first, third, fourth), (second, third, fourth)):
        if _dot(_cross(_sub(points[b], points[a]), _sub(points[c], points[a])), _sub(centre, points[a])) > 0.0:
            b, c = c, b

        add_face(a, b, c)

    def assign(indexes: typing.Iterable[int], candidates: typing.Sequence[int]) -> None:
        for index in indexes:
            for face in candidates:
                if height(face, index) > epsilon:
                    if not faces[face][2]:
                        pending.append(face)

                    faces[face][2].append(index)
                    break

    assign((index for index in range(len(points)) if index not in simplex), list(faces))
    while pending:
        face = pending.pop()
        if face not in faces or not faces[face][2]:
            continue

        farthest = max(faces[face][2], key=lambda index: height(face, index))
        visible = set()
        stack = [face]
        while stack:
            current = stack.pop()
            if current in visible:
                continue

            visible.add(current)
            a, b, c = faces[current][0]
            for edge in ((b, a), (c, b), (a, c)):
                twin = edges.get(edge)
                if twin is not None and twin not in visible and height(twin, farthest) > epsilon:
                    stack.append(twin)

        horizon = []
        orphans = []
        for current in visible:
            a, b, c = faces[current][0]
            for edge in ((a, b), (b, c), (c, a)):
                if edges.get((edge[1], edge[0])) not in visible:
                    horizon.append(edge)

            orphans.extend(index for index in faces[current][2] if index != farthest)

        for current in visible:
            a, b, c = faces[current][0]
            for edge in ((a, b), (b, c), (c, a)):
                if edges.get(edge) == current:
                    del edges[edge]

            del faces[current]

        assign(orphans, [add_face(a, b, farthest) for a, b in horizon])

    return _compact(points, (corners for corners, _, _ in faces.values()), 0)


def _solve_planes(first, second, third) -> typing.Optional[Vector3]:
    """Where three planes (a, b, c, d with a x + b y + c z + d = 0) meet, none when two are parallel"""
    (a1, b1, c1, d1), (a2, b2, c2, d2), (a3, b3, c3, d3) = first[:4], second[:4], third[:4]
    det = a1 * (b2 * c3 - c2 * b3) - b1 * (a2 * c3 - c2 * a3) + c1 * (a2 * b3 - b2 * a3)
    if abs(det) < 1e-9:
        return None

    x = (-d1 * (b2 * c3 - c2 * b3) - b1 * (-d2 * c3 + c2 * d3) + c1 * (-d2 * b3 + b2 * d3)) / det
    y = (a1 * (-d2 * c3 + c2 * d3) + d1 * (a2 * c3 - c2 * a3) + c1 * (-a2 * d3 + d2 * a3)) / det
    z = (a1 * (-b2 * d3 + d2 * b3) - b1 * (-a2 * d3 + d2 * a3) - d1 * (a2 * b3 - b2 * a3)) / det
    return x, y, z


def _corners_of_planes(planes: typing.Sequence[tuple], centre: Vector3) -> typing.Optional[typing.Tuple[typing.List[Vector3], typing.List[typing.Set[int]]]]:
    """The corners of the convex shape inside the planes (centre inside all of them) and each plane's neighbours, through the hull of
    the planes' dual points: a plane a distance h from the centre is the point of its normal over h"""
    duals = []
    for a, b, c, d, _ in planes:
        height = -(a * centre[0] + b * centre[1] + c * centre[2] + d)
        if height <= 0.0:
            return None

        duals.append((a / height, b / height, c / height))

    hull = convex_hull(duals, flat=1e-12)
    if hull is None:
        return None

    index_of = {dual: index for index, dual in enumerate(duals)}
    neighbours: typing.List[typing.Set[int]] = [set() for _ in planes]
    corners: typing.List[Vector3] = []
    for triangle in hull[1]:
        first, second, third = (index_of[hull[0][vertex]] for vertex in triangle)
        for one, other in ((first, second), (second, third), (third, first)):
            neighbours[one].add(other)
            neighbours[other].add(one)

        a, b, c = (duals[index] for index in (first, second, third))
        normal = _cross(_sub(b, a), _sub(c, a))
        offset = _dot(normal, a)
        if offset <= 0.0:
            return None

        corners.append((centre[0] + normal[0] / offset, centre[1] + normal[1] / offset, centre[2] + normal[2] / offset))

    return corners, neighbours


def _growth(planes: typing.Sequence[tuple], removed: int, neighbours: typing.Iterable[int]) -> float:
    """How far past the removed plane the shape grows without it, as far as its neighbours let it: infinite when they let it go on"""
    around = [planes[index] for index in neighbours]
    plane = planes[removed]
    most = 0.0
    for first in range(len(around)):
        for second in range(first + 1, len(around)):
            # A way out along two neighbours' edge the others don't stop
            edge = _cross(around[first][:3], around[second][:3])
            for way in (edge, (-edge[0], -edge[1], -edge[2])):
                if _dot(way, plane[:3]) > 1e-9 and all(_dot(way, other[:3]) <= 1e-9 for other in around):
                    return float("inf")

            for third in range(second + 1, len(around)):
                corner = _solve_planes(around[first], around[second], around[third])
                if corner is None or any(_dot(other[:3], corner) + other[3] > 1e-7 for other in around):
                    continue

                most = max(most, _dot(plane[:3], corner) + plane[3])

    return most


def simplify_hull(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle], tolerance: float,
                  floor_tolerance: typing.Optional[float] = None) -> typing.Tuple[typing.List[Vector3], typing.List[Triangle]]:
    """A convex hull with the planes of its faces left out while it grows by no more than the tolerance past them (floors by the floor
    tolerance): bevels and rounded edges go, the planes that make the shape stay. It only grows, so a thin slab stays a slab"""
    floor_tolerance = tolerance if floor_tolerance is None else min(tolerance, floor_tolerance)
    planes: typing.Dict[tuple, tuple] = {}
    for a, b, c in triangles:
        plane = _plane_through(_cross(_sub(positions[b], positions[a]), _sub(positions[c], positions[a])), positions[a])
        if plane is not None:
            key = (round(plane[0] * 1e6), round(plane[1] * 1e6), round(plane[2] * 1e6), round(plane[3] * 1e5))
            planes.setdefault(key, plane + (plane[1] >= FLOOR,))

    planes = list(planes.values())
    used = sorted({vertex for triangle in triangles for vertex in triangle})
    centre = tuple(sum(positions[vertex][axis] for vertex in used) / len(used) for axis in range(3))
    while True:
        shape = _corners_of_planes(planes, centre)
        if shape is None:
            return [tuple(position) for position in positions], [tuple(triangle) for triangle in triangles]

        corners, neighbours = shape
        growths = sorted((_growth(planes, index, neighbours[index]), index) for index in range(len(planes)))
        # Planes no two of which are neighbours go at once: what grows past one is held by its neighbours, which stay
        gone: typing.Set[int] = set()
        for growth, index in growths:
            if growth > (floor_tolerance if planes[index][4] else tolerance):
                continue

            if not neighbours[index] & gone and len(planes) - len(gone) > 4:
                gone.add(index)

        if not gone:
            break

        planes = [plane for index, plane in enumerate(planes) if index not in gone]

    shape = _corners_of_planes(planes, centre)
    hull = convex_hull(shape[0]) if shape is not None else None
    return hull if hull is not None else ([tuple(position) for position in positions], [tuple(triangle) for triangle in triangles])


def _closest_on_triangle(p: Vector3, a: Vector3, b: Vector3, c: Vector3) -> Vector3:
    """The point of the triangle closest to p (Ericson's Real-Time Collision Detection, 5.1.5)"""
    ab, ac, ap = _sub(b, a), _sub(c, a), _sub(p, a)
    d1, d2 = _dot(ab, ap), _dot(ac, ap)
    if d1 <= 0.0 and d2 <= 0.0:
        return a

    bp = _sub(p, b)
    d3, d4 = _dot(ab, bp), _dot(ac, bp)
    if d3 >= 0.0 and d4 <= d3:
        return b

    vc = d1 * d4 - d3 * d2
    if vc <= 0.0 and d1 >= 0.0 and d3 <= 0.0:
        v = d1 / (d1 - d3)
        return a[0] + v * ab[0], a[1] + v * ab[1], a[2] + v * ab[2]

    cp = _sub(p, c)
    d5, d6 = _dot(ab, cp), _dot(ac, cp)
    if d6 >= 0.0 and d5 <= d6:
        return c

    vb = d5 * d2 - d1 * d6
    if vb <= 0.0 and d2 >= 0.0 and d6 <= 0.0:
        w = d2 / (d2 - d6)
        return a[0] + w * ac[0], a[1] + w * ac[1], a[2] + w * ac[2]

    va = d3 * d6 - d5 * d4
    if va <= 0.0 and d4 - d3 >= 0.0 and d5 - d6 >= 0.0:
        w = (d4 - d3) / ((d4 - d3) + (d5 - d6))
        return b[0] + w * (c[0] - b[0]), b[1] + w * (c[1] - b[1]), b[2] + w * (c[2] - b[2])

    denominator = 1.0 / (va + vb + vc)
    v, w = vb * denominator, vc * denominator
    return a[0] + ab[0] * v + ac[0] * w, a[1] + ab[1] * v + ac[1] * w, a[2] + ab[2] * v + ac[2] * w


def _hull_fits(hull: typing.Tuple[typing.List[Vector3], typing.List[Triangle]], positions: typing.Sequence[Vector3],
               triangles: typing.Sequence[Triangle], distance: float) -> bool:
    """Whether the hull's surface stays within the distance of the triangles: a hull over a hollow, a gap or between parts far apart
    doesn't"""
    hull_positions, hull_triangles = hull
    corners = [tuple(positions[vertex] for vertex in triangle) for triangle in triangles]
    low = [min(corner[axis] for triangle in corners for corner in triangle) for axis in range(3)]
    high = [max(corner[axis] for triangle in corners for corner in triangle) for axis in range(3)]
    # Cells of the distance, but not so many that a big mesh's triangles fill too many of them
    cell = max(distance, max(high[axis] - low[axis] for axis in range(3)) / 48.0, 1e-6)
    grid: typing.Dict[typing.Tuple[int, int, int], typing.List[int]] = {}
    for index, triangle in enumerate(corners):
        box_low = [min(corner[axis] for corner in triangle) - distance for axis in range(3)]
        box_high = [max(corner[axis] for corner in triangle) + distance for axis in range(3)]
        for x in range(int(math.floor(box_low[0] / cell)), int(math.floor(box_high[0] / cell)) + 1):
            for y in range(int(math.floor(box_low[1] / cell)), int(math.floor(box_high[1] / cell)) + 1):
                for z in range(int(math.floor(box_low[2] / cell)), int(math.floor(box_high[2] / cell)) + 1):
                    grid.setdefault((x, y, z), []).append(index)

    limit = distance * distance

    def near(point: Vector3) -> bool:
        for index in grid.get(tuple(int(math.floor(value / cell)) for value in point), ()):
            closest = _sub(point, _closest_on_triangle(point, *corners[index]))
            if _dot(closest, closest) <= limit:
                return True

        return False

    faces = [tuple(hull_positions[vertex] for vertex in triangle) for triangle in hull_triangles]
    # The middles first, a hull that doesn't fit mostly shows it there
    if not all(near(tuple(sum(corner[axis] for corner in face) / 3.0 for axis in range(3))) for face in faces):
        return False

    spacing = distance * 0.5
    for a, b, c in faces:
        longest = math.sqrt(max(_dot(_sub(b, a), _sub(b, a)), _dot(_sub(c, a), _sub(c, a)), _dot(_sub(c, b), _sub(c, b))))
        steps = min(max(int(math.ceil(longest / spacing)), 1), 48)
        for i in range(steps + 1):
            for j in range(steps + 1 - i):
                s, t = i / steps, j / steps
                point = (a[0] + s * (b[0] - a[0]) + t * (c[0] - a[0]), a[1] + s * (b[1] - a[1]) + t * (c[1] - a[1]),
                         a[2] + s * (b[2] - a[2]) + t * (c[2] - a[2]))
                if not near(point):
                    return False

    return True


def _components(triangles: typing.Sequence[Triangle]) -> typing.List[typing.List[Triangle]]:
    """The triangles in groups joined by their corners"""
    parents: typing.Dict[int, int] = {}

    def root(vertex: int) -> int:
        parents.setdefault(vertex, vertex)
        while parents[vertex] != vertex:
            parents[vertex] = parents[parents[vertex]]
            vertex = parents[vertex]

        return vertex

    for a, b, c in triangles:
        for other in (b, c):
            first, second = root(a), root(other)
            if first != second:
                parents[second] = first

    groups: typing.Dict[int, typing.List[Triangle]] = {}
    for triangle in triangles:
        groups.setdefault(root(triangle[0]), []).append(triangle)

    return list(groups.values())


def _fitting_hull(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle],
                  distance: float) -> typing.Optional[typing.Tuple[typing.List[Vector3], typing.List[Triangle]]]:
    hull = convex_hull([positions[vertex] for vertex in sorted({vertex for triangle in triangles for vertex in triangle})])
    return hull if hull is not None and _hull_fits(hull, positions, triangles, distance) else None


def _box(positions: typing.Sequence[Vector3], triangles: typing.Iterable[Triangle]) -> typing.Tuple[Vector3, Vector3]:
    vertexes = {vertex for triangle in triangles for vertex in triangle}
    return (tuple(min(positions[vertex][axis] for vertex in vertexes) for axis in range(3)),
            tuple(max(positions[vertex][axis] for vertex in vertexes) for axis in range(3)))


def _gap(first: typing.Tuple[Vector3, Vector3], second: typing.Tuple[Vector3, Vector3]) -> float:
    """How far apart two boxes are, 0 when they overlap"""
    apart = [max(first[0][axis] - second[1][axis], second[0][axis] - first[1][axis], 0.0) for axis in range(3)]
    return math.sqrt(_dot(apart, apart))


def hull_shapes(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle],
                distance: float) -> typing.Tuple[typing.List[typing.Tuple[typing.List[Vector3], typing.List[Triangle]]], typing.List[Triangle]]:
    """The convex hulls a mesh's triangles can be made of, and the triangles none fits: the whole mesh's hull when it stays within
    the distance of it, else the hulls of its parts that aren't joined to each other, parts near each other in one hull while it
    stays within the distance of them, the nearest first (a raft's planks become one)"""
    hull = _fitting_hull(positions, triangles, distance)
    if hull is not None:
        return [hull], []

    parts = _components(triangles)
    if len(parts) == 1:
        return [], list(triangles)

    rest: typing.List[Triangle] = []
    clusters: typing.Dict[int, tuple] = {}
    for part in parts:
        hull = _fitting_hull(positions, part, distance)
        if hull is None:
            rest.extend(part)
        else:
            clusters[len(clusters)] = (part, _box(positions, part), hull)

    pairs: typing.List[typing.Tuple[float, int, int]] = []

    def pair_with_the_rest(cluster: int) -> None:
        for other in clusters:
            if other != cluster:
                gap = _gap(clusters[cluster][1], clusters[other][1])
                if gap <= 2.0 * distance:
                    heapq.heappush(pairs, (gap, min(cluster, other), max(cluster, other)))

    for cluster in list(clusters):
        pair_with_the_rest(cluster)

    made = len(clusters)
    while pairs:
        _, first, second = heapq.heappop(pairs)
        if first not in clusters or second not in clusters:
            continue

        joined = clusters[first][0] + clusters[second][0]
        hull = _fitting_hull(positions, joined, distance)
        if hull is None:
            continue

        del clusters[first], clusters[second]
        clusters[made] = (joined, _box(positions, joined), hull)
        pair_with_the_rest(made)
        made += 1

    return [cluster[2] for cluster in clusters.values()], rest


def _area(a: Vector3, b: Vector3, c: Vector3) -> float:
    normal = _cross(_sub(b, a), _sub(c, a))
    return 0.5 * math.sqrt(_dot(normal, normal))


def _unit_normal(a: Vector3, b: Vector3, c: Vector3) -> Vector3:
    normal = _cross(_sub(b, a), _sub(c, a))
    length = math.sqrt(_dot(normal, normal))
    return (normal[0] / length, normal[1] / length, normal[2] / length) if length > 0.0 else (0.0, 0.0, 0.0)


# A part lies on another where the other's triangles face within about 25 degrees of its own
SAME_WAY = 0.9


def drop_covered(positions: typing.Sequence[Vector3], meshes: typing.Sequence[typing.Sequence[Triangle]], tolerance: float,
                 covers: typing.Sequence[typing.Tuple[Vector3, Vector3, Vector3]] = ()) -> typing.Tuple[typing.List[typing.List[Triangle]], int]:
    """The meshes without their parts (triangles joined by their corners) that lie on other parts or on the covers (triangles wound
    like the meshes that stay, the collision's own): every point of them within the tolerance of triangles facing the same way, like
    the layers of grass and water the game's meshes draw just over the ground, or a mesh the collision was made of already. The
    smallest go first, of two alike one stays. Comes with how many parts were left out"""
    parts = []
    for mesh, triangles in enumerate(meshes):
        for part in _components(triangles):
            area = sum(_area(positions[a], positions[b], positions[c]) for a, b, c in part)
            parts.append((area, len(parts), mesh, part))

    if not parts or len(parts) + len(covers) < 2 or tolerance <= 0.0:
        return [list(triangles) for triangles in meshes], 0

    corners = []
    owners = []
    normals = []
    for _, index, _, part in parts:
        for triangle in part:
            corners.append(tuple(positions[vertex] for vertex in triangle))
            owners.append(index)
            normals.append(_unit_normal(*corners[-1]))

    for cover in covers:
        corners.append(tuple(cover))
        owners.append(-1)
        normals.append(_unit_normal(*cover))

    low = [min(corner[axis] for triangle in corners for corner in triangle) for axis in range(3)]
    high = [max(corner[axis] for triangle in corners for corner in triangle) for axis in range(3)]
    cell = max(1.0, max(high[axis] - low[axis] for axis in range(3)) / 128.0)
    grid: typing.Dict[typing.Tuple[int, int, int], typing.List[int]] = {}
    for index, triangle in enumerate(corners):
        box_low = [min(corner[axis] for corner in triangle) - tolerance for axis in range(3)]
        box_high = [max(corner[axis] for corner in triangle) + tolerance for axis in range(3)]
        for x in range(int(math.floor(box_low[0] / cell)), int(math.floor(box_high[0] / cell)) + 1):
            for y in range(int(math.floor(box_low[1] / cell)), int(math.floor(box_high[1] / cell)) + 1):
                for z in range(int(math.floor(box_low[2] / cell)), int(math.floor(box_high[2] / cell)) + 1):
                    grid.setdefault((x, y, z), []).append(index)

    limit = tolerance * tolerance
    dropped: typing.Set[int] = set()

    def lies_on_others(point: Vector3, normal: Vector3, part: int) -> bool:
        for index in grid.get(tuple(int(math.floor(value / cell)) for value in point), ()):
            if owners[index] == part or owners[index] in dropped or _dot(normals[index], normal) < SAME_WAY:
                continue

            closest = _sub(point, _closest_on_triangle(point, *corners[index]))
            if _dot(closest, closest) <= limit:
                return True

        return False

    start = 0
    starts = []
    for _, _, _, part in parts:
        starts.append(start)
        start += len(part)

    for _, index, _, part in sorted(parts):
        triangles = [(corners[starts[index] + offset], normals[starts[index] + offset]) for offset in range(len(part))]
        # The middles first, a part that doesn't lie on others mostly shows it there
        if not all(lies_on_others(tuple(sum(corner[axis] for corner in triangle) / 3.0 for axis in range(3)), normal, index) for triangle, normal in triangles):
            continue

        covered = True
        for (a, b, c), normal in triangles:
            longest = math.sqrt(max(_dot(_sub(b, a), _sub(b, a)), _dot(_sub(c, a), _sub(c, a)), _dot(_sub(c, b), _sub(c, b))))
            steps = min(max(int(math.ceil(longest / 0.5)), 1), 16)
            for i in range(steps + 1):
                for j in range(steps + 1 - i):
                    s, t = i / steps, j / steps
                    point = (a[0] + s * (b[0] - a[0]) + t * (c[0] - a[0]), a[1] + s * (b[1] - a[1]) + t * (c[1] - a[1]),
                             a[2] + s * (b[2] - a[2]) + t * (c[2] - a[2]))
                    if not lies_on_others(point, normal, index):
                        covered = False
                        break

                if not covered:
                    break

            if not covered:
                break

        if covered:
            dropped.add(index)

    kept: typing.List[typing.List[Triangle]] = [[] for _ in meshes]
    for _, index, mesh, part in parts:
        if index not in dropped:
            kept[mesh].extend(part)

    return kept, len(dropped)


def _weld_sources(welder: _Welder, known: typing.Set[Triangle], sources: typing.Iterable[typing.Tuple[Vector3, Vector3, Vector3]],
                  counts: typing.List[int]) -> typing.List[Triangle]:
    """The sources' triangles by the welder's vertexes, without the flat ones and the ones known. Counts sources, skipped, dropped"""
    welded: typing.List[Triangle] = []
    for source in sources:
        counts[0] += 1
        before = len(welder.positions)
        corners = tuple(welder.index_of(corner) for corner in source)
        if len(set(corners)) < 3 or is_flat(*(welder.positions[corner] for corner in corners)):
            welder.forget_after(before)
            counts[2] += 1
            continue

        key = tuple(sorted(corners))
        if key in known:
            welder.forget_after(before)
            counts[1] += 1
            continue

        known.add(key)
        welded.append(corners)

    return welded


def add_triangles(positions: typing.Sequence[Vector3], triangles: typing.Iterable[Triangle], sources: typing.Iterable[typing.Tuple[Vector3, Vector3, Vector3]],
                  weld: float = 1e-3, flip: bool = True) -> Result:
    """The triangles of the sources (each three corners, counter-clockwise seen from outside like Blender's) added to a collision of
    the positions and triangles as they are. A source triangle with the same three vertexes as one there, whichever way round, is
    skipped."""
    welder = _Welder(weld, positions)
    counts = [0, 0, 0]
    added = _weld_sources(welder, {tuple(sorted(triangle)) for triangle in triangles}, sources, counts)
    if flip:
        added = [(a, c, b) for a, b, c in added]

    return Result(welder.positions[len(positions):], added, counts[1], counts[2], counts[0])


def _mesh_shapes(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle], tolerance: float, hull_distance: float,
                 floor_tolerance: float) -> typing.Tuple[typing.List[typing.Tuple[typing.List[Vector3], typing.List[Triangle]]], typing.List[Triangle]]:
    """A mesh's hulls, made coarser like the rest gets made (a hull is already as far from the mesh as the hull distance, half of that
    more is still close, but its floors), and the rest of its triangles as they are"""
    if hull_distance <= 0.0:
        return [], list(triangles)

    hulls, rest = hull_shapes(positions, triangles, hull_distance)
    return [simplify_hull(hull_positions, hull_triangles, max(tolerance, 0.5 * hull_distance), floor_tolerance) for hull_positions, hull_triangles in hulls], rest


def _with_hulls(positions: typing.Sequence[Vector3], triangles: typing.List[Triangle], hulls,
                weld: float) -> typing.Tuple[typing.List[Vector3], typing.List[Triangle]]:
    """The hulls' triangles added to the triangles, their corners joining the vertexes within the weld distance"""
    joiner = _Welder(weld, positions)
    known = {tuple(sorted(triangle)) for triangle in triangles}
    for hull_positions, hull_triangles in hulls:
        for triangle in hull_triangles:
            corners = tuple(joiner.index_of(hull_positions[vertex]) for vertex in triangle)
            key = tuple(sorted(corners))
            if len(set(corners)) < 3 or key in known or is_flat(*(joiner.positions[corner] for corner in corners)):
                continue

            known.add(key)
            triangles.append(corners)

    return joiner.positions, triangles


# How much coarser a mesh is made, its tolerance times these, while the player would still touch more than CROWDED triangles somewhere
# on it: a margin below the game's MOST_TRIANGLES for what's around it and for running faster. Its floors stay within the tolerance
# until the last, and the hull distance stays: hulls filling wider gaps would close passages Crash fits through
COARSER = (1.0, 2.0, 4.0)
CROWDED = 24


def add_meshes(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle],
               meshes: typing.Iterable[typing.Iterable[typing.Tuple[Vector3, Vector3, Vector3]]], weld: float = 1e-3, flip: bool = True,
               tolerance: float = TOLERANCE, hull_distance: float = HULL_DISTANCE, coarser_where_crowded: bool = True) -> Result:
    """The meshes' triangles (each three corners, counter-clockwise seen from outside like Blender's) made into collision like the
    game's and added to a collision of the positions and triangles. Parts of the meshes lying on others are left out (drop_covered),
    a mesh whose convex hull stays within the hull distance of it becomes the hull, other meshes' parts that aren't joined to each
    other the hulls of them that stay within it (hull_shapes), like a table's top and legs. The rest is made coarser within the
    tolerance, the meshes' triangles together so meshes that meet stay joined. A mesh on which the player would still touch more
    triangles than the game takes is made coarser again (COARSER). Source triangles the collision has are skipped like add_triangles
    skips them, the collision's own vertexes stay as they are."""
    welder = _Welder(weld, positions)
    own = [tuple(triangle) for triangle in triangles]
    known = {tuple(sorted(triangle)) for triangle in own}
    existing = set(known)
    counts = [0, 0, 0]
    rest: typing.List[Triangle] = []
    tolerances: typing.Dict[int, float] = {}
    floor_tolerances: typing.Dict[int, float] = {}
    hulls: typing.List[typing.Tuple[typing.List[Vector3], typing.List[Triangle]]] = []
    hull_count = coarsened = 0
    scales = COARSER if coarser_where_crowded and (tolerance > 0.0 or hull_distance > 0.0) else COARSER[:1]
    welded_meshes = [_weld_sources(welder, known, mesh, counts) for mesh in meshes]
    covered = 0
    if tolerance > 0.0 or hull_distance > 0.0:
        # As close as a hull is made coarser: layers drawn over the ground a little apart (grass, water edges) are no floor of their own.
        # The collision's own triangles are wound like the game's, turned back to compare them with the meshes
        covers = [tuple(positions[vertex] for vertex in ((a, c, b) if flip else (a, b, c))) for a, b, c in own]
        welded_meshes, covered = drop_covered(welder.positions, welded_meshes, max(tolerance, 0.5 * hull_distance), covers)

    for welded in welded_meshes:
        if not welded:
            continue

        for step, scale in enumerate(scales):
            mesh_tolerance = tolerance * scale
            floor_tolerance = mesh_tolerance if step == len(COARSER) - 1 else tolerance
            mesh_hulls, mesh_rest = _mesh_shapes(welder.positions, welded, mesh_tolerance, hull_distance, floor_tolerance)
            if step == len(scales) - 1:
                break

            alone = simplify(welder.positions, mesh_rest, mesh_tolerance, floor_tolerance=floor_tolerance) if mesh_tolerance > 0.0 else _compact(welder.positions, mesh_rest, 0)
            alone = _with_hulls(alone[0], alone[1], mesh_hulls, weld)
            # Made as the meshes are, wound like Blender's: its floors face up
            if crowding(alone[0], alone[1], range(len(alone[1])), flipped=False, limit=CROWDED + 1).places == 0:
                break

        coarsened += step > 0
        hull_count += len(mesh_hulls)
        hulls.extend(mesh_hulls)
        rest.extend(mesh_rest)
        for triangle in mesh_rest:
            for vertex in triangle:
                tolerances[vertex] = min(tolerances.get(vertex, mesh_tolerance), mesh_tolerance)
                floor_tolerances[vertex] = min(floor_tolerances.get(vertex, floor_tolerance), floor_tolerance)

    if tolerance > 0.0 or coarsened:
        made, added = simplify(welder.positions, rest, [tolerances.get(index, 0.0) for index in range(len(welder.positions))], len(positions),
                               [floor_tolerances.get(index, 0.0) for index in range(len(welder.positions))])
    else:
        made, added = _compact(welder.positions, rest, len(positions))

    # Made coarser onto the collision's own vertexes, a triangle can come out as one the collision has
    kept = [triangle for triangle in added if tuple(sorted(triangle)) not in existing]
    counts[1] += len(added) - len(kept)
    made, added = _with_hulls(made, kept, hulls, weld)
    if flip:
        added = [(a, c, b) for a, b, c in added]

    return Result(made[len(positions):], added, counts[1], counts[2], counts[0], hull_count, coarsened, covered)


def _touches_box(a: Vector3, b: Vector3, c: Vector3, centre: Vector3, half: Vector3) -> bool:
    """Whether the triangle and the box overlap, by the separating axes of Akenine-Moller's test"""
    v0, v1, v2 = _sub(a, centre), _sub(b, centre), _sub(c, centre)
    edges = (_sub(v1, v0), _sub(v2, v1), _sub(v0, v2))
    axes = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0), _cross(edges[0], edges[1])]
    for edge in edges:
        axes.append((0.0, -edge[2], edge[1]))
        axes.append((edge[2], 0.0, -edge[0]))
        axes.append((-edge[1], edge[0], 0.0))

    for axis in axes:
        p0, p1, p2 = _dot(v0, axis), _dot(v1, axis), _dot(v2, axis)
        reach = half[0] * abs(axis[0]) + half[1] * abs(axis[1]) + half[2] * abs(axis[2])
        if min(p0, p1, p2) > reach or max(p0, p1, p2) < -reach:
            return False

    return True


def crowding(positions: typing.Sequence[Vector3], triangles: typing.Sequence[Triangle], floors: typing.Iterable[int],
             flipped: bool = True, limit: int = MOST_TRIANGLES) -> Crowding:
    """Where the player standing on the floors (triangles by their index, the ones facing up of them) would touch the limit of the
    collision's triangles or more, by default as many as the game takes at a time"""
    cell = 2.0 * REACH_SIDE
    grid: typing.Dict[typing.Tuple[int, int, int], typing.List[int]] = {}
    boxes = []
    for index, triangle in enumerate(triangles):
        corners = [positions[vertex] for vertex in triangle]
        low = tuple(min(corner[axis] for corner in corners) for axis in range(3))
        high = tuple(max(corner[axis] for corner in corners) for axis in range(3))
        boxes.append((low, high))
        for x in range(int(math.floor(low[0] / cell)), int(math.floor(high[0] / cell)) + 1):
            for y in range(int(math.floor(low[1] / cell)), int(math.floor(high[1] / cell)) + 1):
                for z in range(int(math.floor(low[2] / cell)), int(math.floor(high[2] / cell)) + 1):
                    grid.setdefault((x, y, z), []).append(index)

    places = most = 0
    worst = None
    for floor in floors:
        a, b, c = (positions[vertex] for vertex in triangles[floor])
        normal = _cross(_sub(b, a), _sub(c, a))
        length = math.sqrt(_dot(normal, normal))
        if length == 0.0 or (-normal[1] if flipped else normal[1]) < 0.7 * length:
            continue

        foot = ((a[0] + b[0] + c[0]) / 3.0, (a[1] + b[1] + c[1]) / 3.0, (a[2] + b[2] + c[2]) / 3.0)
        low = (foot[0] - REACH_SIDE, foot[1] - REACH_BELOW, foot[2] - REACH_SIDE)
        high = (foot[0] + REACH_SIDE, foot[1] + REACH_ABOVE, foot[2] + REACH_SIDE)
        centre = tuple(0.5 * (low[axis] + high[axis]) for axis in range(3))
        half = tuple(0.5 * (high[axis] - low[axis]) for axis in range(3))
        seen = set()
        for x in range(int(math.floor(low[0] / cell)), int(math.floor(high[0] / cell)) + 1):
            for y in range(int(math.floor(low[1] / cell)), int(math.floor(high[1] / cell)) + 1):
                for z in range(int(math.floor(low[2] / cell)), int(math.floor(high[2] / cell)) + 1):
                    for index in grid.get((x, y, z), ()):
                        if index in seen:
                            continue

                        seen.add(index)

        touching = 0
        for index in seen:
            box_low, box_high = boxes[index]
            if any(box_low[axis] > high[axis] or box_high[axis] < low[axis] for axis in range(3)):
                continue

            if _touches_box(*(positions[vertex] for vertex in triangles[index]), centre, half):
                touching += 1

        if worst is None or touching > most:
            most, worst = touching, foot

        if touching >= limit:
            places += 1

    return Crowding(places, most, worst)
