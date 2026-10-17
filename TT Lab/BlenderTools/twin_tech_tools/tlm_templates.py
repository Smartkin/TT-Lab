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

An OGI template is a root with an armature of joint 0, a box that's its rigid body and the holders of the exit points and collision
hulls, a skydome template a dome, a model template a box with its picture. A scenery template is a root with the tree node the game
culls it with holding a ground mesh, its lights, its collision and its dynamic scenery. A save icon template is a root with the icon's
mesh and its textured material. Their geometry is laid out in the game's space, Y up, and put in Blender's axes the way an import puts a
model's (axes).
"""

import json
import typing
import uuid

import bpy
from mathutils import Vector

from . import axes
from . import gs_material
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


def _mesh_object(name: str, geometry: Geometry, kind: str, parent: bpy.types.Object, collection: bpy.types.Collection,
                 frame: str = tlm_blender.MODEL_FRAME) -> bpy.types.Object:
    """A mesh of the geometry of the game's space with the attributes imported meshes have, all of it one part, in the frame's axes."""
    positions, faces, uvs = geometry
    convert = axes.joint_point if frame == tlm_blender.JOINT_FRAME else axes.point
    turned_faces = []
    corner_uvs = []
    start = 0
    for face in faces:
        # The reflection turns the faces around, their corners' UVs go along
        order = axes.face(list(range(len(face))))
        turned_faces.append(tuple(face[corner] for corner in order))
        corner_uvs.extend(uvs[start + corner] for corner in order)
        start += len(face)

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([convert(position) for position in positions], [], turned_faces)
    mesh.update()
    mesh.uv_layers.new(name="UVMap").data.foreach_set("uv", [value for uv in corner_uvs for value in uv])
    mesh.attributes.new(tlm_blender.PART_ATTRIBUTE, "INT", "FACE")
    mesh.attributes.new(tlm_blender.VERTEX_PART_ATTRIBUTE, "INT", "POINT")
    blender_object = tlm_blender._new_object(name, mesh, parent, collection)
    blender_object[tlm_blender.KIND_PROPERTY] = kind
    return blender_object


def new_ogi(context: bpy.types.Context, name: str = "OGI") -> bpy.types.Object:
    """A new OGI: the root with its bounding box around the box, an armature of joint 0 and the box, a rigid body of joint 0 (a mesh
    neither deformed nor on a bone is one, parts), with the holders exit points and hulls go in. Meshes rigged to the armature the way
    Blender rigs them become the model's: deformed by it its skin (the blend skin with shape keys), parented to a bone a rigid body of the
    bone's joint. The game gives a model of one joint and no exit points no animator and draws only its rigid models
    (ModelNode::SetOgi): a skin needs a bone under the root."""
    root, collection = _root(context, name, "Ogi", "ogi")
    half = SKIN_SIZE / 2.0
    tlm_blender._read_data(root, "Ogi", {"BoundingBoxMin": (-half, 0.0, -half, 1.0), "BoundingBoxMax": (half, SKIN_SIZE, half, 1.0)})

    armature_data = bpy.data.armatures.new(name + " Armature")
    armature = tlm_blender._new_object("armature", armature_data, root, collection)
    armature[tlm_blender.KIND_PROPERTY] = "armature"
    context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    # Pointing up it rests unturned, like the game's roots
    root_bone = armature_data.edit_bones.new("Joint 0")
    root_bone.head = axes.point((0.0, 0.0, 0.0))
    root_bone.tail = axes.point((0.0, half, 0.0))
    bpy.ops.object.mode_set(mode="OBJECT")
    tlm_blender._read_data(armature_data.bones["Joint 0"], "Joint", {"Index": 0})
    armature.pose.bones["Joint 0"].rotation_mode = "QUATERNION"

    body = _mesh_object("Body", box(SKIN_SIZE, SKIN_SIZE, SKIN_SIZE), "", root, collection)
    del body[tlm_blender.KIND_PROPERTY]
    for holder_name, kind in (("Exit Points", "exit_points"), ("Collision Hulls", "collision_hulls")):
        _empty(holder_name, kind, root, collection, "PLAIN_AXES", 0.1)

    return root


# The sky's dome: big enough to hold what the camera sees of a level from where it is, drawn around it (the game draws it at the camera)
SKY_RADIUS = 50.0
SKY_TOP = (0.35, 0.55, 0.95)
SKY_HORIZON = (0.85, 0.9, 1.0)


def _dome(radius: float, rings: int = 8, segments: int = 16) -> Geometry:
    """The upper half of a sphere around the origin and a ring below the horizon, its faces facing in: the camera is inside it."""
    import math

    positions = [(0.0, radius, 0.0)]
    for ring in range(1, rings + 1):
        # Down to a little below the horizon
        angle = math.radians(100.0) * ring / rings
        for segment in range(segments):
            turn = 2.0 * math.pi * segment / segments
            positions.append((radius * math.sin(angle) * math.cos(turn), radius * math.cos(angle), radius * math.sin(angle) * math.sin(turn)))

    # Counter-clockwise seen from inside
    faces = []
    for segment in range(segments):
        following = (segment + 1) % segments
        faces.append((0, 1 + segment, 1 + following))

    for ring in range(rings - 1):
        start = 1 + ring * segments
        below = start + segments
        for segment in range(segments):
            following = (segment + 1) % segments
            faces.append((start + segment, below + segment, below + following, start + following))

    uvs = [(0.0, 0.0)] * sum(len(face) for face in faces)
    return positions, faces, uvs


def new_skydome(context: bpy.types.Context, name: str = "Skydome") -> bpy.types.Object:
    """A new sky: the root and a dome drawn with the vertex colors of its corners, a sky's material (the game's skies are shader type 10 in
    render bucket 0 testing no depth, drawn around the camera before the level)."""
    root, collection = _root(context, name, "Skydome", "skydome")
    dome = _mesh_object("Sky", _dome(SKY_RADIUS), "skydome_mesh", root, collection)
    tlm_blender._read_data(dome, "SkydomeMesh", {"Order": 0})
    colors = dome.data.color_attributes.new(tlm_blender.COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT")
    shades = []
    for vertex in dome.data.vertices:
        height = max(0.0, min(1.0, vertex.co.z / SKY_RADIUS))
        shades.extend([horizon + (top - horizon) * height for top, horizon in zip(SKY_TOP, SKY_HORIZON)] + [1.0])

    colors.data.foreach_set("color_srgb", shades)
    dome.data.color_attributes.active_color = colors
    dome.data.color_attributes.render_color_index = dome.data.color_attributes.find(tlm_blender.COLOR_ATTRIBUTE)
    material = bpy.data.materials.new(name)
    shader = gs_material.new_shader(10)
    shader["DepthTest"] = 1
    shader["ZValueDrawingMask"] = 1
    material_settings.load(material, {"ActivatedShaders": 0, "Name": gs_material.ascii_name(name), "DmaChainIndex": 0, "Shaders": [shader]})
    dome.data.materials.append(material)
    return root


def new_model(context: bpy.types.Context, name: str = "Model") -> bpy.types.Object:
    """A new model the scenery and LODs place: the root and a box standing on the ground with a checkered picture made in Blender."""
    root, collection = _root(context, name, "Mesh", "mesh")
    model = _mesh_object(name, box(SKIN_SIZE, SKIN_SIZE, SKIN_SIZE), "part_mesh", root, collection)
    colors = model.data.color_attributes.new(tlm_blender.COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT")
    colors.data.foreach_set("color_srgb", [128.0 / 255.0, 128.0 / 255.0, 128.0 / 255.0, 1.0] * len(model.data.vertices))
    model.data.color_attributes.active_color = colors
    material = bpy.data.materials.new(name)
    material_settings.load(material, gs_material.new_material(name, textured=True))
    with material_settings.suspended():
        material_settings.settings_of(material).shaders[0].image = _checker_image(name + " Texture", ICON_TEXTURE_SIZE, 8)

    material_settings.refresh(material)
    model.data.materials.append(material)
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
    tlm_blender._set_node_transform(ambient, {"translation": [0.0, 5.0, 0.0]}, tlm_blender.LIGHT_FRAME)
    sun = _empty("Directional Light", "directional_light", lights, collection, *tlm_scenery._EMPTY_DISPLAY["directional_light"])
    tlm_blender._read_data(sun, "DirectionalLight", {"Color": (THIRD_GREY, THIRD_GREY, THIRD_GREY, 0.0), "Intensity": 3.0})
    # The arrow, the node's Z axis, points at where the light comes from: from above, a little to the side so faces don't all get the
    # same light
    turn = Vector((0.0, 0.0, 1.0)).rotation_difference(Vector((0.3, 1.0, 0.3)).normalized())
    tlm_blender._set_node_transform(sun, {"translation": [0.0, 10.0, 0.0], "rotation": [turn.x, turn.y, turn.z, turn.w]}, tlm_blender.LIGHT_FRAME)

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
