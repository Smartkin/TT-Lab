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

"""Parts of TT Lab model files as the one mesh Blender keeps them in, and back.

Every part becomes a range of the mesh's vertexes and faces, the `tt_part` face attribute says which part a face is. The game's
values Blender can't hold stay in attributes next to the ones Blender shows, and what the parts keep besides their geometry
(the strips the game draws them with, how skins pack them) in a list next to the mesh. Nothing here needs Blender: meshes are
described by flat lists the way foreach_get and foreach_set take them.
"""

import array
import typing

from .tlm import TlmFile

# Keys of a part that aren't per vertex, kept next to the mesh while it's in Blender
PART_KEYS = ("strips", "compression")
# Values of a rigid part's vertexes the game can leave out, a part keeps having the ones it had
OPTIONAL_KEYS = ("normal", "uv_q", "emit_color", "alpha_flags")


class MeshData:
    """A mesh as flat lists, per vertex (points), per face corner and per face."""

    def __init__(self):
        self.positions = array.array("f")
        self.triangles = array.array("i")
        # Per vertex
        self.normals: typing.Optional[array.array] = None
        self.twin_normals: typing.Optional[array.array] = None
        self.uvs = array.array("f")
        self.uv_q: typing.Optional[array.array] = None
        self.colors: typing.Optional[array.array] = None
        self.emit_colors: typing.Optional[array.array] = None
        self.alpha_flags: typing.Optional[array.array] = None
        self.twin_joints: typing.Optional[array.array] = None
        self.twin_weights: typing.Optional[array.array] = None
        self.vertex_parts = array.array("i")
        # Offsets of every vertex for every shape
        self.shapes: typing.List[array.array] = []
        # Per face
        self.face_parts = array.array("i")
        self.face_materials = array.array("i")
        # Of every part: its material in the file and what it keeps besides its geometry
        self.parts: typing.List[typing.Dict[str, typing.Any]] = []

    @property
    def vertex_count(self) -> int:
        return len(self.positions) // 3


def _read(file: TlmFile, part: typing.Dict[str, typing.Any], key: str, view_type: str, size: int, count: int, fill: float) -> array.array:
    values = file.read_view(part.get(key), view_type) if key in part else array.array("f" if view_type == "f32" else "B")
    wanted = size * count
    if len(values) < wanted:
        values.extend([fill] * (wanted - len(values)))

    return values[:wanted]


def from_parts(file: TlmFile, mesh: typing.Optional[typing.Dict[str, typing.Any]], skinned: bool) -> MeshData:
    """The parts of a file's mesh as one mesh."""
    result = MeshData()
    parts = [part for part in (mesh or {}).get("parts", []) if isinstance(part, dict)]
    has_normals = any("normal" in part for part in parts)
    has_twin_normals = any("twin_normal" in part for part in parts)
    has_uv_q = any("uv_q" in part for part in parts)
    has_emit = any("emit_color" in part for part in parts)
    has_alpha = any("alpha_flags" in part for part in parts)
    shape_count = max((len(part.get("shapes", [])) for part in parts), default=0)
    result.normals = array.array("f") if has_normals else None
    result.twin_normals = array.array("f") if has_twin_normals else None
    result.uv_q = array.array("f") if has_uv_q else None
    result.colors = array.array("B")
    result.emit_colors = array.array("B") if has_emit else None
    result.alpha_flags = array.array("B") if has_alpha else None
    result.twin_joints = array.array("B") if skinned else None
    result.twin_weights = array.array("f") if skinned else None
    result.shapes = [array.array("f") for _ in range(shape_count)]
    for index, part in enumerate(parts):
        count = int(part.get("vertices", 0))
        start = result.vertex_count
        result.positions.extend(_read(file, part, "position", "f32", 3, count, 0.0))
        if result.normals is not None:
            result.normals.extend(_read(file, part, "normal", "f32", 3, count, 0.0) if "normal" in part else array.array("f", [0.0, 1.0, 0.0] * count))

        if result.twin_normals is not None:
            result.twin_normals.extend(_read(file, part, "twin_normal", "f32", 3, count, 0.0))

        result.uvs.extend(_read(file, part, "uv", "f32", 2, count, 0.0))
        if result.uv_q is not None:
            result.uv_q.extend(_read(file, part, "uv_q", "f32", 1, count, 1.0))

        result.colors.extend(_read(file, part, "color", "u8", 4, count, 0x7F))
        if result.emit_colors is not None:
            result.emit_colors.extend(_read(file, part, "emit_color", "u8", 4, count, 0))

        if result.alpha_flags is not None:
            result.alpha_flags.extend(_read(file, part, "alpha_flags", "u8", 2, count, 0))

        if skinned:
            result.twin_joints.extend(_read(file, part, "joints", "u8", 3, count, 0))
            result.twin_weights.extend(_read(file, part, "weights", "f32", 3, count, 0.0))

        shapes = part.get("shapes", [])
        for shape in range(shape_count):
            offsets = file.read_view(shapes[shape], "f32") if shape < len(shapes) else array.array("f")
            offsets.extend([0.0] * max(0, count * 3 - len(offsets)))
            result.shapes[shape].extend(offsets[:count * 3])

        result.vertex_parts.extend([index] * count)
        faces = file.read_view(part.get("faces"), "u32")
        # Blender can't have a face on a vertex twice, the game's are kept aside with where they were
        degenerate = []
        for i in range(0, len(faces) - 2, 3):
            face = (faces[i], faces[i + 1], faces[i + 2])
            if len(set(face)) < 3:
                degenerate.append([i // 3] + list(face))
                continue

            result.triangles.extend((face[0] + start, face[1] + start, face[2] + start))
            result.face_parts.append(index)
            result.face_materials.append(index)

        kept = {key: _inline(file, part[key]) for key in PART_KEYS if key in part}
        if len(degenerate) > 0:
            kept["degenerate"] = degenerate
            kept["triangles"] = len(faces) // 3 - len(degenerate)
        kept["material"] = int(part.get("material", -1))
        kept["optional"] = [key for key in OPTIONAL_KEYS if key in part]
        result.parts.append(kept)

    return result


def _inline(file: TlmFile, value: typing.Any) -> typing.Any:
    """The value with the arrays its views point at in place of them, so it can be kept as JSON."""
    if isinstance(value, dict):
        if set(value) == {"offset", "count", "type"}:
            return {"type": value["type"], "values": list(file.read_view(value))}

        return {key: _inline(file, item) for key, item in value.items()}

    return value


def _outline(file: TlmFile, value: typing.Any) -> typing.Any:
    if isinstance(value, dict):
        if set(value) == {"type", "values"}:
            return file.write_view(value["values"], value["type"])

        return {key: _outline(file, item) for key, item in value.items()}

    return value


class CornerMesh:
    """A mesh the way Blender has it after editing: triangles of corners, values per vertex and per corner."""

    def __init__(self):
        self.positions: typing.Sequence[float] = []
        # Vertex of every corner, 3 corners a triangle
        self.corner_vertices: typing.Sequence[int] = []
        self.triangle_parts: typing.Sequence[int] = []
        self.triangle_materials: typing.Sequence[int] = []
        # Per corner
        self.corner_uvs: typing.Sequence[float] = []
        self.corner_normals: typing.Optional[typing.Sequence[float]] = None
        self.corner_colors: typing.Optional[typing.Sequence[float]] = None
        # Per vertex
        self.vertex_colors: typing.Optional[typing.Sequence[float]] = None
        self.emit_colors: typing.Optional[typing.Sequence[float]] = None
        self.alpha_flags: typing.Optional[typing.Sequence[int]] = None
        self.twin_normals: typing.Optional[typing.Sequence[float]] = None
        self.twin_uvs: typing.Optional[typing.Sequence[float]] = None
        self.uv_q: typing.Optional[typing.Sequence[float]] = None
        self.twin_joints: typing.Optional[typing.Sequence[float]] = None
        self.twin_weights: typing.Optional[typing.Sequence[float]] = None
        # Up to 4 (joint, weight) of the vertex groups of every vertex
        self.group_influences: typing.Optional[typing.List[typing.List[typing.Tuple[int, float]]]] = None
        self.vertex_parts: typing.Sequence[int] = []
        self.shapes: typing.List[typing.Sequence[float]] = []
        # The game's offsets of every shape, None for shapes made in Blender
        self.twin_shapes: typing.List[typing.Optional[typing.Sequence[float]]] = []
        # What every part kept from the file, by the part's number
        self.parts: typing.List[typing.Dict[str, typing.Any]] = []
        # Material of every material slot, as an index into the file's materials
        self.slot_materials: typing.List[int] = []

    @property
    def vertex_count(self) -> int:
        return len(self.positions) // 3


def _to_byte(value: float) -> int:
    return max(0, min(255, int(round(value * 255.0))))


def to_parts(file: TlmFile, mesh: CornerMesh, skinned: bool) -> typing.Dict[str, typing.Any]:
    """The mesh's parts. Vertexes keep the order they have in Blender, a vertex whose corners need different values in a part is
    written once more for every other value, after the others."""
    triangle_count = len(mesh.corner_vertices) // 3
    groups: typing.Dict[typing.Tuple[int, int], typing.List[int]] = {}
    for triangle in range(triangle_count):
        key = (int(mesh.triangle_parts[triangle]) if triangle < len(mesh.triangle_parts) else 0,
               int(mesh.triangle_materials[triangle]) if triangle < len(mesh.triangle_materials) else 0)
        groups.setdefault(key, []).append(triangle)

    used_vertexes = set(mesh.corner_vertices)
    loose: typing.Dict[int, typing.List[int]] = {}
    for vertex in range(mesh.vertex_count):
        if vertex not in used_vertexes:
            loose.setdefault(int(mesh.vertex_parts[vertex]) if vertex < len(mesh.vertex_parts) else 0, []).append(vertex)

    # Every part the mesh came with stays one even without faces, faces given another material make a part of their own
    part_numbers = set(range(len(mesh.parts))) | {key[0] for key in groups} | set(loose)
    keys: typing.List[typing.Tuple[int, int]] = []
    for number in sorted(part_numbers):
        own_slot = number if number < len(mesh.parts) else None
        materials = sorted({key[1] for key in groups if key[0] == number}, key=lambda slot: (slot != own_slot, slot))
        if len(materials) == 0:
            materials = [own_slot if own_slot is not None else 0]

        keys.extend((number, slot) for slot in materials)

    parts = []
    # Parts made in Blender have the values the first part has
    default_optional = mesh.parts[0].get("optional", list(OPTIONAL_KEYS[:1])) if len(mesh.parts) > 0 else list(OPTIONAL_KEYS[:1])
    for number, slot in keys:
        kept = mesh.parts[number] if number < len(mesh.parts) else {}
        is_original = number < len(mesh.parts) and slot == number
        optional = kept.get("optional", default_optional)
        parts.append(_write_part(file, mesh, groups.get((number, slot), []), loose.get(number, []) if is_original or number >= len(mesh.parts) else [],
                                 kept if is_original else {"optional": optional}, slot, skinned))

    return {"parts": parts}


def _same_corner(mesh: CornerMesh, first: int, second: int) -> bool:
    """Whether two corners of a vertex can share it. Blender keeps normals per corner and gives the corners of a vertex normals a
    rounding error apart, UVs and colors are what they were set to."""
    if first == second:
        return True

    if list(mesh.corner_uvs[first * 2:first * 2 + 2]) != list(mesh.corner_uvs[second * 2:second * 2 + 2]):
        return False

    if mesh.corner_colors is not None and [_to_byte(value) for value in mesh.corner_colors[first * 4:first * 4 + 4]] != \
            [_to_byte(value) for value in mesh.corner_colors[second * 4:second * 4 + 4]]:
        return False

    if mesh.corner_normals is not None and _has_normal(mesh, first) and _has_normal(mesh, second):
        a = mesh.corner_normals[first * 3:first * 3 + 3]
        b = mesh.corner_normals[second * 3:second * 3 + 3]
        if sum(x * y for x, y in zip(a, b)) < 0.9999:
            return False

    return True


def _has_normal(mesh: CornerMesh, corner: int) -> bool:
    """Whether Blender gives the corner a normal. Corners of triangles without an area get whatever it works out, which isn't
    even of unit length."""
    length = sum(value * value for value in mesh.corner_normals[corner * 3:corner * 3 + 3]) ** 0.5
    return abs(length - 1.0) < 1e-3


def _write_part(file: TlmFile, mesh: CornerMesh, triangles: typing.List[int], loose: typing.List[int], kept: typing.Dict[str, typing.Any], slot: int,
                skinned: bool) -> typing.Dict[str, typing.Any]:
    first_corner: typing.Dict[int, int] = {}
    for triangle in triangles:
        for corner in range(triangle * 3, triangle * 3 + 3):
            first_corner.setdefault(int(mesh.corner_vertices[corner]), corner)

    vertexes = sorted(set(first_corner) | set(loose))
    # Where every exported vertex comes from: the mesh's vertex and the corner giving the values Blender keeps per corner
    sources: typing.List[typing.Tuple[int, typing.Optional[int]]] = [(vertex, first_corner.get(vertex)) for vertex in vertexes]
    local = {vertex: index for index, vertex in enumerate(vertexes)}
    copies: typing.Dict[int, typing.List[typing.Tuple[int, int]]] = {}
    faces = array.array("I")
    for triangle in triangles:
        for corner in range(triangle * 3, triangle * 3 + 3):
            vertex = int(mesh.corner_vertices[corner])
            if _same_corner(mesh, first_corner[vertex], corner):
                faces.append(local[vertex])
                continue

            vertex_copies = copies.setdefault(vertex, [])
            index = next((copy for copy_corner, copy in vertex_copies if _same_corner(mesh, copy_corner, corner)), None)
            if index is None:
                index = len(sources)
                sources.append((vertex, corner))
                vertex_copies.append((corner, index))

            faces.append(index)

    # The game's faces on a vertex twice go back where they were while all of the part's triangles are still there
    degenerate = kept.get("degenerate", [])
    if len(degenerate) > 0 and len(triangles) == kept.get("triangles") and all(index < len(sources) for face in degenerate for index in face[1:]):
        triangles_out = [faces[i:i + 3] for i in range(0, len(faces), 3)]
        for position, a, b, c in degenerate:
            triangles_out.insert(position, array.array("I", (a, b, c)))

        faces = array.array("I", (index for triangle in triangles_out for index in triangle))

    part: typing.Dict[str, typing.Any] = {"material": mesh.slot_materials[slot] if 0 <= slot < len(mesh.slot_materials) else -1, "vertices": len(sources)}
    part["faces"] = file.write_view(faces, "u32")
    part["position"] = file.write_view([value for vertex, _ in sources for value in mesh.positions[vertex * 3:vertex * 3 + 3]], "f32")
    optional = kept.get("optional", list(OPTIONAL_KEYS))
    if (skinned or "normal" in optional) and mesh.corner_normals is not None:
        vertex_corners: typing.Dict[int, typing.List[int]] = {}
        for triangle in triangles:
            for corner in range(triangle * 3, triangle * 3 + 3):
                vertex_corners.setdefault(int(mesh.corner_vertices[corner]), []).append(corner)

        normals = []
        for vertex, corner in sources:
            candidates = ([corner] if corner is not None else []) + vertex_corners.get(vertex, [])
            normal_corner = next((candidate for candidate in candidates if _has_normal(mesh, candidate) and _same_corner(mesh, candidate, corner)), None) \
                if corner is not None else None
            normals.extend(_corner_or_default(mesh.corner_normals, normal_corner, 3, _NO_NORMAL) if normal_corner is not None else _loose_normal(mesh, vertex))

        part["normal"] = file.write_view(normals, "f32")
        if mesh.twin_normals is not None:
            part["twin_normal"] = file.write_view([value for vertex, _ in sources for value in mesh.twin_normals[vertex * 3:vertex * 3 + 3]], "f32")

    # Blender's V goes up, the game's down. Vertexes no face uses have no corner to keep a UV, they keep the game's
    uvs = []
    for vertex, corner in sources:
        if corner is None:
            uvs.extend(mesh.twin_uvs[vertex * 2:vertex * 2 + 2] if mesh.twin_uvs is not None else (0.0, 0.0))
            continue

        u, v = _corner_or_default(mesh.corner_uvs, corner, 2, (0.0, 0.0))
        uvs.extend((u, 1.0 - v))

    part["uv"] = file.write_view(uvs, "f32")
    if mesh.twin_uvs is not None:
        part["twin_uv"] = file.write_view([value for vertex, _ in sources for value in mesh.twin_uvs[vertex * 2:vertex * 2 + 2]], "f32")

    if mesh.uv_q is not None and not skinned and "uv_q" in optional:
        part["uv_q"] = file.write_view([mesh.uv_q[vertex] for vertex, _ in sources], "f32")

    colors = []
    for vertex, corner in sources:
        if mesh.corner_colors is not None and corner is not None:
            colors.extend(_to_byte(value) for value in mesh.corner_colors[corner * 4:corner * 4 + 4])
        elif mesh.vertex_colors is not None:
            colors.extend(_to_byte(value) for value in mesh.vertex_colors[vertex * 4:vertex * 4 + 4])
        else:
            colors.extend((0x7F, 0x7F, 0x7F, 0xFF if not skinned else 0x77))

    part["color"] = file.write_view(colors, "u8")
    if mesh.emit_colors is not None and not skinned and "emit_color" in optional:
        part["emit_color"] = file.write_view([_to_byte(value) for vertex, _ in sources for value in mesh.emit_colors[vertex * 4:vertex * 4 + 4]], "u8")

    if mesh.alpha_flags is not None and not skinned and "alpha_flags" in optional:
        part["alpha_flags"] = file.write_view([int(value) for vertex, _ in sources for value in mesh.alpha_flags[vertex * 2:vertex * 2 + 2]], "u8")

    if skinned:
        if mesh.twin_joints is not None:
            part["joints"] = file.write_view([max(0, min(255, int(round(value)))) for vertex, _ in sources for value in mesh.twin_joints[vertex * 3:vertex * 3 + 3]], "u8")

        if mesh.twin_weights is not None:
            part["weights"] = file.write_view([value for vertex, _ in sources for value in mesh.twin_weights[vertex * 3:vertex * 3 + 3]], "f32")

        if mesh.group_influences is not None:
            joints = []
            weights = []
            for vertex, _ in sources:
                influences = sorted(mesh.group_influences[vertex], key=lambda influence: -influence[1])[:4]
                influences += [(-1, 0.0)] * (4 - len(influences))
                joints.extend(joint for joint, _ in influences)
                weights.extend(weight for _, weight in influences)

            part["group_joints"] = file.write_view(joints, "i32")
            part["group_weights"] = file.write_view(weights, "f32")

        if len(mesh.shapes) > 0:
            part["shapes"] = [file.write_view([value for vertex, _ in sources for value in shape[vertex * 3:vertex * 3 + 3]], "f32") for shape in mesh.shapes]
            twin_shapes = [shape if shape is not None else mesh.shapes[index] for index, shape in enumerate(mesh.twin_shapes[:len(mesh.shapes)])]
            if len(twin_shapes) == len(mesh.shapes):
                part["twin_shapes"] = [file.write_view([value for vertex, _ in sources for value in shape[vertex * 3:vertex * 3 + 3]], "f32") for shape in twin_shapes]

    for key in PART_KEYS:
        if key in kept:
            part[key] = _outline(file, kept[key])

    return part


# TT Lab shows the game's normals of zero length pointing up
_NO_NORMAL = (0.0, 1.0, 0.0)


def _loose_normal(mesh: CornerMesh, vertex: int) -> typing.Tuple[float, ...]:
    """The normal of a vertex no face gives one: the game's."""
    if mesh.twin_normals is None:
        return _NO_NORMAL

    normal = mesh.twin_normals[vertex * 3:vertex * 3 + 3]
    length = sum(value * value for value in normal) ** 0.5
    return tuple(value / length for value in normal) if length > 1e-6 else _NO_NORMAL


def _corner_or_default(values: typing.Sequence[float], corner: typing.Optional[int], size: int, default: typing.Tuple[float, ...]) -> typing.Tuple[float, ...]:
    if corner is None:
        return default

    return tuple(values[corner * size:corner * size + size])
