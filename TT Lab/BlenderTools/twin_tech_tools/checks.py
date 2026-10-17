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

"""What keeps a model from being written and what it's written without, worked out before anything is written or changed: the export
refuses a model with anything refused and the Twin Tech Checks panel lists all of it. What breaks the game or loses something of the
model is refused, what the game takes but changes is a warning. TT Lab adds what the objects using the model need once it's linked."""

import typing

import bpy

from . import hulls
from . import parts
from . import skeleton
from . import tlm_blender

Problem = parts.Problem

# The most objects a problem names
_NAMED = 5


def _named(names: typing.Sequence[str]) -> str:
    shown = ", ".join(names[:_NAMED])
    return shown + (" and %d more" % (len(names) - _NAMED) if len(names) > _NAMED else "")


def problems(root: bpy.types.Object, path: typing.Optional[str] = None) -> typing.List[Problem]:
    """Everything wrong with the model written to the path, its own file by default."""
    kind = root.get(tlm_blender.KIND_PROPERTY, "")
    if not root.get(tlm_blender.ROOT_PROPERTY) or kind not in tlm_blender.KIND_TYPES:
        return [Problem("%s lost what makes it a TT Lab model's root (its Twin Tech properties): import the model again or start from Add > Twin Tech"
                        % root.name, [root.name])]

    descendants = tlm_blender._descendants(root)
    infos = [tlm_blender.object_info(blender_object, root) for blender_object in descendants]
    result: typing.List[Problem] = []
    if kind == "ogi":
        result.extend(_ogi(root, descendants, infos))
    elif kind in tlm_blender.TREE_KINDS:
        result.extend(parts.other_objects(infos))
    elif kind == "save_icon":
        result.extend(parts.other_objects(infos))
        if not any(tlm_blender._draws(child) for child in descendants):
            result.append(Problem("%s has no mesh with faces: the icon is drawn from one under its root" % root.name, [root.name]))
    elif kind != "collision":
        result.extend(parts.other_objects(infos))
        if not any(child.type == "MESH" for child in descendants):
            result.append(Problem("%s has no mesh under its root to write" % root.name, [root.name]))

    result.extend(_names(descendants))
    result.extend(_hulls(descendants))
    lit = tlm_blender.lit_without_emit_colors(root, path if path is not None else root.get(tlm_blender.PATH_PROPERTY, ""))
    if lit:
        result.append(Problem(tlm_blender._lit_without_emit_colors_problem(lit), [name for name, _, _ in lit], fix="ttt.add_emit_colors"))

    result.extend(_warnings(root, descendants))
    return result


def refused(root: bpy.types.Object, path: typing.Optional[str] = None) -> typing.List[Problem]:
    return [problem for problem in problems(root, path) if problem.refused]


def _ogi(root: bpy.types.Object, descendants: typing.List[bpy.types.Object], infos: typing.List[parts.ObjectInfo]) -> typing.List[Problem]:
    _, result = parts.ogi_roles(infos)
    armature = next((child for child in descendants if child.type == "ARMATURE"), None)
    if armature is None:
        result.append(Problem("%s has no armature: an OGI needs one under its root, its root bone is joint 0" % root.name, [root.name]))
        return result

    if len(armature.data.bones) == 0:
        result.append(Problem("%s has no bones: an OGI's skeleton needs its root bone, joint 0" % armature.name, [armature.name]))
        return result

    roles = {child.name: tlm_blender.role_of(child) for child in descendants}
    skinned = any(roles[child.name] in tlm_blender.SKINNED_KINDS and tlm_blender._draws(child) for child in descendants)
    try:
        skeleton.check_skeleton(tlm_blender.bone_infos(armature), skinned)
    except ValueError as error:
        result.append(Problem(str(error)[0].upper() + str(error)[1:], [armature.name]))

    # What follows a bone the armature no longer has would be left at the model's root
    bones = {bone.name for bone in armature.data.bones}
    by_name = {info.name: info for info in infos}
    lost = []
    for blender_object, info in zip(descendants, infos):
        if roles[blender_object.name] not in ("body", "exit_point", "hull"):
            continue

        bone = parts.joint_bone(info, armature.name, by_name)
        if bone and bone not in bones:
            lost.append("%s (on %s)" % (blender_object.name, bone))

    if lost:
        result.append(Problem("%s follow bones %s doesn't have: they'd be left where the model's root is. Put them on bones of the armature"
                              % (_named(lost), armature.name), [name.split(" (on ")[0] for name in lost]))

    shapes = {child.name: [block.name for block in list(child.data.shape_keys.key_blocks)[1:]]
              for child in descendants if roles[child.name] == "shape" and tlm_blender._draws(child) and child.data.shape_keys is not None}
    result.extend(parts.shape_key_problems(shapes, tlm_blender.MOST_SHAPES))
    result.extend(_shared_materials(descendants, roles))
    return result


def _shared_materials(descendants: typing.List[bpy.types.Object], roles: typing.Dict[str, str]) -> typing.List[Problem]:
    """A material made in Blender drawing the skin and rigid parts: the game draws skins with the skinned shader alone and rigid parts with
    others, one material can't be both. The project's materials are what the project has (TT Lab warns about a skin's of another shader)."""
    kinds: typing.Dict[str, typing.Set[str]] = {}
    for blender_object in descendants:
        role = roles[blender_object.name]
        if blender_object.type != "MESH" or role not in ("skin", "shape", "body") or not tlm_blender._draws(blender_object):
            continue

        for material in tlm_blender.drawn_materials(blender_object):
            if not material.get(tlm_blender.URI_PROPERTY):
                kinds.setdefault(material.name, set()).add("skin" if role in tlm_blender.SKINNED_KINDS else "rigid")

    return [Problem("%s draws the skin and rigid parts: the game draws skins only with the skinned shader and rigid parts with others. Split it "
                    "into a skin copy and a rigid copy" % name, [name], fix="ttt.split_material")
            for name, used in sorted(kinds.items()) if len(used) > 1]


def _names(descendants: typing.List[bpy.types.Object]) -> typing.List[Problem]:
    """Materials and pictures become assets of their names, which the game's files keep a byte per character."""
    materials: typing.List[bpy.types.Material] = []
    for blender_object in descendants:
        if blender_object.type == "MESH":
            materials.extend(material for material in tlm_blender.drawn_materials(blender_object) if material not in materials)

    names = [material.name for material in materials if not material.name.isascii()]
    images = sorted({image.name for material in materials for image in _pictures(material) if not image.name.isascii()})
    result = []
    if names:
        result.append(Problem("The materials %s have names past plain ASCII, which the game's files can't hold: rename them" % _named(names), names))

    if images:
        result.append(Problem("The pictures %s have names past plain ASCII, which the game's files can't hold: rename them" % _named(images), images))

    return result


def _pictures(material: bpy.types.Material) -> typing.List[bpy.types.Image]:
    from . import material_settings

    if material_settings.is_managed(material):
        return [shader.image for shader in material_settings.settings_of(material).shaders if shader.image is not None]

    if material.node_tree is None:
        return []

    return [node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image is not None]


def _hulls(descendants: typing.List[bpy.types.Object]) -> typing.List[Problem]:
    result = []
    for blender_object in descendants:
        if blender_object.type != "MESH" or tlm_blender.role_of(blender_object) != "hull":
            continue

        mesh = blender_object.data
        try:
            hulls.check_hull(blender_object.name, len(mesh.vertices), len(mesh.polygons))
        except ValueError as error:
            result.append(Problem(str(error), [blender_object.name]))

    return result


def _warnings(root: bpy.types.Object, descendants: typing.List[bpy.types.Object]) -> typing.List[Problem]:
    """What the model is written without, or changed in: the export says it again."""
    result = [Problem(text, refused=False) for text in tlm_blender._unapplied_modifiers(descendants)]
    big = [blender_object.name for blender_object in descendants if blender_object.type == "MESH" and tlm_blender.is_too_big(blender_object.data)]
    if big:
        result.append(Problem("%s %s more than %d vertexes, past the game's biggest skin: %s written decimated (but meshes with shape keys)"
                              % (_named(big), "has" if len(big) == 1 else "have", tlm_blender.MAX_EXPORT_VERTICES, "it's" if len(big) == 1 else "they're"),
                              big, refused=False))

    # What TT Lab found taking the model the last time, about the game objects drawing it. TT Lab refuses those itself, the panel says why
    from . import link_sync

    for problem in link_sync.tt_lab_problems(root):
        text = ("TT Lab didn't take the model: %s" if problem.get("refused") else "TT Lab: %s") % problem["text"]
        result.append(Problem(text, [root.name], refused=False))

    if root.get(tlm_blender.KIND_PROPERTY) == "ogi":
        from . import replace

        # What retargeting couldn't carry over to the rig
        result.extend(Problem(note, [root.name], refused=False) for note in replace.notes_of(root))
        armature = next((child for child in descendants if child.type == "ARMATURE"), None)
        skinned = any(tlm_blender.role_of(child) in tlm_blender.SKINNED_KINDS and tlm_blender._draws(child) for child in descendants)
        exit_points = any(tlm_blender.role_of(child) == "exit_point" for child in descendants)
        if skinned and armature is not None and len(armature.data.bones) < 2 and not exit_points:
            result.append(Problem("%s has one joint and no exit points: the game draws such a model's rigid bodies only, never its skin. Add a bone "
                                  "under the root and weight the skin to it" % root.name, [armature.name], refused=False))

    return result


def subjects(problem: Problem) -> typing.List[bpy.types.Object]:
    """The objects of the scene a problem is about."""
    return [bpy.data.objects[name] for name in problem.subjects if name in bpy.data.objects]


# What the Twin Tech Checks panel shows, worked out again once the scene changed
_cache: typing.Dict[str, typing.Tuple[int, typing.List[Problem]]] = {}
_changes = [0]


# Kept when a file is loaded: Blender drops other handlers then, and opening a blend file left the panel and new materials unlooked at
@bpy.app.handlers.persistent
def note_change(*_: typing.Any) -> None:
    _changes[0] += 1


def cached(root: bpy.types.Object) -> typing.List[Problem]:
    stamp, found = _cache.get(root.name, (-1, []))
    if stamp != _changes[0]:
        found = problems(root)
        _cache[root.name] = (_changes[0], found)

    return found


def register():
    if note_change not in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.append(note_change)


def unregister():
    if note_change in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.remove(note_change)

    _cache.clear()
