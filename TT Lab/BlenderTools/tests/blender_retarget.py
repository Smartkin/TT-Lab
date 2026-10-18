"""Retargets the fixture model's animations to other rigs with the retargeting panel's operators, in Blender.

    blender --background --factory-startup --python tests/blender_retarget.py

A model file's rig (the fixture with a joint moved and renamed) gets the model's animations: its bones are the joints the table pairs them
with, it plays the model's animations the way the model did under their names and IDs and keeps its own exit points and hulls, and the
model loses its animations and nothing else (its place, name, file and collection stay). Then a rig made in Blender (bigger, with a bone
of no joint) gets the animations of the model imported again: a pair that isn't a subtree of the skeleton is refused, the exit point and
hull follow their joints' bones scaled, the joint IDs come along, and a rig leaving a joint out gets what followed it on the joint above. The mirrored fixture's exit points and hull follow bones whose own axes
are picked of their joints': imported, they're where the file puts them from their bones (an exit point's frame the game's in its
joint's, whichever the bone's own axes are); retargeted to a rig made in Blender over the model with every bone rolled a quarter turn,
the exported file puts them where they were, the exit points' frames (one scaled unevenly) too, an exit point taken off its bone keeps
its joint, and on a rig whose forearms point other ways the ones following them move with them about their heads, the hull turned with
its forearm, the exit point keeping its axes (the rig's bones turn in the world as their joints do). What follows no bone
goes where it was in its model whatever the roots' places, a model moved and turned retargets with everything where it is on it, and a
rig of bones named longer than 50 bytes retargets. Undone, the model has its animations back. Prints RETARGET OK."""
import math
import os
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import axes, pairing_ui, retargeting, tlm, tlm_blender, tlm_math, twintech_properties  # noqa: E402

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
    """The fixture model with its bone "Joint 2" named "Tail" and "Joint 1" resting higher, saved with its own animations, which
    importing it as the rig drops."""
    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    armature = tlm_blender.armature_of(root)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    bone = armature.data.edit_bones["Joint 1"]
    # Up, the game's Y
    bone.head += Vector(axes.point((0.0, 0.1, 0.0)))
    bone.tail += Vector(axes.point((0.0, 0.1, 0.0)))
    armature.data.edit_bones["Joint 2"].name = "Tail"
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    tlm_blender.export_file(root, path)
    _clear()


def _rig_model(rig):
    """The model the rig is part of once it got the animations."""
    root = tlm_blender.find_root(rig)
    check(root is not None and root.get(tlm_blender.KIND_PROPERTY) == "ogi", "%s isn't part of a model" % rig.name)
    return root


def _check_model_kept(root, name, path, collection, armature):
    """Retargeting only deleted the model's animations: it's the model it was where it was, at no hidden place, its bones' settings of the
    deleted animations gone too."""
    check(root.name in bpy.data.objects and root.name == name and root.get(tlm_blender.PATH_PROPERTY, "") == path, "the model isn't where it was: %s" % root.name)
    check(list(root.users_collection) == [collection] and not retargeting.is_source(root), "the model was moved or hidden")
    check(tlm_blender.armature_of(root) == armature and armature.visible_get(), "the model's armature was replaced or hidden")
    check(tlm_blender.animations_of(root) == [], "the model kept the animations %s" % [action.name for action in tlm_blender.animations_of(root)])
    check(all(len(getattr(bone, "ttt_animation_flags")) == 0 for bone in armature.data.bones), "the bones kept settings of the deleted animations")
    check(not any(blender_object.get(retargeting.SOURCE_PROPERTY) for blender_object in bpy.data.objects), "a model was kept hidden as a retarget source")


def _rig(name, heads, parents, scale, offset):
    """An armature made in Blender: its bones' heads at the joints' scaled and moved, each pointing at its first child or up."""
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    bones = {}
    for bone_name, head in heads.items():
        bone = data.edit_bones.new(bone_name)
        bone.head = Vector(head) * scale + Vector(offset)
        bone.tail = bone.head + Vector((0.0, 0.0, 0.05 * scale))
        bones[bone_name] = bone

    for bone_name, parent in parents.items():
        if parent is not None:
            bones[bone_name].parent = bones[parent]

    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def _set_table(rig, pairs):
    for row in rig.data.ttt_pairs:
        if row.bone in pairs:
            row.joint = pairs[row.bone] or ""


def check_from_a_file():
    _clear()
    incoming_path = os.path.join(tempfile.gettempdir(), "ttt_retarget_incoming.tlm")
    _make_incoming_file(incoming_path)

    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    original = tlm_blender.armature_of(root)
    originals = {action.name: action for action in tlm_blender.animations_of(root)}
    check(sorted(originals) == ["Walk", "Wave"], "the fixture has %s" % sorted(originals))
    ids = {name: tlm_blender._animation_meta(action).get("id") for name, action in originals.items()}
    old_name, old_path = root.name, root[tlm_blender.PATH_PROPERTY]
    old_collection = root.users_collection[0]

    # How the original turns and where its second joint is on every frame of Walk, for the rig to match
    tlm_blender.assign_action(original, originals["Walk"])
    turns, expected_positions = {}, {}
    offset = root.matrix_world @ Vector(axes.point((0.0, 0.1, 0.0))) - root.matrix_world @ Vector()
    for frame in range(int(originals["Walk"].frame_range[1]) + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        turns[frame] = {name: _world_turn(original, name) for name in ("Joint 0", "Joint 1", "Joint 2")}
        # The second joint rests higher in the rig, and goes as far above where the root's turn leaves it as the original's does
        expected_positions[frame] = _world_position(original, "Joint 1") + _world_turn(original, "Joint 0") @ offset

    skin = next(child for child in root.children if tlm_blender.role_of(child) == "skin")
    bpy.context.view_layer.objects.active = skin
    check(bpy.ops.ttt.import_rig(filepath=incoming_path) == {"FINISHED"}, "the rig wasn't imported")
    os.remove(incoming_path)
    rig = pairing_ui.rig_of(root)
    check(rig is not None and rig != original, "the imported rig isn't the model's rig")
    check(retargeting.table_of(rig) == {"Joint 0": "Joint 0", "Joint 1": "Joint 1", "Tail": "Joint 2"}, "the table is %s" % retargeting.table_of(rig))
    check(bpy.ops.ttt.retarget.poll(), "Retarget is grayed out")
    rig_model = tlm_blender.find_root(rig)
    rig_name, rig_path = rig_model.name, rig_model[tlm_blender.PATH_PROPERTY]
    bpy.context.view_layer.objects.active = skin
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rig didn't get the model's animations")

    # The model stays the model it was, without animations, the rig's model the model of its own file
    _check_model_kept(root, old_name, old_path, old_collection, original)
    new_root = _rig_model(rig)
    check(new_root == rig_model and new_root.name == rig_name and new_root[tlm_blender.PATH_PROPERTY] == rig_path, "the rig's model took another's name or file")
    bpy.context.view_layer.objects.active = root
    check(not bpy.ops.ttt.retarget.poll(), "Retarget is pressable for a model without animations")

    armature = tlm_blender.armature_of(new_root)
    names = {bone.name: int(twintech_properties.get(bone).joint.index) for bone in armature.data.bones}
    check(names == {"Joint 0": 0, "Joint 1": 1, "Joint 2": 2}, "the bones weren't named like the joints: %s" % names)
    check(armature.data.bones["Joint 2"].parent.name == "Joint 1", "the hierarchy changed")
    check(all(pose_bone.rotation_mode == "QUATERNION" for pose_bone in armature.pose.bones), "the bones don't rotate by quaternions")
    # The rig's own exit points and hulls win over the model's of the same place
    kinds = [tlm_blender.role_of(child) for child in tlm_blender._descendants(new_root)]
    check(kinds.count("exit_point") == 1 and kinds.count("hull") == 1, "the rig has %s" % sorted(kinds))

    actions = {action.name: action for action in tlm_blender.animations_of(new_root)}
    check(sorted(actions) == ["Walk", "Wave"], "the rig's animations are %s" % sorted(actions))
    for name, action in actions.items():
        check(tlm_blender._animation_meta(action).get("id") == ids[name], "%s lost the original's ID" % name)
        check(_bone_names(action) == {"Joint 0", "Joint 1", "Joint 2"}, "%s animates other bones: %s" % (name, _bone_names(action)))

    check(armature.animation_data is not None and armature.animation_data.action == actions["Walk"], "the rig doesn't play its first animation")
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
    check(sorted(joints) == [0, 1, 2] and joints[2]["parent"] == 1 and joints[1]["parent"] == 0, "the file has joints %s" % sorted(joints))
    check([animation.get("name") for animation in armature_node["animations"]] == ["Walk", "Wave"], "the file lost animations")
    check([animation.get("id") for animation in armature_node["animations"]] == [ids["Walk"], ids["Wave"]], "the file's animations lost their IDs")
    original_file = tlm.TlmFile.load(os.path.join(FIXTURES, "ogi.tlm"))
    original_joints = {joint["index"]: joint for joint in tlm.find_child(original_file.root, "armature")["joints"]}
    # The bind's translation is its last column, Y at index 7
    check(abs(joints[1]["bind"][7] - (original_joints[1]["bind"][7] + 0.1)) < 1e-4, "the second joint's bind isn't at the moved rest: %s" % joints[1]["bind"])
    os.remove(path)
    return new_root, turns, warnings


def _fresh_model(name="ogi.tlm"):
    _clear()
    return tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, name))


def check_from_the_scene(turns):
    """A rig made in Blender, twice as big and elsewhere, with a bone sticking out of its root that stands for no joint."""
    model = source = _fresh_model()
    source_armature = tlm_blender.armature_of(source)
    kept = (model.name, model.get(tlm_blender.PATH_PROPERTY, ""), model.users_collection[0], source_armature)
    heads = {bone.name: tuple(source_armature.matrix_world @ bone.head_local) for bone in source_armature.data.bones}
    parents = {bone.name: bone.parent.name if bone.parent is not None else None for bone in source_armature.data.bones}
    bones = {"base": heads["Joint 0"], "mid": heads["Joint 1"], "tip": heads["Joint 2"], "extra": tuple(Vector(heads["Joint 0"]) + Vector((0.3, 0.0, 0.0)))}
    custom = _rig("Custom", bones, {"base": None, "mid": "base", "tip": "mid", "extra": "base"}, 2.0, (3.0, 0.0, 0.0))
    check(parents == {"Joint 0": None, "Joint 1": "Joint 0", "Joint 2": "Joint 1"}, "the fixture's skeleton is %s" % parents)
    model.ttt_retarget_rig = custom
    bpy.context.view_layer.objects.active = model
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed")
    check(retargeting.table_of(custom) == {"base": "Joint 0", "mid": "Joint 1", "tip": "Joint 2", "extra": None}, "the guessed table is %s" % retargeting.table_of(custom))

    # A bone standing for its parent's joint isn't the skeleton: refused, nothing changes
    _set_table(custom, {"tip": "Joint 1", "mid": "Joint 2"})
    check(not bpy.ops.ttt.retarget.poll() and retargeting.problems(source, custom), "a table that isn't a subtree of the skeleton is taken")
    _set_table(custom, {"tip": "Joint 2", "mid": "Joint 1"})
    check(bpy.ops.ttt.retarget.poll(), "Retarget is grayed out for a subtree")

    exit_point = next(child for child in tlm_blender._descendants(source) if tlm_blender.role_of(child) == "exit_point")
    hull = next(child for child in tlm_blender._descendants(source) if tlm_blender.role_of(child) == "hull")
    bpy.context.view_layer.update()
    joint_of = {}
    for blender_object in (exit_point, hull):
        attachment = tlm_blender._rest_attachment(blender_object, source_armature, set(heads))
        joint_of[blender_object.name] = (attachment[1].name, (attachment[2].translation - Vector(heads[attachment[1].name])))

    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rig made in Blender didn't get the model's animations")
    _check_model_kept(model, *kept)
    # Made a model of its own, of no file: the model keeps its file and TT Lab's link
    new_root = _rig_model(custom)
    rig = tlm_blender.armature_of(new_root)
    check(rig == custom and new_root != model and not new_root.get(tlm_blender.PATH_PROPERTY), "the rig made in Blender isn't a model of its own")
    names = {bone.name: int(twintech_properties.get(bone).joint.index) for bone in rig.data.bones}
    check(names == {"Joint 0": 0, "Joint 1": 1, "Joint 2": 2, "Joint 3": 3}, "the bones are %s" % names)
    identifier = int(tlm_blender._write_data(rig.data.bones["Joint 1"]).get("Id", 255))
    check(identifier == int(tlm_blender._write_data(source_armature.data.bones["Joint 1"]).get("Id", 255)), "the joint ID didn't come along: %d" % identifier)
    check(sorted(action.name for action in tlm_blender.animations_of(new_root)) == ["Walk", "Wave"], "the rig's animations are %s" % sorted(a.name for a in bpy.data.actions))

    bpy.context.view_layer.update()
    for name, (joint, offset) in joint_of.items():
        copy = next(child for child in tlm_blender._descendants(new_root) if tlm_blender.role_of(child) == tlm_blender.role_of(bpy.data.objects[name]))
        attachment = tlm_blender._rest_attachment(copy, rig, set(bone.name for bone in rig.data.bones))
        check(attachment is not None and attachment[1].name == joint, "%s doesn't follow %s on the rig" % (name, joint))
        moved = attachment[2].translation - (rig.matrix_world @ rig.data.bones[joint].head_local)
        check((moved - offset * 2.0).length < 1e-4, "%s isn't as far from its joint scaled: %s vs %s" % (name, moved, offset * 2.0))

    # The paired bones turn like the joints, the new one stays at rest
    tlm_blender.assign_action(rig, next(action for action in tlm_blender.animations_of(new_root) if action.name == "Walk"))
    for frame, original_turns in turns.items():
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        check(abs(abs(original_turns["Joint 1"].dot(_world_turn(rig, "Joint 1"))) - 1.0) < 1e-5, "the rig's Joint 1 doesn't turn like the model's on frame %d" % frame)
        check(abs(abs(_world_turn(rig, "Joint 3").dot(_world_turn(rig, "Joint 0"))) - 1.0) < 1e-5, "the new joint doesn't just follow the root on frame %d" % frame)

    path = os.path.join(tempfile.gettempdir(), "ttt_retarget_custom.tlm")
    tlm_blender.export_file(new_root, path)
    joints = {joint["index"]: joint for joint in tlm.find_child(tlm.TlmFile.load(path).root, "armature")["joints"]}
    check(sorted(joints) == [0, 1, 2, 3] and joints[3]["parent"] == 0, "the file has joints %s" % joints)
    os.remove(path)
    return new_root


def check_a_left_out_joint():
    """A rig of the root and the second joint only: what followed the third goes to the second, said in the notes."""
    model = source = _fresh_model()
    source_armature = tlm_blender.armature_of(source)
    heads = {bone.name: tuple(source_armature.matrix_world @ bone.head_local) for bone in source_armature.data.bones}
    short = _rig("Short", {"hips": heads["Joint 0"], "chest": heads["Joint 1"]}, {"hips": None, "chest": "hips"}, 1.0, (-3.0, 0.0, 0.0))
    model.ttt_retarget_rig = short
    bpy.context.view_layer.objects.active = model
    bpy.ops.ttt.guess_pairs()
    check(retargeting.table_of(short) == {"hips": "Joint 0", "chest": "Joint 1"}, "the table is %s" % retargeting.table_of(short))
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the short rig wasn't retargeted")
    new_root = _rig_model(short)
    rig = tlm_blender.armature_of(new_root)
    bpy.context.view_layer.update()
    for child in tlm_blender._descendants(new_root):
        if tlm_blender.role_of(child) in ("exit_point", "hull"):
            attachment = tlm_blender._rest_attachment(child, rig, set(bone.name for bone in rig.data.bones))
            check(attachment is not None and attachment[1].name == "Joint 1", "%s doesn't follow the joint above its own" % child.name)

    notes = retargeting.notes_of(new_root)
    check(any("followed joints no bone stands for" in note for note in notes), "the notes don't say what moved: %s" % notes)
    check(tlm_blender.animations_of(model) == [], "the model kept its animations")


def _file_places(path, root_world):
    """Where the model file puts its exit points (their frames) and its hulls' vertexes at rest, in the world: their joints' binds times
    their transforms, the game's frames shown the way Blender shows joints' (axes.bone_matrix, M G D) whichever bones they follow."""
    file = tlm.TlmFile.load(path)
    binds = {joint["index"]: Matrix(tlm_math.from_rows(joint["bind"])) for joint in tlm.find_child(file.root, "armature")["joints"]}
    places = {"exit_point": [], "hull": []}
    for node in tlm.traverse(file.root):
        kind = node.get("kind")
        if kind not in places:
            continue

        local = Matrix(tlm_math.compose(node.get("translation", (0.0, 0.0, 0.0)), tlm_math.xyzw_to_wxyz(node.get("rotation", (0.0, 0.0, 0.0, 1.0))),
                                        node.get("scale", (1.0, 1.0, 1.0))))
        game = binds[node["joint"]] @ local if node.get("joint") in binds else local
        if kind == "exit_point":
            places[kind].append(root_world @ Matrix(axes.bone_matrix([list(row) for row in game])))
        else:
            # x, y, z and w
            values = file.read_view(node["vertices"], "f32")
            places[kind].append([root_world @ Vector(axes.point(tuple(game @ Vector(values[i:i + 3])))) for i in range(0, len(values) - 3, 4)])

    return places


def _exported_places(root):
    path = os.path.join(tempfile.gettempdir(), "ttt_retarget_places.tlm")
    tlm_blender.export_file(root, path)
    places = _file_places(path, root.matrix_world)
    os.remove(path)
    return places


def _same_places(a, b, label):
    for kind in ("exit_point", "hull"):
        check(len(a[kind]) == len(b[kind]), "%s: %d %ss, expected %d" % (label, len(a[kind]), kind, len(b[kind])))

    for index, (shown, expected) in enumerate(zip(a["exit_point"], b["exit_point"])):
        off = max(abs(shown[row][column] - expected[row][column]) for row in range(4) for column in range(4))
        check(off < 1e-4, "%s: exit point %d is at\n%s\nexpected\n%s" % (label, index, shown, expected))

    for index, (shown, expected) in enumerate(zip(a["hull"], b["hull"])):
        check(len(shown) == len(expected) and all((p - q).length < 1e-4 for p, q in zip(shown, expected)), "%s: hull %d's vertexes moved" % (label, index))


def _turned_places(places, turns):
    """The places with the exit points (by index) moved and hulls (by index) turned about a point: {(kind, index): (head, turn)}. An exit
    point keeps its axes, its rig's bone turns in the world as its joint does."""
    result = {"exit_point": list(places["exit_point"]), "hull": list(places["hull"])}
    for (kind, index), (head, turn) in turns.items():
        about = Matrix.Translation(head) @ turn.to_matrix().to_4x4() @ Matrix.Translation(-head)
        if kind == "exit_point":
            frame = places[kind][index]
            result[kind][index] = Matrix.Translation(about @ frame.translation) @ Matrix.Translation(-frame.translation) @ frame
        else:
            result[kind][index] = [about @ vertex for vertex in places[kind][index]]

    return result


def _rig_over(name, armature, turns):
    """A rig made in Blender with the armature's bones (their names, heads, tails and parents), every one rolled a quarter turn about
    itself and the ones turns has turned about their heads: {bone: turn}."""
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(rig)
    rests = {bone.name: (armature.matrix_world @ bone.matrix_local, bone.length, bone.parent.name if bone.parent is not None else None) for bone in armature.data.bones}
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for bone_name, (rest, length, _) in rests.items():
        edit_bone = data.edit_bones.new(bone_name)
        edit_bone.head = (0.0, 0.0, 0.0)
        edit_bone.tail = (0.0, length, 0.0)
        turn = turns.get(bone_name)
        about = Matrix.Translation(rest.translation) @ turn.to_matrix().to_4x4() @ Matrix.Translation(-rest.translation) if turn is not None else Matrix.Identity(4)
        edit_bone.matrix = about @ rest @ Matrix.Rotation(math.pi / 2.0, 4, "Y")

    for bone_name, (_, _, parent) in rests.items():
        if parent is not None:
            data.edit_bones[bone_name].parent = data.edit_bones[parent]

    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def _without_child_of(blender_object):
    world = blender_object.matrix_world.copy()
    for constraint in list(blender_object.constraints):
        blender_object.constraints.remove(constraint)

    bpy.context.view_layer.update()
    blender_object.matrix_world = world
    bpy.context.view_layer.update()


def check_what_follows_aimed_bones():
    _clear()
    # The right hand's exit point scaled unevenly, like CameraBot's on an aimed bone: its scale stays along the joint's axes
    file = tlm.TlmFile.load(os.path.join(FIXTURES, "ogi_mirrored.tlm"))
    hand = next(node for node in tlm.traverse(file.root) if node.get("name") == "Exit Point 0")
    hand["scale"] = [0.75, 0.75, 1.0]
    hand.pop("matrix", None)
    path = os.path.join(tempfile.mkdtemp(prefix="ttt_retarget_"), "ogi_mirrored_scaled_point.tlm")
    file.save(path)
    root = tlm_blender.import_file(bpy.context, path)
    armature = tlm_blender.armature_of(root)
    bpy.context.view_layer.update()
    bones = set(bone.name for bone in armature.data.bones)
    check(any(tlm_blender.bone_aim(bone) != axes.IDENTITY_AIM for bone in armature.data.bones), "the fixture's bones all have their joints' axes")
    # Where the file puts them from their bones as Blender keeps them (the eyes' a few thousandths of a radian off their binds): an exit
    # point's frame is the game's in the joint's whichever the bone's own axes are, D T D from the bone's rest with its axes picked back
    file = tlm.TlmFile.load(path)
    shown, expected = {"exit_point": [], "hull": []}, {"exit_point": [], "hull": []}
    pairs = []
    for kind in shown:
        nodes = [node for node in tlm.traverse(file.root) if node.get("kind") == kind]
        objects = tlm_blender.in_file_order(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == kind)
        check(len(nodes) == len(objects), "%d %ss imported of %d" % (len(objects), kind, len(nodes)))
        pairs.extend((kind, blender_object, node) for blender_object, node in zip(objects, nodes))

    for kind, blender_object, node in pairs:
        _, bone, rest = tlm_blender._rest_attachment(blender_object, armature, bones)
        frame = Matrix(axes.unaimed([list(row) for row in armature.matrix_world @ bone.matrix_local], tlm_blender.bone_aim(bone)))
        local = tlm_math.compose(node.get("translation", (0.0, 0.0, 0.0)), tlm_math.xyzw_to_wxyz(node.get("rotation", (0.0, 0.0, 0.0, 1.0))),
                                 node.get("scale", (1.0, 1.0, 1.0)))
        placed = frame @ Matrix(axes.joint_matrix(local))
        if kind == "exit_point":
            shown[kind].append(rest)
            expected[kind].append(placed)
        else:
            values = file.read_view(node["vertices"], "f32")
            shown[kind].append([rest @ vertex.co for vertex in blender_object.data.vertices])
            expected[kind].append([placed @ Vector(axes.joint_point(values[i:i + 3])) for i in range(0, len(values) - 3, 4)])

    _same_places(shown, expected, "imported")

    # The head's exit point taken off its bone where it is at rest (the import poses the model): it keeps its joint
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    _without_child_of(next(child for child in tlm_blender._descendants(root) if child.name == "Exit Point 1"))
    armature.data.pose_position = "POSE"

    # Over the model, its bones rolled: what follows them stays where it is
    rig = _rig_over("Rolled", armature, {})
    root.ttt_retarget_rig = rig
    bpy.context.view_layer.objects.active = root
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed")
    check(all(joint == bone for bone, joint in retargeting.table_of(rig).items()), "the table is %s" % retargeting.table_of(rig))
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rolled rig wasn't retargeted")
    model = _rig_model(rig)
    check(tlm_blender.armature_of(model) == rig, "the rolled rig isn't the model")
    _same_places(_exported_places(model), shown, "on rolled bones")
    exported = os.path.join(tempfile.mkdtemp(prefix="ttt_retarget_"), "rolled.tlm")
    tlm_blender.export_file(model, exported)
    head_point = next(node for node in tlm.traverse(tlm.TlmFile.load(exported).root) if node.get("kind") == "exit_point" and node["data"]["Id"] == 1)
    check(head_point["joint"] == 3, "the exit point taken off its bone went from joint 3 to %d" % head_point["joint"])

    # The forearms pointing other ways: the exit point on the right one and the hull on the left one turn with them about their heads.
    # From the rolled rig's model, which plays the animations now and has what followed the model's bones where it was
    source_armature = tlm_blender.armature_of(model)
    turns = {"Joint 10": Matrix.Rotation(math.radians(60.0), 3, "X").to_quaternion(), "Joint 8": Matrix.Rotation(math.radians(-45.0), 3, "Z").to_quaternion()}
    reposed = _rig_over("Reposed", source_armature, turns)
    model.ttt_retarget_rig = reposed
    bpy.context.view_layer.objects.active = model
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed for the turned forearms")
    _set_table(reposed, {name: name for name in retargeting.table_of(reposed)})
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rig with turned forearms wasn't retargeted")
    model = _rig_model(reposed)
    # By the shortest turn from where the bone pointed to where it points: the rolls of rigs differ, the ways their bones point don't
    swings = {}
    for name, turn in turns.items():
        rest = source_armature.matrix_world @ source_armature.data.bones[name].matrix_local
        way = rest.to_3x3() @ Vector((0.0, 1.0, 0.0))
        swings[name] = (rest.translation, way.rotation_difference(turn @ way))

    expected = _turned_places(shown, {("exit_point", 0): swings["Joint 10"], ("hull", 0): swings["Joint 8"]})
    _same_places(_exported_places(model), expected, "on turned forearms")


def check_long_bone_names():
    """A rig whose bones' names are longer than a temporary name of them leaves room for (Blender keeps 63 bytes of a name)."""
    _clear()
    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    armature = tlm_blender.armature_of(root)
    names = {bone.name: "%s of a rig whose bones are named at great length %s" % (bone.name, "x" * 10) for bone in armature.data.bones}
    heads = {names[bone.name]: tuple(armature.matrix_world @ bone.head_local) for bone in armature.data.bones}
    parents = {names[bone.name]: names[bone.parent.name] if bone.parent is not None else None for bone in armature.data.bones}
    check(all(len(name) > 50 for name in heads), "the names are short")
    rig = _rig("Long Names", heads, parents, 1.0, (0.0, 0.0, 0.0))
    root.ttt_retarget_rig = rig
    bpy.context.view_layer.objects.active = root
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed")
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rig of long names wasn't retargeted")
    check(sorted(bone.name for bone in rig.data.bones) == sorted(bone.name for bone in armature.data.bones), "the bones are %s" % [bone.name for bone in rig.data.bones])


def check_copies_on_no_bone_keep_their_place_in_the_model():
    """What follows no bone goes where it was in its model: both models' roots moved, it isn't moved by the old root twice."""
    _clear()
    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi_mirrored.tlm"))
    root.matrix_world = Matrix.Translation((1.5, -0.5, 0.25)) @ Matrix.Rotation(math.radians(30.0), 4, "Z")
    bpy.context.view_layer.update()
    point = next(child for child in tlm_blender._descendants(root) if child.name == "Exit Point 1")
    _without_child_of(point)
    target = bpy.data.objects.new("Target", None)
    root.users_collection[0].objects.link(target)
    target.matrix_world = Matrix.Translation((-2.0, 1.0, 0.5)) @ Matrix.Rotation(math.radians(-50.0), 4, "Z")
    bpy.context.view_layer.update()
    armature = tlm_blender.armature_of(root)
    copy = retargeting._copy_attachment(point, "exit_point", root, target, armature, None, point.matrix_world.copy(), None, armature, 1.0)
    bpy.context.view_layer.update()
    expected = target.matrix_world @ root.matrix_world.inverted() @ point.matrix_world
    off = max(abs(copy.matrix_world[row][column] - expected[row][column]) for row in range(4) for column in range(4))
    check(off < 1e-5, "the copy is %g from where its model had it" % off)


def check_a_moved_model_retargets_in_place():
    """A model whose root was moved and turned retargets to a rig over it in the world: its exit points and hull go where they are on
    the moved model, the root counting once."""
    _clear()
    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi_mirrored.tlm"))
    armature = tlm_blender.armature_of(root)
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    bones = set(bone.name for bone in armature.data.bones)
    unmoved = {"exit_point": [], "hull": []}
    for kind in unmoved:
        for blender_object in tlm_blender.in_file_order(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == kind):
            rest = tlm_blender._rest_attachment(blender_object, armature, bones)[2]
            unmoved[kind].append(rest if kind == "exit_point" else [rest @ vertex.co for vertex in blender_object.data.vertices])

    moved = Matrix.Translation((1.5, -0.5, 0.25)) @ Matrix.Rotation(math.radians(30.0), 4, "Z")
    root.matrix_world = moved
    bpy.context.view_layer.update()
    expected = {"exit_point": [moved @ frame for frame in unmoved["exit_point"]], "hull": [[moved @ vertex for vertex in hull] for hull in unmoved["hull"]]}
    rig = _rig_over("Over the moved model", armature, {})
    root.ttt_retarget_rig = rig
    bpy.context.view_layer.objects.active = root
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed")
    check(bpy.ops.ttt.retarget() == {"FINISHED"}, "the rig over the moved model wasn't retargeted")
    _same_places(_exported_places(_rig_model(rig)), expected, "retargeted from a moved model")


def check_undo_gives_the_animations_back():
    """Retargeting is one step: undone, the model has its animations again and the rig is no model."""
    root = _fresh_model()
    root_name = root.name
    rig = _rig_over("Undone", tlm_blender.armature_of(root), {})
    root.ttt_retarget_rig = rig
    bpy.context.view_layer.objects.active = root
    check(bpy.ops.ttt.guess_pairs() == {"FINISHED"}, "the pairs weren't guessed")
    # Undo in the background starts with the first step pushed
    bpy.ops.ed.undo_push(message="Paired")
    check(bpy.ops.ttt.retarget("EXEC_DEFAULT", True) == {"FINISHED"}, "the rig wasn't retargeted")
    check(tlm_blender.animations_of(bpy.data.objects[root_name]) == [], "the model kept its animations")
    bpy.ops.ed.undo()
    animations = sorted(action.name for action in tlm_blender.animations_of(bpy.data.objects[root_name]))
    check(animations == ["Walk", "Wave"] and tlm_blender.find_root(bpy.data.objects["Undone"]) is None, "undo gave the model %s" % animations)


def main():
    twin_tech_tools.register()
    _, turns, warnings = check_from_a_file()
    check_from_the_scene(turns)
    check_a_left_out_joint()
    check_what_follows_aimed_bones()
    check_long_bone_names()
    check_copies_on_no_bone_keep_their_place_in_the_model()
    check_a_moved_model_retargets_in_place()
    check_undo_gives_the_animations_back()
    print("RETARGET OK, warnings: %s" % (warnings or "none"))


main()
