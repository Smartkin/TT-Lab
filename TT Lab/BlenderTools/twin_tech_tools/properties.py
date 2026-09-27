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

from bpy.types import PropertyGroup


def _flags_changed(self, context):
    from .animator import apply_to_all_armatures

    apply_to_all_armatures(context.scene)


class TTT_AnimationFlags(PropertyGroup):
    action: bpy.props.PointerProperty(type=bpy.types.Action, name="Animation")
    independent_scaling: bpy.props.BoolProperty(
        name="Independent scaling",
        description="The bone doesn't inherit its parent's scale while this animation plays",
        default=False,
        update=_flags_changed,
    )
    uses_additional_rotation: bpy.props.BoolProperty(
        name="Additional rotation",
        description="The joint's additional rotation from the model is applied on top of this animation's rotation",
        default=False,
    )
    # What the imported file had, resetting the flags goes back to it
    imported: bpy.props.BoolProperty(options={"HIDDEN"})
    imported_independent_scaling: bpy.props.BoolProperty(options={"HIDDEN"})
    imported_uses_additional_rotation: bpy.props.BoolProperty(options={"HIDDEN"})
