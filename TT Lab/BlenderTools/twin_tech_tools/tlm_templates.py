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

"""New models to build in Blender, laid out the way imported ones are so they export the same way.

An OGI template is a root with an armature of two joints, a box rigid body on the second, an empty skin and blend skin the armature
deforms and the holders of the rigid bodies, exit points and collision hulls. A scenery template is a root with the tree node the game
culls it with holding a ground mesh, its lights, its collision and its dynamic scenery. A save icon template is a root with the icon's
mesh and its textured material. Everything under a root is in the game's space, Y up, the root turns it Z up for Blender.
"""

import json
import typing
import uuid

import bpy
from mathutils import Vector

from . import material_settings
from . import tlm_blender
from . import tlm_scenery

# Sizes in the game's units: the OGI's body a box a unit wide standing on the ground, the scenery's ground 20 units across, the save icon
# a box of 3 (the game's own icon is about 4 wide and 5 tall)
SKIN_SIZE = 1.0
GROUND_SIZE = 20.0
ICON_SIZE = 3.0
# The save icon's texture, the size the console takes
ICON_TEXTURE_SIZE = 128
# The game's icon's header (Startup\Crash.ico): no run length encoding, one frame, played at the console's speed
ICON_HEADER = {"FileId": 0x10000, "TextureType": 6, "HeaderValue": 0x3F800000, "AnimationTag": 1, "FrameLength": 1, "AnimationSpeed": 1.0, "PlayOffset": 0}
# Half the size of the box the game keeps the chunk's objects in, around the origin, what TT Lab gives new chunks (SceneryBounds)
SCENERY_HALF_SIZE = (200.0, 100.0, 200.0)
# The collision's placeholder surface, TT Lab takes the project's surface of the material's name (its first one otherwise)
DEFAULT_SURFACE = "SURF_DEFAULT_0"
# The lights' color, white the way the game's tools kept colors: adding up to 1
THIRD_GREY = 1.0 / 3.0

Geometry = typing.Tuple[typing.List[typing.Tuple[float, float, float]], typing.List[typing.Tuple[int, ...]], typing.List[typing.Tuple[float, float]]]


def box(width: float, height: float, depth: float) -> Geometry:
    """A box standing on the ground, centered on the up axis: its corners, its faces wound counter-clockwise seen from outside and
    a UV square per face corner."""
    x, z = width / 2.0, depth / 2.0
    corners = [(-x, 0.0, -z), (x, 0.0, -z), (x, 0.0, z), (-x, 0.0, z), (-x, height, -z), (x, height, -z), (x, height, z), (-x, height, z)]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (3, 2, 6, 7), (1, 0, 4, 5), (2, 1, 5, 6), (0, 3, 7, 4)]
    return corners, faces, [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)] * len(faces)


def ground(size: float) -> Geometry:
    """A square on the ground facing up, centered on the origin."""
    half = size / 2.0
    return [(-half, 0.0, -half), (half, 0.0, -half), (half, 0.0, half), (-half, 0.0, half)], [(0, 3, 2, 1)], [(0.0, 0.0), (0.0, 1.0), (1.0, 1.0), (1.0, 0.0)]


def _root(context: bpy.types.Context, name: str, asset_type: str, kind: str) -> typing.Tuple[bpy.types.Object, bpy.types.Collection]:
    collection = bpy.data.collections.new(name)
    context.scene.collection.children.link(collection)
    tlm_blender.use_standard_view(context)
    root = tlm_blender._new_object(name, None, None, collection)
    root.empty_display_type = "PLAIN_AXES"
    root.matrix_basis = tlm_blender.Y_UP
    root[tlm_blender.ROOT_PROPERTY] = asset_type
    root[tlm_blender.PATH_PROPERTY] = ""
    root[tlm_blender.UID_PROPERTY] = uuid.uuid4().hex
    root[tlm_blender.KIND_PROPERTY] = kind
    return root, collection


def _empty(name: str, kind: str, parent: bpy.types.Object, collection: bpy.types.Collection, display: str, size: float) -> bpy.types.Object:
    blender_object = tlm_blender._new_object(name, None, parent, collection)
    blender_object[tlm_blender.KIND_PROPERTY] = kind
    blender_object.empty_display_type = display
    blender_object.empty_display_size = size
    return blender_object


EMPTY: Geometry = ([], [], [])


def _mesh_object(name: str, geometry: Geometry, kind: str, parent: bpy.types.Object, collection: bpy.types.Collection) -> bpy.types.Object:
    """A mesh of the geometry with the attributes imported meshes have, all of it one part."""
    positions, faces, uvs = geometry
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(positions, [], faces)
    mesh.update()
    mesh.uv_layers.new(name="UVMap").data.foreach_set("uv", [value for uv in uvs for value in uv])
    mesh.attributes.new(tlm_blender.PART_ATTRIBUTE, "INT", "FACE")
    mesh.attributes.new(tlm_blender.VERTEX_PART_ATTRIBUTE, "INT", "POINT")
    blender_object = tlm_blender._new_object(name, mesh, parent, collection)
    blender_object[tlm_blender.KIND_PROPERTY] = kind
    return blender_object


def new_ogi(context: bpy.types.Context, name: str = "OGI") -> bpy.types.Object:
    """A new OGI: the root with its bounding box around the box, an armature of joint 0 and joint 1 under it, the box a rigid body following
    joint 1, a skin and a blend skin the armature deforms that are empty, to be filled with the model's meshes (the model has none of them
    until they are), and the empty holders exit points and hulls go under. The game gives a model of one joint and no exit points no
    animator and draws only its rigid models (ModelNode::SetOgi): the second joint lets a skin show once it's filled."""
    root, collection = _root(context, name, "Ogi", "ogi")
    half = SKIN_SIZE / 2.0
    tlm_blender._read_data(root, "Ogi", {"BoundingBoxMin": (-half, 0.0, -half, 1.0), "BoundingBoxMax": (half, SKIN_SIZE, half, 1.0)})

    armature_data = bpy.data.armatures.new(name + " Armature")
    armature = tlm_blender._new_object("armature", armature_data, root, collection)
    armature[tlm_blender.KIND_PROPERTY] = "armature"
    context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    root_bone = armature_data.edit_bones.new("Joint 0")
    root_bone.head = (0.0, 0.0, 0.0)
    root_bone.tail = (0.0, half, 0.0)
    body_bone = armature_data.edit_bones.new("Joint 1")
    body_bone.head = (0.0, half, 0.0)
    body_bone.tail = (0.0, SKIN_SIZE, 0.0)
    body_bone.parent = root_bone
    bpy.ops.object.mode_set(mode="OBJECT")
    for index, bone_name in enumerate(("Joint 0", "Joint 1")):
        tlm_blender._read_data(armature_data.bones[bone_name], "Joint", {"Index": index})
        armature.pose.bones[bone_name].rotation_mode = "QUATERNION"

    skin = _mesh_object("Skin", EMPTY, "skin", root, collection)
    tlm_blender._read_data(skin, "Skin", {})
    skin.modifiers.new("Armature", "ARMATURE").object = armature
    blend_skin = _mesh_object("Blend Skin", EMPTY, "shape", root, collection)
    tlm_blender._read_data(blend_skin, "BlendSkin", {})
    # Its shapes are the shape keys over this one
    blend_skin.shape_key_add(name="Basis", from_mix=False).interpolation = "KEY_LINEAR"
    blend_skin.modifiers.new("Armature", "ARMATURE").object = armature

    bodies = _empty("Rigid Bodies", "rigid_bodies", root, collection, "PLAIN_AXES", 0.1)
    body = _mesh_object("Body", box(SKIN_SIZE, SKIN_SIZE, SKIN_SIZE), "body", bodies, collection)
    tlm_blender._read_data(body, "Body", {})
    # Like an imported body: it follows its bone through a Child Of constraint, where it is in the joint's space. Joint 1 rests half way up
    body[tlm_blender.JOINT_PROPERTY] = 1
    body.location = (0.0, -half, 0.0)
    constraint = body.constraints.new("CHILD_OF")
    constraint.target = armature
    constraint.subtarget = "Joint 1"
    constraint.inverse_matrix = root.matrix_basis.inverted()
    for holder_name, kind in (("Exit Points", "exit_points"), ("Collision Hulls", "collision_hulls")):
        _empty(holder_name, kind, root, collection, "PLAIN_AXES", 0.1)

    return root


def new_scenery(context: bpy.types.Context, name: str = "Scenery") -> bpy.types.Object:
    """A new scenery: the root with the box the game keeps the chunk's objects in that TT Lab gives new chunks, a ground mesh in its
    Meshes and no LODs yet, an ambient and a directional light, the ground again as the collision with a placeholder surface, and an
    empty dynamic scenery."""
    root, collection = _root(context, name, "Scenery", "scenery")
    tlm_blender._read_data(root, "Scenery", {"BoundsMin": list(-value for value in SCENERY_HALF_SIZE), "BoundsMax": list(SCENERY_HALF_SIZE)})
    meshes = _empty("Meshes", "scenery_meshes", root, collection, *tlm_scenery._EMPTY_DISPLAY["scenery_meshes"])
    _empty("LODs", "scenery_lods", root, collection, *tlm_scenery._EMPTY_DISPLAY["scenery_lods"])
    mesh = _mesh_object("Ground", ground(GROUND_SIZE), "scenery_mesh", meshes, collection)
    tlm_blender._read_data(mesh, "SceneryMesh", {})

    # The lights TT Lab gives new chunks, the brightness in the intensity: every level has an ambient light of a third grey at 3 to 6
    lights = _empty("Lights", "lights", root, collection, *tlm_scenery._EMPTY_DISPLAY["lights"])
    ambient = _empty("Ambient Light", "ambient_light", lights, collection, *tlm_scenery._EMPTY_DISPLAY["ambient_light"])
    tlm_blender._read_data(ambient, "AmbientLight", {"Color": (THIRD_GREY, THIRD_GREY, THIRD_GREY, 0.0), "Intensity": 4.5})
    ambient.location = (0.0, 5.0, 0.0)
    sun = _empty("Directional Light", "directional_light", lights, collection, *tlm_scenery._EMPTY_DISPLAY["directional_light"])
    tlm_blender._read_data(sun, "DirectionalLight", {"Color": (THIRD_GREY, THIRD_GREY, THIRD_GREY, 0.0), "Intensity": 3.0})
    # The arrow points at where the light comes from: from above, a little to the side so faces don't all get the same light
    sun.rotation_mode = "QUATERNION"
    sun.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(Vector((0.3, 1.0, 0.3)).normalized())
    sun.location = (0.0, 10.0, 0.0)

    collision = _mesh_object("Collision", ground(GROUND_SIZE), "collision", root, collection)
    tlm_blender._read_data(collision, "Collision", {})
    collision.data.materials.append(tlm_scenery.placeholder_surface(DEFAULT_SURFACE))
    dynamic = _empty("Dynamic Scenery", "dynamic_scenery", root, collection, *tlm_scenery._EMPTY_DISPLAY["dynamic_scenery"])
    tlm_blender._read_data(dynamic, "DynamicScenery", {})
    return root


def _checker_image(name: str, size: int, squares: int) -> bpy.types.Image:
    """A picture of two greys in squares, packed into the file: what goes into the model file with its material."""
    image = bpy.data.images.new(name, size, size, alpha=True)
    cell = max(1, size // squares)
    pixels = []
    for y in range(size):
        for x in range(size):
            value = 0.6 if (x // cell + y // cell) % 2 == 0 else 0.4
            pixels.extend((value, value, value, 1.0))

    image.pixels.foreach_set(pixels)
    image.pack()
    return image


def new_save_icon(context: bpy.types.Context, name: str = "Save Icon") -> bpy.types.Object:
    """A new PS2 memory card icon (Startup\\Crash.ico, the game has one): the root with the game's icon's header, its one mesh a box standing
    on the ground with the vertex colors the console draws the texture at (0x80), and a material made in Blender whose 128x128 picture the
    icon's texture is made of. Shape keys added to the mesh are the icon's shapes and keys on their values its animation, the root's Frame
    Length how long it loops."""
    root, collection = _root(context, name, "SaveIcon", "save_icon")
    tlm_blender._read_data(root, "SaveIcon", dict(ICON_HEADER))
    root[tlm_blender.SAVE_ICON_PROPERTY] = json.dumps({"frames": [{"shape": 0, "keys": [0.0, 1.0]}]})
    icon = _mesh_object("Icon", box(ICON_SIZE, ICON_SIZE, ICON_SIZE), "icon_mesh", root, collection)
    colors = icon.data.color_attributes.new(tlm_blender.COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT")
    colors.data.foreach_set("color_srgb", [128.0 / 255.0, 128.0 / 255.0, 128.0 / 255.0, 1.0] * len(icon.data.vertices))
    icon.data.color_attributes.active_color = colors
    icon.data.color_attributes.render_color_index = icon.data.color_attributes.find(tlm_blender.COLOR_ATTRIBUTE)
    material = bpy.data.materials.new(name)
    material[tlm_blender.BLENDER_ID_PROPERTY] = uuid.uuid4().hex
    material_settings.draw_default(material, _checker_image(name + " Texture", ICON_TEXTURE_SIZE, 8))
    icon.data.materials.append(material)
    return root
