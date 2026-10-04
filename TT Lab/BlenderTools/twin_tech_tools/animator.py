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
from bpy.app.handlers import persistent

from . import flags


# The exporter writes the bones' keyframes the way they'd look with full inheritance, which is how TT Lab stores them. The
# preview switching bones to not inherit scale would get baked into them, so it's paused for as long as an export runs
_suspended = False


def suspend():
    global _suspended
    _suspended = True


def resume():
    global _suspended
    _suspended = False


def is_model_armature(blender_object):
    """Whether the armature is a Twin Tech model's: the per-animation inherit scale previews only touch those, other rigs in the scene keep
    their bones' settings."""
    from . import tlm_blender

    return blender_object.type == "ARMATURE" and (blender_object.get(tlm_blender.KIND_PROPERTY) == "armature" or tlm_blender.find_root(blender_object) is not None)


def reset_inherit_scale(scene):
    for blender_object in scene.objects:
        if not is_model_armature(blender_object):
            continue

        for bone in blender_object.data.bones:
            if bone.inherit_scale != "FULL":
                bone.inherit_scale = "FULL"


def apply_to_all_armatures(scene):
    if scene is None or _suspended:
        return

    from . import tlm_blender

    for blender_object in scene.objects:
        if is_model_armature(blender_object):
            flags.apply_inherit_scale(blender_object)
            # The shape plays the facial part of the animation the armature plays
            if blender_object.get(tlm_blender.KIND_PROPERTY) == "armature":
                tlm_blender.sync_shape_keys(blender_object)


def subscribe_to_blender():
    bpy.app.handlers.frame_change_pre.append(frame_change_pre)
    bpy.app.handlers.depsgraph_update_post.append(depsgraph_update_post)


def unsubscribe_from_blender():
    for handlers, handler in ((bpy.app.handlers.frame_change_pre, frame_change_pre), (bpy.app.handlers.depsgraph_update_post, depsgraph_update_post)):
        if handler in handlers:
            handlers.remove(handler)


@persistent
def frame_change_pre(scene, depsgraph=None):
    apply_to_all_armatures(scene)


# Switching the active animation doesn't change the frame so the bones get updated whenever the scene changes too
@persistent
def depsgraph_update_post(scene, depsgraph):
    apply_to_all_armatures(scene)
