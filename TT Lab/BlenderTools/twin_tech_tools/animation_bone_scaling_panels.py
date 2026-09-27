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

import bpy

from . import flags


def _active_armature(context):
    armature_object = context.object
    if armature_object is None or armature_object.type != "ARMATURE":
        return None

    return armature_object


def _active_action(animated_object):
    animation_data = animated_object.animation_data if animated_object is not None else None
    return animation_data.action if animation_data is not None else None


def _draw_flags(layout, joint, action, add_operator, read_operator):
    box = layout.box()
    if action is None:
        box.label(text="No animation is playing", icon="INFO")
    else:
        box.label(text=f"Playing: {action.name}", icon="ACTION")
        entry = flags.find_flags(joint, action.name)
        if entry is None:
            box.operator(add_operator, icon="ADD")
        else:
            box.prop(entry, "independent_scaling", text="Doesn't inherit parent's scale")
            box.prop(entry, "uses_additional_rotation", text="Uses additional rotation")

    entries = [entry for entry in getattr(joint, flags.FLAGS_PROPERTY) if entry.action is not None]
    if len(entries) > 0:
        column = layout.column(heading="Doesn't inherit parent's scale in")
        for entry in entries:
            column.prop(entry, "independent_scaling", text=entry.action.name)

    layout.operator(read_operator, icon="FILE_REFRESH")


class TTT_OT_AddAnimationFlags(bpy.types.Operator):
    """Adds settings for the animation that is currently playing to the active bone"""

    bl_idname = "ttt.add_animation_flags"
    bl_label = "Add Current Animation"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return context.bone is not None and _active_action(_active_armature(context)) is not None

    def execute(self, context):
        flags.get_or_add_flags(context.bone, _active_action(_active_armature(context)))
        return {"FINISHED"}


class TTT_OT_ReadAnimationFlags(bpy.types.Operator):
    """Puts the per animation settings of every bone back the way the imported file had them"""

    bl_idname = "ttt.read_animation_flags"
    bl_label = "Reset To Imported"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return _active_armature(context) is not None

    def execute(self, context):
        for bone in _active_armature(context).data.bones:
            flags.reset_to_imported(bone)

        return {"FINISHED"}


class TTT_PT_AnimationBoneScalingPanel(bpy.types.Panel):
    bl_label = "Twin Tech Bone Settings Per Animation"
    bl_idname = "TTT_PT_AnimationBoneScalingPanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "bone"

    @classmethod
    def poll(cls, context):
        return context.bone is not None and _active_armature(context) is not None

    def draw(self, context):
        _draw_flags(self.layout, context.bone, _active_action(_active_armature(context)), TTT_OT_AddAnimationFlags.bl_idname,
                    TTT_OT_ReadAnimationFlags.bl_idname)
