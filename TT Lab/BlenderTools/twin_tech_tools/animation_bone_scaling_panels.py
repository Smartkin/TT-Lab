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
import typing
from .properties import NodeHasIndependentScaling

class TTT_Panel:
    bl_label = "Twin Tech Bone Scaling Per Animation"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_category = "Twin Tech Tools"

    def draw_per_animation_layout(self, layout: bpy.types.UILayout, object: typing.Any):
        for item in object.NodeHasIndependentScaling.links:
            row = layout.row()
            row.prop(item, "independentScaling", text="Scales independently in " + item.animationLink.name)


class TTT_PT_AnimationBoneScalingForBonePanel(TTT_Panel, bpy.types.Panel):
    bl_idname = "TTT_PT_AnimationBoneScalingForBonePanel"
    bl_context = "bone"

    @classmethod
    def poll(cls, context):
        return (context.bone is not None)

    def draw(self, context):
        layout = self.layout

        super().draw_per_animation_layout(layout, context.bone)


class TTT_PT_AnimationBoneScalingForMeshPanel(TTT_Panel, bpy.types.Panel):
    bl_idname = "TTT_PT_AnimationBoneScalingForMeshPanel"
    bl_context = "object"

    @classmethod
    def poll(cls, context):
        return (context.object is not None and context.object.type == "MESH")

    def draw(self, context):
        layout = self.layout

        super().draw_per_animation_layout(layout, context.object)

class TTT_PT_AnimationBoneScalingForNodePanel(TTT_Panel, bpy.types.Panel):
    bl_idname = "TTT_PT_AnimationBoneScalingForNodePanel"
    bl_context = "object"

    @classmethod
    def poll(cls, context):
        return (context.object is not None and context.object.type == "EMPTY") # Empty is a node

    def draw(self, context):
        layout = self.layout

        super().draw_per_animation_layout(layout, context.object)