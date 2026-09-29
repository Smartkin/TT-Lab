"""Imports a TT Lab model file with the add-on and exports it back, in Blender.

    blender --background --factory-startup --python tests/blender_roundtrip.py -- fixtures/ogi.tlm fixtures/ogi_from_blender.tlm

TT Lab's tests read the export of tests/fixtures/ogi.tlm (ogi_from_blender.tlm) and check it's the same model, run this again
after changing how the add-on imports or exports. With --edit the bone "Joint 1" is turned by 30 degrees on frame 2 of "Walk" and the
first vertex of the rigid body moved along X before exporting, which makes ogi_edited_in_blender.tlm. With --customize the model
gets what a modder does to it (the skin's object moved, a shape key, bodies and an exit point put on bones), making
ogi_customized_in_blender.tlm, and with --swap its skin and shape are swapped for meshes made in Blender, making
ogi_swapped_in_blender.tlm. With --retarget its animations are retargeted to a second copy of it whose bone "Joint 1" was moved
and "Joint 2" renamed, so the joint animates from where it was moved to, and the copy is exported as ogi_retargeted_in_blender.tlm.
They write what TT Lab should read next to the file, as JSON. With --blend the scene is also saved, to look at it.
"""

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm_blender  # noqa: E402


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
# body and an exit point following bones through constraints, a body parented to a bone and a collision hull moved, all exported while
# an animation is posed
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

    tlm_blender.assign_action(armature, bpy.data.actions["Walk"])
    bpy.context.scene.frame_set(2)
    return expectations


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

    modes = [mode for mode in ("--edit", "--customize", "--swap", "--retarget") if mode in arguments]
    for mode in modes:
        arguments.remove(mode)

    source, target = arguments
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    root = tlm_blender.import_file(bpy.context, os.path.abspath(source))
    _check_face_at_rest()
    expectations = None
    if "--edit" in modes:
        _edit(root)

    if "--customize" in modes:
        expectations = _customize(root)

    if "--swap" in modes:
        expectations = _swap(root)

    if "--retarget" in modes:
        root, expectations = _retarget(root, source)

    bpy.context.view_layer.update()
    warnings = tlm_blender.export_file(root, os.path.abspath(target))
    print("Exported %s to %s: %d objects, %d actions%s" % (source, target, len(bpy.data.objects), len(bpy.data.actions), ", " + "; ".join(warnings) if warnings else ""))
    if expectations is not None:
        with open(os.path.splitext(os.path.abspath(target))[0] + ".json", "w", encoding="utf-8") as file:
            json.dump(expectations, file, indent=2)

    if blend is not None:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend))


main()
