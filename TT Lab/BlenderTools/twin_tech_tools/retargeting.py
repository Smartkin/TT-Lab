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

"""Retargeting: a rig plays a model's animations, which leave the model.

The rig is an armature of the scene (made a model when it isn't part of one) or a model file's. Its table says which of the model's joints
each of its bones stands for (pairing: literally a subtree of the model's skeleton, the rest new joints at rest), and is kept with its
armature. Retargeting renames and numbers the bones as their joints with their settings and IDs, gives the rig a copy of every animation of
the model under its name and ID, carries the model's exit points and hulls onto the bones of their joints (the rig's own of the same place
win, what followed a joint no bone stands for goes to the nearest one that does) and deletes the model's animations. Nothing else of the
model changes: it keeps its place, name, file and link to TT Lab (the rig took them over and the model was kept hidden before). What
couldn't be carried over is told in the checks."""

import json
import typing

import bpy
from mathutils import Matrix, Vector

from . import flags
from . import pairing
from . import tlm_blender
from . import twintech_properties as properties

# On a model an earlier add-on kept hidden as what another was retargeted from (its TT Lab Retarget Sources collection): never a model of
# the scene's, the files it has stay
SOURCE_PROPERTY = "ttt_retarget_source"
# On a model made by retargeting: what couldn't be carried over to it, as JSON
NOTES_PROPERTY = "ttt_retarget_notes"


def is_source(root: typing.Optional[bpy.types.Object]) -> bool:
    return root is not None and bool(root.get(SOURCE_PROPERTY))


def notes_of(root: bpy.types.Object) -> typing.List[str]:
    try:
        notes = json.loads(root.get(NOTES_PROPERTY, "[]"))
    except ValueError:
        return []

    return [str(note) for note in notes] if isinstance(notes, list) else []


def skeleton_of(armature: bpy.types.Object) -> pairing.Skeleton:
    """The armature's bones where they rest in the world, with the joint indexes their Joint types give them."""
    indexes = {}
    for bone in armature.data.bones:
        container = properties.get(bone)
        if container is not None and container.type == "Joint":
            indexes[bone.name] = int(container.joint.index)

    bones = [(bone.name, bone.parent.name if bone.parent is not None else None, tuple(armature.matrix_world @ bone.head_local)) for bone in armature.data.bones]
    return pairing.Skeleton(bones, indexes)


def table_of(armature: bpy.types.Object) -> pairing.Pairs:
    """The rig's table: every bone of it and the joint it stands for (None for a new joint)."""
    rows = {row.bone: (row.joint or None) for row in armature.data.ttt_pairs}
    return {bone.name: rows.get(bone.name) for bone in armature.data.bones}


def set_table(armature: bpy.types.Object, pairs: pairing.Pairs) -> None:
    rows = armature.data.ttt_pairs
    rows.clear()
    for bone in armature.data.bones:
        row = rows.add()
        row.bone = bone.name
        row.joint = pairs.get(bone.name) or ""


def guess(source_armature: bpy.types.Object, rig: bpy.types.Object) -> pairing.Pairs:
    pairs = pairing.suggest(skeleton_of(source_armature), skeleton_of(rig))
    set_table(rig, pairs)
    return pairs


def shape_names(root: typing.Optional[bpy.types.Object]) -> typing.List[str]:
    shape = tlm_blender.shape_object_of(root) if root is not None else None
    keys = shape.data.shape_keys if shape is not None else None
    return [block.name for block in list(keys.key_blocks)[1:]] if keys is not None else []


def problems(source_root: bpy.types.Object, rig: bpy.types.Object) -> typing.List[str]:
    """What keeps the rig's table from being retargeted."""
    source_armature = tlm_blender.armature_of(source_root)
    if source_armature is None:
        return ["%s has no armature to retarget from" % source_root.name]

    if rig == source_armature:
        return ["The rig is the model's own armature"]

    if tlm_blender.find_root(rig) == source_root:
        return ["The rig is part of the model: an OGI has one skeleton"]

    return pairing.problems(skeleton_of(source_armature), skeleton_of(rig), table_of(rig))


def as_model(context: bpy.types.Context, rig: bpy.types.Object) -> bpy.types.Object:
    """The model the rig is part of, a new OGI of it and the meshes it deforms (with what's under them) when it's part of none."""
    root = tlm_blender.find_root(rig)
    if root is not None:
        return root

    collection = rig.users_collection[0] if rig.users_collection else context.scene.collection
    root = bpy.data.objects.new(rig.name + " Model", None)
    collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root[tlm_blender.ROOT_PROPERTY] = "Ogi"
    root[tlm_blender.KIND_PROPERTY] = "ogi"
    tlm_blender._read_data(root, "Ogi", {})
    taken = [rig] + [mesh for mesh in tlm_blender._deformed_by(rig) if mesh.parent is None or mesh.parent == rig]
    for blender_object in taken:
        if blender_object.parent is None:
            world = blender_object.matrix_world.copy()
            blender_object.parent = root
            blender_object.matrix_world = world

    return root


def retarget(context: bpy.types.Context, root: bpy.types.Object, rig: bpy.types.Object, shapes: typing.Optional[typing.Dict[str, typing.Optional[str]]] = None
             ) -> typing.Tuple[bpy.types.Object, int, typing.List[str]]:
    """Gives the rig the model's animations, which leave the model: see the module. Refuses a table that isn't a subtree of the model's
    skeleton, nothing changes then. Returns the model the rig is part of, how many animations it got and what couldn't be carried over."""
    found = problems(root, rig)
    if found:
        raise ValueError("; ".join(found))

    source_armature = tlm_blender.armature_of(root)
    source_skeleton = skeleton_of(source_armature)
    target_root = as_model(context, rig)
    shape_pairs = {source: target for target, source in (shapes or pairing.pair_shapes(shape_names(root), shape_names(target_root))).items() if source}
    notes: typing.List[str] = []
    if any(abs(value - rig.matrix_world.to_scale()[0]) > 1e-4 for value in rig.matrix_world.to_scale()):
        notes.append("%s is scaled unevenly (%s): its animations' moves were scaled by its mean scale" % (rig.name, tuple(round(value, 4) for value in rig.matrix_world.to_scale())))

    # Named as the joints first: renaming a bone only renames the curves of the action its armature plays
    _assign_joints(source_armature, rig, table_of(rig), notes)
    pairs = table_of(rig)
    bones = {joint: bone for bone, joint in pairs.items() if joint is not None}
    count = tlm_blender.retarget_animations(root, target_root, bones=bones, shapes=shape_pairs)
    _carry_attachments(root, source_armature, target_root, rig, source_skeleton, skeleton_of(rig), pairs, notes)
    _move_animations(root, source_armature, rig)
    target_root[NOTES_PROPERTY] = json.dumps(notes)
    first = next(iter(tlm_blender.animations_of(target_root)), None)
    if first is not None:
        context.scene.frame_start = 0
        context.scene.frame_end = int(first.frame_end)

    return target_root, count, notes


def _move_animations(root: bpy.types.Object, source_armature: bpy.types.Object, rig: bpy.types.Object) -> None:
    """The model's animations are the rig's now: deleted from the model with its bones' settings of them, their copies take their names
    and are the rig's own from now on. One the rig got no copy of stays."""
    owner = rig.get(tlm_blender.UID_PROPERTY)
    copies = {action.get(tlm_blender.RETARGETED_PROPERTY): action for action in bpy.data.actions
              if action.get(tlm_blender.OWNER_PROPERTY) == owner and action.get(tlm_blender.RETARGETED_PROPERTY)}
    for action in tlm_blender.animations_of(root):
        copy = copies.get(action.get(tlm_blender.UID_PROPERTY))
        if copy is None:
            continue

        name = action.name
        bpy.data.actions.remove(action)
        copy.name = name
        del copy[tlm_blender.RETARGETED_PROPERTY]

    for bone in source_armature.data.bones:
        entries = getattr(bone, flags.FLAGS_PROPERTY)
        for index in reversed(range(len(entries))):
            if entries[index].action is None:
                entries.remove(index)


def _assign_joints(source_armature: bpy.types.Object, rig: bpy.types.Object, pairs: pairing.Pairs, notes: typing.List[str]) -> typing.Dict[str, str]:
    """The bones get their joints' names, indexes and settings (react ID, additional rotation, detail), new joints the next indexes as
    Joint N, and the table follows them. The joint IDs of joints no bone stands for are lost. Returns the bones' new names."""
    joint_index = {bone.name: int(properties.get(bone).joint.index) if properties.get(bone).type == "Joint" else position
                   for position, bone in enumerate(source_armature.data.bones)}
    next_index = max(joint_index.values(), default=-1) + 1
    held = {name: rig.data.bones[name] for name in pairs}
    # Out of the way of the joints' names first, by short names: Blender keeps 63 bytes of a bone's name
    for number, bone in enumerate(held.values()):
        bone.name = "ttt_renaming %d" % number

    renamed: typing.Dict[str, str] = {}
    for name, joint in pairs.items():
        bone = held[name]
        data: typing.Dict[str, typing.Any] = {}
        if joint is not None:
            bone.name = joint
            data = {key: value for key, value in tlm_blender._write_data(source_armature.data.bones[joint]).items() if key in ("Id", "AdditionalAnimationRotation", "Detail")}
            data["Index"] = joint_index[joint]
        else:
            bone.name = "Joint %d" % next_index
            data = {"Index": next_index, "Id": 255}
            next_index += 1

        renamed[name] = bone.name
        tlm_blender._read_data(bone, "Joint", data)
        # The bind the bone was imported with was another joint's, its own axes (AIM_PROPERTY) stay what its rest and keys are in
        for key in (tlm_blender.BIND_PROPERTY, tlm_blender.REST_PROPERTY):
            if key in bone:
                del bone[key]

        rig.pose.bones[bone.name].rotation_mode = "QUATERNION"

    # The table follows the bones' new names
    for row in rig.data.ttt_pairs:
        row.bone = renamed.get(row.bone, row.bone)

    stood = set(joint for joint in pairs.values() if joint is not None)
    lost = []
    for bone in source_armature.data.bones:
        identifier = int(tlm_blender._write_data(bone).get("Id", 255))
        if bone.name not in stood and identifier != 255:
            lost.append("%d (%s)" % (identifier, bone.name))

    if lost:
        notes.append("The joint IDs %s are lost with their joints, which no bone stands for: the model's game objects and scripts find joints by them"
                     % ", ".join(lost))

    return renamed


def _carry_attachments(source_root: bpy.types.Object, source_armature: bpy.types.Object, target_root: bpy.types.Object, rig: bpy.types.Object,
                       source_skeleton: pairing.Skeleton, rig_skeleton: pairing.Skeleton, pairs: pairing.Pairs, notes: typing.List[str]) -> None:
    """The model's exit points and hulls follow the bones of their joints on the rig, as far from them as from their joints scaled by how
    much bigger the rig is. The rig's own of the same place win: exit points by their IDs, hulls by their order."""
    bones = {joint: bone for bone, joint in pairs.items() if joint is not None}
    scale = rig_skeleton.size() / source_skeleton.size()
    descendants = tlm_blender._descendants(target_root)
    own_exit_points = {int(tlm_blender._write_data(child).get("Id", -1)) for child in descendants if tlm_blender.role_of(child) == "exit_point"}
    own_hulls = len([child for child in descendants if tlm_blender.role_of(child) == "hull"])
    moved_away = []
    hull_place = 0
    for blender_object in tlm_blender.in_file_order(child for child in tlm_blender._descendants(source_root) if tlm_blender.role_of(child) in ("exit_point", "hull")):
        kind = tlm_blender.role_of(blender_object)
        if kind == "exit_point" and int(tlm_blender._write_data(blender_object).get("Id", -1)) in own_exit_points:
            continue

        if kind == "hull":
            hull_place += 1
            if hull_place <= own_hulls:
                continue

        attachment = tlm_blender._rest_attachment(blender_object, source_armature, set(bone.name for bone in source_armature.data.bones))
        kept = None
        if attachment is None:
            # On no bone: where it is in the model, on the joint it keeps
            joint, rest = None, blender_object.matrix_world.copy()
            kept = _kept_joint(blender_object, source_armature, source_skeleton, pairs, rig)
        else:
            joint, rest = attachment[1].name, attachment[2]

        target_bone = pairing.nearest_paired(source_skeleton, joint, pairs) if joint is not None else None
        if joint is not None and target_bone is None:
            notes.append("%s follows %s, and no bone stands for it or a joint above it: it's left out" % (blender_object.name, joint))
            continue

        if joint is not None and bones.get(joint) != target_bone:
            moved_away.append("%s (from %s)" % (blender_object.name, joint))

        copy = _copy_attachment(blender_object, kind, source_root, target_root, rig, joint, rest, target_bone, source_armature, scale)
        if kept is not None:
            copy[tlm_blender.JOINT_PROPERTY] = kept
        if kind == "hull":
            # After the rig's own, in the model's order
            copy[tlm_blender.ORDER_PROPERTY] = own_hulls + hull_place - 1

    if moved_away:
        notes.append("%s followed joints no bone stands for: they follow the bones of the nearest joints above them" % ", ".join(moved_away))


def _holder(root: bpy.types.Object, kind: str) -> bpy.types.Object:
    holder_kind = "exit_points" if kind == "exit_point" else "collision_hulls"
    holder = next((child for child in tlm_blender._descendants(root) if child.get(tlm_blender.KIND_PROPERTY) == holder_kind), None)
    if holder is not None:
        return holder

    collection = root.users_collection[0] if root.users_collection else bpy.context.scene.collection
    holder = tlm_blender._new_object("Exit Points" if kind == "exit_point" else "Collision Hulls", None, root, collection)
    holder[tlm_blender.KIND_PROPERTY] = holder_kind
    holder.empty_display_size = 0.1
    return holder


def _kept_joint(blender_object: bpy.types.Object, source_armature: bpy.types.Object, source_skeleton: pairing.Skeleton, pairs: pairing.Pairs,
                rig: bpy.types.Object) -> typing.Optional[int]:
    """The joint an object following no bone keeps (JOINT_PROPERTY) on the rig: the one of the bone standing for it, or for the nearest
    joint above it some bone stands for. One of no joint of the model's (a hull's 255) as it is."""
    stored = blender_object.get(tlm_blender.JOINT_PROPERTY)
    if stored is None:
        return None

    joint = next((bone.name for position, bone in enumerate(source_armature.data.bones)
                  if (int(properties.get(bone).joint.index) if properties.get(bone).type == "Joint" else position) == int(stored)), None)
    standing = pairing.nearest_paired(source_skeleton, joint, pairs) if joint is not None else None
    if standing is None or standing not in rig.data.bones or properties.get(rig.data.bones[standing]).type != "Joint":
        return int(stored)

    return int(properties.get(rig.data.bones[standing]).joint.index)


def _copy_attachment(blender_object: bpy.types.Object, kind: str, source_root: bpy.types.Object, target_root: bpy.types.Object, rig: bpy.types.Object,
                     joint: typing.Optional[str], rest: Matrix, target_bone: typing.Optional[str], source_armature: bpy.types.Object, scale: float) -> bpy.types.Object:
    """A copy of the exit point or hull under the rig's model following the bone, where the original is from its joint's rest: its offset
    scaled and turned as the rig's bone points from the way the joint's bone points (the bones' own axes, the roll about the way they
    point, differ between rigs, their ways don't). A hull turns with it, it wraps the limb. An exit point keeps its axes: the rig's bone
    turns in the world as much as the joint does, so what it holds turns the way it did on every frame, and turned with the bone it
    held it askew. What follows no bone is where it was in its model."""
    copy = blender_object.copy()
    if copy.data is not None:
        copy.data = copy.data.copy()

    for collection in target_root.users_collection:
        collection.objects.link(copy)

    copy.constraints.clear()
    for key in (tlm_blender.JOINT_PROPERTY, tlm_blender.MATRIX_PROPERTY, tlm_blender.ORDER_PROPERTY, tlm_blender.HULL_PROPERTY):
        if key in copy:
            del copy[key]

    if joint is not None and target_bone is not None:
        joint_rest = source_armature.matrix_world @ source_armature.data.bones[joint].matrix_local
        bone_rest = rig.matrix_world @ rig.data.bones[target_bone].matrix_local
        turn = _way(joint_rest).rotation_difference(_way(bone_rest)).to_matrix().to_4x4()
        own_axes = Matrix.Translation(-rest.translation) @ rest
        world = Matrix.Translation(bone_rest.translation + turn.to_3x3() @ ((rest.translation - joint_rest.translation) * scale)) @ \
            (own_axes if kind == "exit_point" else turn @ own_axes)
    else:
        world = target_root.matrix_world @ source_root.matrix_world.inverted() @ blender_object.matrix_world

    holder = _holder(target_root, kind)
    bpy.context.view_layer.update()
    copy.parent = holder
    copy.matrix_parent_inverse = Matrix.Identity(4)
    if target_bone is not None:
        # Like an import's: its own transform is where it is from the bone
        tlm_blender.follow_bone(copy, target_root, rig, target_bone)
        constraint = copy.constraints[-1]
        followed = rig.matrix_world @ rig.data.bones[target_bone].matrix_local @ constraint.inverse_matrix
        copy.matrix_basis = (followed @ tlm_blender.child_of_input(constraint, holder.matrix_world)).inverted() @ world
    else:
        copy.matrix_basis = holder.matrix_world.inverted() @ world

    return copy


def _way(rest: Matrix) -> Vector:
    """The way a bone at its rest points, in the world"""
    return (rest.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()

