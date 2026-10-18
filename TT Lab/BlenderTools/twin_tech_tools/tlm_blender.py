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

Blender has everything the way the console shows it, Z up (axes): the root isn't turned, everything under it is written back relative
to it.
"""

import array
import base64
import contextlib
import hashlib
import json
import math
import os
import re
import struct
import tempfile
import typing
import uuid

import bpy
import numpy
from mathutils import Euler, Matrix, Quaternion, Vector

from . import animation_frames
from . import axes
from . import flags
from . import hulls
from . import model_bounds
from . import parts
from . import png
from . import project as projects
from . import retarget
from . import save_icon
from . import schema
from . import skeleton
from . import tlm
from . import tlm_math
from . import tlm_mesh
from . import gs_material
from . import material_settings
from . import material_sync
from . import twintech_properties as properties

def _addon_version() -> str:
    try:
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "blender_manifest.toml"), encoding="utf-8") as manifest:
            found = re.search(r'^version\s*=\s*"([^"]+)"', manifest.read(), re.MULTILINE)
    except OSError:
        found = None

    return found.group(1) if found else "unknown"


ADDON_VERSION = _addon_version()

ROOT_PROPERTY = "ttt_tlm_type"
PATH_PROPERTY = "ttt_tlm_path"
UID_PROPERTY = "ttt_uid"
# On a model root, the revision TT Lab knows what Blender has of its file by: the one TT Lab answered last, "file:<SHA-256>" of the file it
# was read from, "exported:<SHA-256>" of what it was written into the file as without TT Lab (LINK.md)
REVISION_PROPERTY = "ttt_revision"
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
# The rest Blender made of it (bone.matrix_local): the bone is where it was imported while it's still that one, however far Blender's
# precision took it from the bind (bones pointing close to straight forward come back with their roll a tenth of a degree off)
REST_PROPERTY = "ttt_rest"
# The joint's axes a bone's are (axes.aim_of), its own axes without
AIM_PROPERTY = "ttt_aim"
# The length a bone was made with: Blender keeps a bone by its head and tail, whose length read back is a little off, every import of an
# export would move its tail
LENGTH_PROPERTY = "ttt_length"
# The game's matrix of an exit point, TT Lab keeps it while the object is still where it puts it
MATRIX_PROPERTY = "ttt_matrix"
# The game's rotation of a light, which Blender's (axes.frame_rotation) is a rounding error from: written back while it's still that one
LIGHT_ROTATION_PROPERTY = "ttt_light_rotation"
# The box of an OGI's meshes at rest when it was imported: its bounding box is kept while they still have it
BOUNDS_PROPERTY = "ttt_rest_bounds"
# The place a node had among its kind in the file, which the game's files keep things in (an OGI's hulls are found by their place): the
# export writes them in that order, what's made in Blender after them
ORDER_PROPERTY = "ttt_order"
# On a mesh, the vertexes it was imported with: a mesh of the game's isn't decimated however big it is
IMPORTED_VERTICES_PROPERTY = "ttt_imported_vertices"
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

# How an object's frame stands to the game's (axes): the model's space, a joint's frame (what follows a joint) and a light's
MODEL_FRAME = "model"
JOINT_FRAME = "joint"
LIGHT_FRAME = "light"

# The node kinds and the Twin Tech types of the objects they become
KIND_TYPES = {"ogi": "Ogi", "skin": "Skin", "shape": "BlendSkin", "body": "Body", "exit_point": "ExitPoint", "hull": "CollisionHull", "model": "Model",
              "rigid_model": "RigidModel", "mesh": "Mesh", "scenery": "Scenery", "scenery_mesh": "SceneryMesh",
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


def _set_node_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any], frame: str = MODEL_FRAME,
                        aim: axes.Aim = axes.IDENTITY_AIM) -> None:
    translation = tree_node.get("translation", [0.0, 0.0, 0.0])
    rotation = tree_node.get("rotation", [0.0, 0.0, 0.0, 1.0])
    scale = tree_node.get("scale", [1.0, 1.0, 1.0])
    blender_object.rotation_mode = "QUATERNION"
    if frame == JOINT_FRAME:
        # Relative to the bone it follows, in the bone's own axes
        blender_object.location = axes.aimed_vector(axes.joint_point(translation), aim)
        blender_object.rotation_quaternion = axes.aimed_rotation_wxyz(tlm_math.xyzw_to_wxyz(axes.joint_rotation(rotation)), aim)
        blender_object.scale = axes.aimed_scale(scale, aim)
        return

    if frame == LIGHT_FRAME:
        location, turn, size = axes.point(translation), axes.frame_rotation(rotation), tuple(scale)
        if "rotation" in tree_node:
            blender_object[LIGHT_ROTATION_PROPERTY] = [float(value) for value in rotation]
    else:
        location, turn, size = axes.point(translation), axes.rotation(rotation), axes.scale(scale)

    blender_object.location = location
    blender_object.rotation_quaternion = tlm_math.xyzw_to_wxyz(turn)
    blender_object.scale = size


def _set_joint_frame_transform(blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any], aim: typing.Optional[axes.Aim]) -> None:
    """An exit point's transform, relative to the aimed bone it follows (aim) or the model: its axes are the game's through the joints'
    reflection (M G D, G its frame in the model's space) whichever way the bone points, they're what the game turns what it holds by."""
    translation = tree_node.get("translation", [0.0, 0.0, 0.0])
    rotation = tree_node.get("rotation", [0.0, 0.0, 0.0, 1.0])
    blender_object.rotation_mode = "QUATERNION"
    if aim is not None:
        blender_object.location = axes.aimed_vector(axes.joint_point(translation), aim)
        blender_object.rotation_quaternion = axes.exit_rotation_wxyz(axes.joint_rotation_wxyz(tlm_math.xyzw_to_wxyz(rotation)), aim)
    else:
        blender_object.location = axes.point(translation)
        blender_object.rotation_quaternion = tlm_math.xyzw_to_wxyz(axes.frame_rotation(rotation))

    blender_object.scale = tuple(tree_node.get("scale", [1.0, 1.0, 1.0]))


def local_transform(blender_object: bpy.types.Object) -> typing.Tuple[typing.Any, typing.Any, typing.Any]:
    """The object's translation, rotation (w, x, y, z) and scale relative to its parent: its own values while nothing else is in between,
    as they are, which a matrix worked out of them and taken apart again changes by rounding errors."""
    plain = blender_object.matrix_parent_inverse == Matrix.Identity(4) and tuple(blender_object.delta_location) == (0.0, 0.0, 0.0) and \
        tuple(blender_object.delta_scale) == (1.0, 1.0, 1.0) and tuple(blender_object.delta_rotation_quaternion) == (1.0, 0.0, 0.0, 0.0) and \
        tuple(blender_object.delta_rotation_euler) == (0.0, 0.0, 0.0)
    if not plain:
        return (blender_object.matrix_parent_inverse @ blender_object.matrix_basis).decompose()

    mode = blender_object.rotation_mode
    if mode == "QUATERNION":
        rotation = Quaternion(blender_object.rotation_quaternion).normalized() if blender_object.rotation_quaternion.magnitude > 1e-12 else Quaternion()
        # A normalized quaternion is as it was unless it wasn't of unit length
        if abs(blender_object.rotation_quaternion.magnitude - 1.0) < 1e-6:
            rotation = Quaternion(blender_object.rotation_quaternion)
    elif mode == "AXIS_ANGLE":
        angle, *axis = blender_object.rotation_axis_angle
        rotation = Quaternion(axis, angle)
    else:
        rotation = blender_object.rotation_euler.to_quaternion()

    return Vector(blender_object.location), rotation, Vector(blender_object.scale)


def game_transform(blender: typing.Tuple[typing.Any, typing.Any, typing.Any], frame: str = MODEL_FRAME) -> typing.Tuple[typing.List[float], typing.List[float], typing.List[float]]:
    """An object's translation, rotation (w, x, y, z) and scale relative to its parent in the model's space, local_transform, as the
    game's translation, rotation (x, y, z, w) and scale."""
    translation, rotation, scale = blender
    turn = tlm_math.wxyz_to_xyzw(tuple(rotation))
    if frame == JOINT_FRAME:
        return list(axes.joint_point(translation)), list(axes.joint_rotation(turn)), list(scale)

    if frame == LIGHT_FRAME:
        return list(axes.point_back(translation)), list(axes.frame_rotation_back(turn)), list(scale)

    return list(axes.point_back(translation)), list(axes.rotation_back(turn)), list(axes.scale_back(scale))


def _mesh_to_blender(data: tlm_mesh.MeshData, frame: str, aim: axes.Aim = axes.IDENTITY_AIM) -> None:
    """A mesh of the game's space in the object's frame, its triangles facing the way they do there: what follows a joint in its bone's own
    axes. What's kept of the game's stays the game's (twin_normals, twin_shapes)."""
    def convert(values: typing.Sequence[float]) -> typing.List[float]:
        return axes.aimed_points(axes.joint_points(values), aim) if frame == JOINT_FRAME else axes.points(values)

    data.positions = array.array("f", convert(data.positions))
    if data.normals is not None:
        data.normals = array.array("f", convert(data.normals))

    data.twin_shapes = [array.array("f", offsets) for offsets in data.shapes]
    data.shapes = [array.array("f", convert(offsets)) for offsets in data.shapes]
    data.triangles = array.array("i", axes.triangles(data.triangles))


def _mesh_to_game(mesh: tlm_mesh.CornerMesh, frame: str = MODEL_FRAME, aim: axes.Aim = axes.IDENTITY_AIM) -> tlm_mesh.CornerMesh:
    """The mesh read in the object's frame (baked into the model's, or the bone's own axes of what follows a joint) in the game's space,
    the corners of its triangles turned back."""
    def convert(values: typing.Sequence[float]) -> typing.List[float]:
        return axes.joint_points(axes.unaimed_points(values, aim)) if frame == JOINT_FRAME else axes.points_back(values)

    mesh.positions = array.array("f", convert(mesh.positions))
    if mesh.corner_normals is not None:
        mesh.corner_normals = axes.corner_values(convert(mesh.corner_normals), 3)

    if len(mesh.shape_positions) == len(mesh.shapes):
        mesh.shapes = [array.array("f", (value - base for value, base in zip(convert(shape), mesh.positions))) for shape in mesh.shape_positions]
    else:
        mesh.shapes = [array.array("f", convert(offsets)) for offsets in mesh.shapes]
    mesh.corner_vertices = axes.triangles(mesh.corner_vertices)
    mesh.corner_uvs = axes.corner_values(mesh.corner_uvs, 2)
    if mesh.corner_colors is not None:
        mesh.corner_colors = axes.corner_values(mesh.corner_colors, 4)

    if mesh.corner_emit_colors is not None:
        mesh.corner_emit_colors = axes.corner_values(mesh.corner_emit_colors, 4)

    return mesh


# Materials

def _material_for(project: typing.Optional[projects.Project], entry: typing.Dict[str, typing.Any], file: typing.Optional[tlm.TlmFile] = None,
                  refresh: bool = False, blocks: typing.Any = None) -> bpy.types.Material:
    """The Blender material of one of the file's materials, shared by everything of the project showing the same project material: the
    project's settings once it's made, and again when the model is imported again in its place. Another project's of the same URI (blocks:
    materials.ProjectBlocks) stays that project's."""
    uri = str(entry.get("uri", ""))
    if not uri and file is not None and any(isinstance(entry.get(key), (dict, list)) for key in ("image", "data", "images")):
        return _embedded_material(file, entry, project, blocks)

    candidates = [material for material in bpy.data.materials if uri and material.get(URI_PROPERTY) == uri]
    found = blocks.pick(candidates) if blocks is not None else next(iter(candidates), None)
    if found is not None:
        if refresh:
            show_project_material(found, project, entry, blocks)

        return found

    project_material = project.materials.get(uri) if project is not None else None
    material = bpy.data.materials.new(project_material.name if project_material is not None else str(entry.get("name", "Material")))
    material[URI_PROPERTY] = uri
    show_project_material(material, project, entry, blocks)
    return material


def _embedded_material(file: tlm.TlmFile, entry: typing.Dict[str, typing.Any], project: typing.Optional[projects.Project] = None,
                       blocks: typing.Any = None) -> bpy.types.Material:
    """A material the file has with its pictures, which goes back into the file the same way: with its settings when it has any. TT Lab
    never had them, they're all sent."""
    blender_id = str(entry.get("blender_id", ""))
    for material in bpy.data.materials:
        if blender_id and material.get(BLENDER_ID_PROPERTY) == blender_id:
            return material

    material = bpy.data.materials.new(str(entry.get("name", "Material")))
    material[BLENDER_ID_PROPERTY] = blender_id or uuid.uuid4().hex
    data = entry.get("data")
    if isinstance(data, dict):
        images = [_file_image(file, image_entry, material.name) for image_entry in entry.get("images", []) if isinstance(image_entry, dict)]
        material_settings.load(material, data, project, images, blocks=blocks)
        return material

    image = _file_image(file, entry["image"], material.name) if isinstance(entry.get("image"), dict) else None
    material_settings.draw_default(material, image, blended=entry.get("alpha") == "BLEND")
    return material


def _file_image(file: tlm.TlmFile, image_entry: typing.Dict[str, typing.Any], name: str) -> typing.Optional[bpy.types.Image]:
    """A picture the file has, packed into the blend file. One made in Blender is that picture again while it's still around."""
    blender_id = str(image_entry.get("blender_id", ""))
    for image in bpy.data.images:
        if blender_id and image.get(BLENDER_ID_PROPERTY) == blender_id:
            return image

    image = material_settings.packed_image(file.read_view(image_entry.get("png"), "u8").tobytes(), str(image_entry.get("name", name)))
    if image is not None and blender_id:
        image[BLENDER_ID_PROPERTY] = blender_id

    return image


def show_project_material(material: bpy.types.Material, project: typing.Optional[projects.Project], entry: typing.Optional[typing.Dict[str, typing.Any]] = None,
                          blocks: typing.Any = None) -> None:
    """Gives the material its project material's settings, which TT Lab has, and draws them the way the game does, magenta when the project
    doesn't have it. A material TT Lab made of one of Blender's keeps its Blender ID, its pictures theirs."""
    uri = material.get(URI_PROPERTY, "")
    if entry is not None and entry.get("blender_id"):
        material[BLENDER_ID_PROPERTY] = str(entry["blender_id"])

    project_material = project.materials.get(uri) if project is not None and uri else None
    data = project_material.read() if project_material is not None else None
    if data is not None:
        for image_entry in (entry or {}).get("images", []) or []:
            image = material_settings.texture_image(project, str(image_entry.get("uri", "")), blocks) if isinstance(image_entry, dict) else None
            if image is not None and image_entry.get("blender_id"):
                image[material_settings.IMAGE_ID_PROPERTY] = str(image_entry["blender_id"])

        material_settings.load(material, data, project, known=True, blocks=blocks)
        return

    material_settings.draw_default(material, None)
    if project is not None and uri:
        material.diffuse_color = (1.0, 0.0, 1.0, 1.0)


# What a material sent into model files TT Lab hasn't read yet (material_sync), how many sends it made and the one TT Lab took last
SENT_PROPERTY = "ttt_sent"
SENT_SEQUENCE_PROPERTY = "ttt_sent_sequence"
BASE_SEQUENCE_PROPERTY = "ttt_base_sequence"


def _load_tlm(path: str) -> typing.Optional[tlm.TlmFile]:
    if not path or not os.path.exists(path):
        return None

    try:
        return tlm.TlmFile.load(path)
    except (OSError, tlm.TlmError, ValueError):
        return None


class _FileMaterials:
    """The materials of the file being written, by the Blender materials showing them: a project material Blender changed with what it
    changed since TT Lab had it (gs_material.changes), one made in Blender with all of its settings, the pictures TT Lab doesn't have as
    Blender shows them with them."""

    def __init__(self, file: tlm.TlmFile, path: str = "", linked: bool = False):
        self.file = file
        self.path = path
        # Written through TT Lab, which has every material and picture the link made or changed already
        self.linked = linked
        self.project = projects.open_project(path) if path else None
        self._indexes: typing.Dict[str, int] = {}
        self._sent: typing.List[typing.Tuple[bpy.types.Material, typing.Dict[str, typing.Any], typing.Dict[str, str]]] = []
        self._pngs: typing.Dict[int, typing.Optional[bytes]] = {}
        self._lighting_emit_colors: typing.Set[int] = set()
        if path and not linked:
            self._take_what_tt_lab_read()

    def _take_what_tt_lab_read(self) -> None:
        """TT Lab writes a file it read again without what it took: what was sent into the file is what TT Lab has, the base from now on.
        The materials and pictures TT Lab made of Blender's are its assets from now on."""
        previous = _load_tlm(self.path)
        if previous is None or not material_sync.read_by_tt_lab(previous.json):
            return

        from . import materials as blender_materials

        # Another project's of the same URI isn't the one written
        blocks = blender_materials.ProjectBlocks(self.project.root if self.project is not None else projects.find_root(self.path))
        for entry in previous.materials:
            if not isinstance(entry, dict):
                continue

            uri = str(entry.get("uri", ""))
            blender_id = str(entry.get("blender_id", ""))
            material = blocks.pick(material for material in bpy.data.materials if uri and material.get(URI_PROPERTY) == uri) or \
                next((material for material in bpy.data.materials if blender_id and material.get(BLENDER_ID_PROPERTY) == blender_id
                      and not material.get(URI_PROPERTY)), None)
            if material is None:
                continue

            if uri and not material.get(URI_PROPERTY):
                material[URI_PROPERTY] = uri

            for image_entry in entry.get("images", []) or []:
                image = next((image for image in bpy.data.images if isinstance(image_entry, dict) and image_entry.get("blender_id")
                              and image.get(material_settings.IMAGE_ID_PROPERTY) == image_entry["blender_id"]), None)
                if image is not None and image_entry.get("uri"):
                    image[material_settings.TEXTURE_URI_PROPERTY] = str(image_entry["uri"])

            text, record = material_sync.taken(str(material.get(SENT_PROPERTY, "")), self.path, int(material.get(BASE_SEQUENCE_PROPERTY, -1)))
            material[SENT_PROPERTY] = text
            if record is None:
                continue

            material_settings.confirm(material, record.get("data", {}))
            material[BASE_SEQUENCE_PROPERTY] = int(record.get("seq", 0))
            # The material's own pictures first: another project's of the same texture is that project's
            own = [item.image for item in material_settings.settings_of(material).shaders if item.image is not None] if material_settings.is_managed(material) else []
            for identity, digest in (record.get("pictures") or {}).items():
                image = next((image for image in own if material_settings.picture_identity(image) == identity), None) or blocks.pick(_images_of_identity(identity))
                if image is not None:
                    image[material_settings.PNG_CONFIRMED_PROPERTY] = digest

    def index_of(self, material: typing.Optional[bpy.types.Material]) -> int:
        if material is None:
            return -1

        key = material.name
        if key in self._indexes:
            return self._indexes[key]

        entry = self._entry(material)
        self.file.materials.append(entry)
        self._indexes[key] = len(self.file.materials) - 1
        if emit_lighting_shader(material, self.project) is not None:
            self._lighting_emit_colors.add(self._indexes[key])

        return self._indexes[key]

    def lights_emit_colors(self, index: int) -> bool:
        """Whether the file's material of the index has a shader lighting rigid parts by their emit colors, as TT Lab has it once it reads
        the file."""
        return index in self._lighting_emit_colors

    def _entry(self, material: bpy.types.Material) -> typing.Dict[str, typing.Any]:
        entry: typing.Dict[str, typing.Any] = {"name": material.name}
        uri = material.get(URI_PROPERTY, "")
        if uri:
            entry["uri"] = uri

        if not material_settings.is_managed(material):
            if not uri:
                self._embed(material, entry)

            return entry

        # TT Lab finds a material of Blender's again by it, after it was deleted in TT Lab too
        if material.get(BLENDER_ID_PROPERTY) or not uri:
            self._blender_id(material, entry)

        # The link's TT Lab has it as Blender shows it, one the link didn't make yet comes with the file
        if self.linked and uri:
            return entry

        images: typing.List[bpy.types.Image] = []
        shown = material_settings.shown(material, images, self._sends)
        settings = material_settings.settings_of(material)
        data, changed, sources = gs_material.changes(material_settings.base_of(material), shown, settings.known)
        if uri and settings.known and not changed and sources is None and not any(self._sends(image) for image in images):
            return entry

        entry["data"] = data
        if changed is not None:
            entry["changed"] = changed

        if sources is not None:
            entry["shader_sources"] = sources

        pictures = self._add_images(entry, images)
        self._sent.append((material, gs_material.snapshot(data, shown), pictures))
        return entry

    def _sends(self, image: bpy.types.Image) -> bool:
        """Whether TT Lab doesn't have the picture as Blender shows it."""
        return self._png_hash(image) != image.get(material_settings.PNG_CONFIRMED_PROPERTY, "")

    def _add_images(self, entry: typing.Dict[str, typing.Any], images: typing.List[bpy.types.Image]) -> typing.Dict[str, str]:
        pictures: typing.Dict[str, str] = {}
        if len(images) == 0:
            return pictures

        entry["images"] = []
        for image in images:
            identity = material_settings.picture_identity(image, True)
            image_entry: typing.Dict[str, typing.Any] = {"name": image.name}
            if image.get(material_settings.IMAGE_ID_PROPERTY):
                image_entry["blender_id"] = image[material_settings.IMAGE_ID_PROPERTY]

            if image.get(material_settings.TEXTURE_URI_PROPERTY):
                image_entry["uri"] = image[material_settings.TEXTURE_URI_PROPERTY]

            if self._sends(image):
                png = self._png(image) or b""
                image_entry["png"] = self.file.write_view(png, "u8")
                pictures[identity] = hashlib.sha256(png).hexdigest()

            entry["images"].append(image_entry)

        return pictures

    def _png(self, image: bpy.types.Image) -> typing.Optional[bytes]:
        key = image.as_pointer()
        if key not in self._pngs:
            self._pngs[key] = _png_bytes(image)

        return self._pngs[key]

    def _png_hash(self, image: bpy.types.Image) -> str:
        return hashlib.sha256(self._png(image) or b"").hexdigest()

    def commit(self) -> None:
        """Once the file is written: what was sent into it, which TT Lab takes when it reads the file."""
        for material, data, pictures in self._sent:
            sequence = int(material.get(SENT_SEQUENCE_PROPERTY, 0)) + 1
            material[SENT_SEQUENCE_PROPERTY] = sequence
            material[SENT_PROPERTY] = material_sync.with_sent(str(material.get(SENT_PROPERTY, "")), self.path, sequence, data, pictures)

    def _blender_id(self, material: bpy.types.Material, entry: typing.Dict[str, typing.Any]) -> None:
        if not material.get(BLENDER_ID_PROPERTY):
            material[BLENDER_ID_PROPERTY] = uuid.uuid4().hex

        entry["blender_id"] = material[BLENDER_ID_PROPERTY]

    def _embed(self, material: bpy.types.Material, entry: typing.Dict[str, typing.Any]) -> None:
        """A material without the add-on's settings (a save icon's) goes into the file with its picture."""
        self._blender_id(material, entry)
        if getattr(material, "surface_render_method", "") == "BLENDED" or getattr(material, "blend_method", "") == "BLEND":
            entry["alpha"] = "BLEND"

        image = next((node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image is not None), None) \
            if material.use_nodes and material.node_tree is not None else None
        png = _png_bytes(image) if image is not None else None
        if png is not None:
            entry["image"] = {"png": self.file.write_view(png, "u8"), "name": image.name}


def _images_of_identity(identity: str) -> typing.List[bpy.types.Image]:
    kind, _, value = identity.partition(":")
    key = {"blender": material_settings.IMAGE_ID_PROPERTY, "uri": material_settings.TEXTURE_URI_PROPERTY}.get(kind)
    return [image for image in bpy.data.images if key and image.get(key) == value]



def _png_bytes(image: bpy.types.Image) -> typing.Optional[bytes]:
    """The image as a PNG, the file itself when it's one on the disk or packed that wasn't changed, else its pixels as they are (a
    copy of a picture painted in Blender and never saved was blank)."""
    if image.packed_file is not None and not image.is_dirty:
        packed = bytes(image.packed_file.data)
        if packed.startswith(b"\x89PNG"):
            return packed

    path = bpy.path.abspath(image.filepath) if image.filepath else ""
    if image.packed_file is None and not image.is_dirty and path.lower().endswith(".png") and os.path.exists(path):
        with open(path, "rb") as file:
            return file.read()

    width, height = image.size
    if width == 0 or height == 0:
        return None

    pixels = numpy.empty(width * height * 4, dtype=numpy.float32)
    image.pixels.foreach_get(pixels)
    if image.is_float and not image.colorspace_settings.is_data:
        # Float pictures keep linear values, PNGs the display's
        color = pixels.reshape(-1, 4)[:, :3]
        color[:] = numpy.where(color <= 0.0031308, color * 12.92, 1.055 * numpy.power(numpy.maximum(color, 0.0), 1.0 / 2.4) - 0.055)

    values = numpy.clip(numpy.round(pixels * 255.0), 0, 255).astype(numpy.uint8).reshape(height, width * 4)
    # Blender's rows go from the bottom
    return png.encode(width, height, values[::-1].tobytes())


# Meshes

def _build_mesh(name: str, file: tlm.TlmFile, mesh_node: typing.Optional[typing.Dict[str, typing.Any]], skinned: bool,
                materials: typing.List[bpy.types.Material], frame: str = MODEL_FRAME, aim: axes.Aim = axes.IDENTITY_AIM) -> typing.Tuple[bpy.types.Mesh, tlm_mesh.MeshData]:
    data = tlm_mesh.from_parts(file, mesh_node, skinned)
    _mesh_to_blender(data, frame, aim)
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
    mesh[IMPORTED_VERTICES_PROPERTY] = vertex_count
    mesh.shade_smooth()
    if data.normals is not None:
        mesh.normals_split_custom_set_from_vertices([tuple(data.normals[i:i + 3]) for i in range(0, len(data.normals), 3)])

    return mesh, data


def _add_mesh_object(name: str, file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection,
                     materials: typing.List[bpy.types.Material], skinned: bool, armature: typing.Optional[bpy.types.Object] = None,
                     bone_names: typing.Optional[typing.Dict[int, str]] = None, frame: str = MODEL_FRAME,
                     aim: axes.Aim = axes.IDENTITY_AIM) -> bpy.types.Object:
    mesh, data = _build_mesh(name, file, tree_node.get("mesh"), skinned, materials, frame, aim)
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
                blender_object.data.attributes.new(TWIN_SHAPE_ATTRIBUTE + str(index), "FLOAT_VECTOR", "POINT").data.foreach_set("vector", data.twin_shapes[index])
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


# The game's biggest skin has 2716 vertexes before its strips (Cortex), meshes made in Blender past this (or grown past it, or past what they
# were imported with) get decimated on export
MAX_EXPORT_VERTICES = 4096


def is_too_big(mesh: bpy.types.Mesh) -> bool:
    return len(mesh.vertices) > max(MAX_EXPORT_VERTICES, int(mesh.get(IMPORTED_VERTICES_PROPERTY, 0)))


def natural_key(name: str) -> typing.Tuple[typing.Tuple[int, int, str], ...]:
    """A name's order with its numbers counted, Hull 2 before Hull 10."""
    return tuple((0, int(part), "") if part.isdigit() else (1, 0, part.lower()) for part in re.split(r"(\d+)", name) if part)


def in_file_order(objects: typing.Iterable[bpy.types.Object]) -> typing.List[bpy.types.Object]:
    """The objects in the order the file had them, the ones made in Blender after them by their names (Blender lists children by their
    names, which put Hull 10 before Hull 2)."""
    def key(blender_object: bpy.types.Object) -> typing.Tuple[int, int, typing.Tuple[typing.Tuple[int, int, str], ...]]:
        order = blender_object.get(ORDER_PROPERTY)
        return (0, int(order), ()) if isinstance(order, int) else (1, 0, natural_key(blender_object.name))

    return sorted(objects, key=key)

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
    if not is_too_big(mesh):
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
    colors = vertex_colors_of(mesh)
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
    elif emit is not None and emit.domain == "CORNER":
        values = array.array("f", [0.0] * len(emit.data) * 4)
        emit.data.foreach_get("color_srgb", values)
        result.corner_emit_colors = [value for loop in triangle_loops for value in values[loop * 4:loop * 4 + 4]]

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
            result.shape_positions.append(shape)
            result.shapes.append(array.array("f", (value - base for value, base in zip(shape, basis))))
            result.twin_shapes.append(_attribute(mesh, TWIN_SHAPE_ATTRIBUTE + str(index), "vector", vertex_count, 3, "f"))

    try:
        result.parts = json.loads(mesh.get(PARTS_PROPERTY, "[]"))
    except ValueError:
        result.parts = []

    result.slot_materials = [file_materials.index_of(slot.material) if file_materials is not None else -1 for slot in blender_object.material_slots]
    if file_materials is not None:
        result.emit_slots = {slot for slot, index in enumerate(result.slot_materials) if file_materials.lights_emit_colors(index)}

    return result


def vertex_colors_of(mesh: bpy.types.Mesh) -> typing.Any:
    """The color attribute written as the mesh's vertex colors: Color, or the active one of a mesh made in Blender, never the emit colors."""
    colors = mesh.color_attributes.get(COLOR_ATTRIBUTE)
    if colors is not None:
        return colors

    active = mesh.color_attributes.active_color
    return active if active is not None and active.name != EMIT_COLOR_ATTRIBUTE else None


def _attribute(mesh: bpy.types.Mesh, name: str, value_name: str, count: int, size: int, typecode: str) -> typing.Optional[array.array]:
    attribute = mesh.attributes.get(name)
    if attribute is None or len(attribute.data) != count:
        return None

    values = array.array(typecode, [0] * count * size)
    attribute.data.foreach_get(value_name, values)
    return values


# Importing

def use_standard_view(context: bpy.types.Context) -> None:
    """Shows the game's colors as they are: Blender's default view transform changes them, Standard shows the display's values that
    the materials work out."""
    settings = context.scene.view_settings
    if settings.view_transform != "Standard":
        settings.view_transform = "Standard"
        settings.look = "None"


def import_file(context: bpy.types.Context, path: str, refresh_materials: bool = False, file: typing.Optional[tlm.TlmFile] = None) -> bpy.types.Object:
    """The file's model, its project materials shared with the scene's other models (and given the project's settings again when the model
    is imported again in its place). Given the file read elsewhere (TT Lab's model as it has it), it's the model of the path all the same."""
    revision = ""
    if file is None:
        with open(path, "rb") as source:
            data = source.read()

        file = tlm.TlmFile.from_bytes(data)
        revision = "file:" + hashlib.sha256(data).hexdigest()

    use_standard_view(context)
    # What the project has now: TT Lab may have made assets since the last import
    project = projects.open_project(path, refresh=True)
    collection = bpy.data.collections.new(file.name or os.path.splitext(os.path.basename(path))[0])
    context.scene.collection.children.link(collection)
    from . import materials as blender_materials

    # Another project of the same name has the same URIs: what its models draw stays theirs
    blocks = blender_materials.ProjectBlocks(project.root if project is not None else projects.find_root(path))
    materials = [_material_for(project, entry, file, refresh_materials, blocks) for entry in file.materials]
    root_node = file.root or {"kind": "model"}
    kind = root_node.get("kind", "")
    root = _new_object(root_node.get("name") or file.name, None, None, collection)
    root.empty_display_type = "PLAIN_AXES"
    root[ROOT_PROPERTY] = file.asset_type
    root[PATH_PROPERTY] = path
    root[UID_PROPERTY] = uuid.uuid4().hex
    root[KIND_PROPERTY] = kind
    if revision:
        root[REVISION_PROPERTY] = revision

    type_name = KIND_TYPES.get(kind)
    if type_name is not None and kind != "collision":
        _read_data(root, type_name, root_node.get("data"))

    from . import tlm_scenery

    if kind == "ogi":
        bounds = model_bounds.rest_bounds(file, root_node)
        if bounds is not None:
            root[BOUNDS_PROPERTY] = json.dumps(bounds)

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
            mesh.attributes.new(TWIN_SHAPE_ATTRIBUTE + str(index), "FLOAT_VECTOR", "POINT").data.foreach_set("vector", data.twin_shapes[index])
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
    bone_rests = {index: axes.bone_matrix(tlm_math.compose(translation, rotation)) for index, (translation, rotation) in rests.items()}
    # Bones point down their limbs, as long as to their child, or are what the file's bones were in Blender
    frames = axes.bone_frames(bone_rests, {int(joint.get("index", i)): int(joint.get("parent", -1)) for i, joint in enumerate(joints)},
                              _stored_bones(joints))
    aims: typing.Dict[int, axes.Aim] = {}
    lengths: typing.Dict[int, float] = {}
    for i, joint in enumerate(joints):
        index = int(joint.get("index", i))
        edit_bone = armature_data.edit_bones.new(str(joint.get("name") or "Joint %d" % index))
        aims[index], length = frames[index]
        lengths[index] = length
        edit_bone.head = (0.0, 0.0, 0.0)
        edit_bone.tail = (0.0, length, 0.0)
        edit_bone.matrix = Matrix(axes.aimed(bone_rests[index], aims[index]))
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
            bone[REST_PROPERTY] = [float(value) for row in bone.matrix_local for value in row]
        bone[AIM_PROPERTY] = list(aims[index])
        bone[LENGTH_PROPERTY] = float(lengths[index])
        armature.pose.bones[bone.name].rotation_mode = "QUATERNION"

    shape_object = None
    for order, child in enumerate(tlm.children(root_node)):
        kind = child.get("kind")
        if kind in SKINNED_KINDS:
            skin = _add_mesh_object(child.get("name") or kind, file, child, root, collection, materials, True, armature, bone_names)
            skin[ORDER_PROPERTY] = order
            if kind == "shape":
                shape_object = skin
        elif kind == "rigid_bodies":
            _import_attached(child, "body", root, armature, bone_names, aims, collection, lambda node, parent, frame, aim: _add_mesh_object(
                node.get("name") or "Body", file, node, parent, collection, materials, False, frame=frame, aim=aim))
        elif kind == "exit_points":
            _import_attached(child, "exit_point", root, armature, bone_names, aims, collection,
                             lambda node, parent, frame, aim: _add_exit_point(node, parent, collection), keeps_joint_frame=True)
        elif kind == "collision_hulls":
            _import_attached(child, "hull", root, armature, bone_names, aims, collection,
                             lambda node, parent, frame, aim: add_hull_object(file, node, parent, collection, frame, aim))

    _import_animations(context, file, armature_node, armature, shape_object, rests, joints, bone_names, root, aims)


def _stored_bones(joints: typing.List[typing.Dict[str, typing.Any]]) -> typing.Dict[int, typing.Tuple[axes.Aim, float]]:
    """The axes and lengths the joints' bones had in Blender, which the add-on writes with the joints (bone): a rig made in Blender or
    imported before comes back as it was"""
    stored = {}
    for i, joint in enumerate(joints):
        bone = joint.get("bone")
        if not isinstance(bone, dict) or not axes.is_aim(bone.get("axes")):
            continue

        length = bone.get("length")
        if isinstance(length, (int, float)) and not isinstance(length, bool) and 1e-5 < length < 1e6:
            stored[int(joint.get("index", i))] = (tuple(int(value) for value in bone["axes"]), float(length))

    return stored


def bone_aim(bone: typing.Any) -> axes.Aim:
    """The joint's axes the bone's are (axes.aim_of): the joint's own for a bone made in Blender."""
    value = bone.get(AIM_PROPERTY)
    return tuple(int(component) for component in value) if value is not None and axes.is_aim(value) else axes.IDENTITY_AIM  # type: ignore[return-value]


def bone_length(bone: typing.Any) -> float:
    """The bone's length: the one it was made with while Blender's is still about that one (LENGTH_PROPERTY)."""
    length = float(bone.length)
    made = bone.get(LENGTH_PROPERTY)
    if isinstance(made, float) and abs(length - made) <= 1e-4 * made:
        return made

    return length


def follow_bone(blender_object: bpy.types.Object, root: bpy.types.Object, armature: bpy.types.Object, bone_name: str) -> None:
    """Makes the object follow the bone the way an import does, its transform relative to the bone: a Child Of constraint taking where
    the object is from the model's root, so the root counts once (through the armature) wherever it's moved."""
    constraint = blender_object.constraints.new("CHILD_OF")
    constraint.target = armature
    constraint.subtarget = bone_name
    constraint.space_object = root
    constraint.owner_space = "CUSTOM"
    constraint.inverse_matrix = Matrix.Identity(4)


def child_of_input(constraint: typing.Any, world: Matrix) -> Matrix:
    """A place in the world as a Child Of takes it in, in its owner's space."""
    if constraint.owner_space == "CUSTOM" and constraint.space_object is not None:
        return constraint.space_object.matrix_world.inverted() @ world

    return world


def follow_one_bone(context: bpy.types.Context, blender_object: bpy.types.Object) -> None:
    """Leaves the part one plain Child Of to a bone at most and nothing else moving it, where Blender shows it with the armatures at rest
    (the caller puts them there): of the Child Ofs to a bone of its model's armature that move it the one of the highest influence stays
    (parts.kept_child_of), everything else goes, and the one kept gets Influence 1, every channel, World space (or the Custom space it had)
    and its inverse worked out again so the part doesn't move. An exit point, which can't be sheared, gets the nearest axes that aren't. A
    part squashed flat (its inverse set while its bone was scaled to nothing) goes back where its own transform puts it: the inverse set
    again at rest, the identity for an import's Child Of, which takes the part's transform from the bone."""
    root = find_root(blender_object)
    armature = armature_of(root) if root is not None else None
    shown = blender_object.matrix_world.copy()
    relative = root.matrix_world.inverted() @ shown if root is not None else shown
    squashed = is_squashed(relative)
    if not squashed and role_of(blender_object) == "exit_point" and is_sheared(relative, EXIT_POINT_SHEAR_TOLERANCE):
        shown = Matrix.LocRotScale(*shown.decompose())

    kept_info = parts.kept_child_of(object_info(blender_object, root), armature.name if armature is not None else None)
    kept = blender_object.constraints.get(kept_info.name) if kept_info is not None else None
    for constraint in [constraint for constraint in blender_object.constraints if constraint != kept]:
        blender_object.constraints.remove(constraint)

    if kept is None:
        context.view_layer.update()
        if not squashed:
            blender_object.matrix_world = shown

        return

    kept.influence = 1.0
    for attribute, _ in parts.CHILD_OF_CHANNELS:
        setattr(kept, attribute, True)

    kept.target_space = "WORLD"
    if kept.owner_space != "CUSTOM" or kept.space_object is None:
        kept.owner_space = "WORLD"

    # Where the part is without it, what the Child Of takes it from
    kept.mute = True
    context.view_layer.update()
    unconstrained = blender_object.matrix_world.copy()
    kept.mute = False
    followed = armature.matrix_world @ armature.data.bones[kept.subtarget].matrix_local
    if squashed:
        kept.inverse_matrix = Matrix.Identity(4) if kept.owner_space == "CUSTOM" else followed.inverted_safe()
    else:
        kept.inverse_matrix = followed.inverted_safe() @ shown @ child_of_input(kept, unconstrained).inverted_safe()

    context.view_layer.update()


def _import_attached(container_node: typing.Dict[str, typing.Any], kind: str, root: bpy.types.Object, armature: bpy.types.Object,
                     bone_names: typing.Dict[int, str], aims: typing.Dict[int, axes.Aim], collection: bpy.types.Collection, create: typing.Callable,
                     keeps_joint_frame: bool = False) -> None:
    container = _new_object(container_node.get("name") or container_node.get("kind"), None, root, collection)
    container[KIND_PROPERTY] = container_node.get("kind")
    container.empty_display_size = 0.1
    order = 0
    for tree_node in tlm.traverse(container_node):
        if tree_node.get("kind") != kind:
            continue

        joint = int(tree_node.get("joint", 0))
        # A hull on no joint stays in the model's space, what follows a joint is in its bone's own axes, which twins mirror
        frame = JOINT_FRAME if joint in bone_names else MODEL_FRAME
        aim = aims.get(joint, axes.IDENTITY_AIM)
        blender_object = create(tree_node, container, frame, aim)
        blender_object[ORDER_PROPERTY] = order
        order += 1
        if keeps_joint_frame:
            _set_joint_frame_transform(blender_object, tree_node, aim if frame == JOINT_FRAME else None)
        else:
            _set_node_transform(blender_object, tree_node, frame, aim)
        matrix = tree_node.get("matrix") or (tree_node.get("data") or {}).get("Matrix")
        if isinstance(matrix, list) and len(matrix) == 16:
            blender_object[MATRIX_PROPERTY] = [float(value) for value in matrix]

        blender_object[JOINT_PROPERTY] = joint
        if frame == JOINT_FRAME:
            follow_bone(blender_object, root, armature, bone_names[joint])


def add_hull_object(file: tlm.TlmFile, tree_node: typing.Dict[str, typing.Any], parent: bpy.types.Object, collection: bpy.types.Collection,
                    frame: str = MODEL_FRAME, aim: axes.Aim = axes.IDENTITY_AIM) -> bpy.types.Object:
    """A collision hull as a wire mesh of its faces. What the game reads of it comes along, for TT Lab to keep while the mesh stays."""
    positions = file.read_view(tree_node.get("vertices"), "f32")
    face_bytes = file.read_view(tree_node.get("faces"), "u8")
    faces = []
    offset = 0
    while offset < len(face_bytes):
        count = face_bytes[offset]
        faces.append(axes.face([int(index) for index in face_bytes[offset + 1:offset + 1 + count]]))
        offset += 1 + count

    def convert(point: typing.Sequence[float]) -> Vector:
        return axes.aimed_vector(axes.joint_point(point), aim) if frame == JOINT_FRAME else axes.point(point)

    mesh = bpy.data.meshes.new(tree_node.get("name") or "Hull")
    mesh.from_pydata([convert(positions[i:i + 3]) for i in range(0, len(positions) - 3, 4)], [], [face for face in faces if len(face) >= 3])
    mesh.update()
    blender_object = _new_object(tree_node.get("name") or "Hull", mesh, parent, collection)
    blender_object.display_type = "WIRE"
    blender_object[KIND_PROPERTY] = "hull"
    _read_data(blender_object, "CollisionHull", tree_node.get("data"))
    kept = {key: list(file.read_view(tree_node[key])) for key in HULL_KEYS if key in tree_node}
    blender_object[HULL_PROPERTY] = json.dumps(kept)
    return blender_object


def export_hull(file: tlm.TlmFile, blender_object: bpy.types.Object, tree_node: typing.Dict[str, typing.Any], frame: str = MODEL_FRAME,
                aim: axes.Aim = axes.IDENTITY_AIM, shear: typing.Optional[Matrix] = None) -> None:
    """The hull's vertexes and faces from its mesh, with what it came with for TT Lab to tell whether the mesh changed. The shear of its
    placement (shear_of), which its node's transform can't hold, goes into the vertexes."""
    mesh: bpy.types.Mesh = blender_object.data
    positions = array.array("f", [0.0] * len(mesh.vertices) * 3)
    mesh.vertices.foreach_get("co", positions)

    def convert(point: typing.Sequence[float]) -> Vector:
        converted = axes.joint_point(axes.unaimed_vector(point, aim)) if frame == JOINT_FRAME else axes.point_back(point)
        return shear @ Vector(converted) if shear is not None else converted

    points = [convert(positions[vertex * 3:vertex * 3 + 3]) for vertex in range(len(mesh.vertices))]
    polygons = [axes.face(list(polygon.vertices)) for polygon in mesh.polygons]
    try:
        kept = json.loads(blender_object.get(HULL_PROPERTY, "{}"))
    except ValueError:
        kept = {}

    # The game's own hulls stay as they are while the mesh still has them, some of them aren't convex the way the check takes it
    if not _is_kept_hull(points, polygons, kept, tree_node) and not hulls.is_convex(points, polygons):
        points, polygons = _convex_hull(points)
        _export_notes.append("%s isn't convex and the game collides with the space behind all of a hull's faces: it was written as its convex hull, "
                             "split it into convex hulls to keep its shape" % blender_object.name)

    hulls.check_hull(blender_object.name, len(points), len(polygons))
    tree_node["vertices"] = file.write_view([value for point in points for value in (*point, 1.0)], "f32")
    faces = []
    for polygon in polygons:
        faces.append(len(polygon))
        faces.extend(polygon)

    tree_node["faces"] = file.write_view(faces, "u8")

    for key in ("planes", "edge_directions", "face_normals"):
        if key in kept:
            tree_node[key] = file.write_view(kept[key], "f32")

    if "edges" in kept:
        tree_node["edges"] = file.write_view(kept["edges"], "u8")

    if "vertices" in kept and "faces" in kept:
        tree_node["twin_vertices"] = file.write_view(kept["vertices"], "f32")
        tree_node["twin_faces"] = file.write_view(kept["faces"], "u8")


def _is_kept_hull(points: typing.Sequence[typing.Sequence[float]], polygons: typing.Sequence[typing.Sequence[int]], kept: typing.Dict[str, typing.Any],
                  tree_node: typing.Dict[str, typing.Any]) -> bool:
    """Whether the hull's mesh is still the one it was imported with, unmoved."""
    if "vertices" not in kept or "faces" not in kept or any(key in tree_node for key in ("translation", "rotation", "scale")):
        return False

    faces = []
    for polygon in polygons:
        faces.append(len(polygon))
        faces.extend(polygon)

    vertices = [value for point in points for value in (*point, 1.0)]
    return faces == [int(value) for value in kept["faces"]] and len(vertices) == len(kept["vertices"]) and \
        all(struct.pack("<f", a) == struct.pack("<f", b) for a, b in zip(vertices, kept["vertices"]))


def _convex_hull(points: typing.Sequence[typing.Sequence[float]]) -> typing.Tuple[typing.List[typing.Tuple[float, float, float]], typing.List[typing.List[int]]]:
    """The convex hull of the points, the triangles on one plane joined into one face."""
    import bmesh

    hull = bmesh.new()
    for point in points:
        hull.verts.new(point)

    made = bmesh.ops.convex_hull(hull, input=hull.verts[:])
    inside = {vertex for key in ("geom_interior", "geom_unused") for vertex in made[key] if isinstance(vertex, bmesh.types.BMVert)}
    bmesh.ops.delete(hull, geom=list(inside), context="VERTS")
    hull.normal_update()
    bmesh.ops.dissolve_limit(hull, angle_limit=0.01, verts=hull.verts[:], edges=hull.edges[:], delimit=set())
    hull.verts.index_update()
    result = [tuple(vertex.co) for vertex in hull.verts], [[vertex.index for vertex in face.verts] for face in hull.faces]
    hull.free()
    return result


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
                       bone_names: typing.Dict[int, str], root: bpy.types.Object, aims: typing.Dict[int, axes.Aim]) -> None:
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
        meta["model_joints"] = len(joints)
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
            aim = aims.get(index, axes.IDENTITY_AIM)
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
                location, pose_rotation = axes.joint_point(location), axes.joint_rotation_wxyz(pose_rotation)
                # Both of a rotation's quaternions are the same, the one closer to the last frame's plays without spinning around
                if previous is not None and sum(a * b for a, b in zip(previous, pose_rotation)) < 0:
                    pose_rotation = tuple(-value for value in pose_rotation)

                previous = pose_rotation
                # In the bone's own axes
                poses.append((axes.aimed_vector(location, aim), axes.aimed_rotation_wxyz(pose_rotation, aim), axes.aimed_scale(pose_scale, aim)))

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

    keep_animation(action)
    sync_shape_keys(armature)


def keep_animation(action: typing.Optional[bpy.types.Action]) -> None:
    """An animation a model plays is kept by a fake user, like an imported one: Blender 5 doesn't stash the action it switches from any more,
    and one made in Blender was left without users once another one played, which Blender doesn't save and the export took for deleted:
    syncing wrote only the animation playing. Clearing its fake user (Shift and the action's X) deletes it."""
    if action is not None and not action.use_fake_user and action.library is None:
        action.use_fake_user = True


def sync_shape_keys(armature: bpy.types.Object) -> None:
    """Gives the shape keys of the meshes the armature deforms the action the armature plays."""
    action = armature.animation_data.action if armature.animation_data is not None else None
    key_slot = next((slot for slot in action.slots if slot.target_id_type == "KEY"), None) if action is not None else None
    for blender_object in bpy.data.objects:
        if blender_object.type != "MESH" or blender_object.data.shape_keys is None or not in_scene(blender_object):
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
_CONTAINER_ROLES = {"rigid_bodies": "body", "exit_points": "exit_point", "collision_hulls": "hull", "scenery_meshes": "scenery_mesh",
                    "scenery_lod": "lod_mesh"}


# The game's facial animations count their shapes in 4 bits (MorphJointSettings)
MOST_SHAPES = 15


def object_info(blender_object: bpy.types.Object, root: typing.Optional[bpy.types.Object] = None) -> parts.ObjectInfo:
    """What parts tells the object's role by."""
    container = properties.get(blender_object)
    parent = blender_object.parent
    mesh = blender_object.data if blender_object.type == "MESH" else None
    keys = mesh.shape_keys.key_blocks if mesh is not None and mesh.shape_keys is not None else []
    follows = next(((constraint.target.name, constraint.subtarget) for constraint in blender_object.constraints
                    if constraint.type == "CHILD_OF" and constraint.target is not None and constraint.target.type == "ARMATURE" and constraint.subtarget), None)
    return parts.ObjectInfo(blender_object.name, blender_object.type, parent.name if parent is not None and parent != root else None,
                            container.type if container is not None else "", str(blender_object.get(KIND_PROPERTY, "")), blender_object.parent_type,
                            blender_object.parent_bone, [modifier.object.name for modifier in blender_object.modifiers
                                                         if modifier.type == "ARMATURE" and modifier.object is not None],
                            follows, max(0, len(keys) - 1), [_constraint_info(constraint) for constraint in blender_object.constraints])


def _constraint_info(constraint: bpy.types.Constraint) -> parts.ConstraintInfo:
    target = getattr(constraint, "target", None)
    subtarget = getattr(constraint, "subtarget", "") or ""
    target_type = target.type if target is not None else ""
    channels_off = [label for attribute, label in parts.CHILD_OF_CHANNELS if not getattr(constraint, attribute)] if constraint.type == "CHILD_OF" else []
    return parts.ConstraintInfo(constraint.name, constraint.type, not constraint.mute, constraint.influence, target.name if target is not None else None,
                                target_type, subtarget, target_type != "ARMATURE" or subtarget in target.data.bones, channels_off, constraint.owner_space,
                                constraint.target_space, getattr(constraint, "space_object", None) is not None)


def role_of(blender_object: bpy.types.Object) -> typing.Optional[str]:
    """What the object is in its model: its Twin Tech type, the kind it was imported as or what its holder holds, else in an OGI what
    Blender's rigging makes it (parts)."""
    container = properties.get(blender_object)
    if container is not None and container.type in _TYPE_KINDS:
        return _TYPE_KINDS[container.type]

    kind = blender_object.get(KIND_PROPERTY)
    if kind:
        return kind

    parent = blender_object.parent
    # What the container is made in Blender as well: a LOD made in Blender holds levels
    container_kind = (role_of(parent) or "") if parent is not None else ""
    # A LOD made in Blender is an empty in the scenery's LODs with its levels' meshes under it, a mesh put there is placed on its own
    if container_kind == "scenery_lods":
        return "scenery_lod" if blender_object.type == "EMPTY" else "scenery_mesh"

    held = _CONTAINER_ROLES.get(container_kind)
    if held is not None:
        return held

    root = find_root(blender_object)
    if root is None or root == blender_object or root.get(KIND_PROPERTY) != "ogi":
        return None

    # Under a holder further up, else Blender's rigging
    above = parent
    while above is not None and above != root:
        if above.get(KIND_PROPERTY) in parts.HOLDERS:
            return parts.HOLDERS[above.get(KIND_PROPERTY)]

        above = above.parent

    if blender_object.type == "ARMATURE":
        return "armature"

    if blender_object.type != "MESH":
        return None

    armature = armature_of(root)
    return parts._rigged_role(object_info(blender_object, root), armature.name if armature is not None else None)


def find_root(blender_object: typing.Optional[bpy.types.Object]) -> typing.Optional[bpy.types.Object]:
    while blender_object is not None:
        if blender_object.get(ROOT_PROPERTY):
            return blender_object

        blender_object = blender_object.parent

    return None


class _Objects:
    """The file's objects by their parents and the ones in a scene, gone over once: Object.children and users_scene each go over every
    object of the file, and walking a model asked both of every object of it (a scenery of thousands of objects took seconds)."""

    def __init__(self) -> None:
        self.children: typing.Dict[int, typing.List[bpy.types.Object]] = {}
        for blender_object in bpy.data.objects:
            if blender_object.parent is not None:
                self.children.setdefault(blender_object.parent.as_pointer(), []).append(blender_object)

        self.shown = {blender_object.as_pointer() for scene in bpy.data.scenes for blender_object in scene.objects}


_objects: typing.List[typing.Optional[_Objects]] = [None]


@contextlib.contextmanager
def objects_walked() -> typing.Iterator[None]:
    """The file's objects gone over once for what walks models inside, which adds, removes and parents none of them (what's made meanwhile,
    a mesh's decimated copy, isn't seen)."""
    if _objects[0] is not None:
        yield
        return

    _objects[0] = _Objects()
    try:
        yield
    finally:
        _objects[0] = None


def in_scene(blender_object: bpy.types.Object) -> bool:
    """Whether the object is in one of the file's scenes. A deleted one stays in the file, out of every scene, until Clean Up > Purge
    Unused takes it out: none of it is the model's any more"""
    index = _objects[0]
    if index is not None:
        return blender_object.as_pointer() in index.shown

    return len(blender_object.users_scene) > 0


def live_children(blender_object: bpy.types.Object) -> typing.List[bpy.types.Object]:
    """The object's children that are in a scene, what purging leaves under it, in the order of the file's objects."""
    index = _objects[0]
    if index is not None:
        return [child for child in index.children.get(blender_object.as_pointer(), []) if child.as_pointer() in index.shown]

    return [child for child in blender_object.children if in_scene(child)]


def _descendants(blender_object: bpy.types.Object) -> typing.List[bpy.types.Object]:
    result: typing.List[bpy.types.Object] = []
    with objects_walked():
        pending = list(reversed(live_children(blender_object)))
        while pending:
            child = pending.pop()
            result.append(child)
            pending.extend(reversed(live_children(child)))

    return result


def _writes_rigid_parts(root: bpy.types.Object, blender_object: bpy.types.Object,
                        descendants: typing.Optional[typing.List[bpy.types.Object]] = None) -> bool:
    """Whether exporting the model writes the object as a rigid model's parts drawn with its materials."""
    if blender_object.type != "MESH":
        return False

    kind = root.get(KIND_PROPERTY, "")
    if kind == "ogi":
        return role_of(blender_object) == "body"

    if kind in TREE_KINDS:
        return role_of(blender_object) not in ("collision", "hull")

    # A model's parts are drawn with the materials its rigid model has in the project
    if kind in ("collision", "save_icon", "model") or kind in SKINNED_KINDS:
        return False

    return blender_object == next((child for child in (descendants if descendants is not None else _descendants(root)) if child.type == "MESH"), None)


def is_rigid_part(blender_object: bpy.types.Object) -> bool:
    """Whether the mesh object is written as a rigid model's parts of the model it's in, which the game's lit shaders light by their emit
    colors."""
    root = find_root(blender_object)
    return root is not None and in_scene(blender_object) and _writes_rigid_parts(root, blender_object)


def effective_material(material: bpy.types.Material, project: typing.Optional[projects.Project]) -> typing.Optional[typing.Dict[str, typing.Any]]:
    """The material as TT Lab has it once it reads the exported model: what was changed in Blender over the project's material, the project's
    of a material without settings. None for a material made in Blender without settings, which TT Lab makes an unlit one of a rigid part."""
    uri = material.get(URI_PROPERTY, "")
    project_material = project.materials.get(uri) if project is not None and uri else None
    theirs = project_material.read() if project_material is not None else None
    if not material_settings.is_managed(material):
        return theirs

    base = material_settings.base_of(material)
    return gs_material.merge_material(base, theirs if theirs is not None else base, material_settings.shown(material))[0]


def emit_lighting_shader(material: bpy.types.Material, project: typing.Optional[projects.Project]) -> typing.Optional[str]:
    """The label of the material's first shader type that lights a rigid part by its emit colors once TT Lab reads the exported model,
    None when it has none."""
    data = effective_material(material, project)
    shader_type = next((shader.get("ShaderType") for shader in gs_material._shaders(data) if gs_material.lights_emit_colors(shader.get("ShaderType"))), None)
    return gs_material.type_label(shader_type) if shader_type is not None else None


def drawn_materials(blender_object: bpy.types.Object) -> typing.List[bpy.types.Material]:
    """The materials the mesh object's faces are drawn with."""
    mesh: bpy.types.Mesh = blender_object.data
    indexes = array.array("i", [0] * len(mesh.polygons))
    mesh.polygons.foreach_get("material_index", indexes)
    slots = blender_object.material_slots
    materials: typing.List[bpy.types.Material] = []
    for index in sorted(set(indexes)):
        material = slots[index].material if 0 <= index < len(slots) else None
        if material is not None and material not in materials:
            materials.append(material)

    return materials


def lit_without_emit_colors(root: bpy.types.Object, path: str) -> typing.List[typing.Tuple[str, str, str]]:
    """The rigid meshes of a PS2 model written to the path that are drawn with a shader lighting their emit colors and have none: the
    mesh's name, the material's and the shader type's. The Xbox version's models have one color, which its shaders light."""
    if projects.version_of_file(path) == "XBOX":
        return []

    project = projects.open_project(path) if path else None
    shaders: typing.Dict[int, typing.Optional[str]] = {}
    found = []
    descendants = _descendants(root)
    for blender_object in descendants:
        if not _writes_rigid_parts(root, blender_object, descendants) or blender_object.data.color_attributes.get(EMIT_COLOR_ATTRIBUTE) is not None:
            continue

        for material in drawn_materials(blender_object):
            key = material.as_pointer()
            if key not in shaders:
                shaders[key] = emit_lighting_shader(material, project)

            if shaders[key] is not None:
                found.append((blender_object.name, material.name, shaders[key]))
                break

    return found


# The most meshes an export problem names
_NAMED_MESHES = 5


def _lit_without_emit_colors_problem(lit: typing.List[typing.Tuple[str, str, str]]) -> str:
    if len(lit) == 1:
        name, material, shader = lit[0]
        return ("%s has no emit colors, which its material %s's %s shader lights: the PS2 version lights it with what the model drawn before it "
                "left there, in other colors. Add Emit Colors in the material's Twin Tech Material panel gives it a copy of its vertex colors, or "
                "draw it with an unlit shader" % (name, material, shader))

    named = ", ".join("%s (%s's %s)" % entry for entry in lit[:_NAMED_MESHES])
    if len(lit) > _NAMED_MESHES:
        named += " and %d more" % (len(lit) - _NAMED_MESHES)

    return ("%s have no emit colors, which their materials' shaders light: the PS2 version lights them with what the model drawn before left "
            "there, in other colors. Add Emit Colors in a material's Twin Tech Material panel gives the meshes drawn with it a copy of their "
            "vertex colors, or draw them with unlit shaders" % named)


def export_problem(root: bpy.types.Object, path: typing.Optional[str] = None) -> typing.Optional[str]:
    """The first of what keeps the model from being exported (checks), None when nothing does. The path is the file it's exported to, its
    own by default."""
    from . import checks

    found = checks.refused(root, path)
    return found[0].text if found else None


def build_file(root: bpy.types.Object, path: str, linked: bool = False) -> typing.Tuple[tlm.TlmFile, "_FileMaterials", typing.List[str]]:
    """The model's file as it's written to the path (what was sent of its materials into the file there before found out), what it
    sends of its materials (committed once it's written) and what it doesn't have of the scene, to warn about. Linked, TT Lab has the
    materials and pictures, the file names them."""
    with objects_walked():
        return _build_file(root, path, linked)


def _build_file(root: bpy.types.Object, path: str, linked: bool) -> typing.Tuple[tlm.TlmFile, "_FileMaterials", typing.List[str]]:
    from . import materials as blender_materials

    blender_materials.make_drawn_twin_tech(root)
    kind = root.get(KIND_PROPERTY, "")
    file = tlm.TlmFile(root.get(ROOT_PROPERTY, ""), root.name)
    materials = _FileMaterials(file, path, linked)
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
            tree["mesh"] = tlm_mesh.to_parts(file, _mesh_to_game(_read_mesh(mesh_object, materials if kind != "model" else None, skinned)), skinned)

    if not tree.get("data"):
        tree.pop("data", None)

    file.root = tree
    # TT Lab tells a file it hasn't read yet by it, and writes it again once it took what the file brings
    file.json[material_sync.EXPORTED_BY_KEY] = "twin_tech_tools " + ADDON_VERSION
    warnings.extend(_export_notes)
    _export_notes.clear()
    return file, materials, warnings


def export_file(root: bpy.types.Object, path: str) -> typing.List[str]:
    """Writes the model to the file. Returns what the file doesn't have of the scene, to warn about. Written into another project's file, the
    model is that project's: what it draws of another project is made its own first (link_sync.separate)."""
    from . import link_sync

    previous = root.get(PATH_PROPERTY, "")
    if not link_sync._same_file(previous, path):
        link_sync.separate(root, path)

    file, materials, warnings = build_file(root, path)
    data = file.to_bytes()
    with open(path, "wb") as target:
        target.write(data)

    materials.commit()
    # The model's file from now on, a model made with Add > Twin Tech never learned it. What TT Lab reads of it is what Blender has
    root[PATH_PROPERTY] = path
    root[REVISION_PROPERTY] = "exported:" + hashlib.sha256(data).hexdigest()
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
        tree["mesh"] = tlm_mesh.to_parts(file, _mesh_to_game(mesh), False, shaped=True)

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


# Past this a placement's shear isn't rounding: a rigid body's or hull's goes into its vertexes, an exit point's is refused (it has none)
SHEAR_TOLERANCE = 1e-5
EXIT_POINT_SHEAR_TOLERANCE = 1e-4


def shear_of(matrix: Matrix) -> Matrix:
    """What's left of the matrix's 3x3 past the rotation and scale a node's transform holds (_write_transform): the identity but for a
    sheared one, a Child Of whose inverse was set while its bone was scaled unevenly."""
    _, rotation, scale = matrix.decompose()
    return (rotation.to_matrix() @ Matrix.Diagonal(scale)).inverted_safe() @ matrix.to_3x3()


def is_sheared(matrix: Matrix, tolerance: float = SHEAR_TOLERANCE) -> bool:
    left = shear_of(matrix)
    return any(abs(left[row][column] - (1.0 if row == column else 0.0)) > tolerance for row in range(3) for column in range(3))


def is_squashed(matrix: Matrix) -> bool:
    """Whether the matrix flattens what it places onto a plane, a line or a point: a Child Of's inverse set while its bone was scaled to
    nothing, the way animations hide joints."""
    linear = matrix.to_3x3()
    lengths = [linear.col[index].length for index in range(3)]
    return min(lengths) < 1e-6 or abs(linear.determinant()) < 1e-6 * lengths[0] * lengths[1] * lengths[2]


def _shear_mesh(mesh: tlm_mesh.CornerMesh, shear: Matrix) -> None:
    """Puts the shear of a part's placement into its vertexes, in the game's space (_mesh_to_game): the node's transform holds the rest."""
    positions = array.array("f")
    for i in range(0, len(mesh.positions), 3):
        positions.extend(shear @ Vector(mesh.positions[i:i + 3]))

    mesh.positions = positions
    if mesh.corner_normals is not None:
        normal_matrix = shear.inverted_safe().transposed()
        normals = array.array("f")
        for i in range(0, len(mesh.corner_normals), 3):
            normal = normal_matrix @ Vector(mesh.corner_normals[i:i + 3])
            normals.extend(normal.normalized() if normal.length > 1e-12 else normal)

        mesh.corner_normals = normals

    mesh.shapes = [array.array("f", (value for i in range(0, len(shape), 3) for value in shear @ Vector(shape[i:i + 3]))) for shape in mesh.shapes]


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
    shape_positions = []
    for shape in mesh.shape_positions:
        moved = array.array("f")
        for i in range(0, len(shape), 3):
            moved.extend(matrix @ Vector(shape[i:i + 3]))

        shape_positions.append(moved)

    mesh.shape_positions = shape_positions


def armature_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((child for child in _descendants(root) if child.type == "ARMATURE"), None)


def shape_object_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    return next((child for child in _descendants(root) if role_of(child) == "shape" and child.type == "MESH"), None)


def joints_of_bones(armature: bpy.types.Object) -> typing.Dict[str, int]:
    """The joint of every bone: the index of its Joint type, bones without one or with one another bone has get the next free ones,
    or every bone is numbered from the root when that's no skeleton the game can have (see skeleton.number_joints)."""
    return skeleton.number_joints(bone_infos(armature))[0]


def _rests_of_bones(root: typing.Optional[bpy.types.Object], armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int]) -> typing.Tuple[typing.Dict[int, typing.Any], typing.Dict[int, typing.List[float]]]:
    """Every joint's rest and bind pose in the model's space (the armature's own without a model): the bone's, or the game's the bone was
    imported with while it's still there."""
    armature_matrix = _relative_to_root(root, armature) if root is not None else armature.matrix_world.copy()
    rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]] = {}
    binds: typing.Dict[int, typing.List[float]] = {}
    for bone in armature.data.bones:
        index = joint_of_bone[bone.name]
        rest = [list(row) for row in armature_matrix @ bone.matrix_local]
        bind = _bind_of_rest(rest, bone)
        rests[index] = tlm_math.rest_of(tlm_math.from_rows(bind))
        stored = bone.get(BIND_PROPERTY)
        if stored is not None and len(stored) == 16:
            stored_rest = tlm_math.rest_of(tlm_math.from_rows(list(stored)))
            # Bones pointing close to straight ahead come back a tenth of a degree off from their bind, a bone whose own axes changed
            # (its AIM_PROPERTY taken away) a quarter or half turn
            if _same_rest(stored_rest, rests[index]) or _is_imported_rest(bone.get(REST_PROPERTY), rest) and _same_rest(stored_rest, rests[index], 1e-3, 1e-4):
                bind, rests[index] = [float(value) for value in stored], stored_rest

        binds[index] = bind

    return rests, binds


def _bind_of_rest(rest: typing.Sequence[typing.Sequence[float]], bone: typing.Any) -> typing.List[float]:
    """The game's bind pose of a bone's rest in the model's space: its joint's frame, its own axes picked back."""
    return tlm_math.to_rows(axes.bone_matrix_back(axes.unaimed(rest, bone_aim(bone))))


def _is_imported_rest(recorded: typing.Any, rest: typing.Sequence[typing.Sequence[float]]) -> bool:
    """Whether a bone's rest in the model's space is still the one Blender made of its imported bind, give or take the rounding errors
    of a moved or turned model."""
    if recorded is None or len(recorded) != 16:
        return False

    return _same_rest(tlm_math.rest_of(tlm_math.from_rows(list(recorded))), tlm_math.rest_of([list(row) for row in rest]))


def _relative_rests(armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int], rests: typing.Dict[int, typing.Any]) -> typing.Dict[int, typing.Any]:
    parents = {joint_of_bone[bone.name]: joint_of_bone[bone.parent.name] if bone.parent is not None else -1 for bone in armature.data.bones}
    return {index: tlm_math.relative_rest(rests.get(parents[index]), rests[index]) for index in rests}


def _draws(blender_object: bpy.types.Object) -> bool:
    """Whether the mesh object has faces: a template's skin and blend skin are there empty to be filled, the model has none of them
    until they are"""
    return blender_object.type == "MESH" and len(blender_object.data.polygons) > 0


def _export_ogi(file: tlm.TlmFile, root: bpy.types.Object, tree: typing.Dict[str, typing.Any], descendants: typing.List[bpy.types.Object],
                materials: _FileMaterials) -> None:
    armature = next((child for child in descendants if child.type == "ARMATURE"), None)
    armature_node = tlm.add_child(tree, tlm.node("armature", "Armature"))
    skinned = any(role_of(child) in SKINNED_KINDS and _draws(child) for child in descendants)
    # ModelNode::SetOgi gives a model of one joint and no exit points no animator, the game draws only its rigid models
    if skinned and (armature is None or len(armature.data.bones) < 2) and not any(role_of(child) == "exit_point" for child in descendants):
        _export_notes.append("%s has one joint and no exit points: the game draws such a model's rigid bodies only, never its skin. Add a bone under "
                             "the root and weight the skin to it" % root.name)
    joint_of_bone: typing.Dict[str, int] = {}
    rests: typing.Dict[int, typing.Tuple[typing.Any, typing.Any]] = {}
    joint_of_stored: typing.Dict[int, int] = {}
    if armature is not None:
        infos = bone_infos(armature)
        skeleton.check_skeleton(infos, skinned)
        joint_of_bone, renumbered = skeleton.number_joints(infos)
        if renumbered:
            _export_notes.append("%s's joints were numbered in the order the game walks its skeleton, from the root down and a bone's children "
                                 "by their indexes: the joint indexes of its bones didn't follow it" % armature.name)

        # What bodies, exit points and hulls following no bone keep is the joint their bone had
        joint_of_stored = {index: joint_of_bone[name] for name, _, index in reversed(infos) if index is not None}
        rests, binds = _rests_of_bones(root, armature, joint_of_bone)
        # The binds are in the model's space, the bones' lengths in the armature's: an armature object scaled under the root (an FBX
        # rig's 0.01) stretches them
        stretches = _relative_to_root(root, armature).to_3x3()
        joints = []
        for bone in armature.data.bones:
            index = joint_of_bone[bone.name]
            # Of the bone's unit direction: Blender's own matrices of bones pointing close to straight ahead aren't unit to 1e-5
            stretch = (stretches @ bone.matrix_local.to_3x3().col[1].normalized()).length
            joint: typing.Dict[str, typing.Any] = {"index": index, "parent": joint_of_bone[bone.parent.name] if bone.parent is not None else -1,
                                                   "name": bone.name, "bind": binds[index],
                                                   # What the bone is in Blender, for the next import to make it the same
                                                   "bone": {"axes": list(bone_aim(bone)),
                                                            "length": bone_length(bone) * stretch if abs(stretch - 1.0) > 1e-6 else bone_length(bone)}}
            if properties.get(bone).type == "Joint":
                data = _write_data(bone)
                data.pop("Index", None)
                joint["data"] = data

            joints.append(joint)

        armature_node["joints"] = sorted(joints, key=lambda joint: joint["index"])
        armature_node["animations"], joint_tracks = _export_animations(file, root, armature, joint_of_bone, rests, descendants)
        _choose_additional_rotations(armature_node["joints"], joint_tracks)

    joint_of_group_names = joint_of_bone
    for blender_object in in_file_order(child for child in descendants if role_of(child) in SKINNED_KINDS and _draws(child)):
        groups = {group.index: joint_of_group_names[group.name] for group in blender_object.vertex_groups if group.name in joint_of_group_names}
        skin = tlm.add_child(tree, tlm.node(role_of(blender_object), blender_object.name, _write_data(blender_object) or None))
        mesh = _read_mesh(blender_object, materials, True, groups)
        # The skin is where the object shows it, wherever the object was put under the root
        _bake_transform(mesh, _relative_to_root(root, blender_object))
        skin["mesh"] = tlm_mesh.to_parts(file, _mesh_to_game(mesh), True)

    bodies = tlm.add_child(tree, tlm.node("rigid_bodies", "Rigid Bodies"))
    exit_points = tlm.add_child(tree, tlm.node("exit_points", "Exit Points"))
    hulls = tlm.add_child(tree, tlm.node("collision_hulls", "Collision Hulls"))
    for blender_object in in_file_order(child for child in descendants if role_of(child) == "body" and _draws(child)):
        body = tlm.add_child(bodies, tlm.node("body", blender_object.name, _write_data(blender_object)))
        body["joint"], placement, frame, aim = _attached_placement(root, blender_object, armature, joint_of_bone, rests, joint_of_stored=joint_of_stored)
        _write_transform(body, placement)
        mesh = _mesh_to_game(_read_mesh(blender_object, materials, False), frame, aim)
        if is_sheared(placement):
            _shear_mesh(mesh, shear_of(placement))

        body["mesh"] = tlm_mesh.to_parts(file, mesh, False)

    # The game finds a hull's joint by its place in the hulls (CollisionJointIndices)
    for blender_object in in_file_order(child for child in descendants if role_of(child) == "hull" and child.type == "MESH"):
        hull = tlm.add_child(hulls, tlm.node("hull", blender_object.name, _write_data(blender_object) or None))
        hull["joint"], placement, frame, aim = _attached_placement(root, blender_object, armature, joint_of_bone, rests, NO_JOINT, joint_of_stored)
        _write_transform(hull, placement)
        export_hull(file, blender_object, hull, frame, aim, shear_of(placement) if is_sheared(placement) else None)

    # The game finds an exit point by its place (BindExitPoints, a character's hand is 0 and its head 1) and never reads its ID: the exit
    # points go in the order of their IDs, then the file's, with their places as IDs
    in_order = in_file_order(child for child in descendants if role_of(child) == "exit_point")
    ordered = sorted(((int(_write_data(blender_object).get("Id", 0)), place, blender_object) for place, blender_object in enumerate(in_order)),
                     key=lambda entry: (entry[0], entry[1]))
    ordered = [(stored_id, blender_object) for stored_id, _, blender_object in ordered]
    renumbered = []
    for place, (stored_id, blender_object) in enumerate(ordered):
        data = dict(_write_data(blender_object), Id=place)
        if stored_id != place:
            renumbered.append("%s %d as %d" % (blender_object.name, stored_id, place))

        exit_point = tlm.add_child(exit_points, tlm.node("exit_point", blender_object.name, data))
        exit_point["joint"], placement, _, _ = _attached_placement(root, blender_object, armature, joint_of_bone, rests, joint_of_stored=joint_of_stored,
                                                                   keeps_joint_frame=True)
        _write_transform(exit_point, placement)
        stored = blender_object.get(MATRIX_PROPERTY)
        if stored is not None and len(stored) == 16:
            exit_point["matrix"] = [float(value) for value in stored]

    if renumbered:
        _export_notes.append("%s's exit points got their places as IDs (%s): the game finds an exit point by its place, they're written in the order "
                             "of their IDs" % (root.name, ", ".join(renumbered)))

    data = tree.setdefault("data", {})
    box = (tuple(data.get("BoundingBoxMin", (0.0, 0.0, 0.0))[:3]), tuple(data.get("BoundingBoxMax", (1.0, 1.0, 1.0))[:3]))
    fitted = model_bounds.fitted_box(box, model_bounds.rest_bounds(file, tree), _imported_bounds(root))
    if fitted is not None:
        data["BoundingBoxMin"], data["BoundingBoxMax"] = list(fitted[0]) + [1.0], list(fitted[1]) + [1.0]
        _export_notes.append("%s's meshes went past its bounding box, which the game takes for its instances' collision without hulls, shadows and "
                             "physics: it was written around them" % root.name)


def _same_rest(a: typing.Tuple[typing.Any, typing.Any], b: typing.Tuple[typing.Any, typing.Any], moved_by: float = 1e-4, turned_by: float = 1e-6) -> bool:
    """Whether a bone still rests where it was imported, give or take the precision Blender keeps bones with: moved less than moved_by
    and turned less than turned_by (one less the cosine of half the turn)."""
    moved = sum((x - y) ** 2 for x, y in zip(a[0], b[0])) ** 0.5
    turned = 1.0 - abs(sum(x * y for x, y in zip(a[1], b[1])))
    return moved < moved_by and turned < turned_by


# The joint of a hull that's on none, in the model's space
NO_JOINT = 0xFF
# An animation's frames a second take 5 bits of its header (GameAnimation's bits 18-22), it plays for its frames over them
MAX_ANIMATION_FPS = 31


def _attached_placement(root: bpy.types.Object, blender_object: bpy.types.Object, armature: typing.Optional[bpy.types.Object],
                        joint_of_bone: typing.Dict[str, int], rests: typing.Dict[int, typing.Any], default_joint: int = 0,
                        joint_of_stored: typing.Optional[typing.Dict[int, int]] = None,
                        keeps_joint_frame: bool = False) -> typing.Tuple[int, Matrix, str, axes.Aim]:
    """The joint a body, exit point or hull follows, where it is in the joint's space of the game, and the frame and the bone's own axes
    its own points are in (axes): where the object would be with the armature at rest, whichever way it follows its bone (its own, or the
    one what it's under follows), relative to the joint's rest. Without a joint it's relative to the model. What follows a bone is in the
    bone's own axes, like an import puts it, anything else in the model's: either way gives the same points, the frame and the axes decide
    which way the transform and the points are split. An exit point (keeps_joint_frame) has no points, its axes are the game's through
    the joints' reflection wherever it is."""
    attachment = _rest_attachment(blender_object, armature, joint_of_bone) if armature is not None else None
    if attachment is not None:
        joint, bone, rest_world = joint_of_bone[attachment[1].name], attachment[1], attachment[2]
    else:
        stored = int(blender_object.get(JOINT_PROPERTY, default_joint))
        joint, rest_world = (joint_of_stored or {}).get(stored, stored), blender_object.matrix_world
        bone = next((armature.data.bones[name] for name, index in joint_of_bone.items() if index == joint), None) if armature is not None else None

    relative = [list(row) for row in root.matrix_world.inverted() @ rest_world]
    frame = JOINT_FRAME if attachment is not None else MODEL_FRAME
    aim = bone_aim(attachment[1]) if attachment is not None else axes.IDENTITY_AIM
    if keeps_joint_frame:
        placed = Matrix(axes.joint_frame_back(relative))
    else:
        placed = Matrix(axes.joint_frame_back(axes.unaimed(relative, aim)) if frame == JOINT_FRAME else axes.matrix_back(relative))
    rest = rests.get(joint)
    if bone is not None:
        # Relative to the bone as Blender keeps it, which can be a little turned from the joint's bind: what follows it, or was left
        # where it followed it, is turned with it
        rest = tlm_math.rest_of(tlm_math.from_rows(_bind_of_rest([list(row) for row in root.matrix_world.inverted() @ armature.matrix_world @ bone.matrix_local], bone)))

    if rest is None:
        return joint, placed, frame, aim

    return joint, Matrix(tlm_math.compose(rest[0], rest[1])).inverted() @ placed, frame, aim


def _rest_attachment(blender_object: bpy.types.Object, armature: bpy.types.Object,
                     bones: typing.Container[str]) -> typing.Optional[typing.Tuple[typing.Any, bpy.types.Bone, Matrix]]:
    """How the object follows one of the bones of the armature: the Child Of constraint it follows it through (None when it's
    parented to the bone or under something that follows one), the bone and where the object is with the armature at rest. None when
    neither it nor what it's under follows one of the bones."""
    world, bone, constraint = _rest_world(blender_object, armature, bones, set())
    return (constraint, bone, world) if bone is not None else None


def _rest_world(blender_object: bpy.types.Object, armature: bpy.types.Object, bones: typing.Container[str],
                visited: typing.Set[str]) -> typing.Tuple[Matrix, typing.Optional[bpy.types.Bone], typing.Any]:
    """Where the object is with the armature at rest, the bone that moves it (by its Child Of or bone parent, else the one what it's
    under follows) and its Child Of to it."""
    visited.add(blender_object.name)
    parent = blender_object.parent
    parent_type = blender_object.parent_type
    bone = None
    local = blender_object.matrix_parent_inverse @ blender_object.matrix_basis
    constraint = next((constraint for constraint in blender_object.constraints
                       if constraint.type == "CHILD_OF" and constraint.target == armature and constraint.subtarget in bones), None)
    if parent is None:
        world = blender_object.matrix_basis.copy()
    elif parent_type == "BONE" and parent == armature and blender_object.parent_bone in bones:
        bone = armature.data.bones[blender_object.parent_bone]
        # Blender parents to the bone's tail; with Relative Parenting (Ctrl+P > Bone Relative) by the bone's pose from its rest, at rest
        # by nothing
        tail = Matrix.Identity(4) if bone.use_relative_parent else bone.matrix_local @ Matrix.Translation((0.0, bone.length, 0.0))
        world = armature.matrix_world @ tail @ local
    elif parent == armature and parent_type in ("OBJECT", "ARMATURE", "BONE"):
        # The armature object, which its bones don't move (a Bone parent naming none of them is the object too)
        world = parent.matrix_world @ local
    elif parent_type in ("OBJECT", "ARMATURE") and parent.name not in visited:
        under, bone, _ = _rest_world(parent, armature, bones, visited)
        world = under @ local
    elif constraint is not None:
        # Parented to vertexes (which the checks refuse): where Blender shows it with what its Child Of did at the pose taken out
        bone = armature.data.bones[constraint.subtarget]
        taken = (armature.matrix_world @ armature.pose.bones[constraint.subtarget].matrix @ constraint.inverse_matrix).inverted() @ blender_object.matrix_world
        return armature.matrix_world @ bone.matrix_local @ constraint.inverse_matrix @ taken, bone, constraint
    else:
        world = blender_object.matrix_world.copy()

    if constraint is not None:
        # The constraint puts the object at the bone's pose, as the pose it was set up in moved
        bone = armature.data.bones[constraint.subtarget]
        world = armature.matrix_world @ bone.matrix_local @ constraint.inverse_matrix @ child_of_input(constraint, world)

    return world, bone, constraint


def _animation_meta(action: bpy.types.Action) -> typing.Dict[str, typing.Any]:
    try:
        meta = json.loads(action.get(ANIMATION_PROPERTY, "{}"))
    except ValueError:
        meta = {}

    return meta if isinstance(meta, dict) else {}


def _settings_users() -> typing.Dict[int, int]:
    """How many bones' per animation settings point at each action: Blender counts them as its users, but they don't keep it."""
    counts: typing.Dict[int, int] = {}
    for armature in bpy.data.armatures:
        for bone in armature.bones:
            for entry in getattr(bone, flags.FLAGS_PROPERTY, []):
                if entry.action is not None:
                    key = entry.action.as_pointer()
                    counts[key] = counts.get(key, 0) + 1

    return counts


def is_deleted(action: bpy.types.Action, settings_users: typing.Optional[typing.Dict[int, int]] = None) -> bool:
    """Whether the action was deleted: nothing plays it and it has no fake user, it's only in the file until Clean Up > Purge Unused
    takes it out. The bones' settings of it point at it too, which Blender counts as users."""
    settings_users = _settings_users() if settings_users is None else settings_users
    return action.users <= settings_users.get(action.as_pointer(), 0)


def _owned_actions(root: typing.Optional[bpy.types.Object], armature: bpy.types.Object, bone_names: typing.Iterable[str],
                   deleted: bool = False) -> typing.List[bpy.types.Action]:
    """The actions of the model: the ones imported with it, the ones retargeted to its armature and the ones made in Blender that
    animate its bones. The ones deleted instead with deleted."""
    owners = {root.get(UID_PROPERTY) if root is not None else None, armature.get(UID_PROPERTY)} - {None}
    names = set(bone_names)
    settings_users = _settings_users()
    result = []
    for action in bpy.data.actions:
        if is_deleted(action, settings_users) != deleted:
            continue

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


def _sample(fcurve: typing.Any, frames: int, default: float, start: int = 0, step: int = 1) -> typing.List[float]:
    """The curve's values on the frames from the start on, every step-th frame."""
    if fcurve is None:
        return [default] * frames

    points = fcurve.keyframe_points
    count = len(points)
    if count == frames and step == 1:
        values = array.array("f", [0.0] * count * 2)
        points.foreach_get("co", values)
        # Keys on every frame are read as they are
        if all(values[i * 2] == float(start + i) for i in range(count)):
            return list(values[1::2])

    return [fcurve.evaluate(start + frame * step) for frame in range(frames)]


def _rotation_components(mode: str, channels: typing.Dict[str, typing.Sequence[typing.Sequence[float]]]) -> typing.Optional[typing.Sequence[typing.Sequence[float]]]:
    """A bone's rotation on every frame as the components of quaternions (w, x, y, z), from the curves of its rotation mode, else the ones
    the action has (keyed while the bone turned another way), None without any."""
    preferred = {"QUATERNION": "rotation_quaternion", "AXIS_ANGLE": "rotation_axis_angle"}.get(mode, "rotation_euler")
    channel = next((name for name in (preferred, "rotation_quaternion", "rotation_euler", "rotation_axis_angle") if name in channels), None)
    if channel is None:
        return None

    components = channels[channel]
    if channel == "rotation_quaternion":
        return components

    if channel == "rotation_euler":
        order = mode if mode not in ("QUATERNION", "AXIS_ANGLE") else "XYZ"
        rotations = [Euler(angles, order).to_quaternion() for angles in zip(*components)]
    else:
        rotations = [Quaternion(axis, angle) for angle, *axis in zip(*components)]

    return [[rotation[component] for rotation in rotations] for component in range(4)]


def _unbaked_bones(armature: bpy.types.Object) -> typing.List[str]:
    """The bones whose constraints or drivers move them beyond what the actions' keys say."""
    names = {pose_bone.name for pose_bone in armature.pose.bones if any(constraint.enabled for constraint in pose_bone.constraints)}
    drivers = armature.animation_data.drivers if armature.animation_data is not None else []
    names.update(name for name in (_bone_of_path(fcurve.data_path) for fcurve in drivers) if name is not None)
    return [bone.name for bone in armature.data.bones if bone.name in names]


def _imported_bounds(root: bpy.types.Object) -> typing.Optional[model_bounds.Bounds]:
    try:
        bounds = json.loads(root.get(BOUNDS_PROPERTY, "null"))
    except ValueError:
        return None

    return (tuple(bounds[0]), tuple(bounds[1])) if isinstance(bounds, list) and len(bounds) == 2 else None


def _ordinal(number: int) -> str:
    return "%d%s" % (number, "th" if 10 <= number % 100 <= 20 else {1: "st", 2: "nd", 3: "rd"}.get(number % 10, "th"))


def _location_stretches(root: bpy.types.Object, armature: bpy.types.Object) -> typing.Optional[typing.Dict[str, Matrix]]:
    """How a scaled armature object (FBX rigs come in at 0.01) stretches every bone's pose location, which the bind poses have and pose
    locations don't: the armature's scale in the bone's rest frame. None for an armature of no scale."""
    scale = _relative_to_root(root, armature).to_scale()
    if all(abs(value - 1.0) < 1e-6 for value in scale):
        return None

    axes = Matrix.Diagonal(scale)
    result = {}
    for bone in armature.data.bones:
        rest = bone.matrix_local.to_3x3().normalized()
        result[bone.name] = rest.transposed() @ axes @ rest

    return result


def _give_animation_ids(actions: typing.List[bpy.types.Action]) -> None:
    """Actions made in Blender and copies get IDs of their own, kept with them: TT Lab numbering them in the file's order changed which
    animation an ID was once another action got added or renamed."""
    metas = [_animation_meta(action) for action in actions]
    stored = [int(meta["id"]) if isinstance(meta.get("id"), (int, float)) else None for meta in metas]
    for action, meta, animation_id, given in zip(actions, metas, stored, skeleton.animation_ids(stored)):
        if animation_id != given:
            meta["id"] = given
            action[ANIMATION_PROPERTY] = json.dumps(meta)


def _imported_joints(armature: bpy.types.Object) -> int:
    """How many joints the model had when it was imported, for animations imported before they kept it: the bones that came from the
    file."""
    return sum(1 for bone in armature.data.bones if bone.get(BIND_PROPERTY) is not None)


# A joint's keys in an animation as the export writes them: its entry in the file, its rotation in every frame and the game's angles of it
# the action carries, when it does
JointTrack = typing.Tuple[typing.Dict[str, typing.Any], typing.Any, typing.Optional[animation_frames.GameAngles]]


def _export_animations(file: tlm.TlmFile, root: bpy.types.Object, armature: bpy.types.Object, joint_of_bone: typing.Dict[str, int],
                       rests: typing.Dict[int, typing.Any], descendants: typing.List[bpy.types.Object]
                       ) -> typing.Tuple[typing.List[typing.Dict[str, typing.Any]], typing.Dict[int, typing.List[JointTrack]]]:
    relative_rests = _relative_rests(armature, joint_of_bone, rests)
    shape_object = next((child for child in descendants if role_of(child) == "shape" and _draws(child)), None)
    key_blocks = list(shape_object.data.shape_keys.key_blocks)[1:] if shape_object is not None and shape_object.data.shape_keys is not None else []
    bones = len(armature.data.bones)
    imported_joints = _imported_joints(armature)
    stretches = _location_stretches(root, armature)
    result = []
    joint_tracks: typing.Dict[int, typing.List[JointTrack]] = {}
    actions = _owned_actions(root, armature, joint_of_bone)
    dropped = [action.name for action in _owned_actions(root, armature, joint_of_bone, deleted=True)]
    if dropped:
        _export_notes.append("%s of %s weren't written: nothing plays them and they have no fake user, which makes them deleted (Clean Up > Purge "
                             "Unused takes them out). Give them a fake user to keep them" % (", ".join(dropped), root.name))

    _give_animation_ids(actions)
    unbaked = _unbaked_bones(armature) if actions else []
    if unbaked:
        _export_notes.append("%s's %s %s %s constraints or drivers: the animations are written from the actions' keys, without what those do. Bake "
                             "the actions first (Pose > Animation > Bake Action, with Visual Keying) to keep it"
                             % (root.name, "bone" if len(unbaked) == 1 else "bones", ", ".join(unbaked), "has" if len(unbaked) == 1 else "have"))

    for action in actions:
        meta = _animation_meta(action)
        start, end = (int(round(value)) for value in action.frame_range)
        frames = int(meta["frames"]) if "frames" in meta and start == 0 and end == int(meta.get("frame_end", -1)) else max(1, end - start + 1)
        fps = int(meta.get("fps", bpy.context.scene.render.fps))
        step = 1
        if fps > MAX_ANIMATION_FPS:
            # Every step-th frame at the rate that keeps the animation's length
            step = -(-fps // MAX_ANIMATION_FPS)
            written = max(1, round(fps / step))
            _export_notes.append("%s is %d frames a second and the game's animations have %d at most: it's written at %d, every %s frame"
                                 % (action.name, fps, MAX_ANIMATION_FPS, written, _ordinal(step)))
            fps = written
            frames = (frames - 1) // step + 1

        animation: typing.Dict[str, typing.Any] = {"name": action.name, "fps": fps, "frames": frames}
        if "id" in meta:
            animation["id"] = meta["id"]

        if "joint_count" in meta:
            animation["joint_count"] = skeleton.animation_joint_count(int(meta["joint_count"]), int(meta.get("model_joints", imported_joints)), bones)

        game_angles: typing.List[animation_frames.GameAngles] = []
        if "exact" in meta:
            exact = base64.b64decode(meta["exact"])
            animation["exact"] = file.write_view(exact, "u8")
            try:
                game_angles = animation_frames.game_angles(exact)
            except (struct.error, ValueError):
                # Bytes that don't read keep nothing of the game's
                game_angles = []

        slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
        bag = _existing_channelbag(action, slot) if slot is not None else None
        fcurves = {(fcurve.data_path, fcurve.array_index): fcurve for fcurve in bag.fcurves} if bag is not None else {}
        joints = []
        for bone in armature.data.bones:
            index = joint_of_bone[bone.name]
            aim = bone_aim(bone)
            base = 'pose.bones["%s"].' % bpy.utils.escape_identifier(bone.name)
            tracks = {}
            for path, size, default in _POSE_CHANNELS:
                curves = [fcurves.get((base + path, component)) for component in range(size)]
                if path.startswith("rotation") and all(curve is None for curve in curves):
                    continue

                tracks[path] = [_sample(curve, frames, default[component], start, step) for component, curve in enumerate(curves)]

            rotation_track = _rotation_components(armature.pose.bones[bone.name].rotation_mode, tracks) or [[value] * frames for value in _IDENTITY_ROTATION]
            stretch = stretches.get(bone.name) if stretches is not None else None
            translations, rotations, scales = [], [], []
            for frame in range(frames):
                location = tuple(track[frame] for track in tracks["location"])
                if stretch is not None:
                    location = tuple(stretch @ Vector(location))
                rotation = tuple(track[frame] for track in rotation_track)
                scale = tuple(track[frame] for track in tracks["scale"])
                # Bones are the joints' frames reflected within, their own axes picked of them (axes)
                location, rotation, scale = axes.unaimed_vector(location, aim), axes.unaimed_rotation_wxyz(rotation, aim), axes.unaimed_scale(scale, aim)
                location, rotation = axes.joint_point(location), axes.joint_rotation_wxyz(rotation)
                translation, key_rotation, key_scale = tlm_math.pose_to_key(relative_rests[index], location, rotation, scale)
                translations.extend(translation)
                rotations.extend(tlm_math.wxyz_to_xyzw(key_rotation))
                scales.extend(key_scale)

            entry = flags.find_flags(bone, action.name)
            joint = {
                "joint": index,
                "translation": file.write_view(_constant_or_all(translations, 3), "f32"),
                "rotation": file.write_view(_constant_or_all(rotations, 4), "f32"),
                "scale": file.write_view(_constant_or_all(scales, 3), "f32"),
                "independent_scaling": bool(entry.independent_scaling) if entry is not None else False,
                "additional_rotation": bool(entry.uses_additional_rotation) if entry is not None else False,
            }
            joints.append(joint)
            # Every frame's rotation as (w, x, y, z), like tlm_math's
            joint_tracks.setdefault(index, []).append((joint, numpy.array(rotations, dtype=numpy.float64).reshape(-1, 4)[:, (3, 0, 1, 2)],
                                                       game_angles[index] if index < len(game_angles) else None))

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
            # The game reads them at the main animation's frame (SetAnimationData)
            facial_frames = frames
            stored = facial.get("weights", [])
            stored_frames = len(stored) // stored_shapes if stored_shapes > 0 else 0
            tracks = []
            for shape in range(shapes):
                fcurve = _shape_curve(key_curves, key_blocks, shape) if shape < len(key_blocks) else None
                if fcurve is not None or shape < len(key_blocks) and (shape >= stored_shapes or stored_frames == 0):
                    tracks.append(_sample(fcurve, facial_frames, key_blocks[shape].value, start, step))
                elif shape < stored_shapes and stored_frames > 0:
                    # Shapes without a shape key, or shape keys no curve animates, keep the weights the game had for them
                    tracks.append([stored[min(frame * step, stored_frames - 1) * stored_shapes + shape] for frame in range(facial_frames)])
                else:
                    tracks.append([0.0] * facial_frames)

            animation["facial"] = dict({key: value for key, value in facial.items() if key != "weights"}, frames=facial_frames, shapes=shapes,
                                       weights=file.write_view([track[frame] for frame in range(facial_frames) for track in tracks], "f32"))

        result.append(animation)

    return result, joint_tracks


def _choose_additional_rotations(joints: typing.List[typing.Dict[str, typing.Any]], tracks: typing.Dict[int, typing.List[JointTrack]]) -> None:
    """Gives every joint the additional rotation the game blends its animations best with (animation_frames), and turns the angles of the
    animations made of their keys by it. Joints keep theirs while an animation keeping the game's values for them turns by it: TT Lab
    keeps the game's angles of every frame whose keys still hold them (TlmAnimations.HoldsRotation)."""
    for joint in joints:
        joint_tracks = tracks.get(joint["index"], [])
        stored = joint.get("data", {}).get("AdditionalAnimationRotation")
        current = tlm_math.xyzw_to_wxyz(stored) if isinstance(stored, list) and len(stored) == 4 else animation_frames.IDENTITY
        keeping = animation_frames.holding([(game[1] if game is not None else [], rotations, current if entry["additional_rotation"] else None)
                                            for entry, rotations, game in joint_tracks])
        if any(keeps and track[0]["additional_rotation"] for keeps, track in zip(keeping, joint_tracks)):
            continue

        made = [track for keeps, track in zip(keeping, joint_tracks) if not keeps]
        chosen = animation_frames.choose_frame([rotations for _, rotations, _ in made], current)
        if chosen == current:
            continue

        joint.setdefault("data", {})["AdditionalAnimationRotation"] = list(tlm_math.wxyz_to_xyzw(chosen))
        if chosen != animation_frames.IDENTITY:
            for entry, _, _ in made:
                entry["additional_rotation"] = True


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
        if blender_object.type != "MESH" or not in_scene(blender_object):
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


def model_of_file(path: str) -> typing.Optional[bpy.types.Object]:
    """The model of the scene imported from the file or exported to it last, None when there's none."""
    wanted = os.path.normcase(os.path.abspath(path))
    for blender_object in bpy.data.objects:
        source = blender_object.get(PATH_PROPERTY, "")
        if blender_object.get(ROOT_PROPERTY) and source and in_scene(blender_object) and not blender_object.get("ttt_retarget_source") \
                and os.path.normcase(os.path.abspath(source)) == wanted:
            return blender_object

    return None


def reimport(context: bpy.types.Context, root: bpy.types.Object, path: str, file: typing.Optional[tlm.TlmFile] = None) -> bpy.types.Object:
    """Puts the file's model in the model's place: the model goes with its animations and the file's comes in under its name, in its
    collections, its animations without Blender's numbers."""
    name = root.name
    collections = list(root.users_collection)
    old_actions = animations_of(root) if root.get(KIND_PROPERTY) == "ogi" else []
    new_root = import_file(context, path, refresh_materials=True, file=file)
    _move_to_collections(new_root, collections)
    new_actions = animations_of(new_root) if new_root.get(KIND_PROPERTY) == "ogi" else []
    for action in old_actions:
        bpy.data.actions.remove(action)

    remove_model(root)
    new_root.name = name
    _drop_name_numbers(_descendants(new_root))
    for action in new_actions:
        base, dot, number = action.name.rpartition(".")
        if dot and number.isdigit() and len(number) == 3 and bpy.data.actions.get(base) is None:
            action.name = base

    return new_root


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


def retarget_animations(source: bpy.types.Object, target: bpy.types.Object, bones: typing.Optional[typing.Dict[str, str]] = None,
                        shapes: typing.Optional[typing.Dict[str, str]] = None) -> int:
    """Makes the target armature (or model) play the source's animations: a copy of every action of the source becomes the
    target's, in which every bone standing for a joint (bones: source joint -> target bone, else the one of the same joint or name)
    turns from its own rest, in the world, as much as the source's turned from its on every frame, and moves as far beyond where its
    parent's turn leaves it as the source's did (scaled by how much bigger the target skeleton is, and in each armature's own scale),
    whatever way its axes point. The shape keys are animated in the same order, or as shapes pairs them. Rerunning it writes the copies
    again. Returns how many animations were retargeted."""
    source_armature, target_armature = _armature_object(source), _armature_object(target)
    if source_armature is None or target_armature is None or source_armature == target_armature:
        return 0

    source_root, target_root = find_root(source_armature), find_root(target_armature)
    bones = bones if bones is not None else _matched_bones(source_armature, target_armature)
    original, incoming = _skeleton(source_armature), _skeleton(target_armature)
    scale = retarget.size_ratio(original, incoming, {target_name: source_name for source_name, target_name in bones.items()})
    source_shape = shape_object_of(source_root) if source_root is not None else None
    target_shape = shape_object_of(target_root) if target_root is not None else None
    source_shape, target_shape = source_shape or _shape_of_armature(source_armature), target_shape or _shape_of_armature(target_armature)
    if shapes is None:
        shapes = {}
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

        # Kept like an imported one while nothing plays it
        copy.use_fake_user = True

        copy[OWNER_PROPERTY] = owner
        meta = _animation_meta(copy)
        if "joint_count" in meta and "model_joints" not in meta:
            meta["model_joints"] = _imported_joints(source_armature)
            copy[ANIMATION_PROPERTY] = json.dumps(meta)

        _retarget_action(action, copy, bones, original, incoming, scale, source_armature, target_armature, shapes)
        first = first or copy

    if first is not None and (target_armature.animation_data is None or target_armature.animation_data.action is None):
        assign_action(target_armature, first)
        flags.apply_inherit_scale(target_armature)

    return len(actions)


def _mean_scale(blender_object: bpy.types.Object) -> float:
    scale = blender_object.matrix_world.to_scale()
    return max((abs(scale[0]) + abs(scale[1]) + abs(scale[2])) / 3.0, 1e-9)


def stored_joints_of_bones(armature: bpy.types.Object) -> typing.Dict[str, int]:
    """The joint of every bone as it's set up: the index of its Joint type, bones without one or with one another bone has get the next
    free ones. Exporting numbers them in the order the game walks the skeleton instead (joints_of_bones)."""
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


def _matched_bones(source_armature: bpy.types.Object, target_armature: bpy.types.Object) -> typing.Dict[str, str]:
    """The target bone of every source bone: the one of the same joint, else the one of the same name."""
    source_joints = stored_joints_of_bones(source_armature)
    target_joints = stored_joints_of_bones(target_armature)
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


_IDENTITY_ROTATION = (1.0, 0.0, 0.0, 0.0)
# A pose bone's channels with their values at rest; an axis and angle is the angle first
_POSE_CHANNELS = (("location", 3, (0.0, 0.0, 0.0)), ("rotation_quaternion", 4, _IDENTITY_ROTATION), ("rotation_euler", 3, (0.0, 0.0, 0.0)),
                  ("rotation_axis_angle", 4, (0.0, 0.0, 1.0, 0.0)), ("scale", 3, (1.0, 1.0, 1.0)))


def _retarget_action(action: bpy.types.Action, copy: bpy.types.Action, bones: typing.Dict[str, str], original: retarget.Skeleton, incoming: retarget.Skeleton,
                     scale: float, source_armature: bpy.types.Object, target_armature: bpy.types.Object, shapes: typing.Dict[str, str]) -> None:
    slot = next((slot for slot in action.slots if slot.target_id_type == "OBJECT"), None)
    bag = _existing_channelbag(action, slot) if slot is not None else None
    start, end = (int(round(value)) for value in action.frame_range)
    frames = list(range(start, max(start, end) + 1))
    # Every bone of the source counts, a bone the target doesn't have still turns everything under it
    poses = _sample_poses(bag, source_armature, list(original), frames) if bag is not None else {}
    # Pose bones move in their armature's scale: an FBX rig's 0.01 made its moves a hundred times too short
    source_scale, target_scale = _mean_scale(source_armature), _mean_scale(target_armature)
    poses = {name: (tuple(value * source_scale for value in location), rotation, size) for name, (location, rotation, size) in poses.items()}
    retargeted = retarget.retarget_pose(original, incoming, {target_name: source_name for source_name, target_name in bones.items()}, poses, scale)
    retargeted = {name: (tuple(value / target_scale for value in location), rotation, size) for name, (location, rotation, size) in retargeted.items()}

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
            components = tracks[bone][channel] = [numpy.full(len(frames), default[component]) for component in range(size)]

        components[fcurve.array_index] = numpy.array(_samples(fcurve, frames), dtype=numpy.float64)

    result: typing.Dict[str, retarget.Pose] = {}
    for bone in bones:
        channels = tracks.get(bone)
        if channels is None:
            continue

        mode = armature.pose.bones[bone].rotation_mode if bone in armature.pose.bones else "QUATERNION"
        location = tuple(channels["location"]) if "location" in channels else (0.0, 0.0, 0.0)
        scale = tuple(channels["scale"]) if "scale" in channels else (1.0, 1.0, 1.0)
        components = _rotation_components(mode, channels)
        rotation = tuple(numpy.asarray(component, dtype=numpy.float64) for component in components) if components is not None else _IDENTITY_ROTATION

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
