"""Imports a character whose joints are aimed and mirrored the way the game's are and checks the bones point down their limbs, in Blender:

    blender --background --factory-startup --python tests/blender_bone_axes.py [-- fixtures/ogi_mirrored_from_blender.tlm fixtures/ogi_mirrored_edited_in_blender.tlm]

The character (fixtures/ogi_mirrored.tlm, TT Lab's TlmFixtureTests write it from TestAssets.AddMirroredOgi) has joints aiming one of their
axes at their child: the arms behavior-mirrored (the right joint's frame the left's mirror image with every axis turned around, so the
right arm aims along -X), the legs plainly mirrored (only X turned around), the chest at the spine's place, the head and nose looking a
hair off straight ahead (Blender's -Y) on the middle line and the eyes to the side, short and far from the origin, which Blender keeps
with their roll a few thousandths of a radian off. Every bone gets axes of its own: twins are Blender's mirrored bones, bones on the
middle line have X to the model's left, a bone with children points at the child lying closest to one of its joint's axes, and no bone is
shorter than a fortieth of the skeleton. Exported and imported again, every bone is as it was, and so is every bone of a rig made in
Blender with rolls and tails of its own. Exported unchanged, after a round trip of the armature through edit mode and while posed on another frame,
the model is the file's: the joints' bind poses, the animations' bytes, the exit points' matrices, the bodies' vertexes and the hull to
the bit, the animations' keys and the exit points' places as close as floats get. A body put on an aimed bone at an offset in its joint's
frame (through the bone's own axes) comes out at that offset, an eye's too, and a turn about a joint's Z keyed on its bone through the bone's axes comes out as that
turn after the joint's rest in the game's keys. Given paths, the unchanged export is written to the first and the edited one to the
second, with what TT Lab should read in it next to it as JSON (TlmFixtureTests read them). Prints BONE AXES OK."""

import json
import math
import os
import random
import re
import shutil
import struct
import sys
import tempfile

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import axes, checks, save_hook, tlm, tlm_blender, tlm_math  # noqa: E402

FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "ogi_mirrored.tlm")
# The character's joints (TestAssets.AddMirroredOgi): the hips, spine, chest at the spine's place, head and nose on the middle line, then
# the eyes, the upper arms and hands, the thighs and shins, left (the game's -X, Blender's +X) and right
MIDDLE = ["Joint 0", "Joint 1", "Joint 2", "Joint 3", "Joint 4"]
TWINS = [("Joint 5", "Joint 6"), ("Joint 7", "Joint 9"), ("Joint 8", "Joint 10"), ("Joint 11", "Joint 13"), ("Joint 12", "Joint 14")]
# The head and nose looking a hair off straight ahead on the middle line, the eyes to the side, short and far from the origin, which
# Blender keeps with their roll a few thousandths of a radian off
LOOKING_AHEAD = ["Joint 3", "Joint 4", "Joint 5", "Joint 6"]
# The child each bone points at: the one lying closest to one of its joint's axes, looking past the chest at the spine's place
POINTS_AT = {"Joint 0": "Joint 1", "Joint 1": "Joint 3", "Joint 2": "Joint 3", "Joint 3": "Joint 4", "Joint 7": "Joint 8", "Joint 9": "Joint 10",
             "Joint 11": "Joint 12", "Joint 13": "Joint 14"}
# Blender's mirror in X
S = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))
# The bodies put on aimed bones in the game's joint frame: the mirrored right upper arm and the left eye
BODIES = [("Shield", "Joint 9", (0.1, 0.05, -0.02)), ("Visor", "Joint 5", (0.0, 0.03, 0.05))]
TURNED = ("Run", "Joint 9", 2, 30.0)
# Floats through Blender's single precision
CLOSE = 1e-5


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def _bits(values):
    values = [float(value) for value in values]
    return struct.pack("<%df" % len(values), *values)


def _same(a, b):
    """Whether two values of a file's JSON are the same, numbers to the bit as the files keep them"""
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(_same(a[key], b[key]) for key in a)

    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(_same(x, y) for x, y in zip(a, b))

    if isinstance(a, (int, float)) and not isinstance(a, bool) and isinstance(b, (int, float)) and not isinstance(b, bool):
        return _bits([a]) == _bits([b])

    return a == b


def _bones(armature):
    return armature.data.bones


def _angle(a, b):
    return a.angle(b) if a.length > 1e-12 and b.length > 1e-12 else math.pi


def _column(matrix, index):
    return Vector(matrix.col[index][:3])


def check_bones(armature):
    """The bones point down their limbs, twins mirrored and the middle line's across, none too short"""
    bones = _bones(armature)
    for left_name, right_name in TWINS:
        left, right = bones[left_name].matrix_local, bones[right_name].matrix_local
        mirrored = S @ left
        for index, sign, axis in ((0, -1.0, "X"), (1, 1.0, "Y"), (2, 1.0, "Z")):
            difference = (_column(right, index) - sign * _column(mirrored, index)).length
            check(difference < 1e-4, "%s's %s isn't the mirror of %s's (%s against %s)" % (right_name, axis, left_name, tuple(_column(right, index)), tuple(_column(mirrored, index))))

        check((_column(right, 3) - _column(mirrored, 3)).length < 1e-4, "%s's head isn't the mirror of %s's" % (right_name, left_name))
        check(abs(bones[left_name].length - bones[right_name].length) < 1e-5, "%s and %s are %f and %f long" % (left_name, right_name, bones[left_name].length, bones[right_name].length))

    for name in MIDDLE:
        x = _column(bones[name].matrix_local, 0)
        check(abs(x.x - 1.0) < 1e-4 and abs(x.y) < 1e-4 and abs(x.z) < 1e-4, "%s on the middle line has X %s, not to the model's left" % (name, tuple(x)))

    first = bones["Joint 0"].head_local
    size = max((bone.head_local - first).length for bone in bones)
    for bone in bones:
        check(bone.length >= 0.025 * size - 1e-6, "%s is %f long, a fortieth of the skeleton is %f" % (bone.name, bone.length, 0.025 * size))
        check(tlm_blender.bone_aim(bone) != axes.IDENTITY_AIM or bone.name == "Joint 0", "%s kept its joint's axes" % bone.name)

    for bone in bones:
        if not bone.children:
            continue

        # Past the children at its place to theirs
        ways, pending = {}, list(bone.children)
        while pending:
            child = pending.pop()
            if (child.head_local - bone.head_local).length > 1e-4:
                ways[child.name] = child.head_local - bone.head_local
            else:
                pending.extend(child.children)

        y = _column(bone.matrix_local, 1)
        nearest = min(ways, key=lambda name: _angle(y, ways[name]))
        check(nearest == POINTS_AT[bone.name] and _angle(y, ways[nearest]) < math.radians(1.0),
              "%s points %.3f degrees from %s, %.3f from %s" % (bone.name, math.degrees(_angle(y, ways[nearest])), nearest,
                                                            math.degrees(_angle(y, ways[POINTS_AT[bone.name]])), POINTS_AT[bone.name]))

    # The fixture does what it's for: these bones point a hair off Blender's -Y, past where Blender snaps a bone to it
    for name in LOOKING_AHEAD:
        off = _angle(_column(bones[name].matrix_local, 1), Vector((0.0, -1.0, 0.0)))
        check(3e-4 <= off <= 2e-3, "%s points %g radians from -Y" % (name, off))


def _keys(file, view, size, frames):
    values = list(file.read_view(view, "f32"))
    count = len(values) // size
    return [values[min(frame, count - 1) * size:min(frame, count - 1) * size + size] for frame in range(frames)]


def _same_rotation(a, b, tolerance=CLOSE):
    return min(max(abs(x - y) for x, y in zip(a, b)), max(abs(x + y) for x, y in zip(a, b))) <= tolerance


def _transform(node):
    """A node's translation, rotation (x, y, z, w) and scale, the identity's for what it hasn't"""
    return (node.get("translation", [0.0, 0.0, 0.0]), node.get("rotation", [0.0, 0.0, 0.0, 1.0]), node.get("scale", [1.0, 1.0, 1.0]))


def _close_transform(a, b, tolerance=CLOSE):
    (ta, ra, sa), (tb, rb, sb) = _transform(a), _transform(b)
    return all(abs(x - y) <= tolerance for x, y in zip(ta + sa, tb + sb)) and _same_rotation(ra, rb, tolerance)


def _resolved(file, value):
    """A value of the file's JSON with the binary data its views point at in their place"""
    if isinstance(value, dict) and "offset" in value and "type" in value:
        return value["type"], file.read_view(value).tobytes()

    if isinstance(value, dict):
        return {key: _resolved(file, item) for key, item in value.items()}

    return value


def _views_of(file, part, keys):
    return {key: _resolved(file, part[key]) for key in keys if key in part}


def compare(source_path, exported_path, label, edited=(), numbered=False):
    """The export is the file's model: what the add-on keeps of the game's to the bit, the rest as close as floats get. The joints, frames
    and bodies edited are left out. Numbered, the actions have the numbers Blender gives names other actions have"""
    source, exported = tlm.TlmFile.load(source_path), tlm.TlmFile.load(exported_path)
    # The add-on writes the joint ID count TT Lab works out as none (-1), TT Lab leaves it out
    root_data = [dict({"JointIdCount": -1}, **file.root.get("data", {})) for file in (source, exported)]
    check(_same(*root_data) or "bodies" in edited, "%s: the root's values are %s, were %s" % (label, root_data[1], root_data[0]))
    armatures = tlm.find_child(source.root, "armature"), tlm.find_child(exported.root, "armature")
    joints = [sorted(armature["joints"], key=lambda joint: joint["index"]) for armature in armatures]
    check(len(joints[0]) == len(joints[1]), "%s: %d joints, were %d" % (label, len(joints[1]), len(joints[0])))
    for a, b in zip(*joints):
        check((a["index"], a["parent"], a["name"]) == (b["index"], b["parent"], b["name"]), "%s: joint %s became %s" % (label, a["index"], b["index"]))
        check(_bits(a["bind"]) == _bits(b["bind"]), "%s: %s's bind pose is %s, was %s" % (label, a["name"], b["bind"], a["bind"]))
        check(_same(a.get("data"), b.get("data")), "%s: %s's values are %s, were %s" % (label, a["name"], b.get("data"), a.get("data")))

    animations = [armature["animations"] for armature in armatures]
    names = [[re.sub(r"\.\d{3}$", "", animation["name"]) if numbered else animation["name"] for animation in side] for side in animations]
    check(names[0] == names[1], "%s: the animations are %s" % (label, names[1]))
    for a, b in zip(*animations):
        for key in ("id", "fps", "frames", "joint_count"):
            check(a.get(key) == b.get(key), "%s: %s's %s is %s, was %s" % (label, a["name"], key, b.get(key), a.get(key)))

        check(source.read_view(a["exact"], "u8").tobytes() == exported.read_view(b["exact"], "u8").tobytes(), "%s: %s's bytes changed" % (label, a["name"]))
        frames = int(a["frames"])
        keys = {keys["joint"]: keys for keys in a["joints"]}, {keys["joint"]: keys for keys in b["joints"]}
        check(keys[0].keys() == keys[1].keys(), "%s: %s has the joints %s" % (label, a["name"], sorted(keys[1])))
        for joint, a_keys in keys[0].items():
            b_keys = keys[1][joint]
            for flag in ("independent_scaling", "additional_rotation"):
                check(a_keys.get(flag) == b_keys.get(flag), "%s: %s's joint %d's %s is %s" % (label, a["name"], joint, flag, b_keys.get(flag)))

            for channel, size in (("translation", 3), ("rotation", 4), ("scale", 3)):
                a_track, b_track = _keys(source, a_keys[channel], size, frames), _keys(exported, b_keys[channel], size, frames)
                for frame in range(frames):
                    if (a["name"], joint, frame) in edited:
                        continue

                    if channel == "rotation":
                        close = _same_rotation(a_track[frame], b_track[frame])
                    else:
                        # The game's values too, TT Lab rounds them to 4096ths
                        close = all(abs(x - y) <= CLOSE * max(1.0, abs(x)) and round(x * 4096) == round(y * 4096) for x, y in zip(a_track[frame], b_track[frame]))

                    check(close, "%s: %s's joint %d's %s on frame %d is %s, was %s" % (label, a["name"], joint, channel, frame, b_track[frame], a_track[frame]))

    nodes = {}
    for file in (source, exported):
        for kind in ("rigid_bodies", "exit_points", "collision_hulls"):
            nodes.setdefault(kind, []).append([node for node in tlm.traverse(tlm.find_child(file.root, kind)) if node.get("kind") in ("body", "exit_point", "hull")])

    exit_points = nodes["exit_points"]
    check(len(exit_points[0]) == len(exit_points[1]), "%s: %d exit points" % (label, len(exit_points[1])))
    for a, b in zip(*exit_points):
        check(a["joint"] == b["joint"] and _same(a.get("data"), b.get("data")), "%s: %s is on %s with %s" % (label, a["name"], b["joint"], b.get("data")))
        check(_bits(a["matrix"]) == _bits(b["matrix"]), "%s: %s's matrix is %s, was %s" % (label, a["name"], b["matrix"], a["matrix"]))
        check(_close_transform(a, b), "%s: %s is at %s, was at %s" % (label, a["name"], _transform(b), _transform(a)))

    bodies = nodes["rigid_bodies"]
    check(len(bodies[1]) == len(bodies[0]) + (len(BODIES) if "bodies" in edited else 0), "%s: %d bodies" % (label, len(bodies[1])))
    for a, b in zip(*bodies):
        check(a["joint"] == b["joint"] and _same(a.get("data"), b.get("data")), "%s: %s is on %s with %s" % (label, a["name"], b["joint"], b.get("data")))
        check(not any(key in b for key in ("translation", "rotation", "scale")), "%s: %s was moved to %s" % (label, a["name"], _transform(b)))
        for a_part, b_part in zip(a["mesh"]["parts"], b["mesh"]["parts"]):
            # The normals Blender shows go next to the game's (twin_normal), which TT Lab keeps while they still match
            views = _views_of(source, a_part, ("position", "twin_normal", "uv", "color", "emit_color", "faces", "strips"))
            written = _views_of(exported, b_part, views.keys())
            check(views == written, "%s: %s's %s changed" % (label, a["name"], ", ".join(key for key in views if views[key] != written.get(key))))

    hulls = nodes["collision_hulls"]
    check(len(hulls[0]) == len(hulls[1]), "%s: %d hulls" % (label, len(hulls[1])))
    for a, b in zip(*hulls):
        check(a["joint"] == b["joint"] and not any(key in b for key in ("translation", "rotation", "scale")), "%s: %s is on %s at %s" % (label, a["name"], b["joint"], _transform(b)))
        keys = ("vertices", "faces", "planes", "edge_directions", "face_normals", "edges")
        check(_views_of(source, a, keys) == _views_of(exported, b, keys), "%s: %s changed" % (label, a["name"]))

    skins = [tlm.find_child(file.root, "skin") for file in (source, exported)]
    for a_part, b_part in zip(skins[0]["mesh"]["parts"], skins[1]["mesh"]["parts"]):
        views = _views_of(source, a_part, ("position", "twin_normal", "uv", "color", "joints", "weights", "faces", "strips"))
        written = _views_of(exported, b_part, views.keys())
        check(views == written, "%s: the skin's %s changed" % (label, ", ".join(key for key in views if views[key] != written.get(key))))


def _new_body(name, root, bodies, armature, bone_name, offset, material, order):
    """A cube following the bone the way an import sets up what follows a joint, at the offset in the joint's frame, which is in the bone's
    own axes"""
    mesh = bpy.data.meshes.new(name)
    size = 0.02
    mesh.from_pydata([(x * size, y * size, z * size) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)], [],
                     [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
    mesh.update()
    mesh.materials.append(material)
    body = bpy.data.objects.new(name, mesh)
    root.users_collection[0].objects.link(body)
    body.parent = bodies
    body.ttt.type = "Body"
    body.ttt.body.order = order
    body.matrix_basis = Matrix.Translation(axes.aimed_vector(axes.joint_point(offset), tlm_blender.bone_aim(armature.data.bones[bone_name])))
    tlm_blender.follow_bone(body, root, armature, bone_name)
    return body


def _turn(armature, action_name, bone_name, frame, degrees):
    """Turns the bone about its joint's Z on the frame, through its bone's axes"""
    action = bpy.data.actions[action_name]
    bag = tlm_blender._existing_channelbag(action, next(slot for slot in action.slots if slot.target_id_type == "OBJECT"))
    path = 'pose.bones["%s"].rotation_quaternion' % bone_name
    curves = [next(fcurve for fcurve in bag.fcurves if fcurve.data_path == path and fcurve.array_index == index) for index in range(4)]
    rotation = Quaternion([curve.evaluate(frame) for curve in curves])
    aim = tlm_blender.bone_aim(armature.data.bones[bone_name])
    turn = Quaternion(axes.aimed_rotation_wxyz(axes.joint_rotation_wxyz(tuple(Quaternion((0.0, 0.0, 1.0), math.radians(degrees)))), aim))
    turned = turn @ rotation
    for index, curve in enumerate(curves):
        if len(curve.keyframe_points) == 1:
            value = curve.keyframe_points[0].co[1]
            curve.keyframe_points.add(frame)
            for key in range(frame + 1):
                curve.keyframe_points[key].co = (key, value)
                curve.keyframe_points[key].interpolation = "LINEAR"

        next(point for point in curve.keyframe_points if point.co[0] == frame).co[1] = turned[index]
        curve.update()


def check_turn(source_path, exported_path):
    """The turned key is the turn about the joint's Z at rest, after its rest relative to its parent: rest Rz rest^-1 key"""
    action_name, bone_name, frame, degrees = TURNED
    index = int(bone_name.split()[1])
    source, exported = tlm.TlmFile.load(source_path), tlm.TlmFile.load(exported_path)
    joints = {joint["index"]: joint for joint in tlm.find_child(source.root, "armature")["joints"]}
    rests = {joint: tlm_math.rest_of(tlm_math.from_rows(value["bind"])) for joint, value in joints.items()}
    rest = tlm_math.relative_rest(rests.get(joints[index]["parent"]), rests[index])[1]
    keys = []
    for file in (source, exported):
        animation = next(animation for animation in tlm.find_child(file.root, "armature")["animations"] if animation["name"] == action_name)
        track = next(keys for keys in animation["joints"] if keys["joint"] == index)
        keys.append(tlm_math.xyzw_to_wxyz(_keys(file, track["rotation"], 4, int(animation["frames"]))[frame]))

    half = math.radians(degrees) / 2.0
    turn = (math.cos(half), 0.0, 0.0, math.sin(half))
    expected = tlm_math.quat_multiply(tlm_math.quat_multiply(tlm_math.quat_multiply(rest, turn), tlm_math.quat_inverse(rest)), keys[0])
    check(_same_rotation(expected, keys[1]), "the turned key is %s, the turn after the joint's rest is %s" % (keys[1], expected))
    angle = 2.0 * math.acos(min(1.0, abs(sum(a * b for a, b in zip(tlm_math.quat_normalized(keys[0]), tlm_math.quat_normalized(keys[1]))))))
    check(abs(math.degrees(angle) - degrees) < 1e-3, "the key turned by %f degrees" % math.degrees(angle))


def check_bodies(exported_path):
    file = tlm.TlmFile.load(exported_path)
    joints = {joint["name"]: joint["index"] for joint in tlm.find_child(file.root, "armature")["joints"]}
    bodies = {node["name"]: node for node in tlm.traverse(tlm.find_child(file.root, "rigid_bodies")) if node.get("kind") == "body"}
    for name, bone_name, offset in BODIES:
        body = bodies[name]
        translation, rotation, scale = _transform(body)
        check(body["joint"] == joints[bone_name], "%s is on joint %s" % (name, body["joint"]))
        check(all(abs(a - b) <= CLOSE for a, b in zip(translation, offset)), "%s is at %s in its joint's frame, it was put at %s" % (name, translation, offset))
        check(_same_rotation(rotation, (0.0, 0.0, 0.0, 1.0)) and all(abs(value - 1.0) <= CLOSE for value in scale), "%s is turned by %s and scaled by %s" % (name, rotation, scale))


def _bone_shapes(armature):
    return {bone.name: ([value for row in bone.matrix_local for value in row], bone.length) for bone in _bones(armature)}


def check_bones_come_back(path, before, label):
    """Imported again, every bone is as it was: its head, axes and length (the file keeps what the bones were in Blender)"""
    file = tlm.TlmFile.load(path)
    for joint in tlm.find_child(file.root, "armature")["joints"]:
        check(isinstance(joint.get("bone"), dict) and axes.is_aim(joint["bone"].get("axes")), "%s: %s has no bone's axes" % (label, joint["name"]))

    for blender_object in list(bpy.data.objects):
        blender_object.hide_set(False)

    root = tlm_blender.import_file(bpy.context, path)
    bpy.context.view_layer.update()
    after = _bone_shapes(tlm_blender.armature_of(root))
    check(sorted(after) == sorted(before), "%s: the bones are %s, were %s" % (label, sorted(after), sorted(before)))
    for name, (matrix, length) in before.items():
        moved = max(abs(a - b) for a, b in zip(matrix, after[name][0]))
        check(moved < 1e-5 and abs(length - after[name][1]) < 1e-5, "%s: %s came back off by %g, %f long instead of %f" % (label, name, moved, after[name][1], length))

    return root


def check_made_rig(directory):
    """A rig made in Blender, bones of rolls of their own and tails pointing off their children's way, comes back as it was"""
    root = tlm_blender.import_file(bpy.context, FIXTURE)
    armature = tlm_blender.armature_of(root)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    for index, bone in enumerate(armature.data.edit_bones):
        bone.roll = bone.roll + math.radians(37.0 * (index + 1))
        if not bone.children:
            bone.tail = bone.head + Vector((0.01 * index, -0.05, 0.02 * (index % 3)))

    bpy.ops.object.mode_set(mode="OBJECT")
    before = _bone_shapes(armature)
    path = os.path.join(directory, "made_rig.tlm")
    _export(root, path)
    _remove(check_bones_come_back(path, before, "a rig of rolls of its own"))
    _remove(root)


def _export(root, path):
    bpy.context.view_layer.update()
    return tlm_blender.export_file(root, path)


def _export_again(root, directory):
    path = os.path.join(directory, "again.tlm")
    _export(root, path)
    return path


def _remove(root):
    for blender_object in [root] + list(tlm_blender._descendants(root)):
        bpy.data.objects.remove(blender_object)


def _clear():
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    for blocks in (bpy.data.meshes, bpy.data.armatures):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def _copy_of_fixture(directory, name):
    path = os.path.join(directory, name)
    shutil.copyfile(FIXTURE, path)
    return path


def _places(path):
    """Where the file puts every body's, exit point's and hull's points in the model's space, and on which joint: an exit point's frame
    (its joint's bind times its transform), a body's and a hull's vertexes."""
    file = tlm.TlmFile.load(path)
    binds = {joint["index"]: Matrix(tlm_math.from_rows(joint["bind"])) for joint in tlm.find_child(file.root, "armature")["joints"]}
    places = {}
    for node in tlm.traverse(file.root):
        kind = node.get("kind")
        if kind not in ("body", "exit_point", "hull"):
            continue

        local = Matrix(tlm_math.compose(node.get("translation", (0.0, 0.0, 0.0)), tlm_math.xyzw_to_wxyz(node.get("rotation", (0.0, 0.0, 0.0, 1.0))),
                                        node.get("scale", (1.0, 1.0, 1.0))))
        game = binds[node["joint"]] @ local if node.get("joint") in binds else local
        if kind == "exit_point":
            points = [game.col[column].to_3d() for column in range(4)]
        elif kind == "hull":
            values = file.read_view(node["vertices"], "f32")
            points = [game @ Vector(values[i:i + 3]) for i in range(0, len(values) - 3, 4)]
        else:
            values = [value for part in node["mesh"]["parts"] for value in file.read_view(part["position"], "f32")]
            points = [game @ Vector(values[i:i + 3]) for i in range(0, len(values) - 2, 3)]

        places[node["name"]] = (node.get("joint"), points)

    return places


def _same_places(places, expected, joints, label):
    for name, (joint, points) in expected.items():
        check(name in places, "%s: %s isn't written" % (label, name))
        written_joint, written = places[name]
        check(written_joint == joints.get(name, joint), "%s: %s is on joint %s, expected %s" % (label, name, written_joint, joints.get(name, joint)))
        off = max((p - q).length for p, q in zip(written, points)) if len(written) == len(points) else float("inf")
        check(off < 1e-4, "%s: %s is %g from where it was" % (label, name, off))


def _parent_to(blender_object, parent, bone_name=None, kind="OBJECT"):
    """Parents the object the way Ctrl+P does, which leaves it where it is (Keep Transform would bake a Child Of into it)."""
    bpy.ops.object.select_all(action="DESELECT")
    blender_object.select_set(True)
    parent.select_set(True)
    bpy.context.view_layer.objects.active = parent
    if bone_name is not None:
        parent.data.bones.active = parent.data.bones[bone_name]

    check(bpy.ops.object.parent_set(type=kind) == {"FINISHED"}, "%s wasn't parented to %s" % (blender_object.name, parent.name))
    bpy.context.view_layer.update()


def _without_child_of(blender_object):
    world = blender_object.matrix_world.copy()
    for constraint in list(blender_object.constraints):
        blender_object.constraints.remove(constraint)

    bpy.context.view_layer.update()
    blender_object.matrix_world = world
    bpy.context.view_layer.update()


def check_parenting(directory):
    """Bodies, exit points and hulls put on bones the other ways Blender has stay where they are and get those bones' joints, whatever the
    pose: Ctrl+P > Bone Relative, parented to a body that follows a bone (a hull, and a muzzle made in Blender), and parented to a bone
    keeping the Child Of an import gives, which is refused until the Child Of goes (Blender moves it with both bones). The arms' bones, not
    the eyes' and head's Blender keeps a little off their binds: what moves to another bone takes where Blender shows it along. An exit
    point taken off an eye's bone where it is stays on its joint as it was, a body parented to the armature object keeping its Child Of
    stays on its bone, and the fix clicked while posed keeps the place at rest."""
    _clear()
    root = tlm_blender.import_file(bpy.context, _copy_of_fixture(directory, "parenting.tlm"))
    armature = tlm_blender.armature_of(root)
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    path = os.path.join(directory, "parenting_before.tlm")
    _export(root, path)
    before = _places(path)
    objects = {child.name: child for child in tlm_blender._descendants(root)}
    _without_child_of(objects["Exit Point 1"])
    _parent_to(objects["Exit Point 1"], armature, "Joint 3", "BONE_RELATIVE")
    _without_child_of(objects["Hull 0"])
    _parent_to(objects["Hull 0"], objects["Rigid Model 1"])
    # Taken off its eye's bone, which Blender keeps a little off its bind, where it is: on its joint as it was
    _without_child_of(objects["Exit Point 2"])
    muzzle = bpy.data.objects.new("Muzzle", None)
    root.users_collection[0].objects.link(muzzle)
    muzzle.parent = objects["Exit Points"] if "Exit Points" in objects else root
    muzzle.ttt.type = "ExitPoint"
    muzzle.ttt.exit_point.id = 3
    muzzle.matrix_world = objects["Rigid Model 1"].matrix_world @ Matrix.Translation((0.05, 0.1, 0.0)) @ Matrix.Rotation(0.4, 4, "Z")
    bpy.context.view_layer.update()
    _parent_to(muzzle, objects["Rigid Model 1"])
    muzzle_frame = Matrix(axes.joint_frame_back([list(row) for row in root.matrix_world.inverted() @ muzzle.matrix_world]))
    before["Muzzle"] = (before["Rigid Model 1"][0], [muzzle_frame.col[column].to_3d() for column in range(4)])

    # Parented to the armature object (Relations panel's Armature), keeping its Child Of: the armature's bones don't move it
    body = objects["Rigid Model 0"]
    world = body.matrix_world.copy()
    body.parent, body.parent_type = armature, "ARMATURE"
    body.matrix_parent_inverse = armature.matrix_world.inverted()
    bpy.context.view_layer.update()
    check(max(abs(a - b) for row_a, row_b in zip(world, body.matrix_world) for a, b in zip(row_a, row_b)) < 1e-6, "parenting moved the body")

    _parent_to(objects["Exit Point 0"], armature, "Joint 9", "BONE")
    refused = [problem for problem in checks.refused(root) if "Exit Point 0" in problem.subjects]
    check(len(refused) == 1 and refused[0].fix == "ttt.remove_child_of", "following two bones isn't refused: %s" % checks.refused(root))
    # Clicked while posed: the place it keeps is the one at rest
    armature.data.pose_position = "POSE"
    tlm_blender.assign_action(armature, next(action for action in tlm_blender.animations_of(root) if action.name.startswith("Run")))
    bpy.context.scene.frame_set(3)
    bpy.context.view_layer.objects.active = root
    check(bpy.ops.ttt.remove_child_of(subjects="Exit Point 0") == {"FINISHED"}, "the Child Of wasn't removed")
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    check(not checks.refused(root), "still refused: %s" % checks.refused(root))
    joints = {"Hull 0": before["Rigid Model 1"][0], "Exit Point 0": 9}
    for pose in ("REST", "POSE"):
        armature.data.pose_position = pose
        if pose == "POSE":
            bpy.context.scene.frame_set(3)

        path = os.path.join(directory, "parenting_%s.tlm" % pose.lower())
        _export(root, path)
        _same_places(_places(path), before, joints, "parented the other ways, exported in %s position" % pose.lower())

    _clear()


def check_saving_in_edit_mode(directory):
    """Saving the blend file in Edit Mode writes the model as it's edited, a mesh being edited whole: Blender only puts what's edited into
    the bones and meshes once it has written the blend file, and hides a mesh's UVs while it's edited."""
    _clear()
    path = _copy_of_fixture(directory, "saved.tlm")
    root = tlm_blender.import_file(bpy.context, path)
    armature = tlm_blender.armature_of(root)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "saved.blend"))
    saved = tlm.TlmFile.load(path)
    joints = {joint["name"]: joint for joint in tlm.find_child(saved.root, "armature")["joints"]}

    # Joint 13 moved along Blender's -Y, the game's +Z
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    for end in ("head", "tail"):
        point = getattr(armature.data.edit_bones["Joint 13"], end)
        point.y -= 0.2

    bpy.ops.wm.save_mainfile()
    check(armature.mode == "EDIT", "the save left Edit Mode")
    edited = {joint["name"]: joint for joint in tlm.find_child(tlm.TlmFile.load(path).root, "armature")["joints"]}
    # The bind's translation is its last column, Z at index 11
    moved = edited["Joint 13"]["bind"][11] - joints["Joint 13"]["bind"][11]
    check(abs(moved - 0.2) < 1e-4, "saved in Edit Mode, Joint 13 moved %g in the model file, not 0.2" % moved)
    bpy.ops.object.mode_set(mode="OBJECT")

    body = next(child for child in tlm_blender._descendants(root) if child.name == "Rigid Model 0")
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.wm.save_mainfile()
    check(body.mode == "EDIT", "the save left the body's Edit Mode")
    written = tlm.TlmFile.load(path)
    parts = [next(node for node in tlm.traverse(file.root) if node.get("name") == "Rigid Model 0")["mesh"]["parts"] for file in (saved, written)]
    for a, b in zip(*parts):
        for key in ("position", "uv", "color"):
            check(list(saved.read_view(a[key], "f32" if key != "color" else "u8")) == list(written.read_view(b[key], "f32" if key != "color" else "u8")),
                  "saved with the body in Edit Mode, its %s changed" % key)

    bpy.ops.object.mode_set(mode="OBJECT")

    # In Edit Mode while nothing is active
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.context.view_layer.objects.active = None
    bpy.ops.wm.save_mainfile()
    check(not save_hook._refused, "saved with the body in Edit Mode and nothing active: %s" % save_hook._refused)
    check(body.mode == "EDIT", "the save left the body's Edit Mode while nothing was active")
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="OBJECT")
    _clear()


def check_syncing_in_edit_mode(directory):
    """With Only Manually saving writes nothing, and Sync with TT Lab in Edit Mode writes the model as it's edited (into its file, no TT Lab
    has the project open), stays in Edit Mode and leaves the blend file as it was."""
    _clear()
    path = _copy_of_fixture(directory, "synced.tlm")
    root = tlm_blender.import_file(bpy.context, path)
    armature = tlm_blender.armature_of(root)
    blend = os.path.join(directory, "synced.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    joints = {joint["name"]: joint for joint in tlm.find_child(tlm.TlmFile.load(path).root, "armature")["joints"]}
    save_hook.SYNC_ON_SAVE = "MANUAL"
    try:
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.mode_set(mode="EDIT")
        for end in ("head", "tail"):
            getattr(armature.data.edit_bones["Joint 13"], end).y -= 0.2

        with open(path, "rb") as file:
            before = file.read()

        bpy.ops.wm.save_mainfile()
        with open(path, "rb") as file:
            check(file.read() == before, "saving with Only Manually wrote the model")

        saved = os.stat(blend).st_mtime_ns
        check(bpy.ops.ttt.sync_with_tt_lab() == {"FINISHED"}, "Sync with TT Lab didn't run")
        check(armature.mode == "EDIT", "syncing left Edit Mode")
        edited = {joint["name"]: joint for joint in tlm.find_child(tlm.TlmFile.load(path).root, "armature")["joints"]}
        moved = edited["Joint 13"]["bind"][11] - joints["Joint 13"]["bind"][11]
        check(abs(moved - 0.2) < 1e-4, "synced in Edit Mode, Joint 13 moved %g in the model file, not 0.2" % moved)
        check(os.stat(blend).st_mtime_ns == saved, "syncing saved the blend file")
        bpy.ops.object.mode_set(mode="OBJECT")
    finally:
        save_hook.SYNC_ON_SAVE = "ALWAYS"

    _clear()


def check_moved_root(directory):
    """A model's root moved and turned keeps its bodies, exit points and hulls where they are on it, in Blender and in the export: the
    import's Child Of takes where they are from the root, which counts once."""
    _clear()
    root = tlm_blender.import_file(bpy.context, FIXTURE)
    armature = tlm_blender.armature_of(root)
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    attached = [child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) in ("body", "exit_point", "hull")]
    shown = {child.name: root.matrix_world.inverted() @ child.matrix_world for child in attached}
    path = os.path.join(directory, "unmoved.tlm")
    _export(root, path)
    before = _places(path)
    root.matrix_world = Matrix.Translation((1.0, 2.0, 0.5)) @ Matrix.Rotation(math.radians(30.0), 4, "Z")
    bpy.context.view_layer.update()
    for child in attached:
        on_root = root.matrix_world.inverted() @ child.matrix_world
        off = max(abs(a - b) for row_a, row_b in zip(on_root, shown[child.name]) for a, b in zip(row_a, row_b))
        check(off < 1e-5, "%s moved %g on its model's root as the root moved" % (child.name, off))

    path = os.path.join(directory, "moved.tlm")
    _export(root, path)
    _same_places(_places(path), before, {}, "the root moved")
    _clear()


def check_lengths_stay(directory):
    """Bones come back as long as they were made through export after export: Blender keeps a bone by its head and tail, the length it
    gives back is a little off."""
    _clear()
    current, lengths = FIXTURE, None
    for cycle in range(4):
        root = tlm_blender.import_file(bpy.context, current)
        if cycle == 0:
            # Bones made of matrices like an import makes them, folding back along their parents (close to the parent's -Y, where
            # Blender works a bone's matrix out with less precision), and their children: Blender keeps their matrices up to 1e-5 off unit
            armature = tlm_blender.armature_of(root)
            bpy.context.view_layer.objects.active = armature
            bpy.ops.object.mode_set(mode="EDIT")
            generator = random.Random(3)
            for number in range(12):
                parent = armature.data.edit_bones["Joint 3" if number % 2 else "Joint 4"]
                frame = parent.matrix.to_3x3()
                tilt = math.sqrt(2.0 * generator.uniform(1e-3, 1e-2))
                side = frame.col[0] * math.cos(number) + frame.col[2] * math.sin(number)
                way = (-frame.col[1] * math.cos(tilt) + side * math.sin(tilt)).normalized()
                turn = Vector((0.0, 1.0, 0.0)).rotation_difference(way).to_matrix() @ Matrix.Rotation(generator.uniform(-math.pi, math.pi), 3, "Y")
                bone = armature.data.edit_bones.new("Ahead %d" % number)
                bone.head, bone.tail = (0.0, 0.0, 0.0), (0.0, 0.05, 0.0)
                bone.matrix = Matrix.Translation(parent.tail) @ turn.to_4x4()
                bone.parent = parent
                tip = armature.data.edit_bones.new("Ahead %d tip" % number)
                tip.head, tip.tail = bone.tail, bone.tail + Vector((0.01, -0.04, 0.02))
                tip.parent = bone

            bpy.ops.object.mode_set(mode="OBJECT")
            check(max(abs(bone.matrix_local.to_3x3().col[1].length - 1.0) for bone in armature.data.bones) > 1e-6, "Blender kept every bone's matrix unit")

        current = os.path.join(directory, "lengths_%d.tlm" % cycle)
        _export(root, current)
        written = [joint["bone"]["length"] for joint in tlm.find_child(tlm.TlmFile.load(current).root, "armature")["joints"]]
        check(lengths is None or written == lengths, "exported %d times, the bones' lengths changed: %s" % (cycle + 1, [
            (index, a, b) for index, (a, b) in enumerate(zip(lengths or [], written)) if a != b]))
        lengths = written
        _clear()


def check_scaled_armature(directory):
    """An armature object scaled under the model's root (an FBX rig's 0.01) comes back with its bones as long as they were shown."""
    _clear()
    root = tlm_blender.import_file(bpy.context, FIXTURE)
    armature = tlm_blender.armature_of(root)
    armature.scale = (0.01, 0.01, 0.01)
    bpy.context.view_layer.update()

    def shown(model, rig):
        relative = (model.matrix_world.inverted() @ rig.matrix_world).to_3x3()
        return {bone.name: (relative @ (bone.tail_local - bone.head_local)).length for bone in rig.data.bones}

    before = shown(root, armature)
    path = os.path.join(directory, "scaled.tlm")
    _export(root, path)
    _clear()
    again = tlm_blender.import_file(bpy.context, path)
    after = shown(again, tlm_blender.armature_of(again))
    for name, length in before.items():
        check(abs(after[name] - length) <= 1e-4 * length, "%s is %g long imported from the scaled armature's export, was shown %g long" % (name, after[name], length))

    _clear()


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    twin_tech_tools.register()
    # Saving writes the models, as Always on Save does: these checks are of what's written
    save_hook.SYNC_ON_SAVE = "ALWAYS"
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    directory = tempfile.mkdtemp(prefix="ttt_bone_axes_")
    try:
        root = tlm_blender.import_file(bpy.context, FIXTURE)
        armature = tlm_blender.armature_of(root)
        bpy.context.view_layer.update()
        check_bones(armature)

        unchanged = os.path.abspath(arguments[0]) if len(arguments) > 0 else os.path.join(directory, "unchanged.tlm")
        warnings = _export(root, unchanged)
        compare(FIXTURE, unchanged, "exported unchanged")
        imported = check_bones_come_back(unchanged, _bone_shapes(armature), "imported again")
        compare(FIXTURE, _export_again(imported, directory), "exported, imported again and exported", numbered=True)
        _remove(imported)
        check_made_rig(directory)

        # Blender makes the bones again of their heads, tails and rolls
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.object.mode_set(mode="OBJECT")
        moved = max(max(abs(a - b) for a, b in zip([value for row in bone.matrix_local for value in row], bone[tlm_blender.REST_PROPERTY])) for bone in _bones(armature))
        check_bones(armature)
        path = os.path.join(directory, "edit_mode.tlm")
        _export(root, path)
        compare(FIXTURE, path, "exported after edit mode")

        tlm_blender.assign_action(armature, bpy.data.actions["Run"])
        bpy.context.scene.frame_set(3)
        path = os.path.join(directory, "posed.tlm")
        _export(root, path)
        compare(FIXTURE, path, "exported posed")

        bodies = next(child for child in root.children if child.get(tlm_blender.KIND_PROPERTY) == "rigid_bodies")
        material = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "skin").material_slots[0].material
        for order, (name, bone_name, offset) in enumerate(BODIES):
            _new_body(name, root, bodies, armature, bone_name, offset, material, 2 + order)

        action_name, bone_name, frame, degrees = TURNED
        _turn(armature, action_name, bone_name, frame, degrees)
        edited = os.path.abspath(arguments[1]) if len(arguments) > 1 else os.path.join(directory, "edited.tlm")
        _export(root, edited)
        compare(FIXTURE, edited, "edited", edited={"bodies", (action_name, int(bone_name.split()[1]), frame)})
        check_bodies(edited)
        check_turn(FIXTURE, edited)
        if len(arguments) > 1:
            expectations = {"bodies": [{"name": name, "joint": int(bone.split()[1]), "offset": list(offset)} for name, bone, offset in BODIES],
                            "turned": {"animation": action_name, "joint": int(bone_name.split()[1]), "frame": frame, "degrees": degrees}}
            with open(os.path.splitext(edited)[0] + ".json", "w", encoding="utf-8") as file:
                json.dump(expectations, file, indent=2)

        check_parenting(directory)
        check_moved_root(directory)
        check_lengths_stay(directory)
        check_scaled_armature(directory)
        check_saving_in_edit_mode(directory)
        check_syncing_in_edit_mode(directory)
    finally:
        shutil.rmtree(directory, ignore_errors=True)

    print("BONE AXES OK, edit mode moved the bones' rests by %g at most, warnings: %s" % (moved, warnings or "none"))


main()
