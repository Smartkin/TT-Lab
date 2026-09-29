"""Puts another model file in the fixture model's place with the retargeting panel's operator, in Blender.

    blender --background --factory-startup --python tests/blender_retarget.py

Prints RETARGET OK when the other model got the model's joints and animations, took its place without leaving anything of the old
model behind, and exports as the model."""
import os
import sys
import tempfile

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm, tlm_blender, twintech_properties  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def _world_turn(armature, bone_name):
    """How much the bone is turned from its rest, in the world."""
    posed = (armature.matrix_world @ armature.pose.bones[bone_name].matrix).to_quaternion()
    return posed @ (armature.matrix_world @ armature.data.bones[bone_name].matrix_local).to_quaternion().inverted()


def _world_position(armature, bone_name):
    return (armature.matrix_world @ armature.pose.bones[bone_name].matrix).translation


def _clear():
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    for blocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.collections):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def _bone_names(action):
    names = set()
    for slot in action.slots:
        bag = action.layers[0].strips[0].channelbag(slot) if action.layers else None
        for fcurve in (bag.fcurves if bag is not None else []):
            bone = tlm_blender._bone_of_path(fcurve.data_path)
            if bone is not None:
                names.add(bone)

    return names


def _make_incoming_file(path):
    """The fixture model with its bone "Joint 2" named "Tail" and "Joint 1" resting higher, saved with its own animations, which the
    operator has to drop."""
    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    armature = tlm_blender.armature_of(root)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    bone = armature.data.edit_bones["Joint 1"]
    bone.head += Vector((0.0, 0.1, 0.0))
    bone.tail += Vector((0.0, 0.1, 0.0))
    armature.data.edit_bones["Joint 2"].name = "Tail"
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    tlm_blender.export_file(root, path)
    _clear()


def main():
    twin_tech_tools.register()
    _clear()
    incoming_path = os.path.join(tempfile.gettempdir(), "ttt_retarget_incoming.tlm")
    _make_incoming_file(incoming_path)

    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    original = tlm_blender.armature_of(root)
    originals = {action.name: action for action in bpy.data.actions if action.get(tlm_blender.OWNER_PROPERTY) == root[tlm_blender.UID_PROPERTY]}
    check(len(originals) == 2, "the fixture has %d animations" % len(originals))
    ids = {name: tlm_blender._animation_meta(action).get("id") for name, action in originals.items()}
    old_name, old_path, old_uid = root.name, root[tlm_blender.PATH_PROPERTY], root[tlm_blender.UID_PROPERTY]
    old_collection = root.users_collection[0]

    # How the original turns and where its second joint is on every frame of Walk, for the new model to match
    tlm_blender.assign_action(original, originals["Walk"])
    turns, expected_positions = {}, {}
    offset = root.matrix_world @ Vector((0.0, 0.1, 0.0)) - root.matrix_world @ Vector()
    for frame in range(int(originals["Walk"].frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        turns[frame] = {name: _world_turn(original, name) for name in ("Joint 0", "Joint 1", "Joint 2")}
        # The second joint rests higher in the new model, and goes as far above where the root's turn leaves it as the original's does
        expected_positions[frame] = _world_position(original, "Joint 1") + _world_turn(original, "Joint 0") @ offset

    skin = next(child for child in root.children if tlm_blender.role_of(child) == "skin")
    bpy.context.view_layer.objects.active = skin
    bpy.ops.ttt.retarget_and_replace(filepath=incoming_path)
    os.remove(incoming_path)

    roots = [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY)]
    check(len(roots) == 1, "the scene has %d models" % len(roots))
    new_root = roots[0]
    check(new_root.name == old_name and new_root[tlm_blender.PATH_PROPERTY] == old_path and new_root[tlm_blender.UID_PROPERTY] != old_uid,
          "the new model didn't take the old one's name and file: %s %s" % (new_root.name, new_root[tlm_blender.PATH_PROPERTY]))
    check(new_root.users_collection[0] == old_collection, "the new model isn't in the old model's collection")
    check(bpy.context.view_layer.objects.active == new_root and new_root.select_get(), "the new model isn't selected")
    check(old_name + ".001" not in bpy.data.objects and "armature" in bpy.data.objects and "armature.001" not in bpy.data.objects,
          "the new model's objects kept Blender's numbers: %s" % sorted(blender_object.name for blender_object in bpy.data.objects))
    check(len([mesh for mesh in bpy.data.meshes if mesh.users > 0]) == len([o for o in bpy.data.objects if o.type == "MESH"]), "meshes of the old model are still used")
    check(len(bpy.data.armatures) == 1, "the old armature is still there: %s" % [armature.name for armature in bpy.data.armatures])

    armature = tlm_blender.armature_of(new_root)
    names = {bone.name: int(twintech_properties.get(bone).joint.index) for bone in armature.data.bones}
    check(names == {"Joint 0": 0, "Joint 1": 1, "Joint 2": 2}, "the bones weren't named like the joints: %s" % names)
    check(armature.data.bones["Joint 2"].parent.name == "Joint 1", "the hierarchy changed")
    check(all(pose_bone.rotation_mode == "QUATERNION" for pose_bone in armature.pose.bones), "the bones don't rotate by quaternions")

    # The model's animations under their own names, nothing of the old model's or the file's left
    actions = {action.name: action for action in bpy.data.actions}
    check(sorted(actions) == ["Walk", "Wave"], "the model's animations aren't the only ones: %s" % sorted(actions))
    for name, action in actions.items():
        check(action.get(tlm_blender.OWNER_PROPERTY) == new_root[tlm_blender.UID_PROPERTY], "%s isn't the new model's" % name)
        check(action.get(tlm_blender.RETARGETED_PROPERTY) is None, "%s is still marked as a copy" % name)
        check(tlm_blender._animation_meta(action).get("id") == ids[name], "%s lost the original's ID" % name)
        check(_bone_names(action) == {"Joint 0", "Joint 1", "Joint 2"}, "%s animates other bones: %s" % (name, _bone_names(action)))

    check(armature.animation_data is not None and armature.animation_data.action == actions["Walk"], "the new model doesn't play its first animation")
    for frame in range(int(actions["Walk"].frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for name, original_turn in turns[frame].items():
            turn = _world_turn(armature, name)
            # The second joint's scale isn't uniform and the third inherits it along other axes, so that one is a little off
            tolerance = 1e-6 if name != "Joint 2" else 2e-3
            check(abs(abs(original_turn.dot(turn)) - 1.0) < tolerance, "%s doesn't turn like the original's on frame %d: %s vs %s" % (name, frame, turn, original_turn))

        check((_world_position(armature, "Joint 1") - expected_positions[frame]).length < 1e-4, "the moved joint isn't where the animation puts it on frame %d" % frame)

    path = os.path.join(tempfile.gettempdir(), "ttt_retarget_smoke.tlm")
    warnings = tlm_blender.export_file(new_root, path)
    file = tlm.TlmFile.load(path)
    armature_node = tlm.find_child(file.root, "armature")
    joints = {joint["index"]: joint for joint in armature_node["joints"]}
    check(sorted(joints) == [0, 1, 2], "the file has joints %s" % sorted(joints))
    check(joints[2]["parent"] == 1 and joints[1]["parent"] == 0, "the file's joints have other parents")
    check([animation.get("name") for animation in armature_node["animations"]] == ["Walk", "Wave"], "the file lost animations")
    check([animation.get("id") for animation in armature_node["animations"]] == [ids["Walk"], ids["Wave"]], "the file's animations lost their IDs")
    skins = [child for child in file.root["children"] if child.get("kind") == "skin"]
    check(len(skins) == 1, "the file has %d skins" % len(skins))
    original_file = tlm.TlmFile.load(os.path.join(FIXTURES, "ogi.tlm"))
    original_joints = {joint["index"]: joint for joint in tlm.find_child(original_file.root, "armature")["joints"]}
    # The bind's translation is its last column, Y at index 7
    check(abs(joints[1]["bind"][7] - (original_joints[1]["bind"][7] + 0.1)) < 1e-4, "the second joint's bind isn't at the moved rest: %s" % joints[1]["bind"])
    os.remove(path)
    print("RETARGET OK, warnings: %s" % (warnings or "none"))


main()
