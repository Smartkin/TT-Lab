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

"""Per animation settings of the joints of TT Lab's models.

Twinsanity decides for every animation whether a joint inherits its parent's scale and whether it uses its additional rotation.
The add-on keeps them on every bone for every action, and applies them to the viewport.
"""

import typing

FLAGS_PROPERTY = "ttt_animation_flags"


def find_flags(joint: typing.Any, action_name: str) -> typing.Any:
    for entry in getattr(joint, FLAGS_PROPERTY, []):
        if entry.action is not None and entry.action.name == action_name:
            return entry

    return None


def get_or_add_flags(joint: typing.Any, action: typing.Any) -> typing.Any:
    entry = find_flags(joint, action.name)
    if entry is None:
        entry = getattr(joint, FLAGS_PROPERTY).add()
        entry.action = action

    return entry


def reset_to_imported(joint: typing.Any) -> None:
    """Puts the flags back the way the imported file had them, the ones added in Blender go away."""
    collection = getattr(joint, FLAGS_PROPERTY)
    for index in reversed(range(len(collection))):
        entry = collection[index]
        if not entry.imported:
            collection.remove(index)
            continue

        entry.independent_scaling = entry.imported_independent_scaling
        entry.uses_additional_rotation = entry.imported_uses_additional_rotation


def desired_inherit_scale(bone: typing.Any, action: typing.Any) -> str:
    entry = find_flags(bone, action.name) if action is not None else None
    return "NONE" if entry is not None and entry.independent_scaling else "FULL"


def apply_inherit_scale(armature_object: typing.Any) -> None:
    """Makes the bones inherit their parent's scale the way the currently playing animation wants them to."""
    animation_data = armature_object.animation_data
    action = animation_data.action if animation_data is not None else None
    for bone in armature_object.data.bones:
        inherit_scale = desired_inherit_scale(bone, action)
        # Changing the property triggers another depsgraph update, only doing it when needed stops that from looping
        if bone.inherit_scale != inherit_scale:
            bone.inherit_scale = inherit_scale
