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
from .properties import TTT_AnimationFlags


def register_props_for_blender_types():
    setattr(bpy.types.Bone, flags.FLAGS_PROPERTY, bpy.props.CollectionProperty(type=TTT_AnimationFlags))


def unregister_props_for_blender_types():
    if hasattr(bpy.types.Bone, flags.FLAGS_PROPERTY):
        delattr(bpy.types.Bone, flags.FLAGS_PROPERTY)
