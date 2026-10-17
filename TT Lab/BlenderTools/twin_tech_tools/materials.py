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

"""Every material a model's parts draw with is a Twin Tech material. A material made in Blender gets the settings the game draws that kind
of part with as soon as a part draws with it: a skin's the skinned shader, a rigid part's the unlit one, the render bucket the game's own
materials of the kind are in, the picture wired to its Base Color as the shader's picture. Its node tree becomes the game's drawing
(material_nodes). One material can't draw a skin and rigid parts, the game draws them with other shaders: split_rigid gives the rigid
parts a copy of their own."""

import os
import typing

import bpy

from . import gs_material
from . import material_settings
from . import project as projects
from . import tlm_blender
from . import tlm_scenery

# The nodes a picture reaches Base Color through, followed back from it
_PASS_THROUGH = ("MIX", "MIX_RGB", "HUE_SAT", "GAMMA", "BRIGHTCONTRAST", "INVERT", "CURVE_RGB", "VALTORGB", "SEPARATE_COLOR", "COMBINE_COLOR",
                 "MATH", "VECT_MATH", "REROUTE", "GROUP")


def picture_of(material: bpy.types.Material) -> typing.Optional[bpy.types.Image]:
    """The picture a material made in Blender draws: the one wired to the Principled BSDF's Base Color (through mixes and the like), else
    the only picture of its node tree."""
    tree = material.node_tree if material.use_nodes else None
    if tree is None:
        return None

    shader = next((node for node in tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if shader is not None:
        pending = [link.from_node for link in tree.links if link.to_node == shader and link.to_socket.name == "Base Color"]
        seen = set()
        while pending:
            node = pending.pop(0)
            if node.as_pointer() in seen:
                continue

            seen.add(node.as_pointer())
            if node.type == "TEX_IMAGE" and node.image is not None:
                return node.image

            if node.type in _PASS_THROUGH:
                pending.extend(link.from_node for link in tree.links if link.to_node == node)

    images = [node.image for node in tree.nodes if node.type == "TEX_IMAGE" and node.image is not None]
    return images[0] if len(set(images)) == 1 else None


def is_blended(material: bpy.types.Material) -> bool:
    return getattr(material, "surface_render_method", "") == "BLENDED" or getattr(material, "blend_method", "") == "BLEND"


def _global_package(root: typing.Optional[bpy.types.Object]) -> bool:
    path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
    return bool(path) and projects.is_global_package_file(path)


def make_twin_tech(material: bpy.types.Material, skin: bool, root: typing.Optional[bpy.types.Object] = None) -> None:
    """Gives a material made in Blender the settings the game draws the kind of part with, drawing its picture."""
    image = picture_of(material)
    material_settings.load(material, gs_material.new_material(material.name, skin=skin, textured=image is not None, blended=is_blended(material),
                                                               global_package=_global_package(root)))
    if image is not None:
        with material_settings.suspended():
            material_settings.settings_of(material).shaders[0].image = image

        material_settings.refresh(material)


def project_id(folder: typing.Optional[str]) -> typing.Optional[str]:
    """What tells a project apart: its UUID, which a copy of it keeps, else its folder (as the system compares them)."""
    if not folder:
        return None

    return projects.uuid_of(folder) or os.path.normcase(os.path.abspath(folder))


def project_id_of_file(path: str) -> typing.Optional[str]:
    return project_id(projects.find_root(path) if path else None)


def _blocks_drawn(roots: typing.Iterable[bpy.types.Object]) -> typing.Set[int]:
    """The materials the models draw and their pictures, by their pointers."""
    blocks: typing.Set[int] = set()
    for root in roots:
        for material, _ in _drawn(root).values():
            blocks.add(material.as_pointer())
            if material_settings.is_managed(material):
                blocks.update(item.image.as_pointer() for item in material_settings.settings_of(material).shaders if item.image is not None)

    return blocks


class ProjectBlocks:
    """The materials and pictures the scene's models draw by whose project they are, worked out once asked: a URI is its project's, and two
    projects of one name have the same URIs. A block of a URI is the project's own one (its models draw it), else one no other project's
    model draws (left over by a deleted model, a model of no file's), never another project's. A model of no file is of no project."""

    def __init__(self, folder: typing.Optional[str]):
        self._project = project_id(folder)
        self._roots: typing.Optional[typing.Tuple[typing.List[bpy.types.Object], typing.List[bpy.types.Object]]] = None
        self._blocks: typing.Optional[typing.Tuple[typing.Set[int], typing.Set[int], typing.Set[int]]] = None

    def roots(self) -> typing.Tuple[typing.List[bpy.types.Object], typing.List[bpy.types.Object]]:
        """The scene's models of the project and of other projects."""
        if self._roots is None:
            ours: typing.List[bpy.types.Object] = []
            theirs: typing.List[bpy.types.Object] = []
            ids: typing.Dict[str, typing.Optional[str]] = {}
            for root in _roots():
                path = str(root.get(tlm_blender.PATH_PROPERTY, ""))
                if path not in ids:
                    ids[path] = project_id_of_file(path)

                if ids[path] is not None and self._project is not None:
                    (ours if ids[path] == self._project else theirs).append(root)

            self._roots = (ours, theirs)

        return self._roots

    def _sets(self) -> typing.Tuple[typing.Set[int], typing.Set[int], typing.Set[int]]:
        if self._blocks is None:
            ours_roots, theirs_roots = self.roots()
            with tlm_blender.objects_walked():
                ours = _blocks_drawn(ours_roots)
                others = _blocks_drawn(theirs_roots)

            self._blocks = (ours, others - ours, ours & others)

        return self._blocks

    def pick(self, candidates: typing.Iterable[typing.Any]) -> typing.Any:
        """The block of the candidates (of one URI) that's the project's."""
        candidates = list(candidates)
        if len(candidates) == 0:
            return None

        if len(candidates) == 1 and not self.roots()[1]:
            return candidates[0]

        ours, theirs, _ = self._sets()
        return next((block for block in candidates if block.as_pointer() in ours), None) or \
            next((block for block in candidates if block.as_pointer() not in theirs), None)

    def is_ours(self, block: typing.Any) -> bool:
        return block.as_pointer() in self._sets()[0]

    def is_theirs(self, block: typing.Any) -> bool:
        """Drawn by other projects' models, by none of the project's."""
        return bool(self.roots()[1]) and block.as_pointer() in self._sets()[1]

    def is_shared(self, block: typing.Any) -> bool:
        """Drawn by the project's models and another project's too."""
        return bool(self.roots()[1]) and block.as_pointer() in self._sets()[2]


def copy_for(block: typing.Any, roots: typing.Iterable[bpy.types.Object]) -> typing.Any:
    """A copy of a material or picture of its own, made in Blender, which the models' parts (a material) or their materials' shaders (a
    picture) draw in its place: the scene's other models keep the block as it is."""
    roots = list(roots)
    if isinstance(block, bpy.types.Material):
        copy = _copy_of(block, "%s %s" % (block.name, roots[0].name) if roots else block.name)
        as_made_in_blender(copy)
        for root in roots:
            for blender_object in tlm_blender._descendants(root):
                if blender_object.type != "MESH":
                    continue

                for slot in blender_object.material_slots:
                    if slot.material == block:
                        slot.material = copy

        return copy

    copy = block.copy()
    copy.name = "%s %s" % (block.name, roots[0].name) if roots else block.name
    for key in (material_settings.TEXTURE_URI_PROPERTY, material_settings.PNG_CONFIRMED_PROPERTY, material_settings.IMAGE_ID_PROPERTY):
        if key in copy:
            del copy[key]

    for root in roots:
        for material, _ in _drawn(root).values():
            for item in material_settings.settings_of(material).shaders if material_settings.is_managed(material) else []:
                if item.image == block:
                    item.image = copy

    return copy


def _drawn(root: bpy.types.Object) -> typing.Dict[str, typing.Tuple[bpy.types.Material, typing.Set[str]]]:
    """The materials the model's parts draw with and the kinds of parts drawing them, skin or rigid."""
    result: typing.Dict[str, typing.Tuple[bpy.types.Material, typing.Set[str]]] = {}
    for blender_object in tlm_blender._descendants(root):
        if blender_object.type != "MESH" or not tlm_blender._draws(blender_object):
            continue

        role = tlm_blender.role_of(blender_object)
        if role in ("collision", "hull"):
            continue

        kind = "skin" if role in tlm_blender.SKINNED_KINDS else "rigid"
        for material in tlm_blender.drawn_materials(blender_object):
            result.setdefault(material.name, (material, set()))[1].add(kind)

    return result


# The models drawn with the game's materials: a save icon's picture is its texture, a collision's materials its surfaces
_GAME_MATERIAL_KINDS = ("ogi", "scenery", "skydome", "dynamic_scenery", "model", "rigid_model", "mesh", "skin", "shape")


def make_drawn_twin_tech(root: bpy.types.Object) -> typing.List[bpy.types.Material]:
    """Gives every material of the model made in Blender without settings the ones of the parts drawing it. A material of both kinds of parts
    waits until it's split (the checks refuse it)."""
    made = []
    if root.get(tlm_blender.KIND_PROPERTY) not in _GAME_MATERIAL_KINDS:
        return made

    for material, kinds in _drawn(root).values():
        # The project's materials are the project's, shown with its settings when they could be read
        if material_settings.is_managed(material) or material.get(tlm_blender.URI_PROPERTY) or len(kinds) != 1 or tlm_scenery.is_surface(material):
            continue

        make_twin_tech(material, "skin" in kinds, root)
        made.append(material)

    return made


def _copy_number(name: str) -> int:
    """Blender's number of a copy's name, Material.001 the first."""
    base, dot, number = name.rpartition(".")
    return int(number) if dot and number.isdigit() else 0


# The projects whose models drew each material when it was last drawn, by its session UID: one no model draws any more stays its project's
_last_drawn: typing.Dict[int, typing.FrozenSet[str]] = {}


def split_copies() -> typing.List[bpy.types.Material]:
    """A material copied in Blender (a single-user copy, the material's copy button) keeps the URI and Blender ID of the one it was copied
    from, and the next model written sent it as that one: the copies become materials of their own made in Blender, the one of the name
    without Blender's number (or the oldest) stays what it was."""
    # Two projects of one name have the same URIs: materials of one drawn by different projects' models aren't copies of each other
    drawing: typing.Dict[int, typing.Set[str]] = {}
    ids: typing.Dict[str, typing.Optional[str]] = {}
    with tlm_blender.objects_walked():
        for root in _roots():
            path = str(root.get(tlm_blender.PATH_PROPERTY, ""))
            if path not in ids:
                ids[path] = project_id_of_file(path)

            for material, _ in _drawn(root).values() if ids[path] is not None else []:
                drawing.setdefault(material.as_pointer(), set()).add(typing.cast(str, ids[path]))

    groups: typing.Dict[typing.Tuple[str, typing.FrozenSet[str]], typing.List[bpy.types.Material]] = {}
    undrawn: typing.Dict[str, typing.List[bpy.types.Material]] = {}
    for material in bpy.data.materials:
        key = material.get(tlm_blender.URI_PROPERTY) or material.get(tlm_blender.BLENDER_ID_PROPERTY)
        projects = frozenset(drawing.get(material.as_pointer(), ()))
        if projects:
            _last_drawn[material.session_uid] = projects
        else:
            # Another project's left over (its model deleted, not purged) isn't a copy of this project's
            projects = _last_drawn.get(material.session_uid, frozenset())

        if key and projects:
            groups.setdefault((str(key), projects), []).append(material)
        elif key:
            undrawn.setdefault(str(key), []).append(material)

    # What no model ever drew is a copy of what one project's models draw of its key (a copy made in Blender), of several projects' it can't
    # tell whose. The slot's copy button leaves the original drawn by none, still its project's
    for key, members in undrawn.items():
        drawn = [group for (group_key, _), group in groups.items() if group_key == key]
        if len(drawn) == 1:
            drawn[0].extend(members)
        elif len(drawn) == 0:
            groups[(key, frozenset())] = members

    made = []
    for members in groups.values():
        if len(members) < 2:
            continue

        original = min(members, key=lambda material: (_copy_number(material.name), material.session_uid))
        for material in members:
            if material != original:
                as_made_in_blender(material)
                made.append(material)

    return made


def _give_vertex_colors(materials: typing.Iterable[bpy.types.Material]) -> None:
    """The meshes drawn with the materials get the grey TT Lab writes for a mesh without colors: the game's drawing multiplies by the vertex
    colors and they showed black without any."""
    from . import material_ui

    wanted = set(material.name for material in materials)
    for blender_object in bpy.data.objects:
        if blender_object.type == "MESH" and any(slot.material is not None and slot.material.name in wanted for slot in blender_object.material_slots) \
                and blender_object.data.color_attributes.get(tlm_blender.COLOR_ATTRIBUTE) is None and not blender_object.data.is_editmode:
            material_ui.add_default_colors(blender_object.data)


def as_made_in_blender(material: bpy.types.Material) -> None:
    """Makes a copy of a material a material of its own made in Blender, which TT Lab never had: writing a model sends all of it."""
    for key in (tlm_blender.URI_PROPERTY, tlm_blender.BLENDER_ID_PROPERTY, tlm_blender.SENT_PROPERTY, tlm_blender.SENT_SEQUENCE_PROPERTY,
                tlm_blender.BASE_SEQUENCE_PROPERTY):
        if key in material:
            del material[key]

    if material_settings.is_managed(material):
        material_settings.settings_of(material).known = False


def _copy_of(material: bpy.types.Material, name: str) -> bpy.types.Material:
    """A copy of the material with a shader animation of its own: Python's copy shares the original's action (Blender copies actions only
    from its interface), and keying the copy keyed the original."""
    copy = material.copy()
    copy.name = name
    animation = copy.animation_data
    if animation is not None and animation.action is not None:
        slot = animation.action_slot.identifier if getattr(animation, "action_slot", None) is not None else ""
        action = animation.action.copy()
        action.name = copy.name + " Shader Animation"
        animation.action = action
        found = next((candidate for candidate in getattr(action, "slots", []) if candidate.identifier == slot), None)
        if found is not None:
            animation.action_slot = found

    return copy


def make_own(material: bpy.types.Material, root: bpy.types.Object) -> bpy.types.Material:
    """Gives the model's parts drawn with the material a copy of it of their own, made in Blender with its settings: the other models keep
    the material as it is."""
    return copy_for(material, [root])


def make_picture_own(image: bpy.types.Image, root: bpy.types.Object) -> bpy.types.Image:
    """Gives the model's own materials (made in Blender) drawing the project texture's picture a copy of it of their own, made in Blender:
    writing the model makes it a texture of the model's, of the project it's written to (another project's texture's URI meant nothing
    there). The project's materials keep the project's picture."""
    copy = image.copy()
    copy.name = "%s %s" % (image.name, root.name)
    for key in (material_settings.TEXTURE_URI_PROPERTY, material_settings.PNG_CONFIRMED_PROPERTY, material_settings.IMAGE_ID_PROPERTY):
        if key in copy:
            del copy[key]

    for material, _ in _drawn(root).values():
        if material.get(tlm_blender.URI_PROPERTY) or not material_settings.is_managed(material):
            continue

        for item in material_settings.settings_of(material).shaders:
            if item.image == image:
                item.image = copy

    return copy


def split_rigid(material: bpy.types.Material, root: typing.Optional[bpy.types.Object]) -> typing.Optional[bpy.types.Material]:
    """Gives the model's rigid parts drawn with the material a copy of it: the skin keeps the material, whose first shader is the skinned
    one, the copy's is the unlit one rigid parts are drawn with."""
    if root is None:
        return None

    copy = _copy_of(material, material.name + " Rigid")
    # The copy is a material of its own, made in Blender
    as_made_in_blender(copy)

    for blender_object in tlm_blender._descendants(root):
        if blender_object.type != "MESH" or tlm_blender.role_of(blender_object) in tlm_blender.SKINNED_KINDS + ("collision", "hull"):
            continue

        for slot in blender_object.material_slots:
            if slot.material == material:
                slot.material = copy

    for target, skin in ((material, True), (copy, False)):
        if not material_settings.is_managed(target):
            # A project material without the project's settings stays the project's
            if not target.get(tlm_blender.URI_PROPERTY):
                make_twin_tech(target, skin, root)

            continue

        shaders = material_settings.settings_of(target).shaders
        if len(shaders) > 0:
            first = shaders[0]
            if skin and first.shader_type != "LitSkinnedModel":
                first.shader_type = "LitSkinnedModel"
            elif not skin and first.shader_type == "LitSkinnedModel":
                first.shader_type = "StandardUnlit"

    return copy


# The scene's changes (checks.note_change) the timer looked at last
_looked_at = [-1]


def _roots() -> typing.List[bpy.types.Object]:
    # A model kept as what another was retargeted from is left as it was
    return [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY) and tlm_blender.in_scene(blender_object)
            and not blender_object.get("ttt_retarget_source")]


def _idle(window: typing.Any) -> bool:
    """Whether the user is doing nothing that a change of the materials would get in the way of: in Object mode, no modal operator."""
    if len(getattr(window, "modal_operators", [])) > 0:
        return False

    active = bpy.context.view_layer.objects.active if bpy.context.view_layer is not None else None
    return active is None or active.mode == "OBJECT"


def _make_new_materials_twin_tech() -> float:
    from . import checks

    if checks._changes[0] == _looked_at[0]:
        return 1.0

    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if len(windows) == 0 or not _idle(windows[0]):
        return 1.0

    _looked_at[0] = checks._changes[0]
    copies = split_copies()
    roots = _roots()
    made = [material for root in roots for material in make_drawn_twin_tech(root)]
    _give_vertex_colors(made)
    pictured = [material for material, _ in {name: entry for root in roots for name, entry in _drawn(root).items()}.values()
                if material_settings.take_node_pictures(material)]
    if made or copies or pictured:
        with bpy.context.temp_override(window=windows[0]):
            bpy.ops.ed.undo_push(message="Twin Tech settings for %s" % ", ".join(material.name for material in copies + made + pictured))

    return 1.0


def register():
    if not bpy.app.timers.is_registered(_make_new_materials_twin_tech):
        bpy.app.timers.register(_make_new_materials_twin_tech, first_interval=1.0, persistent=True)


def unregister():
    if bpy.app.timers.is_registered(_make_new_materials_twin_tech):
        bpy.app.timers.unregister(_make_new_materials_twin_tech)
