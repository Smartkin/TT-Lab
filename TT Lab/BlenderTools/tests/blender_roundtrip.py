"""Imports a TT Lab model file with the add-on and exports it back, in Blender.

    blender --background --factory-startup --python tests/blender_roundtrip.py -- fixtures/ogi.tlm fixtures/ogi_from_blender.tlm

TT Lab's tests read the export of tests/fixtures/ogi.tlm (ogi_from_blender.tlm) and check it's the same model, run this again
after changing how the add-on imports or exports. With --edit the bone "Joint 1" is turned by 30 degrees on frame 2 of "Walk" and the
first vertex of the rigid body moved along X before exporting, which makes ogi_edited_in_blender.tlm. With --customize the model
gets what a modder does to it (the skin's object moved, a shape key, bodies and an exit point put on bones, a bone added, an
animation made longer and one made in Blender from frame 1 at 60 frames a second with a bone in Euler angles), making
ogi_customized_in_blender.tlm, and with --swap its skin and shape are swapped for meshes made in Blender, making
ogi_swapped_in_blender.tlm. With --retarget its animations are retargeted to a second copy of it whose bone "Joint 1" was moved
and "Joint 2" renamed, so the joint animates from where it was moved to, and the copy is exported as ogi_retargeted_in_blender.tlm.
A save icon (fixtures/save_icon.tlm) makes save_icon_from_blender.tlm, with --edit its first vertex is moved half a unit along X and the
second shape's middle key halved, making save_icon_edited_in_blender.tlm. With --collision a scenery (fixtures/scenery.tlm) gets its
collision made anew out of its meshes with the Generate Collision button with every triangle, and the first mesh's added again, which
leaves it as it is, then as coarse as the game's, making scenery_collision_from_blender.tlm. With --add a mesh made in Blender goes into the scenery's Meshes and a LOD of two meshes into
its LODs, making scenery_added_in_blender.tlm. Every scenery imported is checked to have its placed meshes in Meshes and its LODs in LODs.
They write what TT Lab should read next to the file, as JSON. With --scaled the model's armature object is scaled (2, 1, 3), like an
FBX rig's 0.01, and an action moves joint 0: the export checks itself that the joint moves as far as Blender shows (pose locations
don't have the object's scale, the bind poses do). With --blend the scene is also saved, to look at it.
"""

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm  # noqa: E402
from twin_tech_tools import tlm_blender  # noqa: E402
from twin_tech_tools import tlm_scenery  # noqa: E402


def _check(condition, message):
    if not condition:
        raise AssertionError(message)


def _area_normal(mesh, matrix):
    """The faces' normals added up, each as long as its face is big, where the matrix puts them"""
    mesh.calc_loop_triangles()
    total = Vector((0.0, 0.0, 0.0))
    for triangle in mesh.loop_triangles:
        a, b, c = (matrix @ mesh.vertices[index].co for index in triangle.vertices)
        total += (b - a).cross(c - a)

    return total


def _generate_collision(root):
    """Makes the scenery's collision anew out of its meshes with every triangle, adds the first mesh again, which changes nothing, then
    makes it as coarse as the game's and gives what TT Lab should read"""
    sources = tlm_scenery.collision_sources(root)
    for source in sources:
        source.data.calc_loop_triangles()

    corners = sum(len(source.data.loop_triangles) for source in sources)
    _check(len(sources) == 5, "the scenery draws %d meshes" % len(sources))
    bpy.context.view_layer.objects.active = root
    _check(bpy.ops.ttt.generate_collision(source="SCENERY", mode="REPLACE", tolerance=0.0, hull_distance=0.0) == {"FINISHED"}, "the collision wasn't made")
    collision = tlm_scenery.collision_of(root)
    mesh = collision.data
    mesh.calc_loop_triangles()
    triangles = len(mesh.loop_triangles)
    _check(0 < triangles <= corners, "%d triangles made of %d" % (triangles, corners))
    _check(tlm_scenery.COLLISION_TRIANGLE_ATTRIBUTE not in mesh.attributes, "the game's order of triangles stayed")
    used = {mesh.polygons[triangle.polygon_index].material_index for triangle in mesh.loop_triangles}
    _check(len(used) == 1, "the triangles are on %d surfaces" % len(used))
    # Wound like the game's, the other way from the meshes drawn
    first = sources[0]
    to_collision = collision.matrix_world.inverted() @ first.matrix_world
    drawn = _area_normal(first.data, to_collision)
    _check(drawn.length > 0 and _area_normal(mesh, Matrix.Identity(4)).dot(drawn) < 0, "the collision isn't wound like the game's")

    # Made where the scenery has no collision, the new one's vertexes are where the meshes are in the scenery
    corners = sorted(tuple(round(value, 4) for value in vertex.co) for vertex in mesh.vertices)
    bpy.data.objects.remove(collision)
    bpy.context.view_layer.objects.active = root
    _check(bpy.ops.ttt.generate_collision(source="SCENERY", mode="REPLACE", tolerance=0.0, hull_distance=0.0) == {"FINISHED"}, "the collision wasn't made again")
    collision = tlm_scenery.collision_of(root)
    mesh = collision.data
    _check(sorted(tuple(round(value, 4) for value in vertex.co) for vertex in mesh.vertices) == corners, "the collision made anew isn't where the meshes are")

    bpy.context.view_layer.objects.active = first
    _check(bpy.ops.ttt.generate_collision(source="SELECTED", mode="ADD", tolerance=0.0, hull_distance=0.0) == {"FINISHED"}, "the first mesh wasn't added")
    mesh.calc_loop_triangles()
    _check(len(mesh.loop_triangles) == triangles, "adding the first mesh again made %d triangles" % (len(mesh.loop_triangles) - triangles))

    # As coarse as the game's by default
    bpy.context.view_layer.objects.active = root
    _check(bpy.ops.ttt.generate_collision(source="SCENERY", mode="REPLACE") == {"FINISHED"}, "the coarse collision wasn't made")
    mesh = tlm_scenery.collision_of(root).data
    mesh.calc_loop_triangles()
    coarse = len(mesh.loop_triangles)
    _check(0 < coarse <= triangles, "%d coarse triangles made of %d" % (coarse, triangles))
    used = {mesh.polygons[triangle.polygon_index].material_index for triangle in mesh.loop_triangles}
    _check(len(used) == 1, "the coarse triangles are on %d surfaces" % len(used))
    return {"Triangles": coarse, "Vertexes": len(mesh.vertices), "Surface": mesh.materials[next(iter(used))].name}


def _scenery_groups(root):
    """The scenery's Meshes and LODs, checked to hold every placed mesh and LOD"""
    groups = {tlm_blender.role_of(child): child for child in root.children}
    meshes = groups.get("scenery_meshes")
    lods = groups.get("scenery_lods")
    _check(meshes is not None and meshes.name == "Meshes" and lods is not None and lods.name == "LODs",
           "the scenery's root has %s" % sorted(child.name for child in root.children))
    _check(not any(tlm_blender.role_of(child) in ("scenery_mesh", "scenery_lod") for child in root.children), "meshes are left under the root")
    _check(len(meshes.children) > 0 and all(tlm_blender.role_of(child) == "scenery_mesh" for child in meshes.children),
           "Meshes has %s" % sorted(child.name for child in meshes.children))
    _check(all(tlm_blender.role_of(lod) == "scenery_lod" and len(lod.children) > 0 and all(tlm_blender.role_of(level) == "lod_mesh" for level in lod.children)
               for lod in lods.children), "LODs has %s" % sorted(child.name for child in lods.children))
    return meshes, lods


def _add_placements(root):
    """Puts a mesh made in Blender into the scenery's Meshes and a LOD made in Blender, an empty with two meshes, into its LODs: they're
    written as a placed mesh and a LOD of two levels by what they're in"""
    meshes, lods = _scenery_groups(root)
    rock = _new_mesh_object("Rock made in Blender", root, meshes, *_cube())
    rock.location = (10.0, 0.0, 10.0)
    lod = bpy.data.objects.new("LOD made in Blender", None)
    root.users_collection[0].objects.link(lod)
    lod.parent = lods
    lod.location = (-10.0, 0.0, 10.0)
    near = _new_mesh_object("Near", root, lod, *_cube())
    far = _new_mesh_object("Far", root, lod, *_cube(1.0))
    _check(tlm_blender.role_of(rock) == "scenery_mesh", "the mesh made in Meshes is a %s" % tlm_blender.role_of(rock))
    _check(tlm_blender.role_of(lod) == "scenery_lod", "the empty made in LODs is a %s" % tlm_blender.role_of(lod))
    _check(tlm_blender.role_of(near) == tlm_blender.role_of(far) == "lod_mesh", "the meshes made in the LOD are %s" % tlm_blender.role_of(near))
    return {"Meshes": len(meshes.children), "Lods": len(lods.children), "AddedLevels": 2, "AddedMesh": list(rock.location), "AddedLod": list(lod.location)}


def _edit(root):
    from mathutils import Quaternion

    action = bpy.data.actions["Walk"]
    bag = tlm_blender._existing_channelbag(action, next(slot for slot in action.slots if slot.target_id_type == "OBJECT"))
    path = 'pose.bones["Joint 1"].rotation_quaternion'
    curves = [next(fcurve for fcurve in bag.fcurves if fcurve.data_path == path and fcurve.array_index == index) for index in range(4)]
    rotation = Quaternion([curve.evaluate(2) for curve in curves])
    turned = Quaternion((0.0, 0.0, 1.0), math.radians(30)) @ rotation
    for index, curve in enumerate(curves):
        # A track of one key gets a key on every frame, the others keep their values
        if len(curve.keyframe_points) == 1:
            value = curve.keyframe_points[0].co[1]
            curve.keyframe_points.add(3)
            for frame in range(4):
                curve.keyframe_points[frame].co = (frame, value)
                curve.keyframe_points[frame].interpolation = "LINEAR"

        next(point for point in curve.keyframe_points if point.co[0] == 2).co[1] = turned[index]
        curve.update()

    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    body.data.vertices[0].co.x += 0.1


# Blender adds shape keys at full weight, the face has to start at rest while no facial animation plays
def _edit_icon(root):
    """Moves a save icon's first vertex half a unit along X in every shape and halves the second shape's middle key."""
    mesh_object = next(child for child in root.children if child.type == "MESH")
    key = mesh_object.data.shape_keys
    for block in key.key_blocks:
        block.data[0].co.x += 0.5

    bag = tlm_blender._existing_channelbag(key.animation_data.action, key.animation_data.action_slot)
    fcurve = next(fcurve for fcurve in bag.fcurves if fcurve.data_path == 'key_blocks["Shape 1"].value')
    fcurve.keyframe_points[1].co.y = 0.5
    fcurve.update()


def _check_face_at_rest():
    for blender_object in bpy.data.objects:
        key = blender_object.data.shape_keys if blender_object.type == "MESH" else None
        if key is None or (key.animation_data is not None and key.animation_data.action is not None):
            continue

        weights = [block.value for block in list(key.key_blocks)[1:]]
        if any(weight != 0.0 for weight in weights):
            raise AssertionError("%s's shape keys don't start at rest: %s" % (blender_object.name, weights))


def _new_mesh_object(name, root, parent, positions, faces, material=None):
    """A smooth mesh under the model, made the way a modder makes one and given to the add-on by its Twin Tech type."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(positions, [], faces)
    mesh.update()
    mesh.shade_smooth()
    if material is not None:
        mesh.materials.append(material)

    blender_object = bpy.data.objects.new(name, mesh)
    root.users_collection[0].objects.link(blender_object)
    blender_object.parent = parent
    return blender_object


def _cube(size=0.5):
    positions = [(x * size, y * size, z * size) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    return positions, faces


def _follow_bone(blender_object, root, armature, bone_name):
    """A Child Of constraint set up the way the importer sets them up, the object's transform is relative to the bone."""
    constraint = blender_object.constraints.new("CHILD_OF")
    constraint.target = armature
    constraint.subtarget = bone_name
    constraint.inverse_matrix = root.matrix_basis.inverted()


# What a modder does: the skin's object put elsewhere under the root, the shape's weights left unnormalized, a shape key added, a
# body and an exit point following bones through constraints, a body parented to a bone, a collision hull moved, a bone added, an
# animation made longer and one made in Blender, all exported while an animation is posed
def _customize(root):
    armature = tlm_blender.armature_of(root)
    skin = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "skin")
    shape = tlm_blender.shape_object_of(root)
    bodies = next(child for child in root.children if child.get(tlm_blender.KIND_PROPERTY) == "rigid_bodies")
    exit_points = next(child for child in root.children if child.get(tlm_blender.KIND_PROPERTY) == "exit_points")
    expectations = {}

    placement = Matrix.Translation((0.1, 0.2, 0.3)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Diagonal((1.5, 1.5, 1.5, 1.0))
    skin.matrix_basis = placement
    expectations["skin_placement"] = [value for row in placement for value in row]

    # Blender clamps weights to 1, halving keeps every vertex's shares
    for vertex in shape.data.vertices:
        for group in vertex.groups:
            group.weight = group.weight * 0.5

    key = shape.shape_key_add(name="Smile", from_mix=False)
    key.value = 0.0
    key.data[0].co = key.data[0].co + Vector((0.0, 0.1, 0.0))
    expectations["new_shape_offset"] = [0.0, 0.1, 0.0]

    material = skin.material_slots[0].material
    cube = _new_mesh_object("Hat", root, bodies, *_cube(), material=material)
    cube.ttt.type = "Body"
    cube.ttt.body.order = 1
    cube.matrix_basis = Matrix.Translation((0.5, 0.0, 0.0))
    _follow_bone(cube, root, armature, "Joint 2")
    expectations["hat_offset"] = [0.5, 0.0, 0.0]

    badge = _new_mesh_object("Badge", root, armature, *_cube(0.25), material=material)
    badge.ttt.type = "Body"
    badge.ttt.body.order = 2
    badge.parent_type = "BONE"
    badge.parent_bone = "Joint 1"
    badge.matrix_basis = Matrix.Translation((0.3, 0.0, 0.0))
    expectations["badge_offset"] = [0.3, armature.data.bones["Joint 1"].length, 0.0]

    exit_point = bpy.data.objects.new("Hand", None)
    root.users_collection[0].objects.link(exit_point)
    exit_point.parent = exit_points
    exit_point.ttt.type = "ExitPoint"
    exit_point.ttt.exit_point.id = 9
    exit_point.matrix_basis = Matrix.Translation((0.0, 0.25, 0.0))
    _follow_bone(exit_point, root, armature, "Joint 1")
    expectations["hand_offset"] = [0.0, 0.25, 0.0]

    hull = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "hull")
    hull.matrix_basis = Matrix.Translation((0.0, 0.5, 0.0)) @ hull.matrix_basis
    expectations["hull_offset"] = [0.0, 0.5, 0.0]

    # A tail under the last joint, which no animation has keys of, and two frames more of Walk, whose face holds its last weights
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    parent = armature.data.edit_bones["Joint 2"]
    tail = armature.data.edit_bones.new("Tail")
    tail.head = parent.tail
    tail.tail = parent.tail + Vector((0.0, 0.25, 0.0))
    tail.parent = parent
    bpy.ops.object.mode_set(mode="OBJECT")
    expectations["joints"] = len(armature.data.bones)

    # Imported actions have a manual frame range, the animation's length
    bpy.data.actions["Walk"].frame_end = 5
    expectations["walk_frames"] = 6

    # An animation made the way Blender keys one, from frame 1, at 60 frames a second (written at 30, every other frame), turning a bone
    # set to Euler angles (the imported animations still turn it by their quaternions), and the tail bent by a constraint, which the keys
    # don't have
    bpy.context.scene.render.fps = 60
    nod = bpy.data.actions.new("Nod")
    bag = tlm_blender._channelbag(nod, nod.slots.new(id_type="OBJECT", name=armature.name))
    armature.pose.bones["Joint 2"].rotation_mode = "XYZ"
    fcurve = bag.fcurves.new('pose.bones["Joint 2"].rotation_euler', index=0)
    for frame, angle in enumerate([0.0, 0.25, 0.5, 0.75, 1.0]):
        fcurve.keyframe_points.insert(frame + 1, angle)

    expectations["nod_angles"] = [0.0, 0.5, 1.0]
    expectations["nod_fps"] = 30
    constraint = armature.pose.bones["Tail"].constraints.new("LIMIT_ROTATION")
    constraint.use_limit_x = True

    tlm_blender.assign_action(armature, bpy.data.actions["Walk"])
    bpy.context.scene.frame_set(2)
    return expectations


# The armature object scaled and joint 0 moved by an action: returns where Blender shows the joint, which the export has to write
def _scale_armature(root):
    armature = tlm_blender.armature_of(root)
    armature.scale = (2.0, 1.0, 3.0)
    hop = bpy.data.actions.new("Hop")
    bag = tlm_blender._channelbag(hop, hop.slots.new(id_type="OBJECT", name=armature.name))
    for component, value in enumerate((0.3, 0.5, 0.7)):
        fcurve = bag.fcurves.new('pose.bones["Joint 0"].location', index=component)
        fcurve.keyframe_points.insert(0, value)

    tlm_blender.assign_action(armature, hop)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    return root.matrix_world.inverted() @ armature.matrix_world @ armature.pose.bones["Joint 0"].head


def _check_scaled(target, shown):
    file = tlm.TlmFile.load(target)
    hop = next(animation for animation in tlm.find_child(file.root, "armature")["animations"] if animation["name"] == "Hop")
    keys = next(keys for keys in hop["joints"] if keys["joint"] == 0)
    written = list(file.read_view(keys["translation"], "f32"))[:3]
    _check(all(abs(a - b) < 1e-5 for a, b in zip(written, shown)), "joint 0 was written at %s, Blender shows it at %s" % (written, list(shown)))


# The skin and the shape swapped for meshes made in Blender: a cube weighted to two bones without normalizing and a triangle with
# two shape keys of its own, neither with a material
def _swap(root):
    armature = tlm_blender.armature_of(root)
    for child in list(tlm_blender._descendants(root)):
        if tlm_blender.role_of(child) in ("skin", "shape"):
            bpy.data.objects.remove(child)

    skin = _new_mesh_object("Cube Skin", root, root, *_cube())
    skin.ttt.type = "Skin"
    skin.vertex_groups.new(name="Joint 1").add(list(range(8)), 1.0, "REPLACE")
    skin.vertex_groups.new(name="Joint 2").add(list(range(8)), 0.5, "REPLACE")
    skin.modifiers.new("Armature", "ARMATURE").object = armature

    shape = _new_mesh_object("Triangle Face", root, root, [(0.0, 1.0, 0.0), (0.2, 1.0, 0.0), (0.0, 1.2, 0.0)], [(0, 1, 2)])
    shape.ttt.type = "BlendSkin"
    shape.vertex_groups.new(name="Joint 0").add([0, 1, 2], 0.5, "REPLACE")
    shape.modifiers.new("Armature", "ARMATURE").object = armature
    shape.shape_key_add(name="Basis", from_mix=False)
    smile = shape.shape_key_add(name="Smile", from_mix=False)
    smile.value = 0.0
    smile.data[0].co = smile.data[0].co + Vector((0.2, 0.0, 0.0))
    frown = shape.shape_key_add(name="Frown", from_mix=False)
    frown.value = 0.0
    frown.data[1].co = frown.data[1].co + Vector((0.0, 0.0, 0.2))
    tlm_blender.sync_shape_keys(armature)
    return {"skin_weights": [2.0 / 3.0, 1.0 / 3.0], "smile_offset": [0.2, 0.0, 0.0], "frown_offset": [0.0, 0.0, 0.2]}


# A second copy of the model with its bone "Joint 2" named "Tail" (in the file, so nothing gets renamed in Blender) and "Joint 1"
# moved up, without animations of its own: the model's get retargeted to it, the moved joint animating from where it rests now
def _retarget(root, source):
    from twin_tech_tools import tlm

    file = tlm.TlmFile.load(os.path.abspath(source))
    for joint in tlm.find_child(file.root, "armature")["joints"]:
        if joint["index"] == 2:
            joint["name"] = "Tail"

    copy_path = os.path.join(os.path.dirname(os.path.abspath(source)), "ogi_copy_for_retarget.tlm")
    file.save(copy_path)
    other = tlm_blender.import_file(bpy.context, copy_path)
    os.remove(copy_path)
    for action in [action for action in bpy.data.actions if action.get(tlm_blender.OWNER_PROPERTY) == other[tlm_blender.UID_PROPERTY]]:
        bpy.data.actions.remove(action)

    armature = tlm_blender.armature_of(other)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    bone = armature.data.edit_bones["Joint 1"]
    bone.head += Vector((0.0, 0.1, 0.0))
    bone.tail += Vector((0.0, 0.1, 0.0))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    count = tlm_blender.retarget_animations(root, other)
    source_armature = tlm_blender.armature_of(root)
    bones = tlm_blender._matched_bones(source_armature, armature)
    scale = tlm_blender.retarget.size_ratio(tlm_blender._skeleton(source_armature), tlm_blender._skeleton(armature), {target: source for source, target in bones.items()})
    return other, {"retargeted": count, "moved_joint": 1, "offset": [0.0, 0.1, 0.0], "scale": scale}


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    blend = None
    if "--blend" in arguments:
        index = arguments.index("--blend")
        blend = arguments[index + 1]
        del arguments[index:index + 2]

    modes = [mode for mode in ("--edit", "--customize", "--swap", "--retarget", "--collision", "--add", "--scaled") if mode in arguments]
    for mode in modes:
        arguments.remove(mode)

    source, target = arguments
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    root = tlm_blender.import_file(bpy.context, os.path.abspath(source))
    _check_face_at_rest()
    if root.get(tlm_blender.KIND_PROPERTY) == "scenery":
        _scenery_groups(root)

    expectations = None
    if "--edit" in modes:
        if root.get(tlm_blender.KIND_PROPERTY) == "save_icon":
            _edit_icon(root)
        else:
            _edit(root)

    if "--customize" in modes:
        expectations = _customize(root)

    if "--swap" in modes:
        expectations = _swap(root)

    if "--retarget" in modes:
        root, expectations = _retarget(root, source)

    if "--collision" in modes:
        expectations = _generate_collision(root)

    if "--add" in modes:
        expectations = _add_placements(root)

    shown = _scale_armature(root) if "--scaled" in modes else None

    bpy.context.view_layer.update()
    warnings = tlm_blender.export_file(root, os.path.abspath(target))
    if shown is not None:
        _check_scaled(os.path.abspath(target), shown)

    if "--customize" in modes:
        _check(any("bone Tail has constraints or drivers" in warning for warning in warnings), "the constrained bone wasn't warned about: %s" % warnings)
        _check(any("Nod is 60 frames a second" in warning for warning in warnings), "the fast animation wasn't warned about: %s" % warnings)
        _check(any("exit points got their places as IDs (Hand 9 as 1)" in warning for warning in warnings), "the exit point's ID stayed: %s" % warnings)

    print("Exported %s to %s: %d objects, %d actions%s" % (source, target, len(bpy.data.objects), len(bpy.data.actions), ", " + "; ".join(warnings) if warnings else ""))
    if expectations is not None:
        with open(os.path.splitext(os.path.abspath(target))[0] + ".json", "w", encoding="utf-8") as file:
            json.dump(expectations, file, indent=2)

    if blend is not None:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend))


main()
