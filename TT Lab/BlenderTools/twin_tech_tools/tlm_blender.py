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
from mathutils import Matrix, Quaternion, Vector

from . import flags
from . import project as projects
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
JOINT_PROPERTY = "ttt_joint"
# The bind pose a bone was imported with, Blender keeps bones by head, tail and roll, which loses precision
BIND_PROPERTY = "ttt_bind"

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
KIND_TYPES = {"ogi": "Ogi", "skin": "Skin", "shape": "BlendSkin", "body": "Body", "exit_point": "ExitPoint", "model": "Model",
              "rigid_model": "RigidModel", "mesh": "Mesh", "scenery": "Scenery", "tree_node": "SceneryTreeNode", "scenery_mesh": "SceneryMesh",
              "scenery_lod": "SceneryLod", "lod_mesh": "LodMesh", "ambient_light": "AmbientLight", "directional_light": "DirectionalLight",
              "point_light": "PointLight", "negative_light": "NegativeLight", "collision": "Collision", "dynamic_scenery": "DynamicScenery",
              "dynamic_model": "DynamicSceneryModel", "skydome": "Skydome", "skydome_mesh": "SkydomeMesh"}
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


def _node_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any]) -> None:
    translation, rotation, scale = blender_object.matrix_basis.decompose()
    if translation.length > 1e-7:
        tree_node["translation"] = list(translation)

    if abs(abs(rotation.w) - 1.0) > 1e-7:
        tree_node["rotation"] = list(tlm_math.wxyz_to_xyzw(tuple(rotation)))

    if any(abs(value - 1.0) > 1e-7 for value in scale):
        tree_node["scale"] = list(scale)


# Materials

def _material_for(project: typing.Optional[projects.Project], entry: typing.Dict[str, typing.Any]) -> bpy.types.Material:
    """The Blender material of one of the file's materials, shared by everything showing the same project material."""
    uri = str(entry.get("uri", ""))
    for material in bpy.data.materials:
        if uri and material.get(URI_PROPERTY) == uri:
            return material

    project_material = project.materials.get(uri) if project is not None else None
    material = bpy.data.materials.new(project_material.name if project_material is not None else str(entry.get("name", "Material")))
    material[URI_PROPERTY] = uri
    show_project_material(material, project)
    return material


def show_project_material(material: bpy.types.Material, project: typing.Optional[projects.Project]) -> None:
    """Draws the material with its project material's texture, multiplied by the vertex colors the way the game does."""
    uri = material.get(URI_PROPERTY, "")
    project_material = project.materials.get(uri) if project is not None else None
    texture = project.texture_of(project_material) if project is not None and project_material is not None else None
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
    if texture is None:
        links.new(brighten.outputs["Result"], shader.inputs["Base Color"])
        if project is not None and uri and project_material is None:
            material.diffuse_color = (1.0, 0.0, 1.0, 1.0)

        return

    image = next((image for image in bpy.data.images if os.path.normpath(bpy.path.abspath(image.filepath)) == os.path.normpath(texture.png_path)), None)
    if image is None:
        image = bpy.data.images.load(texture.png_path, check_existing=True)

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
    """The image as a PNG, the file itself when it's one on the disk that wasn't changed."""
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


def _read_mesh(blender_object: bpy.types.Object, file_materials: typing.Optional[_FileMaterials], skinned: bool,
               joint_of_group: typing.Optional[typing.Dict[int, int]] = None) -> tlm_mesh.CornerMesh:
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
    materials = [_material_for(project, entry) for entry in file.materials]
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
    else:
        _add_mesh_object(root_node.get("name") or file.name, file, dict(root_node, kind="part_mesh"), root, collection, materials, kind in SKINNED_KINDS)

    return root


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
        joint = int(tree_node.get("joint", 0))
        blender_object[JOINT_PROPERTY] = joint
        constraint = blender_object.constraints.new("CHILD_OF")
        constraint.target = armature
        constraint.subtarget = bone_names.get(joint, "")
        # Follows the bone without what the container and the root already do
        constraint.inverse_matrix = root.matrix_basis.inverted()


def _add_exit_point(tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection) -> bpy.types.Object:
    exit_point = _new_object(tree_node.get("name") or "Exit Point", None, parent, collection)
    exit_point.empty_display_type = "ARROWS"
    exit_point.empty_display_size = 0.1
    exit_point[KIND_PROPERTY] = "exit_point"
    _read_data(exit_point, "ExitPoint", tree_node.get("data"))
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
_CONTAINER_ROLES = {"rigid_bodies": "body", "exit_points": "exit_point"}


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


def export_file(root: bpy.types.Object, path: str) -> None:
    kind = root.get(KIND_PROPERTY, "")
    file = tlm.TlmFile(root.get(ROOT_PROPERTY, ""), root.name)
    materials = _FileMaterials(file)
    tree = tlm.node(kind, root.name, _write_data(root))
    descendants = _descendants(root)
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
    else:
        mesh_object = next((child for child in descendants if child.type == "MESH"), None)
        if mesh_object is not None:
            skinned = kind in SKINNED_KINDS
            tree["mesh"] = tlm_mesh.to_parts(file, _read_mesh(mesh_object, materials if kind != "model" else None, skinned), skinned)

    if not tree.get("data"):
        tree.pop("data", None)

    file.root = tree
    file.save(path)


def _relative_to_root(root: bpy.types.Object, blender_object: bpy.types.Object) -> Matrix:
    return root.matrix_world.inverted() @ blender_object.matrix_world


def _export_ogi(file: tlm.TlmFile, root: bpy.types.Object, tree: typing.Dict[str, typing.Any], descendants: typing.List[bpy.types.Object],
                materials: _FileMaterials) -> None:
    armature = next((child for child in descendants if child.type == "ARMATURE"), None)
    armature_node = tlm.add_child(tree, tlm.node("armature", "Armature"))
    joint_of_bone: typing.Dict[str, int] = {}
    rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]] = {}
    if armature is not None:
        armature_matrix = _relative_to_root(root, armature)
        used = {int(properties.get(bone).joint.index) for bone in armature.data.bones if properties.get(bone).type == "Joint"}
        next_index = max(used, default=-1) + 1
        for bone in armature.data.bones:
            container = properties.get(bone)
            if container.type == "Joint":
                joint_of_bone[bone.name] = int(container.joint.index)
            else:
                joint_of_bone[bone.name] = next_index
                next_index += 1

        joints = []
        for bone in armature.data.bones:
            index = joint_of_bone[bone.name]
            bind = tlm_math.to_rows([list(row) for row in armature_matrix @ bone.matrix_local])
            rests[index] = tlm_math.rest_of(tlm_math.from_rows(bind))
            stored = bone.get(BIND_PROPERTY)
            if stored is not None and len(stored) == 16:
                stored_rest = tlm_math.rest_of(tlm_math.from_rows(list(stored)))
                if _same_rest(stored_rest, rests[index]):
                    bind, rests[index] = [float(value) for value in stored], stored_rest

            joint: typing.Dict[str, typing.Any] = {"index": index, "parent": joint_of_bone[bone.parent.name] if bone.parent is not None else -1,
                                                   "name": bone.name, "bind": bind}
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
            skin["mesh"] = tlm_mesh.to_parts(file, _read_mesh(blender_object, materials, True, groups), True)

    bodies = tlm.add_child(tree, tlm.node("rigid_bodies", "Rigid Bodies"))
    exit_points = tlm.add_child(tree, tlm.node("exit_points", "Exit Points"))
    for blender_object in descendants:
        kind = role_of(blender_object)
        if kind == "body" and blender_object.type == "MESH":
            body = tlm.add_child(bodies, tlm.node("body", blender_object.name, _write_data(blender_object)))
            body["joint"] = _attached_joint(blender_object, joint_of_bone)
            _node_transform(blender_object, body)
            body["mesh"] = tlm_mesh.to_parts(file, _read_mesh(blender_object, materials, False), False)
        elif kind == "exit_point":
            exit_point = tlm.add_child(exit_points, tlm.node("exit_point", blender_object.name, _write_data(blender_object)))
            exit_point["joint"] = _attached_joint(blender_object, joint_of_bone)
            _node_transform(blender_object, exit_point)


def _same_rest(a: typing.Tuple[typing.Any, typing.Any], b: typing.Tuple[typing.Any, typing.Any]) -> bool:
    """Whether a bone still rests where it was imported, give or take the precision Blender keeps bones with. Bones pointing close to
    straight down get their roll a few hundredths of a degree off."""
    moved = sum((x - y) ** 2 for x, y in zip(a[0], b[0])) ** 0.5
    turned = 1.0 - abs(sum(x * y for x, y in zip(a[1], b[1])))
    return moved < 1e-4 and turned < 1e-6


def _attached_joint(blender_object: bpy.types.Object, joint_of_bone: typing.Dict[str, int]) -> int:
    for constraint in blender_object.constraints:
        if constraint.type == "CHILD_OF" and constraint.subtarget in joint_of_bone:
            return joint_of_bone[constraint.subtarget]

    if blender_object.parent_type == "BONE" and blender_object.parent_bone in joint_of_bone:
        return joint_of_bone[blender_object.parent_bone]

    return int(blender_object.get(JOINT_PROPERTY, 0))


def _animation_meta(action: bpy.types.Action) -> typing.Dict[str, typing.Any]:
    try:
        meta = json.loads(action.get(ANIMATION_PROPERTY, "{}"))
    except ValueError:
        meta = {}

    return meta if isinstance(meta, dict) else {}


def _owned_actions(root: bpy.types.Object, armature: bpy.types.Object, bone_names: typing.Iterable[str]) -> typing.List[bpy.types.Action]:
    """The actions of the model: the ones imported with it and the ones made in Blender that animate its bones."""
    uid = root.get(UID_PROPERTY)
    names = set(bone_names)
    result = []
    for action in bpy.data.actions:
        owner = action.get(OWNER_PROPERTY)
        if owner is not None:
            if owner == uid:
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
    parents = {joint_of_bone[bone.name]: joint_of_bone[bone.parent.name] if bone.parent is not None else -1 for bone in armature.data.bones}
    relative_rests = {index: tlm_math.relative_rest(rests.get(parents[index]), rests[index]) for index in rests}
    shape_object = next((child for child in descendants if role_of(child) == "shape" and child.type == "MESH"), None)
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
        if isinstance(facial, dict):
            shapes = int(facial.get("shapes", 0))
            facial_frames = int(facial.get("frames", frames))
            stored = facial.get("weights", [])
            key_bag = _existing_channelbag(action, key_slot) if key_slot is not None else None
            key_curves = {fcurve.data_path: fcurve for fcurve in key_bag.fcurves} if key_bag is not None else {}
            key_blocks = shape_object.data.shape_keys.key_blocks if shape_object is not None and shape_object.data.shape_keys is not None else []
            tracks = []
            for shape in range(shapes):
                name = key_blocks[shape + 1].name if shape + 1 < len(key_blocks) else None
                if name is None:
                    stored_frames = len(stored) // shapes if shapes > 0 else 0
                    tracks.append([stored[min(frame, stored_frames - 1) * shapes + shape] if stored_frames > 0 else 0.0 for frame in range(facial_frames)])
                    continue

                fcurve = key_curves.get('key_blocks["%s"].value' % bpy.utils.escape_identifier(name))
                tracks.append(_sample(fcurve, facial_frames, key_blocks[shape + 1].value))

            animation["facial"] = dict({key: value for key, value in facial.items() if key != "weights"},
                                       weights=file.write_view([track[frame] for frame in range(facial_frames) for track in tracks], "f32"))

        result.append(animation)

    return result


def _constant_or_all(values: typing.List[float], size: int) -> typing.List[float]:
    if all(values[i] == values[i % size] for i in range(size, len(values))):
        return values[:size]

    return values
