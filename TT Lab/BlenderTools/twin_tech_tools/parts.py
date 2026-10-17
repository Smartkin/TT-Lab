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

"""What every object under a model's root is, without Blender.

Everything under the root is part of the model. Its Twin Tech type says what it is, or the kind it was imported as, or the holder it's
in. Otherwise the ordinary rigging does, the way Blender sets it up: in an OGI a mesh the model's armature deforms (Ctrl+P > With
Automatic Weights) is skin, the blend skin when it has shape keys, a mesh on a bone (Ctrl+P > Bone, or following one by Child Of, or
parented to something that does) a rigid body of that bone's joint and a mesh the armature neither deforms nor carries a rigid body of
joint 0. Exit points and hulls have nothing of Blender's to tell them by, they're typed objects or in their holders. Empties are
folders. Objects of other kinds (curves, text, Blender's lights) are refused, they'd be left out: they go outside the root or get
converted to meshes. So is a part following a bone by its Child Of and another by what it's parented to (an imported one parented to a
bone with Ctrl+P > Bone): Blender moves it with both, the game with one joint; and one following vertexes (Ctrl+P > Vertex).
"""

import typing

# Roles of the objects of an OGI
ARMATURE = "armature"
SKIN = "skin"
SHAPE = "shape"
BODY = "body"
EXIT_POINT = "exit_point"
HULL = "hull"
# An empty grouping what's under it, or a holder
FOLDER = "folder"

# What the add-on's types and the kinds of imported objects are
_TYPE_ROLES = {"Skin": SKIN, "BlendSkin": SHAPE, "Body": BODY, "ExitPoint": EXIT_POINT, "CollisionHull": HULL}
_KIND_ROLES = {"skin": SKIN, "shape": SHAPE, "body": BODY, "exit_point": EXIT_POINT, "hull": HULL, "armature": ARMATURE}
# The holders an import makes, and what's put in them
HOLDERS = {"rigid_bodies": BODY, "exit_points": EXIT_POINT, "collision_hulls": HULL}
# Blender's kinds of objects a model can have
MODEL_OBJECT_TYPES = ("MESH", "EMPTY", "ARMATURE")


class ObjectInfo:
    """An object under a model's root as far as telling what it is goes."""

    def __init__(self, name: str, object_type: str, parent: typing.Optional[str] = None, twin_type: str = "", kind: str = "",
                 parent_type: str = "OBJECT", parent_bone: str = "", deformed_by: typing.Sequence[str] = (),
                 follows: typing.Optional[typing.Tuple[str, str]] = None, shape_keys: int = 0):
        self.name = name
        self.object_type = object_type
        # The object it's under, None for the root's own
        self.parent = parent
        self.twin_type = twin_type
        self.kind = kind
        self.parent_type = parent_type
        self.parent_bone = parent_bone
        # The armatures its Armature modifiers deform it by
        self.deformed_by = list(deformed_by)
        # The armature and bone a Child Of constraint makes it follow
        self.follows = follows
        # Shape keys past the basis
        self.shape_keys = shape_keys


class Problem:
    """Something that keeps the model from being written, or that it's written without: refused, or a warning."""

    def __init__(self, text: str, subjects: typing.Sequence[str] = (), refused: bool = True, fix: str = ""):
        self.text = text
        # The objects it's about
        self.subjects = list(subjects)
        self.refused = refused
        # The operator that fixes it, when there's one
        self.fix = fix

    def __repr__(self) -> str:
        return "%s(%r)" % ("Refused" if self.refused else "Warning", self.text)


def ogi_roles(objects: typing.Sequence[ObjectInfo]) -> typing.Tuple[typing.Dict[str, str], typing.List[Problem]]:
    """The role of every object of an OGI and what's wrong with them. The model's armature is the first one, its rigging is what counts."""
    by_name = {info.name: info for info in objects}
    armatures = [info.name for info in objects if info.object_type == "ARMATURE"]
    armature = armatures[0] if armatures else None
    roles: typing.Dict[str, str] = {}
    problems: typing.List[Problem] = []
    if len(armatures) > 1:
        problems.append(Problem("The model has %d armatures (%s): an OGI has one skeleton, delete the others or move them out of the model's root"
                                % (len(armatures), ", ".join(armatures)), armatures[1:]))

    for info in objects:
        role = _given_role(info, by_name)
        if info.object_type == "ARMATURE":
            roles[info.name] = ARMATURE
            continue

        if info.object_type not in MODEL_OBJECT_TYPES:
            problems.append(Problem("%s is a %s, which a model can't have: convert it to a mesh (Object > Convert > Mesh) or move it out of the "
                                    "model's root" % (info.name, _type_label(info.object_type)), [info.name], fix="ttt.convert_to_mesh"))
            continue

        if info.object_type == "EMPTY":
            roles[info.name] = EXIT_POINT if role == EXIT_POINT else FOLDER
            continue

        if role is not None and role != EXIT_POINT:
            roles[info.name] = role
        else:
            roles[info.name] = _rigged_role(info, armature)

        other = [name for name in info.deformed_by if name != armature]
        if other:
            problems.append(Problem("%s is deformed by %s, which isn't the model's armature %s: an OGI's meshes follow its own skeleton"
                                    % (info.name, ", ".join(other), armature or "(none)"), [info.name]))

        if info.follows is not None and info.follows[0] != armature:
            problems.append(Problem("%s follows %s of %s, which isn't the model's armature: it would be left where it is"
                                    % (info.name, info.follows[1], info.follows[0]), [info.name]))

        if roles[info.name] == SKIN and info.shape_keys > 0 and role == SKIN:
            problems.append(Problem("%s is the skin and has shape keys, which only the blend skin's meshes can have: make it the blend skin"
                                    % info.name, [info.name], fix="ttt.make_blend_skin"))

    for info in objects:
        if roles.get(info.name) not in (BODY, EXIT_POINT, HULL):
            continue

        vertexes = _vertex_parent(info, by_name, armature)
        if vertexes is not None:
            problems.append(Problem("%s follows vertexes of %s (Ctrl+P > Vertex): the game moves a part with one joint, not vertexes. Parent it to %s "
                                    "with Ctrl+P > Object, or to a bone" % (info.name, vertexes, vertexes), [info.name]))
            continue

        under = _bone_under(info, by_name, armature) if info.follows is not None and info.follows[0] == armature else None
        if under is not None:
            problems.append(Problem("%s follows %s by a Child Of and %s by what it's parented to: Blender moves it with both, the game with one "
                                    "joint. Remove its Child Of to keep it on %s, or put it back under the model where it is to keep it on %s: "
                                    "Ctrl+P > Object onto its holder or the model's root (not Keep Transform, which bakes the Child Of into it)"
                                    % (info.name, info.follows[1], under, under, info.follows[1]), [info.name], fix="ttt.remove_child_of"))

    return roles, problems


def _given_role(info: ObjectInfo, by_name: typing.Dict[str, ObjectInfo]) -> typing.Optional[str]:
    """The role its type, the kind it was imported as or the holder it's in gives it."""
    if info.twin_type in _TYPE_ROLES:
        return _TYPE_ROLES[info.twin_type]

    if info.kind in _KIND_ROLES:
        return _KIND_ROLES[info.kind]

    parent = by_name.get(info.parent) if info.parent is not None else None
    while parent is not None:
        if parent.kind in HOLDERS:
            return HOLDERS[parent.kind]

        parent = by_name.get(parent.parent) if parent.parent is not None else None

    return None


def _rigged_role(info: ObjectInfo, armature: typing.Optional[str]) -> str:
    if armature is not None and armature in info.deformed_by:
        return SHAPE if info.shape_keys > 0 else SKIN

    return BODY


def joint_bone(info: ObjectInfo, armature: typing.Optional[str], by_name: typing.Optional[typing.Dict[str, ObjectInfo]] = None) -> typing.Optional[str]:
    """The bone of the model's armature a rigid body, exit point or hull follows: by its Child Of or bone parent, else the one what it's
    parented to follows (by_name: the model's objects), None for none (joint 0 for a body)."""
    if info.follows is not None and info.follows[0] == armature:
        return info.follows[1]

    return _bone_under(info, by_name or {}, armature)


def _bone_under(info: ObjectInfo, by_name: typing.Dict[str, ObjectInfo], armature: typing.Optional[str]) -> typing.Optional[str]:
    """The bone the object's parent makes it follow: the bone it's parented to, or the one the object it's parented to follows (as an
    object: what follows vertexes is refused)."""
    seen = {info.name}
    while True:
        if info.parent_type == "BONE" and info.parent == armature and info.parent_bone:
            return info.parent_bone

        if info.parent_type not in ("OBJECT", "ARMATURE"):
            return None

        parent = by_name.get(info.parent) if info.parent is not None and info.parent != armature else None
        if parent is None or parent.name in seen:
            return None

        seen.add(parent.name)
        if parent.follows is not None and parent.follows[0] == armature:
            return parent.follows[1]

        info = parent


def _vertex_parent(info: ObjectInfo, by_name: typing.Dict[str, ObjectInfo], armature: typing.Optional[str]) -> typing.Optional[str]:
    """What the part follows vertexes of (Ctrl+P > Vertex), itself or through what it's under before anything that follows a bone."""
    seen = {info.name}
    while True:
        if info.parent_type in ("VERTEX", "VERTEX_3"):
            return info.parent

        parent = by_name.get(info.parent) if info.parent is not None and info.parent != armature and info.parent_type != "BONE" else None
        if parent is None or parent.name in seen or parent.follows is not None and parent.follows[0] == armature:
            return None

        seen.add(parent.name)
        info = parent


def _type_label(object_type: str) -> str:
    return {"CURVE": "curve", "SURFACE": "surface", "META": "metaball", "FONT": "text", "GPENCIL": "grease pencil", "GREASEPENCIL": "grease pencil",
            "LIGHT": "Blender light", "CAMERA": "camera", "SPEAKER": "speaker", "LIGHT_PROBE": "light probe", "LATTICE": "lattice",
            "VOLUME": "volume", "POINTCLOUD": "point cloud", "CURVES": "hair curves"}.get(object_type, object_type.lower())


def other_objects(objects: typing.Sequence[ObjectInfo], allowed: typing.Sequence[str] = MODEL_OBJECT_TYPES) -> typing.List[Problem]:
    """The objects of kinds the model can't have, for the models without a skeleton (a scenery's lights are empties)."""
    return [Problem("%s is a %s, which a model can't have: convert it to a mesh (Object > Convert > Mesh) or move it out of the model's root"
                    % (info.name, _type_label(info.object_type)), [info.name], fix="ttt.convert_to_mesh")
            for info in objects if info.object_type not in allowed]


def shape_key_problems(names: typing.Dict[str, typing.List[str]], most: int) -> typing.List[Problem]:
    """What's wrong with the blend skin's meshes' shape keys (each mesh's names past the basis): every mesh has the same, as many as the
    game's blend skins hold at most."""
    problems: typing.List[Problem] = []
    meshes = sorted(names)
    if not meshes:
        return problems

    first = names[meshes[0]]
    different = [mesh for mesh in meshes[1:] if names[mesh] != first]
    if different:
        problems.append(Problem("The blend skin's meshes have different shape keys (%s has %s, %s has %s): every mesh of the blend skin blends the "
                                "same shapes, give them the same shape keys in the same order" % (meshes[0], ", ".join(first) or "none", different[0],
                                                                                                 ", ".join(names[different[0]]) or "none"),
                                [meshes[0]] + different))

    for mesh in meshes:
        if len(names[mesh]) > most:
            problems.append(Problem("%s has %d shape keys, the game's blend skins blend %d at most: the facial animations would be garbled past "
                                    "them" % (mesh, len(names[mesh]), most), [mesh]))

    return problems
