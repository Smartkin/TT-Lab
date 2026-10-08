"""Retargets the fixture model's animations to another armature with the retargeting panel's operator, in Blender.

    blender --background --factory-startup --python tests/blender_retarget.py

Prints RETARGET OK when the target armature got the source's joints and animations under their own names and IDs, the source's own
animations went, and the target's model exports with them. Matching by name only, the target's other bones stay as they are, and a
target whose bones named like the joints are laid out otherwise, or that has none of their names, is refused without changing anything."""
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


def _error_of(operator):
    """What the operator reported as its error, it raises in a script."""
    try:
        operator()
    except RuntimeError as error:
        return str(error)

    return ""


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


def _edit_bones(armature, edit):
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    edit(armature.data.edit_bones)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()


def _imported_pair():
    """The fixture imported as the source with its animations, and again as a target without any."""
    _clear()
    target_root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    for action in [action for action in bpy.data.actions if action.get(tlm_blender.OWNER_PROPERTY) == target_root[tlm_blender.UID_PROPERTY]]:
        bpy.data.actions.remove(action)

    source_root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    return source_root, tlm_blender.armature_of(source_root), target_root, tlm_blender.armature_of(target_root)


def check_name_only():
    scene = bpy.context.scene
    scene.ttt_retarget_match = "NAME_ONLY"

    # Bones named like the joints are those, the rest stay as they are: a renamed bone and one added keep their names and settings
    source_root, source, target_root, target = _imported_pair()

    def add_extra(bones):
        bones["Joint 2"].name = "Tail"
        extra = bones.new("Extra")
        extra.head, extra.tail = bones["Joint 1"].tail, bones["Joint 1"].tail + Vector((0.0, 0.0, 0.2))
        extra.parent = bones["Joint 1"]

    _edit_bones(target, add_extra)
    twintech_properties.get(target.data.bones["Tail"]).joint.id = 5
    scene.ttt_retarget_source, scene.ttt_retarget_target = source, target
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "retargeting by name only failed")
    joints = {bone.name: (twintech_properties.get(bone).type, int(twintech_properties.get(bone).joint.index)) for bone in target.data.bones}
    check(joints == {"Joint 0": ("Joint", 0), "Joint 1": ("Joint", 1), "Tail": ("Joint", 3), "Extra": ("Joint", 4)}, "the bones aren't the joints by name only: %s" % joints)
    check(int(twintech_properties.get(target.data.bones["Tail"]).joint.id) == 5, "the bone kept as it is lost its settings")
    check(sorted(action.name for action in tlm_blender.animations_of(target_root)) == ["Walk", "Wave"], "the target didn't get the animations")
    check(len(tlm_blender.animations_of(source_root)) == 0, "the source kept its animations")
    for action in tlm_blender.animations_of(target_root):
        check({"Joint 0", "Joint 1"} <= _bone_names(action), "%s doesn't animate the matched bones: %s" % (action.name, _bone_names(action)))

    # A bone named like a joint under another joint than its own: refused, nothing changed
    source_root, source, target_root, target = _imported_pair()
    _edit_bones(target, lambda bones: setattr(bones["Joint 2"], "parent", bones["Joint 0"]))
    scene.ttt_retarget_source, scene.ttt_retarget_target = source, target
    error = _error_of(bpy.ops.ttt.retarget)
    check("isn't set up correctly" in error and "Joint 2 is under Joint 0, the source has Joint 2 under Joint 1" in error, "a misplaced bone wasn't refused: %s" % error)
    check(sorted(bone.name for bone in target.data.bones) == ["Joint 0", "Joint 1", "Joint 2"], "the refused target's bones changed")
    check(len(tlm_blender.animations_of(source_root)) == 2 and len(tlm_blender.animations_of(target_root)) == 0, "the refused retargeting moved animations")

    # No bone named like a joint
    source_root, source, target_root, target = _imported_pair()

    def rename(bones):
        for index, name in enumerate(["Joint 2", "Joint 1", "Joint 0"]):
            bones[name].name = "Bone %d" % index

    _edit_bones(target, rename)
    scene.ttt_retarget_source, scene.ttt_retarget_target = source, target
    check("is named like a joint" in _error_of(bpy.ops.ttt.retarget), "a target without the joints' names wasn't refused")
    scene.ttt_retarget_match = "AUTO"


def main():
    twin_tech_tools.register()
    _clear()
    incoming_path = os.path.join(tempfile.gettempdir(), "ttt_retarget_incoming.tlm")
    _make_incoming_file(incoming_path)
    target_root = tlm_blender.import_file(bpy.context, incoming_path)
    os.remove(incoming_path)
    target = tlm_blender.armature_of(target_root)
    # A rig without animations of its own, like one made for a character in another program
    for action in [action for action in bpy.data.actions if action.get(tlm_blender.OWNER_PROPERTY) == target_root[tlm_blender.UID_PROPERTY]]:
        bpy.data.actions.remove(action)

    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    original = tlm_blender.armature_of(root)
    originals = {action.name: action for action in bpy.data.actions if action.get(tlm_blender.OWNER_PROPERTY) == root[tlm_blender.UID_PROPERTY]}
    check(len(originals) == 2, "the fixture has %d animations" % len(originals))
    ids = {name: tlm_blender._animation_meta(action).get("id") for name, action in originals.items()}

    # How the original turns and where its second joint is on every frame of Walk, for the target to match
    tlm_blender.assign_action(original, originals["Walk"])
    turns, expected_positions = {}, {}
    offset = target_root.matrix_world @ Vector((0.0, 0.1, 0.0)) - target_root.matrix_world @ Vector()
    for frame in range(int(originals["Walk"].frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        turns[frame] = {name: _world_turn(original, name) for name in ("Joint 0", "Joint 1", "Joint 2")}
        # The second joint rests higher in the target, and goes as far above where the root's turn leaves it as the original's does
        expected_positions[frame] = _world_position(original, "Joint 1") + _world_turn(original, "Joint 0") @ offset

    # Nothing picked yet: the button stays pressable and says what's missing (an error report raises in a script)
    scene = bpy.context.scene
    check(bpy.ops.ttt.retarget.poll(), "the button is grayed out")
    check("Pick the source armature" in _error_of(bpy.ops.ttt.retarget), "retargeting without armatures didn't say why")
    scene.ttt_retarget_source = original
    scene.ttt_retarget_target = original
    check("the same armature" in _error_of(bpy.ops.ttt.retarget), "retargeting an armature to itself didn't say why")

    scene.ttt_retarget_target = target
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "retargeting failed")

    names = {bone.name: int(twintech_properties.get(bone).joint.index) for bone in target.data.bones}
    check(names == {"Joint 0": 0, "Joint 1": 1, "Joint 2": 2}, "the target's bones weren't named like the joints: %s" % names)
    check(target.data.bones["Joint 2"].parent.name == "Joint 1", "the hierarchy changed")
    check(all(pose_bone.rotation_mode == "QUATERNION" for pose_bone in target.pose.bones), "the bones don't rotate by quaternions")

    # The animations under their own names, the source's gone
    actions = {action.name: action for action in bpy.data.actions}
    check(sorted(actions) == ["Walk", "Wave"], "the animations aren't the only ones, or kept Blender's numbers: %s" % sorted(actions))
    check(len(tlm_blender.animations_of(root)) == 0, "the source still has animations")
    check(sorted(action.name for action in tlm_blender.animations_of(target_root)) == ["Walk", "Wave"], "the target's model doesn't have the animations")
    for name, action in actions.items():
        check(action.get(tlm_blender.RETARGETED_PROPERTY) is None, "%s is still marked as a copy" % name)
        check(tlm_blender._animation_meta(action).get("id") == ids[name], "%s lost the original's ID" % name)
        check(_bone_names(action) == {"Joint 0", "Joint 1", "Joint 2"}, "%s animates other bones: %s" % (name, _bone_names(action)))

    check(original.animation_data is None or original.animation_data.action is None, "the source still plays an animation")
    check(target.animation_data is not None and target.animation_data.action == actions["Walk"], "the target doesn't play its first animation")
    for frame in range(int(actions["Walk"].frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for name, original_turn in turns[frame].items():
            turn = _world_turn(target, name)
            # The second joint's scale isn't uniform and the third inherits it along other axes, so that one is a little off
            tolerance = 1e-6 if name != "Joint 2" else 2e-3
            check(abs(abs(original_turn.dot(turn)) - 1.0) < tolerance, "%s doesn't turn like the original's on frame %d: %s vs %s" % (name, frame, turn, original_turn))

        check((_world_position(target, "Joint 1") - expected_positions[frame]).length < 1e-4, "the moved joint isn't where the animation puts it on frame %d" % frame)

    path = os.path.join(tempfile.gettempdir(), "ttt_retarget_smoke.tlm")
    warnings = tlm_blender.export_file(target_root, path)
    file = tlm.TlmFile.load(path)
    armature_node = tlm.find_child(file.root, "armature")
    joints = {joint["index"]: joint for joint in armature_node["joints"]}
    check(sorted(joints) == [0, 1, 2], "the file has joints %s" % sorted(joints))
    check(joints[2]["parent"] == 1 and joints[1]["parent"] == 0, "the file's joints have other parents")
    check([animation.get("name") for animation in armature_node["animations"]] == ["Walk", "Wave"], "the file lost animations")
    check([animation.get("id") for animation in armature_node["animations"]] == [ids["Walk"], ids["Wave"]], "the file's animations lost their IDs")
    original_file = tlm.TlmFile.load(os.path.join(FIXTURES, "ogi.tlm"))
    original_joints = {joint["index"]: joint for joint in tlm.find_child(original_file.root, "armature")["joints"]}
    # The bind's translation is its last column, Y at index 7
    check(abs(joints[1]["bind"][7] - (original_joints[1]["bind"][7] + 0.1)) < 1e-4, "the second joint's bind isn't at the moved rest: %s" % joints[1]["bind"])
    source_path = os.path.join(tempfile.gettempdir(), "ttt_retarget_source.tlm")
    tlm_blender.export_file(root, source_path)
    check(tlm.find_child(tlm.TlmFile.load(source_path).root, "armature").get("animations", []) == [], "the source's model still exports animations")
    os.remove(path)
    os.remove(source_path)
    check_name_only()
    print("RETARGET OK, warnings: %s" % (warnings or "none"))


main()
