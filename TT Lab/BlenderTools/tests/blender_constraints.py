"""The constraints of a game model's parts, in Blender: the export writes a rigid body, exit point or hull where Blender shows it with the
armature at rest, Set Inverse pressed in a pose included; the checks refuse what it couldn't write so (anything but one plain Child Of to
a bone moving a part, a part squashed flat, an exit point sheared) and Make It Follow One Bone fixes it without moving it; a rigid body's
or hull's shear goes into its vertexes.

    blender --background --factory-startup --python tests/blender_constraints.py -- <scratch folder>
"""

import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Quaternion

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import checks  # noqa: E402
from twin_tech_tools import parts  # noqa: E402
from twin_tech_tools import tlm_blender  # noqa: E402

FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "ogi.tlm")


def _check(condition, message):
    if not condition:
        raise AssertionError(message)


def _clear():
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    for _ in range(3):
        for collection in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials, bpy.data.images, bpy.data.collections):
            for item in list(collection):
                if item.users == 0:
                    collection.remove(item)


def _import(path=FIXTURE):
    """The model imported with no animation on and its bones at rest but for joint 1, turned and moved"""
    _clear()
    root = tlm_blender.import_file(bpy.context, path)
    bpy.context.view_layer.update()
    armature = tlm_blender.armature_of(root)
    if armature.animation_data is not None:
        armature.animation_data.action = None

    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_quaternion = Quaternion()
        pose_bone.scale = (1.0, 1.0, 1.0)

    _pose(armature, "Joint 1", rotation=Euler((0.5, 0.3, -0.4)), location=(0.1, 0.2, 0.05))
    return root, armature


def _pose(armature, bone, rotation=None, location=None, scale=None):
    pose_bone = armature.pose.bones[bone]
    if rotation is not None:
        pose_bone.rotation_quaternion = rotation.to_quaternion()
    if location is not None:
        pose_bone.location = location
    if scale is not None:
        pose_bone.scale = scale

    bpy.context.view_layer.update()


def _holder(root, kind):
    return next(child for child in root.children if child.get(tlm_blender.KIND_PROPERTY) == kind)


def _part(name, root, kind, parent=None, place=None):
    """A cube (a body or hull) or an empty (an exit point) put in the world where a modder puts it"""
    if kind == "ExitPoint":
        blender_object = bpy.data.objects.new(name, None)
    else:
        size = 0.2
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(x * size, y * size, z * size) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)], [],
                         [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
        mesh.update()
        blender_object = bpy.data.objects.new(name, mesh)

    root.users_collection[0].objects.link(blender_object)
    holders = {"Body": "rigid_bodies", "ExitPoint": "exit_points", "CollisionHull": "collision_hulls"}
    blender_object.parent = parent if parent is not None else _holder(root, holders[kind])
    blender_object.matrix_parent_inverse = blender_object.parent.matrix_world.inverted()
    blender_object.ttt.type = kind
    blender_object.matrix_world = place if place is not None else Matrix.Translation((0.4, 0.3, 0.9)) @ Matrix.Rotation(math.radians(20), 4, "X")
    bpy.context.view_layer.update()
    return blender_object


def _child_of(blender_object, target, bone=""):
    """A Child Of the way Blender's Add Object Constraint makes one: its inverse set where the bone is, without pressing Set Inverse"""
    constraint = blender_object.constraints.new("CHILD_OF")
    constraint.target = target
    constraint.subtarget = bone
    bpy.context.view_layer.update()
    return constraint


def _set_inverse(blender_object, constraint):
    with bpy.context.temp_override(object=blender_object, active_object=blender_object):
        _check(bpy.ops.constraint.childof_set_inverse(constraint=constraint.name, owner="OBJECT") == {"FINISHED"}, "Set Inverse didn't run")

    bpy.context.view_layer.update()


def _at_rest(armature, names):
    """Where Blender shows the objects with the armature at rest: a mesh's vertexes or an empty's place and axes"""
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    try:
        return {name: _place(bpy.data.objects[name]) for name in names}
    finally:
        armature.data.pose_position = "POSE"
        bpy.context.view_layer.update()


def _place(blender_object):
    world = blender_object.matrix_world.copy()
    if blender_object.type == "MESH":
        return [world @ vertex.co for vertex in blender_object.data.vertices], None

    return [world.translation.copy()], world.to_3x3().normalized()


def _same_place(before, after, tolerance=1e-4):
    points, turn = before
    other_points, other_turn = after
    off = max(max(min((point - other).length for other in other_points) for point in points),
              max(min((point - other).length for other in points) for point in other_points))
    turned = max((turn.col[i] - other_turn.col[i]).length for i in range(3)) if turn is not None else 0.0
    return off <= tolerance and turned <= tolerance, "points off by %.6f, axes by %.6f" % (off, turned)


def _refused(root, name):
    return [problem for problem in checks.refused(root) if name in problem.subjects]


def _fix(name):
    _check(bpy.ops.ttt.follow_one_bone(subjects=name) == {"FINISHED"}, "%s wasn't fixed" % name)
    bpy.context.view_layer.update()


def _round_trip(root, armature, names, folder, file_name):
    """Exports the model, imports it again and checks the parts are where they were with the armature at rest"""
    _check(not checks.refused(root), "the model is refused: %s" % checks.refused(root))
    before = _at_rest(armature, names)
    target = os.path.join(folder, file_name)
    tlm_blender.export_file(root, target)
    again, armature = _import(target)
    after = _at_rest(armature, names)
    for name in names:
        same, how = _same_place(before[name], after[name])
        _check(same, "%s moved through the file: %s" % (name, how))

    return again, armature


def set_inverse_is_written(folder):
    root, armature = _import()
    rigid, exit_point = bpy.data.objects["Rigid Model 0"], bpy.data.objects["Exit Point 0"]
    _set_inverse(rigid, rigid.constraints[0])
    _set_inverse(exit_point, exit_point.constraints[0])
    hat = _part("Hat", root, "Body")
    _child_of(hat, armature, "Joint 2")
    grip = _part("Grip", root, "ExitPoint")
    _set_inverse(grip, _child_of(grip, armature, "Joint 1"))
    guard = _part("Guard", root, "CollisionHull")
    _child_of(guard, armature, "Joint 2")
    _check(rigid.constraints[0].inverse_matrix != Matrix.Identity(4) and hat.constraints[0].inverse_matrix != Matrix.Identity(4),
           "the inverses weren't set")
    _round_trip(root, armature, ["Rigid Model 0", "Exit Point 0", "Hat", "Grip", "Guard"], folder, "constraints_set_inverse.tlm")
    print("SET INVERSE OK")


def refusals_are_fixed_in_place(folder):
    root, armature = _import()
    socket = bpy.data.objects.new("Socket", None)
    root.users_collection[0].objects.link(socket)
    target = bpy.data.objects.new("Target", None)
    root.users_collection[0].objects.link(target)
    target.location = (1.0, -0.5, 0.25)

    def made(name, kind="Body"):
        return _part(name, root, kind)

    _child_of(made("Muted"), armature, "Joint 2").mute = True
    _child_of(made("Half"), armature, "Joint 2").influence = 0.5
    unturned = _child_of(made("Unturned"), armature, "Joint 1")
    unturned.use_rotation_x = unturned.use_rotation_z = False
    _child_of(made("Local", "ExitPoint"), armature, "Joint 1").owner_space = "LOCAL"
    _child_of(made("Target Local"), armature, "Joint 2").target_space = "LOCAL_WITH_PARENT"
    switching = made("Switching")
    _child_of(switching, armature, "Joint 1")
    _child_of(switching, armature, "Joint 2").influence = 0.0
    _child_of(made("On Socket"), socket)
    _child_of(made("On Armature"), armature)
    tracked = made("Tracked")
    tracked.constraints.new("COPY_LOCATION").target = target
    quiet = made("Quiet")
    _child_of(quiet, armature, "Joint 1")
    muted_track = quiet.constraints.new("COPY_LOCATION")
    muted_track.target = target
    muted_track.mute = True
    holder = bpy.data.objects.new("Holder", None)
    root.users_collection[0].objects.link(holder)
    holder.parent = root
    _child_of(holder, armature, "Joint 1").influence = 0.5
    _part("Held", root, "Body", parent=holder)
    # The targets move after the constraints got their inverses: what they do shows
    socket.location = (0.0, 0.5, -0.5)
    _pose(armature, "Joint 2", rotation=Euler((0.0, 0.0, 0.7)))

    expected = {"Muted": parts.REMOVE_CONSTRAINTS_TEXT, "Half": parts.FOLLOW_ONE_BONE_TEXT, "Unturned": parts.FOLLOW_ONE_BONE_TEXT,
                "Local": parts.FOLLOW_ONE_BONE_TEXT, "Target Local": parts.FOLLOW_ONE_BONE_TEXT, "Switching": parts.FOLLOW_ONE_BONE_TEXT,
                "On Socket": parts.REMOVE_CONSTRAINTS_TEXT, "On Armature": parts.REMOVE_CONSTRAINTS_TEXT, "Tracked": parts.REMOVE_CONSTRAINTS_TEXT,
                "Holder": parts.FOLLOW_ONE_BONE_TEXT}
    for name, label in expected.items():
        found = _refused(root, name)
        _check(len(found) == 1 and found[0].fix == parts.FOLLOW_ONE_BONE and found[0].fix_text == label, "%s's refusal is %s" % (name, found))

    _check(not _refused(root, "Quiet") and not _refused(root, "Held"), "a part moved only by a plain Child Of was refused")
    _check(tlm_blender.export_problem(root) is not None, "the export button doesn't say why it can't export")
    names = list(expected) + ["Held", "Quiet"]
    before = _at_rest(armature, names)
    for name in expected:
        _fix(name)

    after = _at_rest(armature, names)
    for name in names:
        same, how = _same_place(before[name], after[name], 1e-5)
        _check(same, "%s moved when it was fixed: %s" % (name, how))

    plain = {name: [(constraint.type, getattr(constraint, "subtarget", "")) for constraint in bpy.data.objects[name].constraints] for name in expected}
    _check(plain["Switching"] == [("CHILD_OF", "Joint 1")] and plain["Muted"] == [] and plain["Tracked"] == [] and plain["On Socket"] == [],
           "the fix kept %s" % plain)
    for name in ("Half", "Unturned", "Local", "Target Local", "Holder"):
        constraint = bpy.data.objects[name].constraints[0]
        _check(constraint.influence == 1.0 and constraint.owner_space == "WORLD" and constraint.target_space == "WORLD" and
               all(getattr(constraint, attribute) for attribute, _ in parts.CHILD_OF_CHANNELS), "%s's Child Of isn't plain" % name)

    parts_written = [name for name in names if tlm_blender.role_of(bpy.data.objects[name]) in ("body", "exit_point", "hull")]
    _round_trip(root, armature, parts_written, folder, "constraints_fixed.tlm")
    print("REFUSALS OK")


def shears_and_squashes(folder):
    root, armature = _import()
    rigid, exit_point = bpy.data.objects["Rigid Model 0"], bpy.data.objects["Exit Point 0"]
    imported = _at_rest(armature, ["Exit Point 0"])
    # Joint 1 stretched unevenly: what gets a Child Of now is sheared at rest
    _pose(armature, "Joint 1", scale=(1.0, 1.3, 1.0))
    _child_of(_part("Sheared Body", root, "Body"), armature, "Joint 1")
    _child_of(_part("Sheared Hull", root, "CollisionHull"), armature, "Joint 1")
    _child_of(_part("Sheared Point", root, "ExitPoint"), armature, "Joint 1")
    _pose(armature, "Joint 1", scale=(1.0, 1.0, 1.0))
    # Joint 2 hidden the way animations hide joints
    _pose(armature, "Joint 2", scale=(1.0, 0.0, 1.0))
    placed = Matrix.Translation((0.2, -0.3, 0.6))
    _child_of(_part("Squashed", root, "Body", place=placed), armature, "Joint 2")
    _set_inverse(exit_point, exit_point.constraints[0])
    _pose(armature, "Joint 2", scale=(1.0, 1.0, 1.0))

    names = ["Sheared Body", "Sheared Hull", "Sheared Point", "Squashed", "Exit Point 0", "Rigid Model 0"]
    shown = _at_rest(armature, names)
    _check(not _refused(root, "Sheared Body") and not _refused(root, "Sheared Hull") and not _refused(root, "Rigid Model 0"),
           "a rigid body or hull was refused for a shear its vertexes take: %s" % checks.refused(root))
    for name, word in (("Sheared Point", "sheared"), ("Squashed", "squashed"), ("Exit Point 0", "squashed")):
        found = _refused(root, name)
        _check(len(found) == 1 and word in found[0].text and found[0].fix_text == parts.FOLLOW_ONE_BONE_TEXT, "%s's refusal is %s" % (name, found))
        _fix(name)

    fixed = _at_rest(armature, ["Sheared Point", "Squashed", "Exit Point 0"])
    point = bpy.data.objects["Sheared Point"]
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    _check(not tlm_blender.is_sheared(point.matrix_world, tlm_blender.EXIT_POINT_SHEAR_TOLERANCE), "the exit point is still sheared")
    armature.data.pose_position = "POSE"
    _check((fixed["Sheared Point"][0][0] - shown["Sheared Point"][0][0]).length < 1e-5, "the exit point moved when it was straightened")
    same, how = _same_place(fixed["Squashed"], ([placed @ vertex.co for vertex in bpy.data.objects["Squashed"].data.vertices], None), 1e-5)
    _check(same, "the squashed body isn't back where its own transform puts it: %s" % how)
    same, how = _same_place(fixed["Exit Point 0"], imported["Exit Point 0"], 1e-5)
    _check(same, "the imported exit point isn't back where the import put it: %s" % how)
    _round_trip(root, armature, names, folder, "constraints_sheared.tlm")
    print("SHEARS OK")


def main():
    folder = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    twin_tech_tools.register()
    set_inverse_is_written(folder)
    refusals_are_fixed_in_place(folder)
    shears_and_squashes(folder)
    print("CONSTRAINTS OK")


main()
