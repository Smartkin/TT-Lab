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

The meshes placed in a scenery are objects under its root's Meshes and its LODs empties under LODs with their levels' meshes, lights are
empties, the collision is a mesh with a material for every surface and the dynamic models move by an action each. Everything under the
root is written back as the hierarchy it's in, TT Lab finds the placed meshes anywhere under the root and makes the tree the game culls
them with when it builds the scenery.
"""

import array
import base64
import json
import typing

import bmesh
import bpy
from mathutils import Matrix

from . import axes
from . import collision_builder
from . import tlm
from . import tlm_blender
from . import tlm_math
from . import tlm_mesh

# Kinds of the nodes that have a mesh of the model's parts
MESH_KINDS = ("scenery_mesh", "lod_mesh", "skydome_mesh", "dynamic_model")
COLLISION_TRIANGLE_ATTRIBUTE = "tt_collision_triangle"
COLLISION_VERTEX_ATTRIBUTE = "tt_collision_vertex"
SURFACE_PROPERTY = "ttt_surface"
DYNAMIC_PROPERTY = "ttt_dynamic"
# Their arrow, the empty's Z axis, is the node's Z axis: the light's direction
LIGHT_KINDS = ("ambient_light", "directional_light", "point_light", "spot_light")

_EMPTY_DISPLAY = {"scenery_meshes": ("PLAIN_AXES", 0.5), "scenery_lods": ("PLAIN_AXES", 0.5), "scenery_lod": ("PLAIN_AXES", 1.0),
                  "lights": ("PLAIN_AXES", 0.5), "ambient_light": ("SPHERE", 0.5), "directional_light": ("SINGLE_ARROW", 1.0),
                  "point_light": ("SPHERE", 0.5), "spot_light": ("SPHERE", 0.5), "dynamic_scenery": ("PLAIN_AXES", 0.5)}


def import_children(context: bpy.types.Context, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object,
                    collection: bpy.types.Collection, materials: typing.List[bpy.types.Material]) -> None:
    for order, child in enumerate(tlm.children(tree_node)):
        blender_object = _import_node(context, file, child, parent, collection, materials)
        blender_object[tlm_blender.ORDER_PROPERTY] = order
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

    if kind == "dynamic_model":
        _import_movement(context, file, tree_node, blender_object)
    else:
        tlm_blender._set_node_transform(blender_object, tree_node, tlm_blender.LIGHT_FRAME if kind in LIGHT_KINDS else tlm_blender.MODEL_FRAME)

    return blender_object


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
        part_positions = axes.points(file.read_view(surface.get("position"), "f32"))
        faces = axes.triangles(file.read_view(surface.get("faces"), "u32"))
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


def is_surface(material: typing.Optional[bpy.types.Material]) -> bool:
    if material is None:
        return False

    if material.get(SURFACE_PROPERTY) is not None:
        return True

    container = tlm_blender.properties.get(material)
    return container is not None and container.type == "CollisionSurface"


def placeholder_surface(name: str) -> bpy.types.Material:
    """A collision surface material TT Lab takes the project's surface of the material's name for (its first one otherwise)"""
    material = bpy.data.materials.get(name)
    if is_surface(material):
        return material

    material = bpy.data.materials.new(name)
    material[SURFACE_PROPERTY] = ""
    tlm_blender._read_data(material, "CollisionSurface", {"Surface": ""})
    material.diffuse_color = (0.5, 0.5, 0.5, 1.0)
    return material


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
    """A dynamic model's movement as an action of its object: its location and rotation on every frame, the game's angles in Blender's
    axes (axes.euler)."""
    movement = tree_node.get("animation")
    blender_object.rotation_mode = axes.BLENDER_EULER_ORDER
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

    translations = axes.points(file.read_view(movement.get("translation"), "f32"))
    rotations = file.read_view(movement.get("rotation"), "f32")
    rotations = [value for start in range(0, len(rotations) - 2, 3) for value in axes.euler(rotations[start:start + 3])]
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
    for child in tlm_blender.in_file_order(tlm_blender.live_children(blender_object)):
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
        node["mesh"] = tlm_mesh.to_parts(file, tlm_blender._mesh_to_game(tlm_blender._read_mesh(blender_object, materials, False)), False)

    if kind == "dynamic_model":
        node["animation"] = _export_movement(file, blender_object)
    else:
        _local_transform(blender_object, node)

    return node


def _local_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    """The object's transform relative to its parent, the way Blender's parenting has it, in the game's space."""
    frame = tlm_blender.LIGHT_FRAME if tlm_blender.role_of(blender_object) in LIGHT_KINDS else tlm_blender.MODEL_FRAME
    local = tlm_blender.local_transform(blender_object)
    translation, rotation, scale = tlm_blender.game_transform(local, frame)
    stored = blender_object.get(tlm_blender.LIGHT_ROTATION_PROPERTY)
    # A light still turned the way it was imported has the game's rotation, which Blender's is of give or take its rounding errors
    if frame == tlm_blender.LIGHT_FRAME and stored is not None and len(stored) == 4:
        shown = axes.frame_rotation([float(value) for value in stored])
        current = tlm_math.wxyz_to_xyzw(tuple(local[1]))
        # The game's quaternions aren't always of unit length to the last bit, nor are Blender's
        lengths = sum(value * value for value in shown) ** 0.5 * sum(value * value for value in current) ** 0.5
        if lengths > 1e-12 and abs(sum(a * b for a, b in zip(shown, current))) / lengths > 1.0 - 1e-9:
            rotation = [float(value) for value in stored]
    if sum(value * value for value in translation) ** 0.5 > 1e-7:
        tree_node["translation"] = translation

    if abs(abs(rotation[3]) - 1.0) > 1e-7:
        tree_node["rotation"] = rotation

    if any(abs(value - 1.0) > 1e-7 for value in scale):
        tree_node["scale"] = scale


def _export_collision(file: tlm.TlmFile, blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    """Every material's faces as a surface, with where the game had the triangles and vertexes while the mesh still has them."""
    mesh: bpy.types.Mesh = blender_object.data
    mesh.calc_loop_triangles()
    vertex_count = len(mesh.vertices)
    positions = array.array("f", [0.0] * vertex_count * 3)
    mesh.vertices.foreach_get("co", positions)
    positions = axes.points_back(positions)
    triangle_count = len(mesh.loop_triangles)
    triangle_vertices = array.array("i", [0] * triangle_count * 3)
    mesh.loop_triangles.foreach_get("vertices", triangle_vertices)
    triangle_vertices = axes.triangles(triangle_vertices)
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
    start = 0
    if action is not None and action.use_frame_range:
        start, end = (int(round(value)) for value in action.frame_range)
        frames = max(1, end - start + 1)
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
    for path, key, rest, convert in (("location", "translation", tuple(blender_object.location), axes.point_back),
                                     ("rotation_euler", "rotation", tuple(blender_object.rotation_euler), axes.euler_back)):
        tracks = [tlm_blender._sample(curves.get((path, component)), frames, rest[component], start) for component in range(3)]
        movement[key] = file.write_view([value for frame in range(frames) for value in convert([track[frame] for track in tracks])], "f32")

    return movement


# Nothing under these is drawn where the scenery has it: the collision itself, the hulls and the dynamic scenery, which moves
_NOT_COLLISION_SOURCES = ("collision", "hull", "collision_hulls", "dynamic_scenery", "dynamic_model", "lights")


def collision_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((child for child in tlm_blender._descendants(root) if child.type == "MESH" and tlm_blender.role_of(child) == "collision"), None)


def collision_sources(start: bpy.types.Object) -> typing.List[bpy.types.Object]:
    """The meshes the scenery draws from the object down: its placed meshes and the closest level of its LODs"""
    sources = []
    pending = [start]
    while pending:
        blender_object = pending.pop()
        kind = tlm_blender.role_of(blender_object) or ""
        if kind in _NOT_COLLISION_SOURCES:
            continue

        if blender_object.type == "MESH" and (kind in ("", "mesh", "scenery_mesh") or kind == "lod_mesh" and int(tlm_blender._write_data(blender_object).get("Level", 0)) == 0):
            sources.append(blender_object)

        pending.extend(reversed(tlm_blender.in_file_order(tlm_blender.live_children(blender_object))))

    return sources


def generate_collision(root: bpy.types.Object, start: bpy.types.Object, surface: bpy.types.Material, replace: bool = True, weld: float = 1e-3,
                       flip: bool = True, tolerance: float = collision_builder.TOLERANCE, hull_distance: float = collision_builder.HULL_DISTANCE,
                       coarser_where_crowded: bool = True) -> typing.Tuple[collision_builder.Result, collision_builder.Crowding]:
    """Makes the scenery's collision, or adds to it, out of the meshes it draws from the start object down, every triangle on the
    surface, as coarse as the game's (collision_builder.add_meshes: convex hulls where they stay within the hull distance of a mesh,
    the rest made coarser within the tolerance, more where the player would touch too many triangles; 0 and 0 keep every triangle).
    Corners closer than the weld distance become one vertex, flat triangles and ones the collision has are left out. A collision made
    anew has no order of the game's, TT Lab makes the tree the game finds collisions with for it. Comes with where the player would
    still touch more triangles than the game takes on the new collision."""
    collision = collision_of(root)
    if collision is None:
        collection = root.users_collection[0] if root.users_collection else bpy.context.scene.collection
        collision = tlm_blender._new_object("Collision", bpy.data.meshes.new("Collision"), root, collection)
        collision[tlm_blender.KIND_PROPERTY] = "collision"
        tlm_blender._read_data(collision, "Collision", {})
        # A new object's world matrix is only worked out by an update
        bpy.context.view_layer.update()

    # The collision is made in the game's space (its floors are Y up), the collision mesh's points of Blender's axes (axes)
    to_collision = collision.matrix_world.inverted()
    sources = []
    for source in collision_sources(start):
        mesh = source.data
        mesh.calc_loop_triangles()
        matrix = to_collision @ source.matrix_world
        positions = [axes.point_back(matrix @ vertex.co) for vertex in mesh.vertices]
        # A mirrored object's triangles face the other way where it is, and so does the game's reflection of every one
        turned = not matrix.determinant() < 0
        triangles = []
        for triangle in mesh.loop_triangles:
            a, b, c = (positions[index] for index in triangle.vertices)
            triangles.append((a, c, b) if turned else (a, b, c))

        sources.append(triangles)

    mesh = collision.data
    existing_positions: typing.List[collision_builder.Vector3] = []
    existing_triangles: typing.List[collision_builder.Triangle] = []
    if not replace:
        mesh.calc_loop_triangles()
        existing_positions = [axes.point_back(vertex.co) for vertex in mesh.vertices]
        existing_triangles = [(triangle.vertices[0], triangle.vertices[2], triangle.vertices[1]) for triangle in mesh.loop_triangles]

    result = collision_builder.add_meshes(existing_positions, existing_triangles, sources, weld, flip, tolerance, hull_distance, coarser_where_crowded)
    positions = list(existing_positions) + list(result.positions)
    every_triangle = list(existing_triangles) + list(result.triangles)
    crowding = collision_builder.crowding(positions, every_triangle, range(len(existing_triangles), len(every_triangle)), flip)
    if replace:
        # The game's order of triangles and vertexes is gone with them
        for name in (COLLISION_TRIANGLE_ATTRIBUTE, COLLISION_VERTEX_ATTRIBUTE):
            if name in mesh.attributes:
                mesh.attributes.remove(mesh.attributes[name])

    if mesh.materials.find(surface.name) < 0:
        mesh.materials.append(surface)

    slot = mesh.materials.find(surface.name)
    target = bmesh.new()
    if not replace:
        target.from_mesh(mesh)

    triangle_order = target.faces.layers.int.get(COLLISION_TRIANGLE_ATTRIBUTE)
    vertex_order = target.verts.layers.int.get(COLLISION_VERTEX_ATTRIBUTE)
    target.verts.ensure_lookup_table()
    vertexes = list(target.verts)
    for position in result.positions:
        vertex = target.verts.new(axes.point(position))
        if vertex_order is not None:
            vertex[vertex_order] = -1

        vertexes.append(vertex)

    for corners in result.triangles:
        face = target.faces.new([vertexes[corner] for corner in axes.face(list(corners))])
        face.material_index = slot
        if triangle_order is not None:
            face[triangle_order] = -1

    target.to_mesh(mesh)
    target.free()
    mesh.update()
    return result, crowding
