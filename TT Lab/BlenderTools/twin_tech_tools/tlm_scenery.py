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

"""Sceneries, collisions, dynamic sceneries and skydomes of TT Lab model files in Blender.

Their tree becomes a hierarchy of objects under the root: the tree nodes the game culls the scenery with are empties with the meshes
and LODs they cull under them, lights are empties, the collision is a mesh with a material for every surface and the dynamic
models move by an action each. Everything under the root is written back as the hierarchy it's in, TT Lab works out what's in which
tree node.
"""

import array
import base64
import json
import typing

import bpy
from mathutils import Matrix

from . import tlm
from . import tlm_blender
from . import tlm_mesh

# Kinds of the nodes that have a mesh of the model's parts
MESH_KINDS = ("scenery_mesh", "lod_mesh", "skydome_mesh", "dynamic_model")
COLLISION_TRIANGLE_ATTRIBUTE = "tt_collision_triangle"
COLLISION_VERTEX_ATTRIBUTE = "tt_collision_vertex"
SURFACE_PROPERTY = "ttt_surface"
DYNAMIC_PROPERTY = "ttt_dynamic"

_EMPTY_DISPLAY = {"tree_node": ("CUBE", 1.0), "scenery_lod": ("PLAIN_AXES", 1.0), "lights": ("PLAIN_AXES", 0.5), "ambient_light": ("SPHERE", 0.5),
                  "directional_light": ("SINGLE_ARROW", 1.0), "point_light": ("SPHERE", 0.5), "negative_light": ("SPHERE", 0.5),
                  "dynamic_scenery": ("PLAIN_AXES", 0.5)}


def import_children(context: bpy.types.Context, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object,
                    collection: bpy.types.Collection, materials: typing.List[bpy.types.Material]) -> None:
    for child in tlm.children(tree_node):
        blender_object = _import_node(context, file, child, parent, collection, materials)
        import_children(context, file, child, blender_object, collection, materials)


def _import_node(context: bpy.types.Context, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object,
                 collection: bpy.types.Collection, materials: typing.List[bpy.types.Material]) -> bpy.types.Object:
    kind = tree_node.get("kind", "")
    name = tree_node.get("name") or kind
    if kind == "collision":
        blender_object = _import_collision(file, tree_node, parent, collection)
    elif kind == "hull":
        blender_object = tlm_blender.add_hull_object(file, tree_node, parent, collection)
    elif "mesh" in tree_node or kind in MESH_KINDS:
        blender_object = tlm_blender._add_mesh_object(name, file, tree_node, parent, collection, materials, False)
    else:
        blender_object = tlm_blender._new_object(name, None, parent, collection)
        blender_object[tlm_blender.KIND_PROPERTY] = kind
        display, size = _EMPTY_DISPLAY.get(kind, ("PLAIN_AXES", 0.5))
        blender_object.empty_display_type = display
        blender_object.empty_display_size = size
        type_name = tlm_blender.KIND_TYPES.get(kind)
        if type_name is not None:
            tlm_blender._read_data(blender_object, type_name, tree_node.get("data"))

    if kind == "tree_node":
        _fit_to_bounds(blender_object, tree_node.get("data", {}))

    if kind == "dynamic_model":
        _import_movement(context, file, tree_node, blender_object)
    else:
        tlm_blender._set_node_transform(blender_object, tree_node)

    return blender_object


def _fit_to_bounds(blender_object: bpy.types.Object, data: typing.Dict[str, typing.Any]) -> None:
    """Shows a tree node as its box. The box is only shown, TT Lab works it out from what's under the node."""
    low, high = data.get("BoundsMin"), data.get("BoundsMax")
    if not isinstance(low, list) or not isinstance(high, list) or len(low) < 3 or len(high) < 3:
        return

    blender_object.empty_display_size = max(1e-3, max(abs(h - l) for l, h in zip(low[:3], high[:3])) / 2)


def _import_collision(file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object,
                      collection: bpy.types.Collection) -> bpy.types.Object:
    """The collision as one mesh with a material for every surface. Where the game had every triangle and vertex is kept in attributes,
    the tree the game finds collisions with comes out the same while nothing was added or removed."""
    positions = array.array("f")
    corners = array.array("i")
    surfaces = array.array("i")
    triangle_order = array.array("i")
    vertex_order = array.array("i")
    mesh = bpy.data.meshes.new(tree_node.get("name") or "Collision")
    for index, surface in enumerate(tree_node.get("surfaces", [])):
        start = len(positions) // 3
        part_positions = file.read_view(surface.get("position"), "f32")
        faces = file.read_view(surface.get("faces"), "u32")
        triangles = file.read_view(surface.get("triangles"), "i32")
        vertexes = file.read_view(surface.get("vertexes"), "i32")
        positions.extend(part_positions)
        vertex_order.extend(vertexes if len(vertexes) == len(part_positions) // 3 else [-1] * (len(part_positions) // 3))
        for triangle in range(len(faces) // 3):
            corners.extend(faces[triangle * 3 + corner] + start for corner in range(3))
            surfaces.append(index)
            triangle_order.append(triangles[triangle] if triangle < len(triangles) else -1)

        mesh.materials.append(_surface_material(surface))

    mesh.vertices.add(len(positions) // 3)
    mesh.vertices.foreach_set("co", positions)
    mesh.loops.add(len(corners))
    mesh.loops.foreach_set("vertex_index", corners)
    mesh.polygons.add(len(corners) // 3)
    mesh.polygons.foreach_set("loop_start", array.array("i", range(0, len(corners), 3)))
    mesh.update(calc_edges=True)
    mesh.polygons.foreach_set("material_index", surfaces)
    mesh.attributes.new(COLLISION_TRIANGLE_ATTRIBUTE, "INT", "FACE").data.foreach_set("value", triangle_order)
    mesh.attributes.new(COLLISION_VERTEX_ATTRIBUTE, "INT", "POINT").data.foreach_set("value", vertex_order)
    blender_object = tlm_blender._new_object(tree_node.get("name") or "Collision", mesh, parent, collection)
    blender_object[tlm_blender.KIND_PROPERTY] = "collision"
    tlm_blender._read_data(blender_object, "Collision", tree_node.get("data"))
    return blender_object


def _surface_material(surface: typing.Dict[str, typing.Any]) -> bpy.types.Material:
    uri = str(surface.get("surface", ""))
    for material in bpy.data.materials:
        if uri and material.get(SURFACE_PROPERTY) == uri:
            return material

    material = bpy.data.materials.new(str(surface.get("name", "Surface")))
    material[SURFACE_PROPERTY] = uri
    tlm_blender._read_data(material, "CollisionSurface", {"Surface": uri})
    color = surface.get("color", [0.5, 0.5, 0.5, 1.0])
    material.diffuse_color = tuple(color)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is not None:
        shader.inputs["Base Color"].default_value = tuple(color)
        shader.inputs["Alpha"].default_value = color[3]

    return material


def _import_movement(context: bpy.types.Context, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], blender_object: bpy.types.Object) -> None:
    """A dynamic model's movement as an action of its object: its location and rotation on every frame, as the game has them."""
    movement = tree_node.get("animation")
    blender_object.rotation_mode = "XYZ"
    if not isinstance(movement, dict):
        return

    frames = int(movement.get("frames", 0))
    meta = {"frames": frames}
    exact = file.read_view(movement.get("exact"), "u8")
    if len(exact) > 0:
        meta["exact"] = base64.b64encode(exact.tobytes()).decode("ascii")

    blender_object[DYNAMIC_PROPERTY] = json.dumps(meta)
    if frames == 0:
        return

    translations = file.read_view(movement.get("translation"), "f32")
    rotations = file.read_view(movement.get("rotation"), "f32")
    action = bpy.data.actions.new("%s Movement" % blender_object.name)
    action.use_fake_user = True
    slot = action.slots.new(id_type="OBJECT", name=blender_object.name)
    bag = tlm_blender._channelbag(action, slot)
    for path, values in (("location", translations), ("rotation_euler", rotations)):
        for component in range(3):
            track = [values[frame * 3 + component] for frame in range(frames) if frame * 3 + component < len(values)]
            tlm_blender._add_track(bag, path, component, track)

    animation_data = blender_object.animation_data or blender_object.animation_data_create()
    animation_data.action = action
    animation_data.action_slot = slot
    blender_object.location = translations[0:3]
    blender_object.rotation_euler = rotations[0:3]


def export_children(file: tlm.TlmFile, blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any],
                    materials: "tlm_blender._FileMaterials") -> None:
    for child in sorted(blender_object.children, key=lambda child: child.name):
        node = _export_node(file, child, materials)
        if node is not None:
            tlm.add_child(tree_node, node)
            export_children(file, child, node, materials)


def _export_node(file: tlm.TlmFile, blender_object: bpy.types.Object, materials: "tlm_blender._FileMaterials") -> typing.Optional[typing.Dict[str, typing.Any]]:
    kind = tlm_blender.role_of(blender_object) or ""
    data = tlm_blender._write_data(blender_object)
    node = tlm.node(kind, blender_object.name, data if data else None)
    if kind == "collision" and blender_object.type == "MESH":
        _export_collision(file, blender_object, node)
    elif kind == "hull" and blender_object.type == "MESH":
        tlm_blender.export_hull(file, blender_object, node)
    elif blender_object.type == "MESH":
        node["mesh"] = tlm_mesh.to_parts(file, tlm_blender._read_mesh(blender_object, materials, False), False)

    if kind == "dynamic_model":
        node["animation"] = _export_movement(file, blender_object)
    else:
        _local_transform(blender_object, node)

    return node


def _local_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    """The object's transform relative to its parent, the way Blender's parenting has it."""
    local = blender_object.matrix_parent_inverse @ blender_object.matrix_basis
    translation, rotation, scale = local.decompose()
    if translation.length > 1e-7:
        tree_node["translation"] = list(translation)

    if abs(abs(rotation.w) - 1.0) > 1e-7:
        tree_node["rotation"] = [rotation.x, rotation.y, rotation.z, rotation.w]

    if any(abs(value - 1.0) > 1e-7 for value in scale):
        tree_node["scale"] = list(scale)


def _export_collision(file: tlm.TlmFile, blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    """Every material's faces as a surface, with where the game had the triangles and vertexes while the mesh still has them."""
    mesh: bpy.types.Mesh = blender_object.data
    mesh.calc_loop_triangles()
    vertex_count = len(mesh.vertices)
    positions = array.array("f", [0.0] * vertex_count * 3)
    mesh.vertices.foreach_get("co", positions)
    triangle_count = len(mesh.loop_triangles)
    triangle_vertices = array.array("i", [0] * triangle_count * 3)
    mesh.loop_triangles.foreach_get("vertices", triangle_vertices)
    triangle_polygons = array.array("i", [0] * triangle_count)
    mesh.loop_triangles.foreach_get("polygon_index", triangle_polygons)
    polygon_materials = array.array("i", [0] * len(mesh.polygons))
    mesh.polygons.foreach_get("material_index", polygon_materials)
    triangle_order = tlm_blender._attribute(mesh, COLLISION_TRIANGLE_ATTRIBUTE, "value", len(mesh.polygons), 1, "i")
    vertex_order = tlm_blender._attribute(mesh, COLLISION_VERTEX_ATTRIBUTE, "value", vertex_count, 1, "i")
    by_surface: typing.Dict[int, typing.List[int]] = {}
    for triangle in range(triangle_count):
        by_surface.setdefault(polygon_materials[triangle_polygons[triangle]], []).append(triangle)

    # Polygons split into more triangles than they were aren't the game's anymore
    triangles_per_polygon: typing.Dict[int, int] = {}
    for polygon in triangle_polygons:
        triangles_per_polygon[polygon] = triangles_per_polygon.get(polygon, 0) + 1

    surfaces = []
    for slot, triangles in sorted(by_surface.items()):
        material = blender_object.material_slots[slot].material if slot < len(blender_object.material_slots) else None
        local: typing.Dict[int, int] = {}
        faces = array.array("I")
        for triangle in triangles:
            for vertex in triangle_vertices[triangle * 3:triangle * 3 + 3]:
                faces.append(local.setdefault(vertex, len(local)))

        vertexes = list(local)
        surface: typing.Dict[str, typing.Any] = {
            "surface": _surface_of(material),
            "name": material.name if material is not None else "Surface",
            "vertices": len(vertexes),
            "position": file.write_view([value for vertex in vertexes for value in positions[vertex * 3:vertex * 3 + 3]], "f32"),
            "faces": file.write_view(faces, "u32"),
        }
        if material is not None:
            surface["color"] = list(material.diffuse_color)

        if triangle_order is not None and all(triangles_per_polygon[triangle_polygons[triangle]] == 1 and triangle_order[triangle_polygons[triangle]] >= 0 for triangle in triangles):
            surface["triangles"] = file.write_view([triangle_order[triangle_polygons[triangle]] for triangle in triangles], "i32")

        if vertex_order is not None and all(vertex_order[vertex] >= 0 for vertex in vertexes):
            surface["vertexes"] = file.write_view([vertex_order[vertex] for vertex in vertexes], "i32")

        surfaces.append(surface)

    tree_node["surfaces"] = surfaces


def _surface_of(material: typing.Optional[bpy.types.Material]) -> str:
    if material is None:
        return ""

    data = tlm_blender._write_data(material)
    return str(data.get("Surface") or material.get(SURFACE_PROPERTY, ""))


def _export_movement(file: tlm.TlmFile, blender_object: bpy.types.Object) -> typing.Dict[str, typing.Any]:
    try:
        meta = json.loads(blender_object.get(DYNAMIC_PROPERTY, "{}"))
    except ValueError:
        meta = {}

    animation_data = blender_object.animation_data
    action = animation_data.action if animation_data is not None else None
    frames = int(meta.get("frames", 0))
    if action is not None and action.use_frame_range:
        frames = max(1, int(round(action.frame_range[1])) - int(round(action.frame_range[0])) + 1)
    elif action is not None and frames == 0:
        frames = max(1, int(round(action.frame_range[1])) + 1)

    movement: typing.Dict[str, typing.Any] = {"frames": frames}
    if "exact" in meta:
        movement["exact"] = file.write_view(base64.b64decode(meta["exact"]), "u8")

    if frames == 0:
        return movement

    slot = animation_data.action_slot if animation_data is not None and action is not None else None
    bag = tlm_blender._existing_channelbag(action, slot) if slot is not None else None
    curves = {(fcurve.data_path, fcurve.array_index): fcurve for fcurve in bag.fcurves} if bag is not None else {}
    for path, key, rest in (("location", "translation", tuple(blender_object.location)), ("rotation_euler", "rotation", tuple(blender_object.rotation_euler))):
        tracks = [tlm_blender._sample(curves.get((path, component)), frames, rest[component]) for component in range(3)]
        movement[key] = file.write_view([track[frame] for frame in range(frames) for track in tracks], "f32")

    return movement
