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

"""TT Lab model files in Blender.

An OGI becomes a root empty holding its data, with the armature, the skin, the shape (the blend skin), and empties grouping
the rigid bodies and the exit points under it. Bodies and exit points follow their bone through a Child Of constraint. Every
animation is an action with a slot for the armature and one for the shape's shape keys.

A save icon (the PS2 memory card icon) is a root empty with one mesh: the icon's shapes are its shape keys, its animation the
shape keys' curves, its texture the material's image.

The game is Y up, the root turns everything under it Z up. Everything under the root is written back relative to it.
"""

import array
import base64
import json
import math
import os
import tempfile
import typing
import uuid

import bpy
import numpy
from mathutils import Euler, Matrix, Quaternion, Vector

from . import flags
from . import project as projects
from . import retarget
from . import save_icon
from . import schema
from . import tlm
from . import tlm_math
from . import tlm_mesh
from . import twintech_properties as properties

ROOT_PROPERTY = "ttt_tlm_type"
PATH_PROPERTY = "ttt_tlm_path"
UID_PROPERTY = "ttt_uid"
URI_PROPERTY = "ttt_uri"
# ID of a material made in Blender, TT Lab makes it a material of the project once and uses that one for it from then on
BLENDER_ID_PROPERTY = "ttt_blender_id"
PARTS_PROPERTY = "ttt_parts"
KIND_PROPERTY = "ttt_kind"
ANIMATION_PROPERTY = "ttt_animation"
OWNER_PROPERTY = "ttt_owner"
# On a retargeted copy of an action, the action it was made from
RETARGETED_PROPERTY = "ttt_retargeted_from"
JOINT_PROPERTY = "ttt_joint"
# The bind pose a bone was imported with, Blender keeps bones by head, tail and roll, which loses precision
BIND_PROPERTY = "ttt_bind"
# The game's matrix of an exit point, TT Lab keeps it while the object is still where it puts it
MATRIX_PROPERTY = "ttt_matrix"
# What a collision hull came with: the game's planes, axes and edges and the vertexes and faces they were made for, TT Lab keeps
# them while the mesh still has those
HULL_PROPERTY = "ttt_hull"
HULL_KEYS = ("vertices", "faces", "planes", "edge_directions", "face_normals", "edges")
# What a save icon keeps that Blender doesn't show: the order of its animation's shapes, the first shape's keys (the basis has no curve)
# and the icon as the game has it
SAVE_ICON_PROPERTY = "ttt_save_icon"

PART_ATTRIBUTE = "tt_part"
VERTEX_PART_ATTRIBUTE = "tt_vertex_part"
TWIN_NORMAL_ATTRIBUTE = "twin_normal"
TWIN_UV_ATTRIBUTE = "twin_uv"
UV_Q_ATTRIBUTE = "tt_uv_q"
COLOR_ATTRIBUTE = "Color"
EMIT_COLOR_ATTRIBUTE = "EmitColor"
COLOR_ALPHA_ATTRIBUTE = "tt_color_alpha"
EMIT_ALPHA_ATTRIBUTE = "tt_emit_alpha"
TWIN_JOINTS_ATTRIBUTE = "twin_joints"
TWIN_WEIGHTS_ATTRIBUTE = "twin_weights"
# The game's offsets of a shape, followed by the shape's number
TWIN_SHAPE_ATTRIBUTE = "twin_shape_"

Y_UP = Matrix.Rotation(math.pi / 2, 4, "X")

# The node kinds and the Twin Tech types of the objects they become
KIND_TYPES = {"ogi": "Ogi", "skin": "Skin", "shape": "BlendSkin", "body": "Body", "exit_point": "ExitPoint", "hull": "CollisionHull", "model": "Model",
              "rigid_model": "RigidModel", "mesh": "Mesh", "scenery": "Scenery", "tree_node": "SceneryTreeNode", "scenery_mesh": "SceneryMesh",
              "scenery_lod": "SceneryLod", "lod_mesh": "LodMesh", "ambient_light": "AmbientLight", "directional_light": "DirectionalLight",
              "point_light": "PointLight", "spot_light": "SpotLight", "collision": "Collision", "dynamic_scenery": "DynamicScenery",
              "dynamic_model": "DynamicSceneryModel", "skydome": "Skydome", "skydome_mesh": "SkydomeMesh", "save_icon": "SaveIcon"}
# Roots holding a tree of objects
TREE_KINDS = ("scenery", "skydome", "dynamic_scenery")
SKINNED_KINDS = ("skin", "shape")


# Twin Tech data of objects and bones

def _read_data(element: typing.Any, type_name: str, data: typing.Optional[typing.Dict[str, typing.Any]]) -> None:
    properties.read(element, {schema.KEY: dict(data or {}, **{schema.TYPE_KEY: type_name})})


def _write_data(element: typing.Any) -> typing.Dict[str, typing.Any]:
    twin = properties.write(element) or {}
    return {key: value for key, value in twin.items() if key != schema.TYPE_KEY}


def _new_object(name: str, data: typing.Any, parent: typing.Optional[bpy.types.Object], collection: bpy.types.Collection) -> bpy.types.Object:
    blender_object = bpy.data.objects.new(name, data)
    collection.objects.link(blender_object)
    blender_object.parent = parent
    return blender_object


def _set_node_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    translation = tree_node.get("translation", [0.0, 0.0, 0.0])
    rotation = tree_node.get("rotation", [0.0, 0.0, 0.0, 1.0])
    scale = tree_node.get("scale", [1.0, 1.0, 1.0])
    blender_object.rotation_mode = "QUATERNION"
    blender_object.location = translation
    blender_object.rotation_quaternion = tlm_math.xyzw_to_wxyz(rotation)
    blender_object.scale = scale


# Materials

def _material_for(project: typing.Optional[projects.Project], entry: typing.Dict[str, typing.Any], file: typing.Optional[tlm.TlmFile] = None) -> bpy.types.Material:
    """The Blender material of one of the file's materials, shared by everything showing the same project material."""
    uri = str(entry.get("uri", ""))
    if not uri and file is not None and isinstance(entry.get("image"), dict):
        return _embedded_material(file, entry)

    for material in bpy.data.materials:
        if uri and material.get(URI_PROPERTY) == uri:
            return material

    project_material = project.materials.get(uri) if project is not None else None
    material = bpy.data.materials.new(project_material.name if project_material is not None else str(entry.get("name", "Material")))
    material[URI_PROPERTY] = uri
    show_project_material(material, project)
    return material


def _embedded_material(file: tlm.TlmFile, entry: typing.Dict[str, typing.Any]) -> bpy.types.Material:
    """A material the file has with its image, which goes back into the file the same way."""
    blender_id = str(entry.get("blender_id", ""))
    for material in bpy.data.materials:
        if blender_id and material.get(BLENDER_ID_PROPERTY) == blender_id:
            return material

    material = bpy.data.materials.new(str(entry.get("name", "Material")))
    material[BLENDER_ID_PROPERTY] = blender_id or uuid.uuid4().hex
    image_entry = entry["image"]
    png = file.read_view(image_entry.get("png"), "u8").tobytes()
    image = None
    if len(png) > 0:
        directory = tempfile.mkdtemp()
        path = os.path.join(directory, "image.png")
        try:
            with open(path, "wb") as image_file:
                image_file.write(png)

            image = bpy.data.images.load(path)
            image.pack()
            image.name = str(image_entry.get("name", material.name))
            image.filepath_raw = ""
        finally:
            if os.path.exists(path):
                os.remove(path)

            os.rmdir(directory)

    _draw_material(material, image)
    return material


def show_project_material(material: bpy.types.Material, project: typing.Optional[projects.Project]) -> None:
    """Draws the material with its project material's texture, multiplied by the vertex colors the way the game does."""
    uri = material.get(URI_PROPERTY, "")
    project_material = project.materials.get(uri) if project is not None else None
    texture = project.texture_of(project_material) if project is not None and project_material is not None else None
    image = None
    if texture is not None:
        image = next((image for image in bpy.data.images if os.path.normpath(bpy.path.abspath(image.filepath)) == os.path.normpath(texture.png_path)), None)
        if image is None:
            image = bpy.data.images.load(texture.png_path, check_existing=True)

    _draw_material(material, image)
    if image is None and project is not None and uri and project_material is None:
        material.diffuse_color = (1.0, 0.0, 1.0, 1.0)


def _draw_material(material: bpy.types.Material, image: typing.Optional[bpy.types.Image]) -> None:
    """The image multiplied by the vertex colors the way the game does, the vertex colors alone without one."""
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (400, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (100, 0)
    links.new(shader.outputs[0], output.inputs["Surface"])
    colors = nodes.new("ShaderNodeVertexColor")
    colors.layer_name = COLOR_ATTRIBUTE
    colors.location = (-500, -200)
    # The game draws a vertex color of 0x80 at full brightness
    brighten = nodes.new("ShaderNodeMix")
    brighten.data_type = "RGBA"
    brighten.blend_type = "MULTIPLY"
    brighten.inputs["Factor"].default_value = 1.0
    brighten.inputs["B"].default_value = (2.0, 2.0, 2.0, 1.0)
    brighten.location = (-300, -200)
    links.new(colors.outputs["Color"], brighten.inputs["A"])
    if image is None:
        links.new(brighten.outputs["Result"], shader.inputs["Base Color"])
        return

    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = image
    image_node.location = (-500, 150)
    multiply = nodes.new("ShaderNodeMix")
    multiply.data_type = "RGBA"
    multiply.blend_type = "MULTIPLY"
    multiply.inputs["Factor"].default_value = 1.0
    multiply.location = (-100, 100)
    links.new(image_node.outputs["Color"], multiply.inputs["A"])
    links.new(brighten.outputs["Result"], multiply.inputs["B"])
    links.new(multiply.outputs["Result"], shader.inputs["Base Color"])
    links.new(image_node.outputs["Alpha"], shader.inputs["Alpha"])


class _FileMaterials:
    """The materials of the file being written, by the Blender materials showing them."""

    def __init__(self, file: tlm.TlmFile):
        self.file = file
        self._indexes: typing.Dict[str, int] = {}

    def index_of(self, material: typing.Optional[bpy.types.Material]) -> int:
        if material is None:
            return -1

        key = material.get(URI_PROPERTY) or "blender:" + material.name
        if key in self._indexes:
            return self._indexes[key]

        uri = material.get(URI_PROPERTY)
        entry: typing.Dict[str, typing.Any] = {"name": material.name}
        if uri:
            entry["uri"] = uri
        else:
            self._embed(material, entry)

        self.file.materials.append(entry)
        self._indexes[key] = len(self.file.materials) - 1
        return self._indexes[key]


    def _embed(self, material: bpy.types.Material, entry: typing.Dict[str, typing.Any]) -> None:
        """A material made in Blender goes into the file with its image, TT Lab adds it to the project."""
        if not material.get(BLENDER_ID_PROPERTY):
            material[BLENDER_ID_PROPERTY] = uuid.uuid4().hex

        entry["blender_id"] = material[BLENDER_ID_PROPERTY]
        if getattr(material, "surface_render_method", "") == "BLENDED" or getattr(material, "blend_method", "") == "BLEND":
            entry["alpha"] = "BLEND"

        image = next((node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image is not None), None) \
            if material.use_nodes and material.node_tree is not None else None
        png = _png_bytes(image) if image is not None else None
        if png is not None:
            entry["image"] = {"png": self.file.write_view(png, "u8"), "name": image.name}


def _png_bytes(image: bpy.types.Image) -> typing.Optional[bytes]:
    """The image as a PNG, the file itself when it's one on the disk or packed that wasn't changed."""
    if image.packed_file is not None and not image.is_dirty:
        packed = bytes(image.packed_file.data)
        if packed.startswith(b"\x89PNG"):
            return packed

    path = bpy.path.abspath(image.filepath) if image.filepath else ""
    if image.packed_file is None and not image.is_dirty and path.lower().endswith(".png") and os.path.exists(path):
        with open(path, "rb") as file:
            return file.read()

    directory = tempfile.mkdtemp()
    target = os.path.join(directory, "image.png")
    copy = image.copy()
    try:
        copy.filepath_raw = target
        copy.file_format = "PNG"
        copy.save()
        with open(target, "rb") as file:
            return file.read()
    except RuntimeError:
        return None
    finally:
        bpy.data.images.remove(copy)
        if os.path.exists(target):
            os.remove(target)

        os.rmdir(directory)


# Meshes

def _build_mesh(name: str, file: tlm.TlmFile, mesh_node: typing.Optional[typing.Dict[str, typing.Any]], skinned: bool,
                materials: typing.List[bpy.types.Material]) -> typing.Tuple[bpy.types.Mesh, tlm_mesh.MeshData]:
    data = tlm_mesh.from_parts(file, mesh_node, skinned)
    mesh = bpy.data.meshes.new(name)
    vertex_count = data.vertex_count
    triangle_count = len(data.triangles) // 3
    mesh.vertices.add(vertex_count)
    mesh.vertices.foreach_set("co", data.positions)
    mesh.loops.add(triangle_count * 3)
    mesh.loops.foreach_set("vertex_index", data.triangles)
    mesh.polygons.add(triangle_count)
    mesh.polygons.foreach_set("loop_start", array.array("i", range(0, triangle_count * 3, 3)))
    mesh.update(calc_edges=True)

    for part in data.parts:
        index = part["material"]
        mesh.materials.append(materials[index] if 0 <= index < len(materials) else None)

    mesh.polygons.foreach_set("material_index", data.face_materials)
    mesh.attributes.new(PART_ATTRIBUTE, "INT", "FACE").data.foreach_set("value", data.face_parts)
    mesh.attributes.new(VERTEX_PART_ATTRIBUTE, "INT", "POINT").data.foreach_set("value", data.vertex_parts)

    # UVs are kept per vertex as the game has them, Blender's are flipped upside down per corner
    corner_uvs = array.array("f")
    for vertex in data.triangles:
        corner_uvs.extend((data.uvs[vertex * 2], 1.0 - data.uvs[vertex * 2 + 1]))

    mesh.uv_layers.new(name="UVMap").data.foreach_set("uv", corner_uvs)
    mesh.attributes.new(TWIN_UV_ATTRIBUTE, "FLOAT2", "POINT").data.foreach_set("vector", data.uvs)
    colors = mesh.color_attributes.new(COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT")
    colors.data.foreach_set("color_srgb", array.array("f", (value / 255.0 for value in data.colors)))
    mesh.color_attributes.active_color = colors
    mesh.color_attributes.render_color_index = mesh.color_attributes.find(COLOR_ATTRIBUTE)
    if data.emit_colors is not None:
        mesh.color_attributes.new(EMIT_COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT").data.foreach_set("color_srgb", array.array("f", (value / 255.0 for value in data.emit_colors)))

    if data.alpha_flags is not None:
        mesh.attributes.new(COLOR_ALPHA_ATTRIBUTE, "BOOLEAN", "POINT").data.foreach_set("value", [bool(flag) for flag in data.alpha_flags[0::2]])
        mesh.attributes.new(EMIT_ALPHA_ATTRIBUTE, "BOOLEAN", "POINT").data.foreach_set("value", [bool(flag) for flag in data.alpha_flags[1::2]])

    if data.uv_q is not None:
        mesh.attributes.new(UV_Q_ATTRIBUTE, "FLOAT", "POINT").data.foreach_set("value", data.uv_q)

    if data.twin_normals is not None:
        mesh.attributes.new(TWIN_NORMAL_ATTRIBUTE, "FLOAT_VECTOR", "POINT").data.foreach_set("vector", data.twin_normals)

    if skinned:
        mesh.attributes.new(TWIN_JOINTS_ATTRIBUTE, "FLOAT_VECTOR", "POINT").data.foreach_set("vector", array.array("f", data.twin_joints))
        mesh.attributes.new(TWIN_WEIGHTS_ATTRIBUTE, "FLOAT_VECTOR", "POINT").data.foreach_set("vector", data.twin_weights)

    mesh[PARTS_PROPERTY] = json.dumps(data.parts)
    mesh.shade_smooth()
    if data.normals is not None:
        mesh.normals_split_custom_set_from_vertices([tuple(data.normals[i:i + 3]) for i in range(0, len(data.normals), 3)])

    return mesh, data


def _add_mesh_object(name: str, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection,
                     materials: typing.List[bpy.types.Material], skinned: bool, armature: typing.Optional[bpy.types.Object] = None,
                     bone_names: typing.Optional[typing.Dict[int, str]] = None) -> bpy.types.Object:
    mesh, data = _build_mesh(name, file, tree_node.get("mesh"), skinned, materials)
    blender_object = _new_object(name, mesh, parent, collection)
    blender_object[KIND_PROPERTY] = tree_node.get("kind", "")
    type_name = KIND_TYPES.get(tree_node.get("kind", ""))
    if type_name is not None:
        _read_data(blender_object, type_name, tree_node.get("data"))

    if skinned:
        _add_weights(blender_object, data, bone_names or {})
        if len(data.shapes) > 0:
            basis = blender_object.shape_key_add(name="Basis", from_mix=False)
            basis.interpolation = "KEY_LINEAR"
            for index, offsets in enumerate(data.shapes):
                blender_object.data.attributes.new(TWIN_SHAPE_ATTRIBUTE + str(index), "FLOAT_VECTOR", "POINT").data.foreach_set("vector", offsets)
                key = blender_object.shape_key_add(name="Shape %d" % index, from_mix=False)
                key.data.foreach_set("co", array.array("f", (position + offset for position, offset in zip(data.positions, offsets))))
                # Blender 5 adds shape keys at full weight, the game's face is at rest without any
                key.value = 0.0

        if armature is not None:
            modifier = blender_object.modifiers.new("Armature", "ARMATURE")
            modifier.object = armature

    return blender_object


def _add_weights(blender_object: bpy.types.Object, data: tlm_mesh.MeshData, bone_names: typing.Dict[int, str]) -> None:
    groups: typing.Dict[int, bpy.types.VertexGroup] = {}
    for vertex in range(data.vertex_count):
        joints = data.twin_joints[vertex * 3:vertex * 3 + 3]
        weights = data.twin_weights[vertex * 3:vertex * 3 + 3]
        influences = []
        for joint, weight in zip(joints, weights):
            if weight <= 0:
                break

            influences.append((joint, weight))

        total = sum(weight for _, weight in influences)
        for joint, weight in influences:
            group = groups.get(joint)
            if group is None:
                group = blender_object.vertex_groups.new(name=bone_names.get(joint, "Joint %d" % joint))
                groups[joint] = group

            group.add([vertex], weight / total, "ADD")


# The game's biggest skin has 2716 vertexes before its strips (Cortex), meshes past this get decimated on export
MAX_EXPORT_VERTICES = 4096

# What the export decimated, reported with the warnings
_export_notes: typing.List[str] = []


def _decimated(blender_object: bpy.types.Object, target: int) -> typing.Tuple[bpy.types.Object, typing.Callable[[], None]]:
    """A temporary copy of the object whose mesh is collapsed down to about the target's vertexes, and what removes it again.
    Vertex groups and UVs survive the collapse, shape keys wouldn't so meshes with them aren't decimated."""
    mesh: bpy.types.Mesh = blender_object.data
    copy = blender_object.copy()
    copy.data = mesh.copy()
    bpy.context.scene.collection.objects.link(copy)
    for modifier in list(copy.modifiers):
        copy.modifiers.remove(modifier)

    decimate = copy.modifiers.new("Decimate", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.use_collapse_triangulate = True
    # The ratio is of faces, the vertexes it leaves are found in a few tries
    ratio = target / len(mesh.vertices)
    decimated = None
    for _ in range(6):
        decimate.ratio = max(0.01, min(1.0, ratio))
        depsgraph = bpy.context.evaluated_depsgraph_get()
        if decimated is not None:
            bpy.data.meshes.remove(decimated)

        decimated = bpy.data.meshes.new_from_object(copy.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
        if len(decimated.vertices) <= target:
            break

        ratio *= 0.9 * target / len(decimated.vertices)

    working_copy = copy.data
    copy.modifiers.remove(decimate)
    copy.data = decimated

    def remove() -> None:
        bpy.data.objects.remove(copy)
        bpy.data.meshes.remove(decimated)
        bpy.data.meshes.remove(working_copy)

    return copy, remove


def _read_mesh(blender_object: bpy.types.Object, file_materials: typing.Optional[_FileMaterials], skinned: bool,
               joint_of_group: typing.Optional[typing.Dict[int, int]] = None, shaped: typing.Optional[bool] = None) -> tlm_mesh.CornerMesh:
    """The mesh as it's written. Shape keys are shapes of skins, or of what's shaped (a save icon)."""
    mesh: bpy.types.Mesh = blender_object.data
    if len(mesh.vertices) <= MAX_EXPORT_VERTICES:
        return _read_mesh_data(blender_object, file_materials, skinned, joint_of_group, shaped)

    if mesh.shape_keys is not None:
        _export_notes.append("%s has %d vertexes, the game's biggest skin has 2716: it wasn't decimated because of its shape keys, expect the game to slow down"
                             % (blender_object.name, len(mesh.vertices)))
        return _read_mesh_data(blender_object, file_materials, skinned, joint_of_group, shaped)

    decimated, remove = _decimated(blender_object, MAX_EXPORT_VERTICES)
    try:
        _export_notes.append("%s has %d vertexes, the game's biggest skin has 2716: written decimated to %d"
                             % (blender_object.name, len(mesh.vertices), len(decimated.data.vertices)))
        return _read_mesh_data(decimated, file_materials, skinned, joint_of_group, shaped)
    finally:
        remove()


def _read_mesh_data(blender_object: bpy.types.Object, file_materials: typing.Optional[_FileMaterials], skinned: bool,
                    joint_of_group: typing.Optional[typing.Dict[int, int]] = None, shaped: typing.Optional[bool] = None) -> tlm_mesh.CornerMesh:
    mesh: bpy.types.Mesh = blender_object.data
    result = tlm_mesh.CornerMesh()
    vertex_count = len(mesh.vertices)
    positions = array.array("f", [0.0] * vertex_count * 3)
    key_blocks = mesh.shape_keys.key_blocks if mesh.shape_keys is not None else []
    if len(key_blocks) > 0:
        key_blocks[0].data.foreach_get("co", positions)
    else:
        mesh.vertices.foreach_get("co", positions)

    result.positions = positions
    mesh.calc_loop_triangles()
    triangle_count = len(mesh.loop_triangles)
    triangle_loops = array.array("i", [0] * triangle_count * 3)
    mesh.loop_triangles.foreach_get("loops", triangle_loops)
    triangle_polygons = array.array("i", [0] * triangle_count)
    mesh.loop_triangles.foreach_get("polygon_index", triangle_polygons)
    loop_vertices = array.array("i", [0] * len(mesh.loops))
    mesh.loops.foreach_get("vertex_index", loop_vertices)
    result.corner_vertices = [loop_vertices[loop] for loop in triangle_loops]
    polygon_parts = _attribute(mesh, PART_ATTRIBUTE, "value", len(mesh.polygons), 1, "i")
    polygon_materials = array.array("i", [0] * len(mesh.polygons))
    mesh.polygons.foreach_get("material_index", polygon_materials)
    result.triangle_parts = [polygon_parts[polygon] if polygon_parts is not None else 0 for polygon in triangle_polygons]
    result.triangle_materials = [polygon_materials[polygon] for polygon in triangle_polygons]
    result.vertex_parts = _attribute(mesh, VERTEX_PART_ATTRIBUTE, "value", vertex_count, 1, "i") or [0] * vertex_count

    uv_layer = mesh.uv_layers.active
    loop_uvs = array.array("f", [0.0] * len(mesh.loops) * 2)
    if uv_layer is not None:
        uv_layer.data.foreach_get("uv", loop_uvs)

    result.corner_uvs = [value for loop in triangle_loops for value in loop_uvs[loop * 2:loop * 2 + 2]]
    loop_normals = array.array("f", [0.0] * len(mesh.loops) * 3)
    mesh.corner_normals.foreach_get("vector", loop_normals)
    result.corner_normals = [value for loop in triangle_loops for value in loop_normals[loop * 3:loop * 3 + 3]]
    colors = mesh.color_attributes.get(COLOR_ATTRIBUTE) or mesh.color_attributes.active_color
    if colors is not None:
        values = array.array("f", [0.0] * len(colors.data) * 4)
        colors.data.foreach_get("color_srgb", values)
        if colors.domain == "CORNER":
            result.corner_colors = [value for loop in triangle_loops for value in values[loop * 4:loop * 4 + 4]]
        else:
            result.vertex_colors = values

    emit = mesh.color_attributes.get(EMIT_COLOR_ATTRIBUTE)
    if emit is not None and emit.domain == "POINT":
        result.emit_colors = array.array("f", [0.0] * vertex_count * 4)
        emit.data.foreach_get("color_srgb", result.emit_colors)

    color_alpha = _attribute(mesh, COLOR_ALPHA_ATTRIBUTE, "value", vertex_count, 1, "b")
    emit_alpha = _attribute(mesh, EMIT_ALPHA_ATTRIBUTE, "value", vertex_count, 1, "b")
    if color_alpha is not None or emit_alpha is not None:
        result.alpha_flags = [flag for vertex in range(vertex_count) for flag in (color_alpha[vertex] if color_alpha is not None else 0, emit_alpha[vertex] if emit_alpha is not None else 0)]

    result.twin_normals = _attribute(mesh, TWIN_NORMAL_ATTRIBUTE, "vector", vertex_count, 3, "f")
    result.twin_uvs = _attribute(mesh, TWIN_UV_ATTRIBUTE, "vector", vertex_count, 2, "f")
    result.uv_q = _attribute(mesh, UV_Q_ATTRIBUTE, "value", vertex_count, 1, "f")
    if skinned:
        result.twin_joints = _attribute(mesh, TWIN_JOINTS_ATTRIBUTE, "vector", vertex_count, 3, "f")
        result.twin_weights = _attribute(mesh, TWIN_WEIGHTS_ATTRIBUTE, "vector", vertex_count, 3, "f")
        if joint_of_group is None:
            joint_of_group = {group.index: int(group.name.rsplit(" ", 1)[-1]) for group in blender_object.vertex_groups
                              if group.name.rsplit(" ", 1)[-1].isdigit()}

        result.group_influences = [[(joint_of_group.get(group.group, -1), group.weight) for group in vertex.groups
                                    if group.weight > 0 and joint_of_group.get(group.group, -1) >= 0] for vertex in mesh.vertices]

    if (skinned if shaped is None else shaped):
        basis = positions
        for index, key_block in enumerate(list(key_blocks)[1:]):
            shape = array.array("f", [0.0] * vertex_count * 3)
            key_block.data.foreach_get("co", shape)
            result.shapes.append(array.array("f", (value - base for value, base in zip(shape, basis))))
            result.twin_shapes.append(_attribute(mesh, TWIN_SHAPE_ATTRIBUTE + str(index), "vector", vertex_count, 3, "f"))

    try:
        result.parts = json.loads(mesh.get(PARTS_PROPERTY, "[]"))
    except ValueError:
        result.parts = []

    result.slot_materials = [file_materials.index_of(slot.material) if file_materials is not None else -1 for slot in blender_object.material_slots]
    return result


def _attribute(mesh: bpy.types.Mesh, name: str, value_name: str, count: int, size: int, typecode: str) -> typing.Optional[array.array]:
    attribute = mesh.attributes.get(name)
    if attribute is None or len(attribute.data) != count:
        return None

    values = array.array(typecode, [0] * count * size)
    attribute.data.foreach_get(value_name, values)
    return values


# Importing

def import_file(context: bpy.types.Context, path: str) -> bpy.types.Object:
    file = tlm.TlmFile.load(path)
    project = projects.open_project(path)
    collection = bpy.data.collections.new(file.name or os.path.splitext(os.path.basename(path))[0])
    context.scene.collection.children.link(collection)
    materials = [_material_for(project, entry, file) for entry in file.materials]
    root_node = file.root or {"kind": "model"}
    kind = root_node.get("kind", "")
    root = _new_object(root_node.get("name") or file.name, None, None, collection)
    root.empty_display_type = "PLAIN_AXES"
    root.matrix_basis = Y_UP
    root[ROOT_PROPERTY] = file.asset_type
    root[PATH_PROPERTY] = path
    root[UID_PROPERTY] = uuid.uuid4().hex
    root[KIND_PROPERTY] = kind
    type_name = KIND_TYPES.get(kind)
    if type_name is not None and kind != "collision":
        _read_data(root, type_name, root_node.get("data"))

    from . import tlm_scenery

    if kind == "ogi":
        _import_ogi(context, file, root_node, root, collection, materials)
    elif kind in TREE_KINDS:
        tlm_scenery.import_children(context, file, root_node, root, collection, materials)
    elif kind == "collision":
        tlm_scenery._import_collision(file, root_node, root, collection)
    elif kind == "save_icon":
        _import_save_icon(file, root_node, root, collection, materials)
    else:
        _add_mesh_object(root_node.get("name") or file.name, file, dict(root_node, kind="part_mesh"), root, collection, materials, kind in SKINNED_KINDS)

    return root


def _import_save_icon(file: tlm.TlmFile, root_node: typing.Dict[str, typing.Any], root: bpy.types.Object, collection: bpy.types.Collection,
                      materials: typing.List[bpy.types.Material]) -> None:
    """The icon's mesh with its shapes after the first as shape keys, its animation's keys on their values."""
    name = root_node.get("name") or "Save icon"
    mesh, data = _build_mesh(name, file, root_node.get("mesh"), False, materials)
    blender_object = _new_object(name, mesh, root, collection)
    blender_object[KIND_PROPERTY] = "icon_mesh"
    if len(data.shapes) > 0:
        basis = blender_object.shape_key_add(name="Basis", from_mix=False)
        basis.interpolation = "KEY_LINEAR"
        for index, offsets in enumerate(data.shapes):
            mesh.attributes.new(TWIN_SHAPE_ATTRIBUTE + str(index), "FLOAT_VECTOR", "POINT").data.foreach_set("vector", offsets)
            key = blender_object.shape_key_add(name="Shape %d" % (index + 1), from_mix=False)
            key.data.foreach_set("co", array.array("f", (position + offset for position, offset in zip(data.positions, offsets))))
            key.value = 0.0

    meta: typing.Dict[str, typing.Any] = {"frames": []}
    exact = file.read_view(root_node.get("exact"), "u8")
    if len(exact) > 0:
        meta["exact"] = base64.b64encode(exact.tobytes()).decode("ascii")

    key = mesh.shape_keys
    bag = None
    for frame in (root_node.get("animation") or {}).get("frames", []):
        shape = int(frame.get("shape", 0))
        keys = list(file.read_view(frame.get("keys"), "f32"))
        # The basis has no value to animate, its keys stay as they are
        if shape == 0 or key is None or shape >= len(key.key_blocks):
            meta["frames"].append({"shape": shape, "keys": keys})
            continue

        keys = keys[:len(keys) // 2 * 2]
        # What the curve was made from, the first shape's keys stay while it's still that
        meta["frames"].append({"shape": shape, "imported": keys})
        if bag is None:
            action = bpy.data.actions.new(name + " animation")
            slot = action.slots.new(id_type="KEY", name=key.name)
            bag = _channelbag(action, slot)
            key_data = key.animation_data or key.animation_data_create()
            key_data.action = action
            key_data.action_slot = slot

        fcurve = bag.fcurves.new('key_blocks["%s"].value' % bpy.utils.escape_identifier(key.key_blocks[shape].name), index=0)
        points = fcurve.keyframe_points
        points.add(len(keys) // 2)
        points.foreach_set("co", array.array("f", keys))
        for point in points:
            point.interpolation = "LINEAR"

        fcurve.update()

    root[SAVE_ICON_PROPERTY] = json.dumps(meta)


def _import_ogi(context: bpy.types.Context, file: tlm.TlmFile, root_node: typing.Dict[str, typing.Any], root: bpy.types.Object,
                collection: bpy.types.Collection, materials: typing.List[bpy.types.Material]) -> None:
    armature_node = tlm.find_child(root_node, "armature") or {}
    armature_data = bpy.data.armatures.new(root.name + " Armature")
    armature = _new_object("armature", armature_data, root, collection)
    armature[KIND_PROPERTY] = "armature"
    joints = [joint for joint in armature_node.get("joints", []) if isinstance(joint, dict)]
    rests = {int(joint.get("index", i)): tlm_math.rest_of(tlm_math.from_rows(joint["bind"])) if len(joint.get("bind", [])) == 16 else ((0.0, 0.0, 0.0), (1.0, 0.0, 0.0, 0.0))
             for i, joint in enumerate(joints)}
    bone_names: typing.Dict[int, str] = {}
    view_layer = context.view_layer
    view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = {}
    length = _bone_length(rests)
    for i, joint in enumerate(joints):
        index = int(joint.get("index", i))
        edit_bone = armature_data.edit_bones.new(str(joint.get("name") or "Joint %d" % index))
        edit_bone.head = (0.0, 0.0, 0.0)
        edit_bone.tail = (0.0, length, 0.0)
        translation, rotation = rests[index]
        edit_bone.matrix = Matrix(tlm_math.compose(translation, rotation))
        edit_bones[index] = edit_bone
        bone_names[index] = edit_bone.name

    for i, joint in enumerate(joints):
        parent = int(joint.get("parent", -1))
        if parent in edit_bones and parent != int(joint.get("index", i)):
            edit_bones[int(joint.get("index", i))].parent = edit_bones[parent]

    bpy.ops.object.mode_set(mode="OBJECT")
    for i, joint in enumerate(joints):
        index = int(joint.get("index", i))
        bone = armature_data.bones[bone_names[index]]
        _read_data(bone, "Joint", dict(joint.get("data", {}), Index=index))
        if len(joint.get("bind", [])) == 16:
            bone[BIND_PROPERTY] = [float(value) for value in joint["bind"]]
        armature.pose.bones[bone.name].rotation_mode = "QUATERNION"

    shape_object = None
    for child in tlm.children(root_node):
        kind = child.get("kind")
        if kind in SKINNED_KINDS:
            skin = _add_mesh_object(child.get("name") or kind, file, child, root, collection, materials, True, armature, bone_names)
            if kind == "shape":
                shape_object = skin
        elif kind == "rigid_bodies":
            _import_attached(child, "body", root, armature, bone_names, collection, lambda node, parent: _add_mesh_object(
                node.get("name") or "Body", file, node, parent, collection, materials, False))
        elif kind == "exit_points":
            _import_attached(child, "exit_point", root, armature, bone_names, collection, lambda node, parent: _add_exit_point(node, parent, collection))
        elif kind == "collision_hulls":
            _import_attached(child, "hull", root, armature, bone_names, collection, lambda node, parent: add_hull_object(file, node, parent, collection))

    _import_animations(context, file, armature_node, armature, shape_object, rests, joints, bone_names, root)


def _bone_length(rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]]) -> float:
    positions = [Vector(translation) for translation, _ in rests.values()]
    if len(positions) < 2:
        return 0.1

    size = max((a - b).length for a in positions for b in positions[:1])
    return max(0.01, size * 0.05)


def _import_attached(container_node: typing.Dict[str, typing.Any], kind: str, root: bpy.types.Object, armature: bpy.types.Object,
                     bone_names: typing.Dict[int, str], collection: bpy.types.Collection, create: typing.Callable) -> None:
    container = _new_object(container_node.get("name") or container_node.get("kind"), None, root, collection)
    container[KIND_PROPERTY] = container_node.get("kind")
    container.empty_display_size = 0.1
    for tree_node in tlm.traverse(container_node):
        if tree_node.get("kind") != kind:
            continue

        blender_object = create(tree_node, container)
        _set_node_transform(blender_object, tree_node)
        matrix = tree_node.get("matrix") or (tree_node.get("data") or {}).get("Matrix")
        if isinstance(matrix, list) and len(matrix) == 16:
            blender_object[MATRIX_PROPERTY] = [float(value) for value in matrix]

        joint = int(tree_node.get("joint", 0))
        blender_object[JOINT_PROPERTY] = joint
        # A hull on no joint stays in the model's space
        if joint not in bone_names:
            continue

        constraint = blender_object.constraints.new("CHILD_OF")
        constraint.target = armature
        constraint.subtarget = bone_names[joint]
        # Follows the bone without what the container and the root already do
        constraint.inverse_matrix = root.matrix_basis.inverted()


def add_hull_object(file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection) -> bpy.types.Object:
    """A collision hull as a wire mesh of its faces. What the game reads of it comes along, for TT Lab to keep while the mesh stays."""
    positions = file.read_view(tree_node.get("vertices"), "f32")
    face_bytes = file.read_view(tree_node.get("faces"), "u8")
    faces = []
    offset = 0
    while offset < len(face_bytes):
        count = face_bytes[offset]
        faces.append([int(index) for index in face_bytes[offset + 1:offset + 1 + count]])
        offset += 1 + count

    mesh = bpy.data.meshes.new(tree_node.get("name") or "Hull")
    mesh.from_pydata([tuple(positions[i:i + 3]) for i in range(0, len(positions) - 3, 4)], [], [face for face in faces if len(face) >= 3])
    mesh.update()
    blender_object = _new_object(tree_node.get("name") or "Hull", mesh, parent, collection)
    blender_object.display_type = "WIRE"
    blender_object[KIND_PROPERTY] = "hull"
    _read_data(blender_object, "CollisionHull", tree_node.get("data"))
    kept = {key: list(file.read_view(tree_node[key])) for key in HULL_KEYS if key in tree_node}
    blender_object[HULL_PROPERTY] = json.dumps(kept)
    return blender_object


def export_hull(file: tlm.TlmFile, blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    """The hull's vertexes and faces from its mesh, with what it came with for TT Lab to tell whether the mesh changed."""
    mesh: bpy.types.Mesh = blender_object.data
    positions = array.array("f", [0.0] * len(mesh.vertices) * 3)
    mesh.vertices.foreach_get("co", positions)
    tree_node["vertices"] = file.write_view([value for vertex in range(len(mesh.vertices)) for value in (*positions[vertex * 3:vertex * 3 + 3], 1.0)], "f32")
    faces = []
    for polygon in mesh.polygons:
        faces.append(len(polygon.vertices))
        faces.extend(polygon.vertices)

    tree_node["faces"] = file.write_view(faces, "u8")
    try:
        kept = json.loads(blender_object.get(HULL_PROPERTY, "{}"))
    except ValueError:
        kept = {}

    for key in ("planes", "edge_directions", "face_normals"):
        if key in kept:
            tree_node[key] = file.write_view(kept[key], "f32")

    if "edges" in kept:
        tree_node["edges"] = file.write_view(kept["edges"], "u8")

    if "vertices" in kept and "faces" in kept:
        tree_node["twin_vertices"] = file.write_view(kept["vertices"], "f32")
        tree_node["twin_faces"] = file.write_view(kept["faces"], "u8")


def _add_exit_point(tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection) -> bpy.types.Object:
    exit_point = _new_object(tree_node.get("name") or "Exit Point", None, parent, collection)
    exit_point.empty_display_type = "ARROWS"
    exit_point.empty_display_size = 0.1
    exit_point[KIND_PROPERTY] = "exit_point"
    _read_data(exit_point, "ExitPoint", {key: value for key, value in (tree_node.get("data") or {}).items() if key != "Matrix"})
    return exit_point


# Animations

def _channelbag(action: bpy.types.Action, slot: typing.Any) -> typing.Any:
    if len(action.layers) == 0:
        action.layers.new("Layer")

    layer = action.layers[0]
    if len(layer.strips) == 0:
        layer.strips.new(type="KEYFRAME")

    return layer.strips[0].channelbag(slot, ensure=True)


def _existing_channelbag(action: bpy.types.Action, slot: typing.Any) -> typing.Any:
    for layer in action.layers:
        for strip in layer.strips:
            bag = strip.channelbag(slot)
            if bag is not None:
                return bag

    return None


def _add_track(bag: typing.Any, data_path: str, index: int, values: typing.Sequence[float], group: typing.Any = None) -> None:
    fcurve = bag.fcurves.new(data_path, index=index)
    if group is not None:
        fcurve.group = group

    points = fcurve.keyframe_points
    points.add(len(values))
    points.foreach_set("co", array.array("f", (value for frame, key in enumerate(values) for value in (float(frame), key))))
    try:
        points.foreach_set("interpolation", array.array("i", [1] * len(values)))
    except (TypeError, RuntimeError):
        for point in points:
            point.interpolation = "LINEAR"

    fcurve.update()


def _track_values(values: typing.Sequence[float], size: int, frames: int) -> typing.List[typing.Tuple[float, ...]]:
    """Every frame's values of a track, a track of one key holds for all of them."""
    count = len(values) // size
    return [tuple(values[min(frame, count - 1) * size:min(frame, count - 1) * size + size]) for frame in range(frames)] if count > 0 else []


def _import_animations(context: bpy.types.Context, file: tlm.TlmFile, armature_node: typing.Dict[str, typing.Any], armature: bpy.types.Object,
                       shape_object: typing.Optional[bpy.types.Object], rests: typing.Dict[int, typing.Any], joints: typing.List[typing.Dict[str, typing.Any]],
                       bone_names: typing.Dict[int, str], root: bpy.types.Object) -> None:
    parents = {int(joint.get("index", i)): int(joint.get("parent", -1)) for i, joint in enumerate(joints)}
    relative_rests = {index: tlm_math.relative_rest(rests.get(parents[index]), rests[index]) for index in rests}
    shape_ranges: typing.Dict[int, typing.List[float]] = {}
    first_action = None
    for order, animation in enumerate(armature_node.get("animations", [])):
        frames = max(1, int(animation.get("frames", 1)))
        action = bpy.data.actions.new(str(animation.get("name") or "Animation"))
        action.use_fake_user = True
        action[OWNER_PROPERTY] = root[UID_PROPERTY]
        meta = {key: animation[key] for key in ("id", "fps", "frames", "joint_count") if key in animation}
        # Blender keeps actions sorted by name
        meta["order"] = order
        exact = file.read_view(animation.get("exact"), "u8")
        if len(exact) > 0:
            meta["exact"] = base64.b64encode(exact.tobytes()).decode("ascii")

        facial = animation.get("facial")
        if isinstance(facial, dict):
            # Weights of shapes the model has no shape key for, or all of them for models without a shape, are written back as they were
            meta["facial"] = dict({key: value for key, value in facial.items() if key != "weights"}, weights=list(file.read_view(facial.get("weights"), "f32")))

        action[ANIMATION_PROPERTY] = json.dumps(meta)
        slot = action.slots.new(id_type="OBJECT", name=armature.name)
        bag = _channelbag(action, slot)
        for keys in animation.get("joints", []):
            index = int(keys.get("joint", -1))
            if index not in bone_names:
                continue

            name = bone_names[index]
            group = bag.groups.new(name)
            translations = _track_values(file.read_view(keys.get("translation"), "f32"), 3, frames)
            rotations = _track_values(file.read_view(keys.get("rotation"), "f32"), 4, frames)
            scales = _track_values(file.read_view(keys.get("scale"), "f32"), 3, frames)
            poses = []
            previous = None
            for frame in range(frames):
                translation = translations[frame] if translations else (0.0, 0.0, 0.0)
                rotation = tlm_math.xyzw_to_wxyz(rotations[frame]) if rotations else (1.0, 0.0, 0.0, 0.0)
                scale = scales[frame] if scales else (1.0, 1.0, 1.0)
                location, pose_rotation, pose_scale = tlm_math.key_to_pose(relative_rests[index], translation, rotation, scale)
                # Both of a rotation's quaternions are the same, the one closer to the last frame's plays without spinning around
                if previous is not None and sum(a * b for a, b in zip(previous, pose_rotation)) < 0:
                    pose_rotation = tuple(-value for value in pose_rotation)

                previous = pose_rotation
                poses.append((location, pose_rotation, pose_scale))

            base = 'pose.bones["%s"].' % bpy.utils.escape_identifier(name)
            for channel, (path, size) in enumerate((("location", 3), ("rotation_quaternion", 4), ("scale", 3))):
                for component in range(size):
                    values = [pose[channel][component] for pose in poses]
                    _add_track(bag, base + path, component, values[:1] if all(value == values[0] for value in values) else values, group)

            bone = armature.data.bones[name]
            entry = flags.get_or_add_flags(bone, action)
            entry.independent_scaling = bool(keys.get("independent_scaling", False))
            entry.uses_additional_rotation = bool(keys.get("additional_rotation", False))
            entry.imported = True
            entry.imported_independent_scaling = entry.independent_scaling
            entry.imported_uses_additional_rotation = entry.uses_additional_rotation

        if isinstance(facial, dict) and shape_object is not None and shape_object.data.shape_keys is not None:
            shapes = int(facial.get("shapes", 0))
            facial_frames = max(1, int(facial.get("frames", frames)))
            weights = _track_values(file.read_view(facial.get("weights"), "f32"), shapes, facial_frames) if shapes > 0 else []
            key = shape_object.data.shape_keys
            key_slot = action.slots.new(id_type="KEY", name=key.name)
            key_bag = _channelbag(action, key_slot)
            for shape in range(shapes):
                if shape + 1 >= len(key.key_blocks) or not weights:
                    continue

                values = [frame_weights[shape] for frame_weights in weights]
                _add_track(key_bag, 'key_blocks["%s"].value' % bpy.utils.escape_identifier(key.key_blocks[shape + 1].name), 0,
                           values[:1] if all(value == values[0] for value in values) else values)
                shape_ranges.setdefault(shape, []).extend((min(values), max(values)))

        action.use_frame_range = True
        action.frame_start = 0
        action.frame_end = frames - 1
        meta["frame_end"] = int(round(action.frame_range[1]))
        action[ANIMATION_PROPERTY] = json.dumps(meta)
        if first_action is None:
            first_action = action
            fps = int(animation.get("fps", 0))
            if fps > 0:
                context.scene.render.fps = fps
                context.scene.render.fps_base = 1.0

    # Blender clamps a shape key to its range, the game's facial animations go past 0 and 1
    if shape_object is not None and shape_object.data.shape_keys is not None:
        key_blocks = shape_object.data.shape_keys.key_blocks
        for shape, values in shape_ranges.items():
            key_blocks[shape + 1].slider_min = max(-10.0, min(0.0, min(values)))
            key_blocks[shape + 1].slider_max = min(10.0, max(1.0, max(values)))

    if first_action is not None:
        assign_action(armature, first_action)
        context.scene.frame_start = 0
        context.scene.frame_end = int(first_action.frame_end)


def assign_action(armature: bpy.types.Object, action: bpy.types.Action) -> None:
    """Plays the action on the armature and on the shape keys of the meshes it deforms."""
    animation_data = armature.animation_data or armature.animation_data_create()
    animation_data.action = action
    object_slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
    if object_slot is not None:
        animation_data.action_slot = object_slot

    sync_shape_keys(armature)


def sync_shape_keys(armature: bpy.types.Object) -> None:
    """Gives the shape keys of the meshes the armature deforms the action the armature plays."""
    action = armature.animation_data.action if armature.animation_data is not None else None
    key_slot = next((slot for slot in action.slots if slot.target_id_type == "KEY"), None) if action is not None else None
    for blender_object in bpy.data.objects:
        if blender_object.type != "MESH" or blender_object.data.shape_keys is None:
            continue

        if not any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in blender_object.modifiers):
            continue

        key = blender_object.data.shape_keys
        key_data = key.animation_data or key.animation_data_create()
        if key_data.action != (action if key_slot is not None else None):
            key_data.action = action if key_slot is not None else None
            # An animation without facial data leaves the face at rest, not at the last frame of the one played before
            if key_data.action is None:
                for block in list(key.key_blocks)[1:]:
                    block.value = 0.0

        if key_slot is not None and key_data.action_slot != key_slot:
            key_data.action_slot = key_slot


# Exporting

_TYPE_KINDS = {type_name: kind for kind, type_name in KIND_TYPES.items()}
_CONTAINER_ROLES = {"rigid_bodies": "body", "exit_points": "exit_point", "collision_hulls": "hull"}


def role_of(blender_object: bpy.types.Object) -> typing.Optional[str]:
    """What the object is in its model: its Twin Tech type, the kind it was imported as, or what its container holds."""
    container = properties.get(blender_object)
    if container is not None and container.type in _TYPE_KINDS:
        return _TYPE_KINDS[container.type]

    kind = blender_object.get(KIND_PROPERTY)
    if kind:
        return kind

    parent = blender_object.parent
    return _CONTAINER_ROLES.get(parent.get(KIND_PROPERTY, "")) if parent is not None else None


def find_root(blender_object: typing.Optional[bpy.types.Object]) -> typing.Optional[bpy.types.Object]:
    while blender_object is not None:
        if blender_object.get(ROOT_PROPERTY):
            return blender_object

        blender_object = blender_object.parent

    return None


def _descendants(blender_object: bpy.types.Object) -> typing.List[bpy.types.Object]:
    result = []
    for child in blender_object.children:
        result.append(child)
        result.extend(_descendants(child))

    return result


def export_file(root: bpy.types.Object, path: str) -> typing.List[str]:
    """Writes the model to the file. Returns what the file doesn't have of the scene, to warn about."""
    kind = root.get(KIND_PROPERTY, "")
    file = tlm.TlmFile(root.get(ROOT_PROPERTY, ""), root.name)
    materials = _FileMaterials(file)
    tree = tlm.node(kind, root.name, _write_data(root))
    descendants = _descendants(root)
    warnings = _unapplied_modifiers(descendants)
    _export_notes.clear()
    from . import tlm_scenery

    if kind == "ogi":
        _export_ogi(file, root, tree, descendants, materials)
    elif kind in TREE_KINDS:
        tlm_scenery.export_children(file, root, tree, materials)
    elif kind == "collision":
        collision = next((child for child in descendants if role_of(child) == "collision" and child.type == "MESH"), None)
        if collision is not None:
            tree["data"] = _write_data(collision)
            tlm_scenery._export_collision(file, collision, tree)
    elif kind == "save_icon":
        _export_save_icon(file, root, tree, descendants, materials)
    else:
        mesh_object = next((child for child in descendants if child.type == "MESH"), None)
        if mesh_object is not None:
            skinned = kind in SKINNED_KINDS
            tree["mesh"] = tlm_mesh.to_parts(file, _read_mesh(mesh_object, materials if kind != "model" else None, skinned), skinned)

    if not tree.get("data"):
        tree.pop("data", None)

    file.root = tree
    file.save(path)
    warnings.extend(_export_notes)
    _export_notes.clear()
    return warnings


def _export_save_icon(file: tlm.TlmFile, root: bpy.types.Object, tree: typing.Dict[str, typing.Any], descendants: typing.List[bpy.types.Object],
                      materials: "_FileMaterials") -> None:
    """The icon's mesh where it is under the root, its shape keys as its shapes and their curves as its animation."""
    meta = json.loads(root.get(SAVE_ICON_PROPERTY, "{}") or "{}")
    mesh_object = next((child for child in descendants if child.type == "MESH"), None)
    key = mesh_object.data.shape_keys if mesh_object is not None else None
    if mesh_object is not None:
        mesh = _read_mesh(mesh_object, materials, False, shaped=True)
        _bake_transform(mesh, _relative_to_root(root, mesh_object))
        tree["mesh"] = tlm_mesh.to_parts(file, mesh, False, shaped=True)

    blocks = list(key.key_blocks) if key is not None else []
    curves: typing.Dict[str, typing.Any] = {}
    action = key.animation_data.action if key is not None and key.animation_data is not None else None
    if action is not None:
        slot = key.animation_data.action_slot or next((slot for slot in action.slots if slot.target_id_type == "KEY"), None)
        bag = _existing_channelbag(action, slot) if slot is not None else None
        if bag is not None:
            curves = {fcurve.data_path: fcurve for fcurve in bag.fcurves}

    def keys_of(shape: int) -> typing.Optional[typing.List[float]]:
        fcurve = curves.get('key_blocks["%s"].value' % bpy.utils.escape_identifier(blocks[shape].name))
        if fcurve is None:
            return None

        return [value for point in fcurve.keyframe_points for value in point.co]

    frames = []
    written = set()
    changed = False
    for frame in meta.get("frames", [{"shape": 0, "keys": [0.0, 1.0]}]):
        shape = int(frame.get("shape", 0))
        if shape == 0:
            frames.append((0, frame.get("keys", [0.0, 1.0])))
            continue

        if shape >= len(blocks):
            changed = changed or "imported" in frame
            continue

        keys = keys_of(shape)
        keys = keys if keys is not None else frame.get("keys", [0.0, blocks[shape].value])
        frames.append((shape, keys))
        written.add(shape)
        changed = changed or keys != frame.get("imported", keys)

    # Shape keys made or animated in Blender after the icon's own
    for shape in range(1, len(blocks)):
        keys = keys_of(shape)
        if shape not in written and (keys is not None or blocks[shape].value != 0.0):
            frames.append((shape, keys if keys is not None else [0.0, blocks[shape].value]))
            changed = True

    # The console draws the shapes times their weights over the weights' sum, Blender the shape keys over the first: the first shape's
    # weight is what the others leave over (save_icon.basis_keys)
    if changed:
        basis = save_icon.basis_keys([keys for shape, keys in frames if shape != 0])
        if any(shape == 0 for shape, _ in frames):
            frames = [(shape, basis if shape == 0 else keys) for shape, keys in frames]
        else:
            frames.insert(0, (0, basis))

        # The browser loops over the frame length, the game's own icon has 1 and plays nothing: keys made in Blender play to the last
        data = tree.get("data") or {}
        last = max((keys[i] for shape, keys in frames if shape != 0 for i in range(0, len(keys) - 1, 2)), default=0.0)
        if int(data.get("FrameLength", 1)) <= 1 and math.ceil(last) > 1:
            data["FrameLength"] = int(math.ceil(last))
            tree["data"] = data
            _read_data(root, KIND_TYPES["save_icon"], data)
            _export_notes.append("%s looped over one frame, its Frame Length is %d now, where its last key is, so the animation plays"
                                 % (root.name, data["FrameLength"]))

    tree["animation"] = {"frames": [{"shape": shape, "keys": file.write_view(keys, "f32")} for shape, keys in frames]}
    if "exact" in meta:
        tree["exact"] = file.write_view(base64.b64decode(meta["exact"]), "u8")


def _unapplied_modifiers(descendants: typing.List[bpy.types.Object]) -> typing.List[str]:
    """Meshes are written as they are, without their modifiers. The armature's is how the skin gets deformed, everything else would
    be lost."""
    warnings = []
    for blender_object in descendants:
        if blender_object.type != "MESH":
            continue

        names = [modifier.name for modifier in blender_object.modifiers if modifier.type != "ARMATURE" and modifier.show_viewport]
        if len(names) > 0:
            warnings.append("%s is written without its %s modifier%s, apply them to keep what they do" % (blender_object.name, ", ".join(names), "s" if len(names) > 1 else ""))

    return warnings


def _relative_to_root(root: bpy.types.Object, blender_object: bpy.types.Object) -> Matrix:
    return root.matrix_world.inverted() @ blender_object.matrix_world


def _is_identity(matrix: Matrix, translation_tolerance: float = 1e-4, rotation_tolerance: float = 1e-6, scale_tolerance: float = 1e-4) -> bool:
    """Whether the matrix is the identity give or take the rounding errors the transforms of objects go through."""
    translation, rotation, scale = matrix.decompose()
    return translation.length <= translation_tolerance and 1.0 - abs(rotation.w) <= rotation_tolerance and \
        all(abs(value - 1.0) <= scale_tolerance for value in scale)


def _write_transform(tree_node: typing.Dict[str, typing.Any], matrix: Matrix) -> None:
    """The node's transform, none for a matrix that's the identity but for rounding errors."""
    if _is_identity(matrix):
        return

    translation, rotation, scale = matrix.decompose()
    tree_node["translation"] = list(translation)
    if 1.0 - abs(rotation.w) > 1e-7:
        tree_node["rotation"] = list(tlm_math.wxyz_to_xyzw(tuple(rotation)))

    if any(abs(value - 1.0) > 1e-6 for value in scale):
        tree_node["scale"] = list(scale)


def _bake_transform(mesh: tlm_mesh.CornerMesh, matrix: Matrix) -> None:
    """Moves the mesh's positions, normals and shapes by the matrix. Skins have no transform in the file, they're in the model's
    space like the bones' bind poses."""
    if _is_identity(matrix, 1e-6, 1e-9, 1e-6):
        return

    linear = matrix.to_3x3()
    positions = array.array("f")
    for i in range(0, len(mesh.positions), 3):
        positions.extend(matrix @ Vector(mesh.positions[i:i + 3]))

    mesh.positions = positions
    if mesh.corner_normals is not None:
        normal_matrix = linear.inverted_safe().transposed()
        normals = array.array("f")
        for i in range(0, len(mesh.corner_normals), 3):
            normal = normal_matrix @ Vector(mesh.corner_normals[i:i + 3])
            normals.extend(normal.normalized() if normal.length > 1e-12 else normal)

        mesh.corner_normals = normals

    shapes = []
    for shape in mesh.shapes:
        offsets = array.array("f")
        for i in range(0, len(shape), 3):
            offsets.extend(linear @ Vector(shape[i:i + 3]))

        shapes.append(offsets)

    mesh.shapes = shapes


def armature_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((child for child in _descendants(root) if child.type == "ARMATURE"), None)


def shape_object_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((child for child in _descendants(root) if role_of(child) == "shape" and child.type == "MESH"), None)


def joints_of_bones(armature: bpy.types.Object) -> typing.Dict[str, int]:
    """The joint of every bone: the index of its Joint type, bones without one or with one another bone has get the next free ones."""
    result: typing.Dict[str, int] = {}
    taken = set()
    for bone in armature.data.bones:
        container = properties.get(bone)
        if container.type == "Joint" and int(container.joint.index) not in taken:
            result[bone.name] = int(container.joint.index)
            taken.add(result[bone.name])

    next_index = max(taken, default=-1) + 1
    for bone in armature.data.bones:
        if bone.name not in result:
            result[bone.name] = next_index
            taken.add(next_index)
            next_index += 1

    return result


def _rests_of_bones(root: typing.Optional[bpy.types.Object], armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int]) -> typing.Tuple[typing.Dict[int, typing.Any], typing.Dict[int, typing.List[float]]]:
    """Every joint's rest and bind pose in the model's space (the armature's own without a model): the bone's, or the game's the bone was
    imported with while it's still there."""
    armature_matrix = _relative_to_root(root, armature) if root is not None else armature.matrix_world.copy()
    rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]] = {}
    binds: typing.Dict[int, typing.List[float]] = {}
    for bone in armature.data.bones:
        index = joint_of_bone[bone.name]
        bind = tlm_math.to_rows([list(row) for row in armature_matrix @ bone.matrix_local])
        rests[index] = tlm_math.rest_of(tlm_math.from_rows(bind))
        stored = bone.get(BIND_PROPERTY)
        if stored is not None and len(stored) == 16:
            stored_rest = tlm_math.rest_of(tlm_math.from_rows(list(stored)))
            if _same_rest(stored_rest, rests[index]):
                bind, rests[index] = [float(value) for value in stored], stored_rest

        binds[index] = bind

    return rests, binds


def _relative_rests(armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int], rests: typing.Dict[int, typing.Any]) -> typing.Dict[int, typing.Any]:
    parents = {joint_of_bone[bone.name]: joint_of_bone[bone.parent.name] if bone.parent is not None else -1 for bone in armature.data.bones}
    return {index: tlm_math.relative_rest(rests.get(parents[index]), rests[index]) for index in rests}


def _export_ogi(file: tlm.TlmFile, root: bpy.types.Object, tree: typing.Dict[str, typing.Any], descendants: typing.List[bpy.types.Object],
                materials: _FileMaterials) -> None:
    armature = next((child for child in descendants if child.type == "ARMATURE"), None)
    armature_node = tlm.add_child(tree, tlm.node("armature", "Armature"))
    joint_of_bone: typing.Dict[str, int] = {}
    rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]] = {}
    if armature is not None:
        joint_of_bone = joints_of_bones(armature)
        rests, binds = _rests_of_bones(root, armature, joint_of_bone)
        joints = []
        for bone in armature.data.bones:
            index = joint_of_bone[bone.name]
            joint: typing.Dict[str, typing.Any] = {"index": index, "parent": joint_of_bone[bone.parent.name] if bone.parent is not None else -1,
                                                   "name": bone.name, "bind": binds[index]}
            if properties.get(bone).type == "Joint":
                data = _write_data(bone)
                data.pop("Index", None)
                joint["data"] = data

            joints.append(joint)

        armature_node["joints"] = sorted(joints, key=lambda joint: joint["index"])
        armature_node["animations"] = _export_animations(file, root, armature, joint_of_bone, rests, descendants)

    joint_of_group_names = joint_of_bone
    for blender_object in descendants:
        kind = role_of(blender_object)
        if blender_object.type == "MESH" and kind in SKINNED_KINDS:
            groups = {group.index: joint_of_group_names[group.name] for group in blender_object.vertex_groups if group.name in joint_of_group_names}
            skin = tlm.add_child(tree, tlm.node(kind, blender_object.name, _write_data(blender_object) or None))
            mesh = _read_mesh(blender_object, materials, True, groups)
            # The skin is where the object shows it, wherever the object was put under the root
            _bake_transform(mesh, _relative_to_root(root, blender_object))
            skin["mesh"] = tlm_mesh.to_parts(file, mesh, True)

    bodies = tlm.add_child(tree, tlm.node("rigid_bodies", "Rigid Bodies"))
    exit_points = tlm.add_child(tree, tlm.node("exit_points", "Exit Points"))
    hulls = tlm.add_child(tree, tlm.node("collision_hulls", "Collision Hulls"))
    for blender_object in descendants:
        kind = role_of(blender_object)
        if kind == "body" and blender_object.type == "MESH":
            body = tlm.add_child(bodies, tlm.node("body", blender_object.name, _write_data(blender_object)))
            body["joint"], placement = _attached_placement(root, blender_object, armature, joint_of_bone, rests)
            _write_transform(body, placement)
            body["mesh"] = tlm_mesh.to_parts(file, _read_mesh(blender_object, materials, False), False)
        elif kind == "exit_point":
            exit_point = tlm.add_child(exit_points, tlm.node("exit_point", blender_object.name, _write_data(blender_object)))
            exit_point["joint"], placement = _attached_placement(root, blender_object, armature, joint_of_bone, rests)
            _write_transform(exit_point, placement)
            stored = blender_object.get(MATRIX_PROPERTY)
            if stored is not None and len(stored) == 16:
                exit_point["matrix"] = [float(value) for value in stored]
        elif kind == "hull" and blender_object.type == "MESH":
            hull = tlm.add_child(hulls, tlm.node("hull", blender_object.name, _write_data(blender_object) or None))
            hull["joint"], placement = _attached_placement(root, blender_object, armature, joint_of_bone, rests, NO_JOINT)
            _write_transform(hull, placement)
            export_hull(file, blender_object, hull)


def _same_rest(a: typing.Tuple[typing.Any, typing.Any], b: typing.Tuple[typing.Any, typing.Any]) -> bool:
    """Whether a bone still rests where it was imported, give or take the precision Blender keeps bones with. Bones pointing close to
    straight down get their roll a few hundredths of a degree off."""
    moved = sum((x - y) ** 2 for x, y in zip(a[0], b[0])) ** 0.5
    turned = 1.0 - abs(sum(x * y for x, y in zip(a[1], b[1])))
    return moved < 1e-4 and turned < 1e-6


# The joint of a hull that's on none, in the model's space
NO_JOINT = 0xFF


def _attached_joint(blender_object: bpy.types.Object, joint_of_bone: typing.Dict[str, int], default: int = 0) -> int:
    for constraint in blender_object.constraints:
        if constraint.type == "CHILD_OF" and constraint.subtarget in joint_of_bone:
            return joint_of_bone[constraint.subtarget]

    if blender_object.parent_type == "BONE" and blender_object.parent_bone in joint_of_bone:
        return joint_of_bone[blender_object.parent_bone]

    return int(blender_object.get(JOINT_PROPERTY, default))


def _attached_placement(root: bpy.types.Object, blender_object: bpy.types.Object, armature: typing.Optional[bpy.types.Object],
                        joint_of_bone: typing.Dict[str, int], rests: typing.Dict[int, typing.Any], default_joint: int = 0) -> typing.Tuple[int, Matrix]:
    """The joint a body, exit point or hull follows and where it is in the joint's space: where the object would be with the armature
    at rest, whichever way it follows its bone, relative to the joint's rest. Without a joint it's relative to the model."""
    joint = _attached_joint(blender_object, joint_of_bone, default_joint)
    parent = blender_object.parent
    unconstrained = parent.matrix_world @ blender_object.matrix_parent_inverse @ blender_object.matrix_basis if parent is not None else blender_object.matrix_basis.copy()
    rest_world = None
    if armature is not None:
        constraint = next((constraint for constraint in blender_object.constraints
                           if constraint.type == "CHILD_OF" and constraint.target == armature and constraint.subtarget in joint_of_bone), None)
        if constraint is not None:
            # The constraint puts the object at the bone's pose, as the pose it was set up in moved
            bone = armature.data.bones[constraint.subtarget]
            rest_world = armature.matrix_world @ bone.matrix_local @ constraint.inverse_matrix @ unconstrained
        elif blender_object.parent_type == "BONE" and parent == armature and blender_object.parent_bone in joint_of_bone:
            # Blender parents to the bone's tail
            bone = armature.data.bones[blender_object.parent_bone]
            rest_world = armature.matrix_world @ bone.matrix_local @ Matrix.Translation((0.0, bone.length, 0.0)) @ blender_object.matrix_parent_inverse @ blender_object.matrix_basis

    if rest_world is None:
        rest_world = blender_object.matrix_world

    relative = root.matrix_world.inverted() @ rest_world
    rest = rests.get(joint)
    if rest is None:
        return joint, relative

    return joint, Matrix(tlm_math.compose(rest[0], rest[1])).inverted() @ relative


def _animation_meta(action: bpy.types.Action) -> typing.Dict[str, typing.Any]:
    try:
        meta = json.loads(action.get(ANIMATION_PROPERTY, "{}"))
    except ValueError:
        meta = {}

    return meta if isinstance(meta, dict) else {}


def _owned_actions(root: typing.Optional[bpy.types.Object], armature: bpy.types.Object, bone_names: typing.Iterable[str]) -> typing.List[bpy.types.Action]:
    """The actions of the model: the ones imported with it, the ones retargeted to its armature and the ones made in Blender that
    animate its bones."""
    owners = {root.get(UID_PROPERTY) if root is not None else None, armature.get(UID_PROPERTY)} - {None}
    names = set(bone_names)
    result = []
    for action in bpy.data.actions:
        owner = action.get(OWNER_PROPERTY)
        if owner is not None:
            if owner in owners:
                result.append(action)

            continue

        slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
        bag = _existing_channelbag(action, slot) if slot is not None else None
        if bag is not None and any(_bone_of_path(fcurve.data_path) in names for fcurve in bag.fcurves):
            result.append(action)

    # In the order the file had them, the ones made in Blender after those
    return sorted(result, key=lambda action: (int(_animation_meta(action).get("order", 1 << 30)), action.name))


def _bone_of_path(data_path: str) -> typing.Optional[str]:
    prefix = 'pose.bones["'
    if not data_path.startswith(prefix):
        return None

    end = data_path.find('"]', len(prefix))
    return data_path[len(prefix):end].replace('\\"', '"').replace("\\\\", "\\") if end >= 0 else None


def _sample(fcurve: typing.Any, frames: int, default: float) -> typing.List[float]:
    if fcurve is None:
        return [default] * frames

    points = fcurve.keyframe_points
    count = len(points)
    if count == frames:
        values = array.array("f", [0.0] * count * 2)
        points.foreach_get("co", values)
        # Keys on every frame are read as they are
        if all(values[i * 2] == float(i) for i in range(count)):
            return list(values[1::2])

    return [fcurve.evaluate(frame) for frame in range(frames)]


def _export_animations(file: tlm.TlmFile, root: bpy.types.Object, armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int],
                       rests: typing.Dict[int, typing.Any], descendants: typing.List[bpy.types.Object]) -> typing.List[typing.Dict[str, typing.Any]]:
    relative_rests = _relative_rests(armature, joint_of_bone, rests)
    shape_object = next((child for child in descendants if role_of(child) == "shape" and child.type == "MESH"), None)
    key_blocks = list(shape_object.data.shape_keys.key_blocks)[1:] if shape_object is not None and shape_object.data.shape_keys is not None else []
    result = []
    for action in _owned_actions(root, armature, joint_of_bone):
        meta = _animation_meta(action)
        start, end = (int(round(value)) for value in action.frame_range)
        frames = int(meta["frames"]) if "frames" in meta and end == int(meta.get("frame_end", -1)) else max(1, end - start + 1)

        animation: typing.Dict[str, typing.Any] = {"name": action.name, "fps": int(meta.get("fps", bpy.context.scene.render.fps)), "frames": frames}
        for key in ("id", "joint_count"):
            if key in meta:
                animation[key] = meta[key]

        if "exact" in meta:
            animation["exact"] = file.write_view(base64.b64decode(meta["exact"]), "u8")

        slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
        bag = _existing_channelbag(action, slot) if slot is not None else None
        fcurves = {(fcurve.data_path, fcurve.array_index): fcurve for fcurve in bag.fcurves} if bag is not None else {}
        joints = []
        for bone in armature.data.bones:
            index = joint_of_bone[bone.name]
            base = 'pose.bones["%s"].' % bpy.utils.escape_identifier(bone.name)
            tracks = {}
            for path, size, default in (("location", 3, 0.0), ("rotation_quaternion", 4, None), ("scale", 3, 1.0)):
                tracks[path] = [_sample(fcurves.get((base + path, component)), frames, (1.0 if component == 0 else 0.0) if default is None else default)
                                for component in range(size)]

            translations, rotations, scales = [], [], []
            for frame in range(frames):
                location = tuple(track[frame] for track in tracks["location"])
                rotation = tuple(track[frame] for track in tracks["rotation_quaternion"])
                scale = tuple(track[frame] for track in tracks["scale"])
                translation, key_rotation, key_scale = tlm_math.pose_to_key(relative_rests[index], location, rotation, scale)
                translations.extend(translation)
                rotations.extend(tlm_math.wxyz_to_xyzw(key_rotation))
                scales.extend(key_scale)

            entry = flags.find_flags(bone, action.name)
            joints.append({
                "joint": index,
                "translation": file.write_view(_constant_or_all(translations, 3), "f32"),
                "rotation": file.write_view(_constant_or_all(rotations, 4), "f32"),
                "scale": file.write_view(_constant_or_all(scales, 3), "f32"),
                "independent_scaling": bool(entry.independent_scaling) if entry is not None else False,
                "additional_rotation": bool(entry.uses_additional_rotation) if entry is not None else False,
            })

        animation["joints"] = sorted(joints, key=lambda joint: joint["joint"])
        facial = meta.get("facial")
        key_slot = next((slot for slot in action.slots if slot.target_id_type == "KEY"), None)
        key_bag = _existing_channelbag(action, key_slot) if key_slot is not None else None
        key_curves = {fcurve.data_path: fcurve for fcurve in key_bag.fcurves} if key_bag is not None else {}
        if not isinstance(facial, dict) and len(key_blocks) > 0 and any(_shape_curve(key_curves, key_blocks, shape) is not None for shape in range(len(key_blocks))):
            # An animation of the shape keys made in Blender
            facial = {"frames": frames, "shapes": 0, "weights": []}

        if isinstance(facial, dict):
            stored_shapes = int(facial.get("shapes", 0))
            # The game blends as many shapes as the model has, the model's shape keys are its shapes
            shapes = len(key_blocks) if len(key_blocks) > 0 else stored_shapes
            facial_frames = int(facial.get("frames", frames))
            stored = facial.get("weights", [])
            stored_frames = len(stored) // stored_shapes if stored_shapes > 0 else 0
            tracks = []
            for shape in range(shapes):
                fcurve = _shape_curve(key_curves, key_blocks, shape) if shape < len(key_blocks) else None
                if fcurve is not None or shape < len(key_blocks) and (shape >= stored_shapes or stored_frames == 0):
                    tracks.append(_sample(fcurve, facial_frames, key_blocks[shape].value))
                elif shape < stored_shapes and stored_frames > 0:
                    # Shapes without a shape key, or shape keys no curve animates, keep the weights the game had for them
                    tracks.append([stored[min(frame, stored_frames - 1) * stored_shapes + shape] for frame in range(facial_frames)])
                else:
                    tracks.append([0.0] * facial_frames)

            animation["facial"] = dict({key: value for key, value in facial.items() if key != "weights"}, shapes=shapes,
                                       weights=file.write_view([track[frame] for frame in range(facial_frames) for track in tracks], "f32"))

        result.append(animation)

    return result


def _shape_curve(key_curves: typing.Dict[str, typing.Any], key_blocks: typing.List[typing.Any], shape: int) -> typing.Any:
    """The curve animating a shape key: named after the key, or after the name the key was imported with when it got renamed or the
    mesh got swapped for one with other names."""
    for name in (key_blocks[shape].name, "Shape %d" % shape):
        fcurve = key_curves.get('key_blocks["%s"].value' % bpy.utils.escape_identifier(name))
        if fcurve is not None:
            return fcurve

    return None


def _constant_or_all(values: typing.List[float], size: int) -> typing.List[float]:
    if all(values[i] == values[i % size] for i in range(size, len(values))):
        return values[:size]

    return values


# Retargeting

def _armature_object(blender_object: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return blender_object if blender_object.type == "ARMATURE" else armature_of(blender_object)


def _deformed_by(armature: bpy.types.Object) -> typing.List[bpy.types.Object]:
    """The meshes the armature deforms, through a modifier or by being parented to it."""
    result = []
    for blender_object in bpy.data.objects:
        if blender_object.type != "MESH":
            continue

        if any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in blender_object.modifiers) or blender_object.parent == armature:
            result.append(blender_object)

    return result


def _shape_of_armature(armature: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((blender_object for blender_object in _deformed_by(armature) if blender_object.data.shape_keys is not None), None)


def bone_infos(armature: bpy.types.Object) -> typing.List[retarget.BoneInfo]:
    """Every bone with its parent and the joint index its Joint type gives it."""
    result = []
    for bone in armature.data.bones:
        container = properties.get(bone)
        index = int(container.joint.index) if container is not None and container.type == "Joint" else None
        result.append((bone.name, bone.parent.name if bone.parent is not None else None, index))

    return result


def joint_matches(original: bpy.types.Object, incoming: bpy.types.Object) -> typing.Dict[str, retarget.Match]:
    """Which of the original armature's joints the incoming armature's bones stand for, see retarget.match_joints."""
    return retarget.match_joints(bone_infos(original), bone_infos(incoming))


def assign_joints(original: bpy.types.Object, incoming: bpy.types.Object) -> typing.Dict[str, int]:
    """Makes the incoming armature's bones the original's joints: every bone gets the name and the joint index of the original bone it
    stands for (bones the original doesn't have get the next joint indexes as Joint N), with the joint's settings the original has
    (its react ID and additional rotation). Returns how many bones were matched each way (retarget.BY_INDEX, BY_NAME, BY_ORDER, NEW)."""
    matches = joint_matches(original, incoming)
    bones = incoming.data.bones
    # Names get swapped around, so nothing may take another bone's name before that one gave it up
    for name in matches:
        bones[name].name = "ttt_renaming " + name

    counts: typing.Dict[str, int] = {}
    for name, match in matches.items():
        bone = bones["ttt_renaming " + name]
        bone.name = retarget.joint_name(match)
        data: typing.Dict[str, typing.Any] = {"Index": match[0]}
        if match[1] is not None:
            original_data = _write_data(original.data.bones[match[1]])
            for key in ("Id", "AdditionalAnimationRotation", "Detail"):
                if key in original_data:
                    data[key] = original_data[key]

        _read_data(bone, "Joint", data)
        # The bind the bone was imported with was another joint's
        if match[2] != retarget.BY_INDEX and BIND_PROPERTY in bone:
            del bone[BIND_PROPERTY]

        incoming.pose.bones[bone.name].rotation_mode = "QUATERNION"
        counts[match[2]] = counts.get(match[2], 0) + 1

    return counts


def animations_of(root: bpy.types.Object) -> typing.List[bpy.types.Action]:
    """The model's animations: the actions imported with it, retargeted to it or made in Blender for its bones."""
    armature = armature_of(root)
    if armature is None:
        return []

    return _owned_actions(root, armature, [bone.name for bone in armature.data.bones])


def remove_model(root: bpy.types.Object) -> None:
    """Removes the model's objects with the meshes and armatures only they used, and its collections once they hold nothing else."""
    objects = [root] + _descendants(root)
    collections = {collection.name for blender_object in objects for collection in blender_object.users_collection}
    for blender_object in objects:
        data = blender_object.data
        bpy.data.objects.remove(blender_object, do_unlink=True)
        if data is None or data.users > 0:
            continue

        if isinstance(data, bpy.types.Mesh):
            bpy.data.meshes.remove(data)
        elif isinstance(data, bpy.types.Armature):
            bpy.data.armatures.remove(data)

    _remove_empty_collections(collections)


def _remove_empty_collections(names: typing.Iterable[str]) -> None:
    for name in sorted(set(names)):
        collection = bpy.data.collections.get(name)
        if collection is not None and collection != bpy.context.scene.collection and len(collection.objects) == 0 and len(collection.children) == 0:
            bpy.data.collections.remove(collection)


def retarget_and_replace(context: bpy.types.Context, root: bpy.types.Object, path: str) -> typing.Tuple[bpy.types.Object, int]:
    """Imports the model file and puts it in the model's place: its bones become the model's joints (assign_joints), every animation
    of the model becomes one of its own under the same name (retarget_animations), and the model goes with its own animations and
    the file's, the new model taking its file, name and collection. Returns the new root and how many animations were retargeted."""
    original = armature_of(root)
    if original is None:
        raise ValueError("%s has no armature" % root.name)

    scene = context.scene
    fps, fps_base = scene.render.fps, scene.render.fps_base
    incoming_root = import_file(context, path)
    scene.render.fps, scene.render.fps_base = fps, fps_base
    incoming = armature_of(incoming_root)
    if incoming_root.get(KIND_PROPERTY) != "ogi" or incoming is None:
        remove_model(incoming_root)
        raise ValueError("%s isn't an OGI with an armature" % os.path.basename(path))

    # The file's own animations go, the model's are the ones it plays from now on
    for action in [action for action in bpy.data.actions if action.get(OWNER_PROPERTY) == incoming_root[UID_PROPERTY]]:
        bpy.data.actions.remove(action)

    assign_joints(original, incoming)
    count = retarget_animations(root, incoming_root)
    names: typing.Dict[str, str] = {}
    for action in animations_of(root):
        names[_uid_of(action)] = action.name
        bpy.data.actions.remove(action)

    owner = _uid_of(incoming)
    copies = [action for action in bpy.data.actions if action.get(OWNER_PROPERTY) == owner and action.get(RETARGETED_PROPERTY)]
    first = None
    for action in sorted(copies, key=lambda action: (int(_animation_meta(action).get("order", 1 << 30)), action.name)):
        source = action[RETARGETED_PROPERTY]
        del action[RETARGETED_PROPERTY]
        action[OWNER_PROPERTY] = incoming_root[UID_PROPERTY]
        if source in names:
            action.name = names[source]

        first = first or action

    incoming_root[PATH_PROPERTY] = root.get(PATH_PROPERTY, "")
    incoming_root[ROOT_PROPERTY] = root.get(ROOT_PROPERTY) or incoming_root.get(ROOT_PROPERTY)
    name = root.name
    # Into the model's collections before the model goes, they'd go with it while empty
    _move_to_collections(incoming_root, list(root.users_collection))
    remove_model(root)
    incoming_root.name = name
    _drop_name_numbers(_descendants(incoming_root))
    if first is not None:
        assign_action(incoming, first)
        flags.apply_inherit_scale(incoming)
        scene.frame_start = 0
        scene.frame_end = int(first.frame_end)

    return incoming_root, count


def _drop_name_numbers(objects: typing.List[bpy.types.Object]) -> None:
    """Takes the numbers Blender gave the objects and their data for names the removed model held (armature.001) off again."""
    for blender_object in objects:
        for block, blocks in ((blender_object, bpy.data.objects), (blender_object.data, None)):
            if block is None:
                continue

            base, dot, number = block.name.rpartition(".")
            if dot and number.isdigit() and len(number) == 3 and base:
                blocks = blocks if blocks is not None else (bpy.data.meshes if isinstance(block, bpy.types.Mesh) else bpy.data.armatures if isinstance(block, bpy.types.Armature) else None)
                if blocks is not None and blocks.get(base) is None:
                    block.name = base


def _move_to_collections(root: bpy.types.Object, collections: typing.List[bpy.types.Collection]) -> None:
    """Puts the model into the collections, out of the ones it was in, which go once empty."""
    left = set()
    for blender_object in [root] + _descendants(root):
        own = list(blender_object.users_collection)
        for collection in collections:
            if blender_object.name not in collection.objects:
                collection.objects.link(blender_object)

        for collection in own:
            if collection not in collections:
                collection.objects.unlink(blender_object)
                left.add(collection.name)

    _remove_empty_collections(left)


def retarget_animations(source: bpy.types.Object, target: bpy.types.Object) -> int:
    """Makes the target armature (or model) play the source's animations: a copy of every action of the source becomes the
    target's, in which every bone of the same joint (or the same name) turns from its own rest, in the world, as much as the
    source's turned from its on every frame, and moves as far beyond where its parent's turn leaves it as the source's did (scaled
    by how much bigger the target skeleton is), whatever way its axes point. The shape keys are animated in the same order.
    Rerunning it writes the copies again. Returns how many animations were retargeted."""
    source_armature, target_armature = _armature_object(source), _armature_object(target)
    if source_armature is None or target_armature is None or source_armature == target_armature:
        return 0

    source_root, target_root = find_root(source_armature), find_root(target_armature)
    bones = _matched_bones(source_armature, target_armature)
    original, incoming = _skeleton(source_armature), _skeleton(target_armature)
    scale = retarget.size_ratio(original, incoming, {target_name: source_name for source_name, target_name in bones.items()})
    source_shape = shape_object_of(source_root) if source_root is not None else None
    target_shape = shape_object_of(target_root) if target_root is not None else None
    source_shape, target_shape = source_shape or _shape_of_armature(source_armature), target_shape or _shape_of_armature(target_armature)
    shapes: typing.Dict[str, str] = {}
    if source_shape is not None and target_shape is not None and source_shape.data.shape_keys is not None and target_shape.data.shape_keys is not None:
        for source_block, target_block in zip(list(source_shape.data.shape_keys.key_blocks)[1:], list(target_shape.data.shape_keys.key_blocks)[1:]):
            shapes[source_block.name] = target_block.name

    for target_name in bones.values():
        target_armature.pose.bones[target_name].rotation_mode = "QUATERNION"

    owner = _uid_of(target_armature)
    copies = {action.get(RETARGETED_PROPERTY): action for action in bpy.data.actions if action.get(OWNER_PROPERTY) == owner and action.get(RETARGETED_PROPERTY)}
    actions = [action for action in _owned_actions(source_root, source_armature, bones) if action.get(OWNER_PROPERTY) != owner]
    first = None
    for action in actions:
        source_id = _uid_of(action)
        copy = copies.get(source_id)
        if copy is None:
            copy = action.copy()
            copy[RETARGETED_PROPERTY] = source_id
            del copy[UID_PROPERTY]

        copy[OWNER_PROPERTY] = owner
        _retarget_action(action, copy, bones, original, incoming, scale, source_armature, target_armature, shapes)
        first = first or copy

    if first is not None and (target_armature.animation_data is None or target_armature.animation_data.action is None):
        assign_action(target_armature, first)
        flags.apply_inherit_scale(target_armature)

    return len(actions)


def _matched_bones(source_armature: bpy.types.Object, target_armature: bpy.types.Object) -> typing.Dict[str, str]:
    """The target bone of every source bone: the one of the same joint, else the one of the same name."""
    source_joints = joints_of_bones(source_armature)
    target_joints = joints_of_bones(target_armature)
    target_of_joint = {index: name for name, index in target_joints.items() if properties.get(target_armature.data.bones[name]).type == "Joint"}
    bones: typing.Dict[str, str] = {}
    for name, index in source_joints.items():
        target_name = target_of_joint.get(index) if properties.get(source_armature.data.bones[name]).type == "Joint" else None
        if target_name is None and name in target_joints:
            target_name = name

        if target_name is not None:
            bones[name] = target_name

    return bones


def _skeleton(armature: bpy.types.Object) -> retarget.Skeleton:
    """The armature's bones with their rests in the world, the space it shares with any other armature."""
    result: retarget.Skeleton = {}
    for bone in armature.data.bones:
        translation, rotation, _ = tlm_math.decompose([list(row) for row in armature.matrix_world @ bone.matrix_local])
        result[bone.name] = (bone.parent.name if bone.parent is not None else None, (translation, rotation))

    return result


def _uid_of(block: typing.Any) -> str:
    if not block.get(UID_PROPERTY):
        block[UID_PROPERTY] = uuid.uuid4().hex

    return block[UID_PROPERTY]


_POSE_CHANNELS = (("location", 3, 0.0), ("rotation_quaternion", 4, None), ("rotation_euler", 3, 0.0), ("scale", 3, 1.0))


def _retarget_action(action: bpy.types.Action, copy: bpy.types.Action, bones: typing.Dict[str, str], original: retarget.Skeleton, incoming: retarget.Skeleton,
                     scale: float, source_armature: bpy.types.Object, target_armature: bpy.types.Object, shapes: typing.Dict[str, str]) -> None:
    slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
    bag = _existing_channelbag(action, slot) if slot is not None else None
    start, end = (int(round(value)) for value in action.frame_range)
    frames = list(range(start, max(start, end) + 1))
    # Every bone of the source counts, a bone the target doesn't have still turns everything under it
    poses = _sample_poses(bag, source_armature, list(original), frames) if bag is not None else {}
    retargeted = retarget.retarget_pose(original, incoming, {target_name: source_name for source_name, target_name in bones.items()}, poses, scale)

    copy_slot = next((slot for slot in copy.slots if slot.target_id_type == "OBJECT"), None)
    copy_bag = _channelbag(copy, copy_slot) if copy_slot is not None else None
    if copy_bag is None and slot is None:
        copy_bag = _channelbag(copy, copy.slots.new("OBJECT", target_armature.name))

    if copy_bag is not None:
        # The copy's bone curves are the source's, written again for the target's bones
        for fcurve in [fcurve for fcurve in copy_bag.fcurves if _bone_of_path(fcurve.data_path) is not None]:
            copy_bag.fcurves.remove(fcurve)

        for target_name in bones.values():
            if target_name in retargeted:
                _write_pose_curves(copy_bag, target_name, frames, retargeted[target_name])

    for source_name, target_name in bones.items():
        entry = flags.find_flags(source_armature.data.bones[source_name], action.name)
        if entry is None:
            continue

        target_entry = flags.get_or_add_flags(target_armature.data.bones[target_name], copy)
        target_entry.independent_scaling = entry.independent_scaling
        target_entry.uses_additional_rotation = entry.uses_additional_rotation

    key_slot = next((slot for slot in copy.slots if slot.target_id_type == "KEY"), None)
    key_bag = _existing_channelbag(copy, key_slot) if key_slot is not None else None
    if key_bag is None:
        return

    paths = {'key_blocks["%s"].value' % bpy.utils.escape_identifier(source_name): 'key_blocks["%s"].value' % bpy.utils.escape_identifier(target_name)
             for source_name, target_name in shapes.items()}
    for fcurve in key_bag.fcurves:
        if fcurve.data_path in paths:
            fcurve.data_path = paths[fcurve.data_path]


def _sample_poses(bag: typing.Any, armature: bpy.types.Object, bones: typing.Iterable[str], frames: typing.List[int]) -> typing.Dict[str, retarget.Pose]:
    """Every bone's pose on the frames, read off its curves (the rest where it has none), each component an array over the frames."""
    tracks: typing.Dict[str, typing.Dict[str, typing.List[typing.Any]]] = {}
    for fcurve in bag.fcurves:
        bone = _bone_of_path(fcurve.data_path)
        channel = fcurve.data_path.split("].", 1)[1] if bone is not None and "]." in fcurve.data_path else None
        size = next((size for name, size, _ in _POSE_CHANNELS if name == channel), 0)
        if channel is None or fcurve.array_index >= size:
            continue

        components = tracks.setdefault(bone, {}).get(channel)
        if components is None:
            default = next(default for name, _, default in _POSE_CHANNELS if name == channel)
            components = tracks[bone][channel] = [numpy.full(len(frames), (1.0 if component == 0 else 0.0) if default is None else default) for component in range(size)]

        components[fcurve.array_index] = numpy.array(_samples(fcurve, frames), dtype=numpy.float64)

    result: typing.Dict[str, retarget.Pose] = {}
    for bone in bones:
        channels = tracks.get(bone)
        if channels is None:
            continue

        mode = armature.pose.bones[bone].rotation_mode if bone in armature.pose.bones else "QUATERNION"
        location = tuple(channels["location"]) if "location" in channels else (0.0, 0.0, 0.0)
        scale = tuple(channels["scale"]) if "scale" in channels else (1.0, 1.0, 1.0)
        if "rotation_quaternion" in channels and (mode == "QUATERNION" or "rotation_euler" not in channels):
            rotation = tuple(channels["rotation_quaternion"])
        elif "rotation_euler" in channels:
            euler_mode = mode if mode not in ("QUATERNION", "AXIS_ANGLE") else "XYZ"
            quaternions = numpy.array([tuple(Euler(angles, euler_mode).to_quaternion()) for angles in zip(*channels["rotation_euler"])], dtype=numpy.float64)
            rotation = tuple(quaternions[:, component] for component in range(4))
        else:
            rotation = (1.0, 0.0, 0.0, 0.0)

        result[bone] = (location, rotation, scale)  # type: ignore[assignment]

    return result


def _samples(fcurve: typing.Any, frames: typing.List[int]) -> typing.List[float]:
    points = fcurve.keyframe_points
    if len(points) == len(frames):
        values = array.array("f", [0.0] * len(points) * 2)
        points.foreach_get("co", values)
        # Keys on every frame are read as they are
        if all(values[index * 2] == float(frame) for index, frame in enumerate(frames)):
            return list(values[1::2])

    return [fcurve.evaluate(frame) for frame in frames]


def _write_pose_curves(bag: typing.Any, bone_name: str, frames: typing.List[int], pose: retarget.Pose) -> None:
    """Keys the bone's location, rotation and scale on every frame, linear like the game's, from arrays over the frames."""
    count = len(frames)
    rotation = numpy.stack([numpy.broadcast_to(numpy.asarray(component, dtype=numpy.float64), (count,)) for component in pose[1]], axis=-1)
    # Keys turning the long way round between two frames get the other sign
    if count > 1:
        flips = numpy.cumprod(numpy.where(numpy.sum(rotation[1:] * rotation[:-1], axis=1) < 0.0, -1.0, 1.0))
        rotation[1:] *= flips[:, None]

    location = [numpy.broadcast_to(numpy.asarray(component, dtype=numpy.float64), (count,)) for component in pose[0]]
    scale = [numpy.broadcast_to(numpy.asarray(component, dtype=numpy.float64), (count,)) for component in pose[2]]
    base = 'pose.bones["%s"].' % bpy.utils.escape_identifier(bone_name)
    group = next((group for group in bag.groups if group.name == bone_name), None) or bag.groups.new(bone_name)
    frame_values = numpy.array(frames, dtype=numpy.float32)
    for channel, components in (("location", location), ("rotation_quaternion", [rotation[:, index] for index in range(4)]), ("scale", scale)):
        for index, values in enumerate(components):
            fcurve = bag.fcurves.new(base + channel, index=index)
            fcurve.group = group
            points = fcurve.keyframe_points
            points.add(count)
            co = numpy.empty(count * 2, dtype=numpy.float32)
            co[0::2] = frame_values
            co[1::2] = values
            points.foreach_set("co", co)
            try:
                points.foreach_set("interpolation", numpy.full(count, 1, dtype=numpy.int32))
            except (TypeError, RuntimeError):
                for point in points:
                    point.interpolation = "LINEAR"

            fcurve.update()
