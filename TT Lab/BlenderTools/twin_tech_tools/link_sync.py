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

"""The scene's models linked to the TT Lab that has their project open (LINK.md).

A timer links the models of a project to a running TT Lab that has it open, takes what TT Lab tells (applied as an undo step of
Blender's, once the user isn't editing or dragging) and sends what changed: pictures and materials made in Blender become the project's
at once, settings changed in Blender go after a moment without more changes, a picture once its stroke is done. Syncing (the Sync with
TT Lab button, or saving the blend file as the add-on's preferences say) writes the models through TT Lab and saves what Blender made and
changed; closing Blender without syncing drops that. Without TT Lab syncing writes the models into their files (save_hook). TT Lab's Use an Existing Blend File makes a model of the blend file a model of TT Lab's (use_for),
written through TT Lab in its place once it's linked and what it draws is the project's, and the blend file saved."""

import hashlib
import json
import os
import time
import traceback
import typing
import uuid

import bpy

from . import gs_material
from . import link_client
from . import link_protocol
from . import link_state
from . import material_settings
from . import project as projects
from . import tlm_blender

# How long a material has to stay unchanged before what changed is sent, and how often the scene looks for TT Lab
QUIET = 0.3
LOOK_EVERY = 3.0
# On the scene, the blend file's UUID TT Lab knows it by
BLEND_PROPERTY = "ttt_blend_uuid"
# On a model root: the revision TT Lab knows what Blender has of the model by (tlm_blender.REVISION_PROPERTY), and whether TT Lab changed
# it since
REVISION_PROPERTY = tlm_blender.REVISION_PROPERTY
CHANGED_IN_TT_LAB_PROPERTY = "ttt_changed_in_tt_lab"
MODEL_URI_PROPERTY = "ttt_model_uri"
# On a model root, what TT Lab found when it last took the model (JSON: text, refused, objects): the checks panel shows it
TT_LAB_PROBLEMS_PROPERTY = "ttt_tt_lab_problems"
# On a model root put in a TT Lab model's place that TT Lab didn't take yet (JSON: the TT Lab model's name and its data file): kept with the
# blend file, so a file opened again writes it in that place once TT Lab has the project open, and leaves the file alone until then
REPLACES_PROPERTY = "ttt_replaces"
# On the scene, the data files of TT Lab's models the blend file had models of and has none of any more (JSON) that no TT Lab heard of yet
RELEASED_PROPERTY = "ttt_released"


class Link:
    """One running TT Lab and the scene's models of its project."""

    def __init__(self, root_folder: str, info: typing.Dict[str, typing.Any], client: link_client.Client):
        self.root_folder = root_folder
        self.info = info
        self.client = client
        self.welcomed = False
        self.models: typing.Dict[str, str] = {}
        # Materials and pictures being made in TT Lab, by their names
        self.making: typing.Set[str] = set()
        # What was sent and not answered yet, by material name
        self.sending: typing.Set[str] = set()
        # What Blender made or changed since the blend file was saved, which saving saves
        self.unsaved: typing.Set[str] = set()
        # What TT Lab has of every linked material, by URI: what Blender's are compared with. Kept out of Blender's undo (undoing a change that
        # came from TT Lab is a change TT Lab gets), it's the materials' base once the blend file is saved
        self.state: typing.Dict[str, typing.Dict[str, typing.Any]] = {}
        # The names of the materials and pictures TT Lab knows, by URI, as Blender had them: renaming one in Blender renames it in TT Lab
        self.names: typing.Dict[str, str] = {}
        # The models asked to be linked and not answered yet (by name and file), and the files of the ones TT Lab didn't link (not asked
        # again), by name
        self.linking: typing.Set[typing.Tuple[str, str]] = set()
        self.unlinkable: typing.Dict[str, str] = {}
        # What TT Lab said of the linked models (their files and revisions, by name) and the URIs of what it made of Blender's (by Blender ID):
        # Blender's undo takes the properties they're kept in back, undoing changes nothing in TT Lab
        self.paths: typing.Dict[str, str] = {}
        self.revisions: typing.Dict[str, str] = {}
        self.made: typing.Dict[str, str] = {}
        # The linked models TT Lab changed since Blender read them, by name: undo takes the mark back too
        self.changed: typing.Set[str] = set()
        # The root each name's revision and mark are of (its ttt_uid): undo bringing back another model of the name (the one Take TT Lab's
        # put another in the place of) brings back what was said of it
        self.uids: typing.Dict[str, str] = {}
        # The files of models TT Lab moved, by the file they had: undo giving the old file back gives the new one again
        self.moved: typing.Dict[str, typing.Tuple[str, str]] = {}

    def record(self, root: bpy.types.Object) -> None:
        """What the root has of the link from now on, whatever undo does to its properties."""
        self.revisions[root.name] = root.get(REVISION_PROPERTY, "")
        self.uids[root.name] = root.get(tlm_blender.UID_PROPERTY, "")
        if root.get(CHANGED_IN_TT_LAB_PROPERTY):
            self.changed.add(root.name)
        else:
            self.changed.discard(root.name)

    def forget(self, name: str) -> None:
        self.models.pop(name, None)
        self.paths.pop(name, None)
        self.revisions.pop(name, None)
        self.uids.pop(name, None)
        self.changed.discard(name)


_links: typing.Dict[str, Link] = {}
_marked: typing.Dict[str, float] = {}
_picture_hashes: typing.Dict[str, str] = {}
_last_look = [0.0]
_last_pictures = [0.0]
# What came from TT Lab and waits for the user to stop editing
_waiting: typing.List[typing.Tuple[Link, typing.Dict[str, typing.Any], bytes]] = []
_applying = [False]


class Replacing:
    """A model of the scene put in the place of a TT Lab model (use_for) as this session tries it, until TT Lab took it: written as soon as
    it's linked and what it draws is the project's (or once that took long enough), then again by every save until TT Lab takes it. What it
    replaces is the root's (REPLACES_PROPERTY)."""

    def __init__(self, name: str, path: str):
        self.name = name
        self.path = path
        self.started = time.time()
        self.attempted = False
        # Sent on the link and not answered yet: a save meanwhile doesn't send it again. The answer goes with a link that closed
        self.in_flight = False
        self.link: typing.Optional["Link"] = None

    def is_in_flight(self) -> bool:
        return self.in_flight and self.link is not None and not self.link.client.closed and self.link in _links.values()


# By the root's name, made of its marker when it's looked at
_replacing: typing.Dict[str, Replacing] = {}
REPLACING_WAIT = 60.0
# The replacements TT Lab took (the root's name and file): undo gives their markers back, they're taken all the same
_taken: typing.Set[typing.Tuple[str, str]] = set()
# What TT Lab's Use an Existing Blend File asked for (model, asset_type, name): until a model of the kind is picked, the scene's models of the
# kind aren't linked and nothing is made for them (they'd be the old model's). The last one stays for the Twin Tech Model panel to offer again
_request: typing.List[typing.Optional[typing.Dict[str, str]]] = [None]
_last_request: typing.List[typing.Optional[typing.Dict[str, str]]] = [None]
# Pictures that can't be read (a file of another computer), by name with the file they were read from: drawn without them, said once
_unreadable: typing.Dict[str, str] = {}
# Pictures TT Lab didn't take (a texture deleted meanwhile), by name with the hash sent: sent again once they change
_failed_pictures: typing.Dict[str, str] = {}
# The models put in place TT Lab took, whose blend file is saved once the user isn't editing or dragging
_finishing: typing.List[typing.Tuple[typing.Any, ...]] = []


def linked_folder(root: bpy.types.Object) -> typing.Optional[str]:
    """The project folder of the model when it's linked to TT Lab."""
    path = root.get(tlm_blender.PATH_PROPERTY, "")
    folder = projects.find_root(path) if path else None
    link = _links.get(folder) if folder else None
    return folder if link is not None and link.welcomed and not link.client.closed else None


def is_linked(root: bpy.types.Object) -> bool:
    return linked_folder(root) is not None


def link_of(root: bpy.types.Object) -> typing.Optional[Link]:
    folder = linked_folder(root)
    return _links.get(folder) if folder else None


def mark(material: bpy.types.Material) -> None:
    """The material's settings changed: what changed goes to TT Lab once they stay so for a moment."""
    if not _applying[0]:
        _marked[material.name] = time.time()


def _blend_uuid() -> str:
    scene = bpy.context.scene
    if not scene.get(BLEND_PROPERTY):
        scene[BLEND_PROPERTY] = uuid.uuid4().hex

    return scene[BLEND_PROPERTY]


def _roots() -> typing.List[bpy.types.Object]:
    return [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY) and tlm_blender.in_scene(blender_object)
            and not blender_object.get("ttt_retarget_source") and blender_object.get(tlm_blender.PATH_PROPERTY)]


def _look_for_tt_lab() -> None:
    """Links the projects of the scene's models to the TT Labs that have them open."""
    folders = {}
    for root in _roots():
        folder = projects.find_root(root[tlm_blender.PATH_PROPERTY])
        if folder is not None and not _held_back(root):
            folders.setdefault(folder, []).append(root)

    for folder, roots in folders.items():
        link = _links.get(folder)
        if link is not None and not link.client.closed:
            # The models the scene got since it linked (imported, made a model's): linked as well
            for root in roots if link.welcomed else []:
                path = root[tlm_blender.PATH_PROPERTY]
                if root.name not in link.models and (root.name, _normal(path)) not in link.linking and link.unlinkable.get(root.name) != path:
                    _link_model(link, root)

            continue

        project = projects.open_project(roots[0][tlm_blender.PATH_PROPERTY])
        info = link_protocol.tt_lab_of(folder, project.uuid if project is not None else "")
        if info is None:
            continue

        try:
            client = link_client.Client(int(info["port"]))
        except (OSError, KeyError, ValueError):
            continue

        link = Link(folder, info, client)
        _links[folder] = link
        client.send({"type": "hello", "token": info.get("token", ""), "protocol": link_protocol.PROTOCOL, "blend": _blend_uuid(), "path": bpy.data.filepath,
                     "blender": bpy.app.version_string, "addon": tlm_blender.ADDON_VERSION},
                    answered=lambda header, payload, link=link: _welcomed(link, header))


def _welcomed(link: Link, header: typing.Dict[str, typing.Any]) -> None:
    if header.get("type") != "welcome":
        print("TT Lab didn't link: %s" % header.get("text", header.get("type")))
        link.client.close()
        return

    link.welcomed = True
    addon = link.info.get("addon_version", "")
    if addon and addon != tlm_blender.ADDON_VERSION:
        print("TT Lab %s comes with the add-on %s, this is %s: install it from TT Lab's Tools menu" % (link.info.get("version", ""), addon, tlm_blender.ADDON_VERSION))

    for root in _roots():
        if projects.find_root(root[tlm_blender.PATH_PROPERTY]) == link.root_folder and not _held_back(root):
            _link_model(link, root)


def _held_back(root: bpy.types.Object) -> bool:
    """Whether the model waits for the user to pick which model of the kind replaces TT Lab's: linked, it'd be its file's model meanwhile."""
    request = _request[0]
    return request is not None and root.get(tlm_blender.ROOT_PROPERTY) == request["asset_type"] and _replacement(root) is None


def _normal(path: str) -> str:
    return os.path.normcase(os.path.abspath(path)) if path else ""


def _link_model(link: Link, root: bpy.types.Object) -> None:
    drawn, pictures = _drawn_uris(root)
    path = root[tlm_blender.PATH_PROPERTY]
    link.linking.add((root.name, _normal(path)))
    # What Blender has of the model: TT Lab says whether it changed it since
    link.client.send({"type": "link_model", "path": path, "materials": drawn, "pictures": pictures, "revision": root.get(REVISION_PROPERTY, "")},
                     answered=lambda header, payload, root_name=root.name, path=path, uid=root.get(tlm_blender.UID_PROPERTY, ""):
                     _model_linked(link, root_name, path, header, uid))


def _drawn_uris(root: bpy.types.Object) -> typing.Tuple[typing.List[str], typing.List[str]]:
    """The URIs of the project's materials the model draws with and of the pictures they draw."""
    from . import materials

    found: typing.List[str] = []
    pictures: typing.List[str] = []
    for material, _ in materials._drawn(root).values():
        uri = material.get(tlm_blender.URI_PROPERTY, "")
        if uri and uri not in found:
            found.append(uri)

        for item in material_settings.settings_of(material).shaders if material_settings.is_managed(material) else []:
            picture = item.image.get(material_settings.TEXTURE_URI_PROPERTY, "") if item.image is not None else ""
            if picture and picture not in pictures:
                pictures.append(picture)

    return found, pictures


def _model_linked(link: Link, root_name: str, path: str, header: typing.Dict[str, typing.Any], uid: str = "") -> None:
    link.linking.discard((root_name, _normal(path)))
    root = bpy.data.objects.get(root_name)
    if root is None:
        return

    if root.get(tlm_blender.UID_PROPERTY, "") != uid:
        # Another model of the name meanwhile (undo of Take TT Lab's): what TT Lab said is of the one asked about, the next look links this one
        _last_look[0] = 0.0
        return

    if not _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), path):
        # Given another model's file meanwhile (Use an Existing Blend File): what TT Lab says of the file it had isn't of the one it has.
        # Linked, the model it had goes, unless another model of the scene stands for it
        if header.get("type") == "model" and header.get("uri") and header["uri"] not in link.models.values():
            link.client.send({"type": "unlink_model", "uri": header["uri"]})

        return

    entry = _replacement(root)
    if header.get("type") != "model":
        link.unlinkable[root_name] = path
        if entry is not None:
            # The file it was given isn't the model's: saving would write it into that file past TT Lab
            _drop_replacement(root)
            unbind(root)
            del root[tlm_blender.PATH_PROPERTY]
            _tell("ERROR", "TT Lab didn't link %s, it doesn't replace %s: %s. It's a model of no file now"
                  % (root_name, entry.name, header.get("text", header.get("type"))))

        return

    root[MODEL_URI_PROPERTY] = header.get("uri", "")
    link.models[root.name] = header.get("uri", "")
    link.paths[root.name] = path
    _redraw()
    missing = set(header.get("missing") or [])
    if missing:
        from . import materials

        _make_again(link, root, missing)
        materials.make_drawn_twin_tech(root)

    revision = header.get("revision", "")
    if entry is not None:
        # Written over TT Lab's whatever TT Lab changed of it, again once it's linked again (its answer went with the link that closed)
        root[REVISION_PROPERTY] = revision
        if not entry.is_in_flight():
            entry.attempted = False
            entry.in_flight = False
    elif header.get("changed"):
        # TT Lab changed it since the revision Blender has, which stays: writing it is a conflict until it's answered
        _mark_changed(link, root)
    else:
        if CHANGED_IN_TT_LAB_PROPERTY in root:
            # Kept with the blend file: TT Lab has the model as Blender has it again (its changes discarded)
            del root[CHANGED_IN_TT_LAB_PROPERTY]

        root[REVISION_PROPERTY] = revision

    link.record(root)
    # What changed on either side since: merged, what Blender changed sent
    others = _others(link)
    for entry in header.get("materials", []):
        material = _material_of(link, str(entry.get("uri", "")), others)
        if material is not None:
            link.names.setdefault(entry["uri"], material.name)

        if material is None or not isinstance(entry.get("data"), dict):
            continue

        # One Blender couldn't read gets TT Lab's settings
        _waiting.append((link, {"type": "relink_material", "uri": entry["uri"], "data": entry["data"]}, b""))

    for entry in header.get("pictures", []):
        image = _picture_of(link, str(entry.get("uri", "")), others)
        if image is not None:
            link.names.setdefault(entry["uri"], image.name)

        confirmed = image.get(material_settings.PNG_CONFIRMED_PROPERTY, "") if image is not None else ""
        if image is not None and entry.get("hash") and entry["hash"] != confirmed and _png_hash(image) == confirmed:
            # Changed in TT Lab and not in Blender
            _get_picture(link, image.name, entry["uri"])


def _make_again(link: Link, root: bpy.types.Object, missing: typing.Set[str]) -> None:
    """What the model draws that the project doesn't have (deleted in TT Lab while Blender was closed, another project's) Blender makes
    again: TT Lab gets it as the model's own."""
    from . import materials

    blocks = _others(link)
    drawn = [material for material, _ in materials._drawn(root).values()]
    for material in drawn:
        if material.get(tlm_blender.URI_PROPERTY, "") in missing:
            _made_again(link, material, blocks)

    for image in {item.image.as_pointer(): item.image for material in drawn for item in _pictured(material)}.values():
        if image.get(material_settings.TEXTURE_URI_PROPERTY, "") in missing:
            _made_again(link, image, blocks)


def _made_again(link: Link, block: typing.Any, blocks: typing.Any) -> typing.Any:
    """The material or picture of a URI TT Lab doesn't have is made again, the project's own made in Blender: another project's models
    drawing it as well keep it, the project's models get a copy."""
    from . import materials

    uri = block.get(tlm_blender.URI_PROPERTY if isinstance(block, bpy.types.Material) else material_settings.TEXTURE_URI_PROPERTY, "")
    link.names.pop(uri, None)
    link.state.pop(uri, None)
    if blocks.is_shared(block):
        return materials.copy_for(block, blocks.roots()[0])

    if isinstance(block, bpy.types.Material):
        materials.as_made_in_blender(block)
    else:
        for key in (material_settings.TEXTURE_URI_PROPERTY, material_settings.PNG_CONFIRMED_PROPERTY):
            if key in block:
                del block[key]

    return block


def _redraw() -> None:
    """The panels say whether the models are linked and what TT Lab found: Blender draws them again only when told to."""
    window_manager = bpy.context.window_manager
    for window in window_manager.windows if window_manager is not None else []:
        for area in window.screen.areas:
            area.tag_redraw()


def _same_file(path: str, other: str) -> bool:
    return bool(path) and bool(other) and os.path.normcase(os.path.abspath(path)) == os.path.normcase(os.path.abspath(other))


def candidates(asset_type: str) -> typing.List[bpy.types.Object]:
    """The view layer's models of the type (their files', "Ogi"): what can take the place of a TT Lab model of it. One out of the view layer
    (an excluded collection, another scene) can't be picked."""
    layer = bpy.context.view_layer
    shown = set(layer.objects.keys()) if layer is not None else set()
    return [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY) == asset_type
            and blender_object.name in shown and not blender_object.get("ttt_retarget_source")]


def unbind(root: bpy.types.Object) -> None:
    """Takes the model out of the links it's in and forgets what TT Lab said of the file it had: it stands for no model of TT Lab's until
    it's linked again."""
    for link in _links.values():
        uri = link.models.get(root.name)
        link.forget(root.name)
        # Another model of the scene may stand for it still
        if uri and uri not in link.models.values() and link.welcomed and not link.client.closed:
            link.client.send({"type": "unlink_model", "uri": uri})

    for key in (MODEL_URI_PROPERTY, REVISION_PROPERTY, CHANGED_IN_TT_LAB_PROPERTY, TT_LAB_PROBLEMS_PROPERTY):
        if key in root:
            del root[key]


def request(model: str, asset_type: str, name: str) -> None:
    """TT Lab's Use an Existing Blend File asked for a model of the kind to take the place of its model of the data file: until one is
    picked, the scene's models of the kind are neither linked nor have anything made for them."""
    _request[0] = {"model": model, "asset_type": asset_type, "name": name}
    _last_request[0] = dict(_request[0])


def drop_request() -> None:
    """Nothing was picked (the choice cancelled, the file has no model of the kind): the scene's models link again. The Twin Tech Model panel
    still offers the request for the models of its kind."""
    _request[0] = None
    _last_look[0] = 0.0


def last_request() -> typing.Optional[typing.Dict[str, str]]:
    return _last_request[0]


def use_for(root: bpy.types.Object, path: str, name: str) -> None:
    """Makes the model TT Lab's model of the data file (TT Lab's Use an Existing Blend File): once it's linked and what it draws is the
    project's (what the project doesn't have made the model's own), it's written through TT Lab in its place and the blend file is saved,
    the model's blend file from then on. Another model the scene had of the file stands for none, two models can't write one file. What
    another project's model draws is made the model's own whatever this project has at the same URIs. Run by an operator: what it changes
    is in that undo step, the Blender IDs of what TT Lab will make of Blender's too (made again after undo, TT Lab gives back the same)."""
    from . import materials

    for other in bpy.data.objects:
        if other != root and other.get(tlm_blender.ROOT_PROPERTY) and not other.get("ttt_retarget_source") \
                and _same_file(other.get(tlm_blender.PATH_PROPERTY, ""), path):
            unbind(other)
            del other[tlm_blender.PATH_PROPERTY]

    previous = root.get(tlm_blender.PATH_PROPERTY, "")
    if previous and not _same_file(previous, path):
        _release(previous)

    unbind(root)
    separate(root, path, previous, before_settings=True)
    root[tlm_blender.PATH_PROPERTY] = path
    # Kept with the blend file: in this undo step, undoing it gives the model its earlier file and nothing to replace
    root[REPLACES_PROPERTY] = json.dumps({"name": name, "path": path})
    _taken.discard((root.name, _normal(path)))
    _replacing.pop(root.name, None)
    materials.make_drawn_twin_tech(root)
    # After the materials made in Blender got their settings: a picture wired to one is in them then
    separate(root, path, previous, before_settings=False)
    _identify_drawn(root)
    _request[0] = None
    folder = projects.find_root(path)
    link = _links.get(folder) if folder else None
    if link is not None and link.welcomed and not link.client.closed:
        _link_model(link, root)
    else:
        _last_look[0] = 0.0


def _same_project(path: str, other: str) -> bool:
    """Whether the files are of one project (copies of it included, by its UUID). A model of no file, or of a file of no project here
    (another system's), is the project's it's put in: TT Lab's link says what it doesn't have of it."""
    from . import materials

    first, second = materials.project_id_of_file(path), materials.project_id_of_file(other)
    return first is None or second is None or first == second


def separate(root: bpy.types.Object, path: str, previous: typing.Optional[str] = None, before_settings: typing.Optional[bool] = None) -> None:
    """The model is the model of the file from now on (Use an Existing Blend File, exported into another project's file): what it draws that
    other projects' models draw as well is copied for it, and when it was another project's model, what it draws is made its own (the URIs
    it has are that project's, whatever this one has at them), copied when the scene's other models draw it too. Materials first, then their
    pictures (once the materials made in Blender got their settings and the pictures wired to them, before_settings False); both when it
    isn't said."""
    from . import materials

    previous = root.get(tlm_blender.PATH_PROPERTY, "") if previous is None else previous
    foreign = not _same_project(previous, path)
    project = materials.project_id_of_file(path)

    def drawn_elsewhere() -> typing.Tuple[typing.Set[int], typing.Set[int]]:
        """What the scene's other models draw, and what other projects' models draw."""
        others: typing.List[bpy.types.Object] = []
        other_projects: typing.List[bpy.types.Object] = []
        for other in _roots_in_scene():
            if other == root:
                continue

            others.append(other)
            other_project = materials.project_id_of_file(str(other.get(tlm_blender.PATH_PROPERTY, "")))
            if other_project is not None and project is not None and other_project != project:
                other_projects.append(other)

        with tlm_blender.objects_walked():
            return materials._blocks_drawn(others), materials._blocks_drawn(other_projects)

    if before_settings is not False:
        everywhere, other_projects = drawn_elsewhere()
        for material, _ in materials._drawn(root).values():
            if material.as_pointer() in (everywhere if foreign else other_projects):
                materials.copy_for(material, [root])
            elif foreign:
                materials.as_made_in_blender(material)

    if before_settings is not True:
        everywhere, other_projects = drawn_elsewhere()
        images = {item.image.as_pointer(): item.image for material, _ in materials._drawn(root).values() for item in _pictured(material)}
        for image in images.values():
            if image.as_pointer() in (everywhere if foreign else other_projects):
                materials.copy_for(image, [root])
            elif foreign:
                for key in (material_settings.TEXTURE_URI_PROPERTY, material_settings.PNG_CONFIRMED_PROPERTY, material_settings.IMAGE_ID_PROPERTY):
                    if key in image:
                        del image[key]

                for material, _ in materials._drawn(root).values():
                    for item in _pictured(material):
                        if item.image == image:
                            with material_settings.suspended():
                                item.texture = ""


def _roots_in_scene() -> typing.List[bpy.types.Object]:
    """Every model of the scene, of a file or not."""
    return [other for other in bpy.data.objects if other.get(tlm_blender.ROOT_PROPERTY) and tlm_blender.in_scene(other) and not other.get("ttt_retarget_source")]


def _pictured(material: bpy.types.Material) -> typing.List[typing.Any]:
    return [item for item in material_settings.settings_of(material).shaders if item.image is not None] if material_settings.is_managed(material) else []


def _identify_drawn(root: bpy.types.Object) -> None:
    """What the model draws that TT Lab will make gets its Blender ID now: undone after it's made, it's asked for again by the same one."""
    from . import materials

    for material, _ in materials._drawn(root).values():
        if not material_settings.is_managed(material):
            continue

        if not material.get(tlm_blender.URI_PROPERTY) and not material.get(tlm_blender.BLENDER_ID_PROPERTY):
            material[tlm_blender.BLENDER_ID_PROPERTY] = uuid.uuid4().hex

        for item in material_settings.settings_of(material).shaders:
            if item.image is not None and not item.image.get(material_settings.TEXTURE_URI_PROPERTY) and not item.image.get(material_settings.IMAGE_ID_PROPERTY):
                item.image[material_settings.IMAGE_ID_PROPERTY] = uuid.uuid4().hex


def _held_paths() -> typing.List[str]:
    return [blender_object.get(tlm_blender.PATH_PROPERTY, "") for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY)
            and tlm_blender.in_scene(blender_object) and not blender_object.get("ttt_retarget_source")]


def _released_paths() -> typing.List[str]:
    """What the blend file's scenes keep of the files released (_release), every one once."""
    paths: typing.List[str] = []
    for scene in bpy.data.scenes:
        try:
            kept = json.loads(scene.get(RELEASED_PROPERTY, "") or "[]")
        except ValueError:
            kept = []

        for path in kept if isinstance(kept, list) else []:
            if isinstance(path, str) and path and not any(_same_file(path, other) for other in paths):
                paths.append(path)

    return paths


# The files released this session, which undo doesn't take back (released at a timer's tick, the scene's list had none of it)
_released_here: typing.List[str] = []


def _release(path: str) -> None:
    """The scene had a model of TT Lab's model of the file and has none now: TT Lab's Edit in Blender forgets the blend file for it once a
    save tells it. Kept with the blend file until a TT Lab heard of it."""
    if not any(_same_file(path, other) for other in _released_here):
        _released_here.append(path)

    paths = _released_paths()
    if not any(_same_file(path, other) for other in paths):
        paths.append(path)
        bpy.context.scene[RELEASED_PROPERTY] = json.dumps(paths)


def forget_released(paths: typing.Sequence[str]) -> None:
    """A TT Lab heard of the files released."""
    _released_here[:] = [path for path in _released_here if not any(_same_file(path, other) for other in paths)]
    for scene in bpy.data.scenes:
        if RELEASED_PROPERTY not in scene:
            continue

        left = [path for path in _released_paths() if not any(_same_file(path, other) for other in paths)]
        if left:
            scene[RELEASED_PROPERTY] = json.dumps(left)
        else:
            del scene[RELEASED_PROPERTY]


def released() -> typing.List[str]:
    """The data files the scene had models of and has none of any more."""
    held = _held_paths()
    return sorted(path for path in _released_paths() if not any(_same_file(path, other) for other in held))


def released_projects(paths: typing.Sequence[str]) -> typing.List[str]:
    """The UUIDs of the projects of released files the scene has no model of any more: their TT Labs forget the blend file for every model
    of them it was remembered for (one TT Lab can't read another project's models)."""
    held = set()
    for path in _held_paths():
        folder = projects.find_root(path)
        if folder is not None:
            held.add(projects.uuid_of(folder))

    result: typing.List[str] = []
    for path in paths:
        folder = projects.find_root(path)
        uuid_text = projects.uuid_of(folder) if folder is not None else ""
        if uuid_text and uuid_text not in held and uuid_text not in result:
            result.append(uuid_text)

    return result


def _marker_of(root: bpy.types.Object) -> typing.Optional[typing.Dict[str, typing.Any]]:
    try:
        marker = json.loads(root.get(REPLACES_PROPERTY, "") or "null")
    except ValueError:
        return None

    return marker if isinstance(marker, dict) else None


def _replacement(root: bpy.types.Object) -> typing.Optional[Replacing]:
    """The TT Lab model the model replaces until TT Lab took it: its marker, while the model is bound to that model's file (renamed, it's
    the model's still) and TT Lab didn't take it already (undo gives a taken marker back). Read only: it's asked while panels draw."""
    marker = _marker_of(root)
    path = root.get(tlm_blender.PATH_PROPERTY, "")
    if marker is None or not _same_file(path, str(marker.get("path", ""))) or (root.name, _normal(path)) in _taken:
        _replacing.pop(root.name, None)
        return None

    entry = _replacing.get(root.name)
    if entry is None or not _same_file(entry.path, path):
        entry = _replacing[root.name] = Replacing(str(marker.get("name", "")), path)

    return entry


def _drop_replacement(root: bpy.types.Object) -> None:
    _replacing.pop(root.name, None)
    if REPLACES_PROPERTY in root:
        del root[REPLACES_PROPERTY]


def replacing(root: bpy.types.Object) -> typing.Optional[str]:
    """The name of the TT Lab model the model replaces once TT Lab takes it, None when it replaces none."""
    entry = _replacement(root)
    return entry.name if entry is not None else None


def _mark_changed(link: Link, root: bpy.types.Object) -> None:
    """TT Lab changed the model since Blender read it: the next sync asks what to do with it. A model put in a TT Lab model's place is
    written over it whatever TT Lab changed of it."""
    if _replacement(root) is not None:
        return

    root[CHANGED_IN_TT_LAB_PROPERTY] = True
    link.changed.add(root.name)


def _tell(level: str, text: str) -> None:
    from . import save_hook

    save_hook.tell(level, text)


# Taking what TT Lab tells

def _idle() -> bool:
    window = bpy.context.window_manager.windows[0] if bpy.context.window_manager and bpy.context.window_manager.windows else None
    if window is not None and len(getattr(window, "modal_operators", [])) > 0:
        return False

    active = bpy.context.view_layer.objects.active if bpy.context.view_layer is not None else None
    return active is None or active.mode == "OBJECT"


def _undo_push(message: str) -> None:
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if len(windows) == 0:
        return

    with bpy.context.temp_override(window=windows[0]):
        bpy.ops.ed.undo_push(message=message)


def _take(link: Link, header: typing.Dict[str, typing.Any], payload: bytes) -> typing.Optional[str]:
    """Applies what TT Lab tells, the undo step's message for it when anything changed."""
    kind = header.get("type")
    if kind in ("material_changed", "relink_material"):
        others = _others(link)
        material = _material_of(link, str(header.get("uri", "")), others)
        if material is None or not isinstance(header.get("data"), dict):
            return None

        settings = material_settings.settings_of(material)
        uri = material[tlm_blender.URI_PROPERTY]
        base = link.state.get(uri) if kind == "material_changed" else None
        known = True if base is not None else settings.known
        theirs = _in_blender_terms(link, header["data"], others)
        if not material_settings.is_managed(material):
            # A project material Blender couldn't read (magenta) has no settings of Blender's: they were taken for all changed, and went
            # over TT Lab's
            merged, sent = theirs, None
        else:
            merged, sent = link_state.relink(base if base is not None else material_settings.base_of(material), known, material_settings.shown(material), theirs)

        unchanged = gs_material.changes(merged, material_settings.shown(material), True)
        if sent is None and material_settings.is_managed(material) and unchanged[1] == [] and unchanged[2] is None:
            # What TT Lab has is what Blender shows, no undo step for it
            link.state[uri] = merged
            return None

        project = _project_with(link, merged, others)
        _fetch_pictures(link, project, merged, others)
        _applying[0] = True
        try:
            material_settings.load(material, merged, project, known=True, blocks=others)
        finally:
            _applying[0] = False

        _marked.pop(material.name, None)
        link.state[uri] = material_settings.base_of(material) or merged
        if sent is not None:
            # What Blender changed goes over TT Lab's, which then has what Blender shows
            link.client.send({"type": "update_material", "uri": uri, "data": sent["data"], "changed": sent["changed"], "shader_sources": sent["shader_sources"], "images": []},
                             answered=lambda header, payload, name=material.name: _merged_sent(link, name, header))

        return "From TT Lab: %s" % material.name

    if kind == "picture_changed":
        image = _picture_of(link, str(header.get("uri", "")))
        if image is not None and header.get("hash") != image.get(material_settings.PNG_CONFIRMED_PROPERTY):
            _get_picture(link, image.name, header["uri"])

        return None

    if kind == "picture":
        image = bpy.data.images.get(header.get("image", ""))
        if image is None or not payload:
            return None

        image.pack(data=payload, data_len=len(payload))
        image.reload()
        image[material_settings.PNG_CONFIRMED_PROPERTY] = hashlib.sha256(payload).hexdigest()
        _picture_hashes[image.name] = image[material_settings.PNG_CONFIRMED_PROPERTY]
        return "From TT Lab: %s" % image.name

    if kind == "renamed":
        uri = str(header.get("uri", ""))
        block = _material_of(link, uri) or _picture_of(link, uri)
        if block is None:
            return None

        new_uri = header.get("new_uri", uri)
        block[tlm_blender.URI_PROPERTY if isinstance(block, bpy.types.Material) else material_settings.TEXTURE_URI_PROPERTY] = new_uri
        if header.get("name"):
            block.name = header["name"]

        link.names.pop(uri, None)
        link.names[new_uri] = block.name
        if uri in link.state:
            link.state[new_uri] = link.state.pop(uri)

        return "From TT Lab: %s renamed" % block.name

    if kind == "deleted":
        blocks = _others(link)
        uri = str(header.get("uri", ""))
        gone = [root for root in _roots() if link.models.get(root.name) == uri]
        for root in gone:
            # The model itself: it stands for no model of TT Lab's, saving would write a file TT Lab doesn't read
            unbind(root)
            _drop_replacement(root)
            del root[tlm_blender.PATH_PROPERTY]
            _tell("WARNING", "%s was deleted in TT Lab: it's a model of no file now" % root.name)

        if gone:
            _redraw()
            return "From TT Lab: %s deleted" % ", ".join(root.name for root in gone)

        material = _material_of(link, uri, blocks)
        if material is not None:
            # Deleted in TT Lab while Blender draws it: Blender makes it again
            material = _made_again(link, material, blocks)
            return "From TT Lab: %s deleted" % material.name

        image = _picture_of(link, uri, blocks)
        if image is not None:
            # A picture too: made again, what draws it then draws the new texture (TT Lab's material drew the placeholder meanwhile)
            image = _made_again(link, image, blocks)
            _failed_pictures.pop(image.name, None)
            for drawing, _ in _linked_materials(link):
                if any(item.image == image for item in _pictured(drawing)):
                    _marked[drawing.name] = time.time() - QUIET

            return "From TT Lab: %s deleted" % image.name

        return None

    if kind == "model_changed":
        # TT Lab's model isn't what Blender has any more (the revision it has: what it wrote itself, a name changed, is no change)
        for root in _roots():
            if link.models.get(root.name) == header.get("uri") and header.get("revision") != root.get(REVISION_PROPERTY):
                _mark_changed(link, root)

        _redraw()
        return None

    if kind == "moved":
        # Moved in TT Lab (another folder or package): the model of its new place and file
        for root in _roots():
            if link.models.get(root.name) == header.get("uri") and header.get("new_uri") and header.get("path"):
                link.moved[_normal(root.get(tlm_blender.PATH_PROPERTY, ""))] = (header["new_uri"], header["path"])
                root[MODEL_URI_PROPERTY] = header["new_uri"]
                root[tlm_blender.PATH_PROPERTY] = header["path"]
                link.models[root.name] = header["new_uri"]
                link.paths[root.name] = header["path"]
                marker = _marker_of(root)
                if marker is not None:
                    root[REPLACES_PROPERTY] = json.dumps(dict(marker, path=header["path"]))

        _redraw()
        return None

    if kind == "edit_in_blender":
        # TT Lab's Edit in Blender of a model the scene has: it's the one selected (another project's of the same URI isn't)
        root = next((root for root in _roots() if link.models.get(root.name) == header.get("uri")), None)
        if root is not None and bpy.context.view_layer is not None:
            for selected in list(bpy.context.selected_objects):
                selected.select_set(False)

            root.select_set(True)
            bpy.context.view_layer.objects.active = root

        return None

    if kind == "use_for":
        # TT Lab's Use an Existing Blend File of this blend file: the model picked of the kind takes its model's place, here
        _run_use_for(header)
        return None

    if kind == "released":
        # Another blend file's model took the model's place: the scene's model of it stands for no model of TT Lab's
        found = []
        for root in _roots():
            if link.models.get(root.name) == header.get("uri"):
                unbind(root)
                _drop_replacement(root)
                del root[tlm_blender.PATH_PROPERTY]
                found.append(root.name)
                _tell("WARNING", "%s isn't TT Lab's %s any more, %s took its place: it's a model of no file now"
                      % (root.name, header.get("name", "model"), header.get("by", "another blend file's model")))

        _redraw()
        return "From TT Lab: %s released" % ", ".join(found) if found else None

    if kind == "closing":
        link.client.close()

    return None


def _run_use_for(header: typing.Dict[str, typing.Any]) -> None:
    arguments = {"model": header.get("model", ""), "asset_type": header.get("asset_type", ""), "name": header.get("name", "")}
    request(arguments["model"], arguments["asset_type"], arguments["name"])
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if len(windows) == 0:
        # No window to ask in (Blender in the background): the file's only model of the kind
        bpy.ops.ttt.use_for_tt_lab_model("EXEC_DEFAULT", True, **arguments)
        return

    with bpy.context.temp_override(window=windows[0]):
        bpy.ops.ttt.use_for_tt_lab_model("INVOKE_DEFAULT", True, **arguments)


def _in_blender_terms(link: Link, data: typing.Dict[str, typing.Any], others: typing.Any) -> typing.Dict[str, typing.Any]:
    """TT Lab's settings of a material with what its shaders draw as Blender knows it: a texture TT Lab made of a picture of Blender's goes
    by the picture's Blender ID, like the material's base (by its URI, every relink took it for a change of picture)."""
    result = json.loads(json.dumps(data))
    for shader in gs_material._shaders(result):
        uri = projects.uri_of(shader.get("TextureId"))
        image = _picture_of(link, uri, others)
        if image is not None:
            shader["Picture"] = material_settings.picture_identity(image)

    return result


def _textures_of(data: typing.Dict[str, typing.Any]) -> typing.List[str]:
    uris = []
    for shader in gs_material._shaders(data):
        uri = projects.uri_of(shader.get("TextureId"))
        if uri and uri != gs_material.EMPTY_URI and uri not in uris:
            uris.append(uri)

    return uris


# While the timer takes what TT Lab told, the scene's blocks by project, by the link's folder: worked out once for all of it
_batch_blocks: typing.Dict[str, typing.Any] = {}
_in_batch = [False]
# What TT Lab tells that changes nothing of what the scene's models draw: the blocks worked out stay right after it
_KEEPS_DRAWING = ("material_changed", "relink_material", "picture_changed", "picture", "renamed", "model_changed")


def _others(link: Link) -> typing.Any:
    """The scene's materials and pictures by their projects (materials.ProjectBlocks) for the link's: TT Lab tells of its project's URIs,
    which another project of the same name has too."""
    from . import materials

    if _in_batch[0] and link.root_folder in _batch_blocks:
        return _batch_blocks[link.root_folder]

    blocks = materials.ProjectBlocks(link.root_folder)
    if _in_batch[0]:
        _batch_blocks[link.root_folder] = blocks

    return blocks


def _material_of(link: Link, uri: str, blocks: typing.Any = None) -> typing.Optional[bpy.types.Material]:
    """The scene's material of the URI of the link's project."""
    found = [material for material in bpy.data.materials if uri and material.get(tlm_blender.URI_PROPERTY) == uri]
    return (blocks if blocks is not None else _others(link)).pick(found) if found else None


def _picture_of(link: Link, uri: str, blocks: typing.Any = None) -> typing.Optional[bpy.types.Image]:
    """The scene's picture of the texture of the URI of the link's project."""
    found = [image for image in bpy.data.images if uri and image.get(material_settings.TEXTURE_URI_PROPERTY) == uri]
    return (blocks if blocks is not None else _others(link)).pick(found) if found else None


def _project_with(link: Link, data: typing.Dict[str, typing.Any], others: typing.Any) -> typing.Optional[projects.Project]:
    """The link's project, read again when it doesn't know a texture the settings draw that the scene has no picture of (made since)."""
    project = projects.open_project_at(link.root_folder)
    if project is not None and any(uri not in project.textures and _picture_of(link, uri, others) is None for uri in _textures_of(data)):
        project = projects.open_project_at(link.root_folder, refresh=True)

    return project


def _fetch_pictures(link: Link, project: typing.Optional[projects.Project], data: typing.Dict[str, typing.Any], others: typing.Any) -> None:
    """The pictures of the textures the settings draw that neither the scene nor the project's files have (TT Lab's, not saved yet), asked of
    TT Lab: the material draws them as TT Lab has them."""
    for uri in _textures_of(data):
        if _picture_of(link, uri, others) is not None or project is not None and uri in project.textures:
            continue

        answer = _ask(link, {"type": "get_picture", "uri": uri})
        if answer is None or answer[0].get("type") != "picture" or not answer[1]:
            continue

        image = material_settings.packed_image(answer[1], str(answer[0].get("name") or uri.rsplit("/", 1)[-1]))
        if image is not None:
            image[material_settings.TEXTURE_URI_PROPERTY] = uri
            image[material_settings.PNG_CONFIRMED_PROPERTY] = hashlib.sha256(answer[1]).hexdigest()
            link.names[uri] = image.name


def _ask(link: Link, header: typing.Dict[str, typing.Any], timeout: float = 5.0) -> typing.Optional[typing.Tuple[typing.Dict[str, typing.Any], bytes]]:
    """Asks TT Lab and waits for the answer, what else comes meanwhile kept for the timer."""
    answers: typing.List[typing.Tuple[typing.Dict[str, typing.Any], bytes]] = []
    link.client.send(header, answered=lambda answer, payload: answers.append((answer, payload)))
    end = time.time() + timeout
    while not answers and not link.client.closed and time.time() < end:
        for pushed, payload in link.client.take():
            _waiting.append((link, pushed, payload))

        if not answers:
            time.sleep(0.01)

    return answers[0] if answers else None


def _merged_sent(link: Link, name: str, header: typing.Dict[str, typing.Any]) -> None:
    material = bpy.data.materials.get(name)
    if material is not None and header.get("type") == "ok":
        link.unsaved.add(material.get(tlm_blender.URI_PROPERTY, ""))


def _get_picture(link: Link, image_name: str, uri: str) -> None:
    link.client.send({"type": "get_picture", "uri": uri},
                     answered=lambda header, payload: _waiting.append((link, dict(header, type="picture", image=image_name), payload)))


# Sending what Blender changed

def _png_hash(image: bpy.types.Image) -> str:
    return hashlib.sha256(tlm_blender._png_bytes(image) or b"").hexdigest()


def _linked_materials(link: Link) -> typing.List[typing.Tuple[bpy.types.Material, bpy.types.Object]]:
    """The materials the link's models draw, with the model drawing each."""
    from . import materials

    result = {}
    with tlm_blender.objects_walked():
        for root in _roots():
            if root.name not in link.models:
                continue

            for name, (material, _) in materials._drawn(root).items():
                result.setdefault(name, (material, root))

    return list(result.values())


def _send_changes(link: Link) -> None:
    now = time.time()
    drawn = _linked_materials(link)
    for material, root in drawn:
        if not material_settings.is_managed(material) or material.name in link.sending:
            continue

        if not material.get(tlm_blender.URI_PROPERTY):
            if not _held_back(root):
                _make_material(link, material, root)
        elif material.name in _marked and now - _marked[material.name] >= QUIET and not _make_pictures(link, material, root):
            _update_material(link, material)

    if now - _last_pictures[0] >= 1.0:
        _last_pictures[0] = now
        _send_pictures(link, drawn)
        _send_renames(link, drawn)


def _make_pictures(link: Link, material: bpy.types.Material, root: bpy.types.Object) -> bool:
    """The material's pictures TT Lab has no texture of become the project's: whether any is still being made. One that can't be read is
    left out, the material drawn without it."""
    images = [item.image for item in material_settings.settings_of(material).shaders if item.image is not None]
    for image in images:
        if not image.get(material_settings.TEXTURE_URI_PROPERTY) and not _cant_read(image):
            _make_picture(link, image, root)

    return any(_to_make(image) for image in images)


def _cant_read(image: bpy.types.Image) -> bool:
    """A picture whose file isn't there (another computer's) can't be read until its file is another."""
    return _unreadable.get(image.name) == image.filepath


def _to_make(image: typing.Optional[bpy.types.Image]) -> bool:
    return image is not None and not image.get(material_settings.TEXTURE_URI_PROPERTY) and not _cant_read(image)


def _make_material(link: Link, material: bpy.types.Material, root: bpy.types.Object) -> None:
    """A material made in Blender becomes the project's, once its pictures are."""
    if material.name in link.making or _make_pictures(link, material, root):
        return

    if not material.get(tlm_blender.BLENDER_ID_PROPERTY):
        material[tlm_blender.BLENDER_ID_PROPERTY] = uuid.uuid4().hex

    files: typing.List[bpy.types.Image] = []
    shown = material_settings.shown(material, files, lambda image: False)
    data, _, _ = gs_material.changes(material_settings.base_of(material), shown, False)
    entry_images = [{"name": image.name, "blender_id": image.get(material_settings.IMAGE_ID_PROPERTY, ""), "uri": image.get(material_settings.TEXTURE_URI_PROPERTY, "")}
                    for image in files]
    link.making.add(material.name)
    link.client.send({"type": "create_material", "owner": link.models.get(root.name, ""), "blender_id": material[tlm_blender.BLENDER_ID_PROPERTY],
                      "name": material.name, "data": data, "images": entry_images},
                     answered=lambda header, payload, name=material.name: _material_made(link, name, header, data, shown))


def _material_made(link: Link, name: str, header: typing.Dict[str, typing.Any], data: typing.Dict[str, typing.Any], shown: typing.Dict[str, typing.Any]) -> None:
    link.making.discard(name)
    material = bpy.data.materials.get(name)
    if material is None or header.get("type") != "created":
        return

    material[tlm_blender.URI_PROPERTY] = header["uri"]
    link.names[header["uri"]] = material.name
    link.made[material.get(tlm_blender.BLENDER_ID_PROPERTY, "")] = header["uri"]
    snapshot = gs_material.snapshot(data, shown)
    material_settings.confirm(material, snapshot)
    link.state[header["uri"]] = snapshot
    link.unsaved.add(header["uri"])


def _make_picture(link: Link, image: bpy.types.Image, root: bpy.types.Object) -> None:
    if image.name in link.making:
        return

    png = tlm_blender._png_bytes(image) or b""
    if not png:
        _unreadable[image.name] = image.filepath
        _tell("WARNING", "The picture %s can't be read (%s): what draws it is made without it until it can" % (image.name, image.filepath or "it has no file"))
        return

    if not image.get(material_settings.IMAGE_ID_PROPERTY):
        image[material_settings.IMAGE_ID_PROPERTY] = uuid.uuid4().hex

    link.making.add(image.name)
    digest = hashlib.sha256(png).hexdigest()
    link.client.send({"type": "create_picture", "owner": link.models.get(root.name, ""), "blender_id": image[material_settings.IMAGE_ID_PROPERTY], "name": image.name},
                     png, answered=lambda header, payload, name=image.name: _picture_made(link, name, header, digest))


def _picture_made(link: Link, name: str, header: typing.Dict[str, typing.Any], digest: str) -> None:
    link.making.discard(name)
    image = bpy.data.images.get(name)
    if image is None or header.get("type") != "created":
        return

    image[material_settings.TEXTURE_URI_PROPERTY] = header["uri"]
    image[material_settings.PNG_CONFIRMED_PROPERTY] = digest
    link.names[header["uri"]] = image.name
    link.made[image.get(material_settings.IMAGE_ID_PROPERTY, "")] = header["uri"]
    link.unsaved.add(header["uri"])


def _update_material(link: Link, material: bpy.types.Material) -> None:
    _marked.pop(material.name, None)
    settings = material_settings.settings_of(material)
    files: typing.List[bpy.types.Image] = []
    shown = material_settings.shown(material, files, lambda image: False)
    uri = material[tlm_blender.URI_PROPERTY]
    state = link.state.get(uri)
    data, changed, sources = gs_material.changes(state if state is not None else material_settings.base_of(material), shown, True if state is not None else settings.known)
    if changed is not None and not changed and sources is None:
        return

    entry_images = [{"name": image.name, "blender_id": image.get(material_settings.IMAGE_ID_PROPERTY, ""), "uri": image.get(material_settings.TEXTURE_URI_PROPERTY, "")}
                    for image in files]
    link.sending.add(material.name)
    link.client.send({"type": "update_material", "uri": uri, "data": data, "changed": changed, "shader_sources": sources, "images": entry_images},
                     answered=lambda header, payload, name=material.name: _material_updated(link, name, header, data, shown))


def _material_updated(link: Link, name: str, header: typing.Dict[str, typing.Any], data: typing.Dict[str, typing.Any], shown: typing.Dict[str, typing.Any]) -> None:
    link.sending.discard(name)
    material = bpy.data.materials.get(name)
    if material is None:
        return

    if header.get("type") == "ok":
        link.state[material.get(tlm_blender.URI_PROPERTY, "")] = gs_material.snapshot(data, shown)
        link.unsaved.add(material.get(tlm_blender.URI_PROPERTY, ""))
    else:
        print("TT Lab didn't take %s: %s" % (name, header.get("text", header.get("type"))))


def _send_renames(link: Link, drawn: typing.List[typing.Tuple[bpy.types.Material, bpy.types.Object]]) -> None:
    """Materials and pictures renamed in Blender are renamed in TT Lab, with a name the game's files can hold."""
    blocks = {}
    for material, _ in drawn:
        blocks.setdefault(material.get(tlm_blender.URI_PROPERTY, ""), material)
        for item in material_settings.settings_of(material).shaders if material_settings.is_managed(material) else []:
            if item.image is not None:
                blocks.setdefault(item.image.get(material_settings.TEXTURE_URI_PROPERTY, ""), item.image)

    for uri, block in blocks.items():
        known = link.names.get(uri)
        if not uri or known is None or known == block.name or block.name in link.sending:
            continue

        if not block.name.isascii():
            continue

        link.names[uri] = block.name
        link.client.send({"type": "rename", "uri": uri, "name": block.name},
                         answered=lambda header, payload, uri=uri: _renamed(link, uri, header))


def _renamed(link: Link, uri: str, header: typing.Dict[str, typing.Any]) -> None:
    if header.get("type") == "ok":
        link.unsaved.add(uri)
    else:
        print("TT Lab didn't rename %s: %s" % (uri, header.get("text", header.get("type"))))


def _send_pictures(link: Link, drawn: typing.List[typing.Tuple[bpy.types.Material, bpy.types.Object]]) -> None:
    """A picture painted is sent once it stopped changing: the same hash two looks in a row."""
    seen = set()
    for material, _ in drawn:
        for item in material_settings.settings_of(material).shaders if material_settings.is_managed(material) else []:
            image = item.image
            if image is None or image.name in seen or not image.get(material_settings.TEXTURE_URI_PROPERTY) or image.name in link.making:
                continue

            seen.add(image.name)
            digest = _png_hash(image)
            if digest == image.get(material_settings.PNG_CONFIRMED_PROPERTY, "") or _failed_pictures.get(image.name) == digest:
                continue

            if _picture_hashes.get(image.name) != digest:
                _picture_hashes[image.name] = digest
                continue

            png = tlm_blender._png_bytes(image) or b""
            link.making.add(image.name)
            link.client.send({"type": "update_picture", "uri": image[material_settings.TEXTURE_URI_PROPERTY]}, png,
                             answered=lambda header, payload, name=image.name, digest=digest: _picture_updated(link, name, header, digest))


def _picture_updated(link: Link, name: str, header: typing.Dict[str, typing.Any], digest: str) -> None:
    link.making.discard(name)
    image = bpy.data.images.get(name)
    if image is not None and header.get("type") == "ok":
        image[material_settings.PNG_CONFIRMED_PROPERTY] = digest
        link.unsaved.add(image.get(material_settings.TEXTURE_URI_PROPERTY, ""))
    elif image is not None:
        # Not sent again until it changes, said once
        _failed_pictures[name] = digest
        _tell("WARNING", "TT Lab didn't take the picture %s: %s" % (name, header.get("text", header.get("type"))))


def tick() -> float:
    now = time.time()
    if now - _last_look[0] >= LOOK_EVERY:
        _last_look[0] = now
        try:
            _look_for_tt_lab()
        except Exception as error:  # noqa: BLE001 - a link that fails is looked for again, never the timer's end
            print("Looking for TT Lab failed: %s" % error)

    for folder, link in list(_links.items()):
        if link.client.closed:
            del _links[folder]
            _redraw()
            continue

        for header, payload in link.client.take():
            _waiting.append((link, header, payload))

        if link.welcomed:
            try:
                _prune(link)
            except Exception:  # noqa: BLE001 - never the timer's end
                traceback.print_exc()

    if _waiting and _idle():
        # What comes while they're taken (answers of what they ask) is taken next time
        batch = list(_waiting)
        del _waiting[:len(batch)]
        messages = []
        _in_batch[0] = True
        try:
            for link, header, payload in batch:
                # A link that closed (the blend file closed, TT Lab quit) told it of another file
                if link not in _links.values():
                    continue

                try:
                    message = _take(link, header, payload)
                except Exception:  # noqa: BLE001 - one message failing leaves the others to be taken
                    traceback.print_exc()
                    continue
                finally:
                    if header.get("type") not in _KEEPS_DRAWING:
                        _batch_blocks.clear()

                if message:
                    messages.append(message)
        finally:
            _in_batch[0] = False
            _batch_blocks.clear()

        if messages:
            _undo_push(messages[0] if len(messages) == 1 else "From TT Lab: %d changes" % len(messages))

    if _finishing and _idle():
        finishing = list(_finishing)
        _finishing.clear()
        for link, root_name, name, warnings in finishing:
            try:
                _finished(link, root_name, name, warnings)
            except Exception as error:  # noqa: BLE001 - taken by the timer, which goes on
                traceback.print_exc()
                _tell("ERROR", "%s replaced %s in TT Lab, but: %s (%s), the system console has the details" % (root_name, name, error, type(error).__name__))

    for link in _links.values():
        if link.welcomed and not link.client.closed and _idle():
            _send_changes(link)
            try:
                _replace_models(link)
            except Exception:  # noqa: BLE001 - a model failing never ends the timer
                traceback.print_exc()

    return 0.2


def _replace_models(link: Link) -> None:
    """Writes the models put in the place of TT Lab's (use_for) once what they draw is the project's, over TT Lab's models whatever TT Lab
    changed of them: the model replaced is a step of its history in TT Lab."""
    from . import checks
    from . import materials

    for root in _roots():
        entry = _replacement(root)
        if entry is None or entry.attempted or entry.is_in_flight() or root.name not in link.models:
            continue

        pending = link.making or link.sending or any(not material.get(tlm_blender.URI_PROPERTY) or any(_to_make(item.image) for item in material_settings.settings_of(material).shaders)
                                                     for material, _ in materials._drawn(root).values() if material_settings.is_managed(material))
        if pending and time.time() - entry.started < REPLACING_WAIT:
            continue

        entry.attempted = True
        try:
            refusal = _write_replacing(link, root, entry, checks)
        except Exception as error:  # noqa: BLE001 - one model failing leaves the others to be written
            traceback.print_exc()
            refusal = "%s doesn't replace %s: %s (%s), the system console has the details" % (root.name, entry.name, error, type(error).__name__)

        if refusal:
            _tell("ERROR", refusal + ". Syncing with TT Lab once it's fixed replaces it")


def _write_replacing(link: Link, root: bpy.types.Object, entry: Replacing, checks: typing.Any) -> typing.Optional[str]:
    """Writes the model over the TT Lab model it takes the place of, whatever TT Lab changed of it. Why it isn't written, None when it's sent."""
    path = root[tlm_blender.PATH_PROPERTY]
    problems = checks.refused(root, path)
    if problems:
        return "%s doesn't replace %s: %s" % (root.name, entry.name, problems[0].text)

    file, file_materials, warnings = tlm_blender.build_file(root, path, linked=True)
    entry.in_flight = True
    entry.link = link
    link.client.send({"type": "write_model", "uri": link.models[root.name], "revision": root.get(REVISION_PROPERTY, ""), "force": True, "replacing": True,
                      "name": root.name, "blend": bpy.data.filepath}, file.to_bytes(),
                     answered=lambda header, payload, root_name=root.name, path=path, name=entry.name, file_materials=file_materials, warnings=warnings:
                     _replaced(link, root_name, path, name, header, file_materials, warnings))
    return None


def _replaced(link: Link, root_name: str, path: str, name: str, header: typing.Dict[str, typing.Any], file_materials: typing.Any,
              warnings: typing.List[str]) -> None:
    """TT Lab took the model in its model's place or said why not (it stays put in place, the next sync writes it again), kept as soon as
    it's answered: a save meanwhile doesn't write it again, closing meanwhile doesn't take back what TT Lab saved with it. Taken, what
    Blender made for it is saved in TT Lab, and the blend file is saved once the user isn't editing, unless the blend file's save writing it
    is still going on (answered later, it was written with the model still waiting): TT Lab's Edit in Blender opens it for the model from
    then on."""
    entry = _replacing.get(root_name)
    if entry is not None and entry.link is link:
        entry.in_flight = False

    root = bpy.data.objects.get(root_name)
    if root is None or not _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), path):
        return

    if header.get("type") in ("ok", "refused"):
        root[TT_LAB_PROBLEMS_PROPERTY] = json.dumps(header.get("problems") or [])

    if header.get("type") != "ok":
        _tell("ERROR", "TT Lab didn't take %s for %s: %s. Syncing with TT Lab once it's fixed replaces it" % (root_name, name, header.get("text", header.get("type"))))
        return

    _taken.add((root_name, _normal(path)))
    _drop_replacement(root)
    root[REVISION_PROPERTY] = header.get("revision", "")
    if CHANGED_IN_TT_LAB_PROPERTY in root:
        del root[CHANGED_IN_TT_LAB_PROPERTY]

    link.record(root)
    file_materials.commit()
    if _blend_saving[0]:
        # The blend file's save going on writes it, what it made saved and taken
        _told_replaced(root, name, warnings, False)
        return

    if link.unsaved:
        link.client.send({"type": "save", "uris": sorted(uri for uri in link.unsaved if uri)})
        link.unsaved.clear()

    _confirm_taken(link)
    _finishing.append((link, root_name, name, warnings))


def _finished(link: Link, root_name: str, name: str, warnings: typing.List[str]) -> None:
    """The blend file saved once TT Lab took a model in its model's place, the user not editing. A blend file of a newer Blender is left to
    the user to save, Blender asks first."""
    root = bpy.data.objects.get(root_name)
    if root is None:
        return

    newer = tuple(bpy.data.version) > tuple(bpy.app.version_file)
    if not newer:
        _save_blend(root_name)

    _told_replaced(root, name, warnings, newer)


def _told_replaced(root: bpy.types.Object, name: str, warnings: typing.List[str], newer: bool) -> None:
    for warning in warnings:
        _tell("WARNING", warning)

    for problem in tt_lab_problems(root):
        _tell("WARNING", "TT Lab: %s" % problem["text"])

    _redraw()
    if newer:
        _tell("WARNING", "%s replaced %s in TT Lab. %s was saved by a newer Blender (%s): save it yourself to make it %s's blend file, Blender asks first"
              % (root.name, name, os.path.basename(bpy.data.filepath), ".".join(str(number) for number in bpy.data.version), name))
    else:
        _tell("INFO", "%s replaced %s in TT Lab, this blend file is its from now on" % (root.name, name))


def _save_blend(replaced: str) -> None:
    """Saves the blend file: the model just written in its TT Lab model's place as it is, the others as any save of the add-on's leaves them
    (written when saving always syncs)."""
    from . import save_hook

    if not bpy.data.filepath:
        return

    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    with save_hook.leaving_models_alone({replaced}):
        if len(windows) > 0:
            with bpy.context.temp_override(window=windows[0]):
                bpy.ops.wm.save_mainfile()
        else:
            bpy.ops.wm.save_mainfile()


def write_models(force: bool = False, skip: typing.Collection[str] = (), only: typing.Optional[typing.Collection[str]] = None) -> typing.List[str]:
    """Writes every linked model through TT Lab and saves what Blender made and changed, the models of no link into their files, but the
    ones skipped (or only the ones named). Returns what kept models from being written. Forced, the models TT Lab changed since get
    Blender's. Edit Mode is left meanwhile: Blender puts what's edited into the bones and meshes only once it writes the blend file, and hides
    a mesh's UVs and attributes until then."""
    with _out_of_edit_mode():
        return _write_models(force, skip, only)


class _out_of_edit_mode:
    """Object Mode within it, Edit Mode again after for what was in it."""

    def __enter__(self):
        layer = bpy.context.view_layer
        self.active = layer.objects.active if layer is not None else None
        self.editing = [blender_object for blender_object in layer.objects if blender_object.mode == "EDIT"] if layer is not None else []
        if not self.editing:
            return

        # Edit Mode is left through the active object, one of those in it
        if self.active not in self.editing:
            layer.objects.active = self.editing[0]

        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except RuntimeError:
            self.editing = []
            layer.objects.active = self.active

    def __exit__(self, *_):
        if not self.editing:
            return

        layer = bpy.context.view_layer
        try:
            bpy.ops.object.mode_set(mode="EDIT")
        except RuntimeError:
            pass

        layer.objects.active = self.active


# The saving in progress waits for TT Lab's answers to its writes, at most this long: the blend file keeps the revisions they give
ANSWERS_WAIT = 10.0
_saving = [False]
# While the blend file's save writes the models (save_hook): a replacement TT Lab takes meanwhile is written with the blend file
_blend_saving = [False]


def _write_models(force: bool, skip: typing.Collection[str], only: typing.Optional[typing.Collection[str]]) -> typing.List[str]:
    from . import checks

    refused = []
    _saving[0] = True
    try:
        # What TT Lab is answering first: a model it's taking in its model's place is that model's from then on, and a link being made
        # links its models
        _wait_for_answers()
        for link in list(_links.values()):
            if link.welcomed and not link.client.closed:
                _prune(link)

        for root in _roots():
            if root.name in skip or only is not None and root.name not in only:
                continue

            try:
                refusal = _write_model(root, force, checks)
            except Exception as error:  # noqa: BLE001 - one model failing leaves the others to be written
                traceback.print_exc()
                refusal = "%s isn't written: %s (%s), the system console has the details" % (root.name, error, type(error).__name__)

            if refusal:
                refused.append(refusal)

        _wait_for_answers()
    finally:
        _saving[0] = False

    _save_assets()
    return refused


def save_assets() -> None:
    """TT Lab saves what Blender made and changed of the project's materials and pictures, which it has already (unsaved until then): the
    blend file being saved keeps them as TT Lab has them, so closing Blender doesn't take back what the blend file keeps (a picture painted,
    taken back, was taken for changed in TT Lab when the blend file was opened again). Writing the models is syncing's (write_models)."""
    _wait_for_answers()
    _save_assets()


def _save_assets() -> None:
    for link in _links.values():
        if not link.welcomed or link.client.closed:
            continue

        if link.unsaved:
            link.client.send({"type": "save", "uris": sorted(uri for uri in link.unsaved if uri)})
            link.unsaved.clear()

        _confirm_taken(link)


def _confirm_taken(link: Link) -> None:
    """What TT Lab has is the base the blend file keeps, linking again merges with it: of what the link's models draw (another project's
    materials of its URIs, or ones nothing draws, keep theirs)."""
    for material, _ in _linked_materials(link):
        state = link.state.get(material.get(tlm_blender.URI_PROPERTY, ""))
        if state is not None and material_settings.is_managed(material):
            material_settings.confirm(material, state)


def _wait_for_answers() -> None:
    """Takes TT Lab's answers to what was sent before the blend file is written: what they say of the models (their revisions) is what the
    blend file keeps. Answered later, its models were taken for changed in TT Lab when it was opened again."""
    links = [link for link in _links.values() if not link.client.closed]
    end = time.time() + ANSWERS_WAIT
    # A link that closes meanwhile answers nothing more
    while any(link.client.pending() and not link.client.closed for link in links) and time.time() < end:
        for link in links:
            for header, payload in link.client.take():
                _waiting.append((link, header, payload))

        time.sleep(0.01)


def _write_model(root: bpy.types.Object, force: bool, checks: typing.Any) -> typing.Optional[str]:
    """Writes the model through TT Lab while it's linked, into its file otherwise. Why it isn't written, None when it is (or sent). A model
    put in a TT Lab model's place is written over it while it's linked, and waits for that otherwise."""
    path = root[tlm_blender.PATH_PROPERTY]
    if not os.path.isabs(path):
        return "%s isn't written: %s isn't a file of this computer, export it to a model file of a project" % (root.name, path)

    folder = projects.find_root(path)
    opening = _links.get(folder) if folder else None
    opening = opening if opening is not None and not opening.client.closed else None
    link = opening if opening is not None and opening.welcomed else None
    entry = _replacement(root)
    if entry is not None:
        if link is None or root.name not in link.models:
            return "%s isn't written: it replaces %s once TT Lab has the project open" % (root.name, entry.name)

        if entry.is_in_flight():
            return "%s isn't written: TT Lab is taking it in %s's place, sync again once it says so" % (root.name, entry.name)

        entry.attempted = True
        return _write_replacing(link, root, entry, checks)

    problems = checks.refused(root, path)
    if problems:
        return "%s isn't written: %s" % (root.name, problems[0].text)

    # Into its file past the TT Lab linking it, TT Lab would read it again as another program's change
    if opening is not None and (link is None or (root.name, _normal(path)) in link.linking):
        return "%s isn't written: TT Lab is linking it, sync again in a moment" % root.name

    if opening is not None and _held_back(root):
        return "%s isn't written while a model of the kind is being picked to replace %s" % (root.name, (_request[0] or {}).get("name", "TT Lab's"))

    if link is not None and _same_file(link.unlinkable.get(root.name, ""), path):
        # TT Lab has no model of the file (moved or deleted in TT Lab): written into it, it'd be a file TT Lab doesn't read
        return "%s isn't written: TT Lab has no model of %s, export it to a model file of the project" % (root.name, os.path.basename(path))

    if link is None or root.name not in link.models:
        # No TT Lab has the project open
        tlm_blender.export_file(root, path)
        return None

    if root.get(CHANGED_IN_TT_LAB_PROPERTY) and not force:
        return "%s was changed in TT Lab since: overwrite it, take TT Lab's or skip it" % root.name

    file, materials, _ = tlm_blender.build_file(root, path, linked=True)
    link.client.send({"type": "write_model", "uri": link.models[root.name], "revision": root.get(REVISION_PROPERTY, ""), "force": force}, file.to_bytes(),
                     answered=lambda header, payload, name=root.name, materials=materials: _model_written(link, name, header, materials))
    return None


def _model_written(link: Link, name: str, header: typing.Dict[str, typing.Any], materials: typing.Any) -> None:
    root = bpy.data.objects.get(name)
    if root is None:
        return

    if header.get("type") in ("ok", "refused"):
        root[TT_LAB_PROBLEMS_PROPERTY] = json.dumps(header.get("problems") or [])

    if header.get("type") == "ok":
        root[REVISION_PROPERTY] = header.get("revision", "")
        if CHANGED_IN_TT_LAB_PROPERTY in root:
            del root[CHANGED_IN_TT_LAB_PROPERTY]

        link.record(root)
        materials.commit()
    elif header.get("type") == "conflict":
        _mark_changed(link, root)
        # A save or a sync asks once its writes are done (save_hook), else it's asked now
        if not _saving[0]:
            from . import save_hook

            save_hook.ask_about_changes()
    else:
        _tell("WARNING", "TT Lab didn't write %s: %s" % (name, header.get("text", header.get("type"))))


def _prune(link: Link) -> None:
    """The linked models the scene doesn't have any more (deleted, renamed, given another file): TT Lab forgets them, the files they stood
    for are released unless another model of the scene has them. One coming back (undo) links again, another named like it links its own."""
    for name, uri in list(link.models.items()):
        root = bpy.data.objects.get(name)
        path = link.paths.get(name, "")
        # The model linked, not another of its name (it deleted, its file imported again): that one links its own
        linked = root is not None and link.uids.get(name, root.get(tlm_blender.UID_PROPERTY, "")) == root.get(tlm_blender.UID_PROPERTY, "")
        if linked and tlm_blender.in_scene(root) and root.get(tlm_blender.ROOT_PROPERTY) and _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), path):
            continue

        link.forget(name)
        if uri and uri not in link.models.values() and link.welcomed and not link.client.closed:
            link.client.send({"type": "unlink_model", "uri": uri})

        if path and not any(_same_file(path, other) for other in _held_paths()):
            _release(path)

        _redraw()


def standing_for(link: Link) -> typing.List[str]:
    """The URIs of the link's models the scene has as TT Lab has them: not changed in TT Lab since, not waiting to take another's place.
    TT Lab's Edit in Blender opens the blend file for them."""
    result: typing.List[str] = []
    for root in _roots():
        uri = link.models.get(root.name)
        if uri and uri not in result and _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), link.paths.get(root.name, "")) \
                and not root.get(CHANGED_IN_TT_LAB_PROPERTY) and _replacement(root) is None:
            result.append(uri)

    return result


def overwriting(root: bpy.types.Object) -> None:
    """Blender's model goes over what TT Lab changed of it (the save's question): written forced."""
    if CHANGED_IN_TT_LAB_PROPERTY in root:
        del root[CHANGED_IN_TT_LAB_PROPERTY]

    root[REVISION_PROPERTY] = ""
    for link in _links.values():
        if root.name in link.models:
            link.record(root)


# How long Take TT Lab's waits for TT Lab's model, which TT Lab answers once it answered what the save sent before it
TAKE_WAIT = 20.0


def take_tt_labs(context: bpy.types.Context, root: bpy.types.Object, path: str) -> typing.Optional[bpy.types.Object]:
    """The model imported again in its place (the save's Take TT Lab's, Import the Model Again): as TT Lab has it while it's linked (its
    unsaved changes too, linked again so its materials are TT Lab's as well: imported, they're the project's files'), else its file. None
    when TT Lab didn't give it: the model stays as it is, what TT Lab said of it too (its file isn't TT Lab's model)."""
    from . import tlm

    name = root.name
    link = link_of(root)
    if link is not None and name in link.models and _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), path):
        answer = _ask(link, {"type": "get_model", "uri": link.models[name]}, TAKE_WAIT)
        file = None
        if answer is not None and answer[0].get("type") == "model" and answer[1]:
            try:
                file = tlm.TlmFile.from_bytes(answer[1])
            except (tlm.TlmError, ValueError):
                file = None

        if file is None:
            _tell("ERROR", "TT Lab didn't give %s (%s): it's as it was, try again" % (name, (answer[0].get("text") or answer[0].get("type")) if answer else "no answer"))
            return None

        new_root = tlm_blender.reimport(context, root, path, file)
        new_root[REVISION_PROPERTY] = answer[0].get("revision", "")
        # TT Lab's model of the URI, linked again so what it draws is TT Lab's too
        new_root[MODEL_URI_PROPERTY] = link.models[name]
        link.forget(name)
        _link_model(link, new_root)
        _redraw()
        return new_root

    new_root = tlm_blender.reimport(context, root, path)
    for each in _links.values():
        # Linked again, TT Lab says what it has of the file
        each.forget(name)

    _last_look[0] = 0.0
    _redraw()
    return new_root


def tt_lab_problems(root: bpy.types.Object) -> typing.List[typing.Dict[str, typing.Any]]:
    """What TT Lab found when it last took the model."""
    try:
        problems = json.loads(root.get(TT_LAB_PROBLEMS_PROPERTY, "[]"))
    except ValueError:
        return []

    return [problem for problem in problems if isinstance(problem, dict) and problem.get("text")]


def drop_all() -> None:
    """The blend file closes without being saved: TT Lab drops what Blender made and changed since it was saved."""
    for link in _links.values():
        if link.welcomed and not link.client.closed:
            link.client.send({"type": "drop"})

        link.client.close()

    _links.clear()
    _replacing.clear()
    _taken.clear()
    _released_here.clear()
    _finishing.clear()
    # What waits to be taken was told of the file that closes
    _waiting.clear()
    _marked.clear()
    _picture_hashes.clear()
    _unreadable.clear()
    _failed_pictures.clear()
    _request[0] = None
    _last_request[0] = None


@bpy.app.handlers.persistent
def _undone(*_: typing.Any) -> None:
    """Undo and redo change materials without their update callbacks: every linked one is looked at again. They take back what TT Lab said
    of the models since (their URIs and revisions, the URIs of what it made of Blender's, the files released), which TT Lab still has: given
    back. Undo bringing back another model of the name (the one Take TT Lab's put another in the place of) brings back what it had, and a
    model undo gave its earlier file again is the model of that file again with what it had of it: linked again, TT Lab says whether it
    changed it since."""
    now = time.time()
    for link in _links.values():
        for name, uri in list(link.models.items()):
            root = bpy.data.objects.get(name)
            if root is None:
                continue

            moved = link.moved.get(_normal(root.get(tlm_blender.PATH_PROPERTY, "")))
            if moved is not None and _same_file(moved[1], link.paths.get(name, "")):
                # The file it had before TT Lab moved the model: it's the model of the new place still
                root[tlm_blender.PATH_PROPERTY] = moved[1]

            if not _same_file(root.get(tlm_blender.PATH_PROPERTY, ""), link.paths.get(name, "")):
                # Its earlier file again: the one it stood for released unless another model has it
                previous = link.paths.get(name, "")
                link.forget(name)
                if uri and uri not in link.models.values() and link.welcomed and not link.client.closed:
                    link.client.send({"type": "unlink_model", "uri": uri})

                _replacing.pop(name, None)
                if previous and not any(_same_file(previous, other) for other in _held_paths()):
                    _release(previous)

                _last_look[0] = 0.0
                continue

            root[MODEL_URI_PROPERTY] = uri
            if link.uids.get(name, "") != root.get(tlm_blender.UID_PROPERTY, ""):
                link.record(root)
                continue

            root[REVISION_PROPERTY] = link.revisions.get(name, "")
            # What TT Lab said changed stays whatever the undo
            if name in link.changed:
                root[CHANGED_IN_TT_LAB_PROPERTY] = True
            elif CHANGED_IN_TT_LAB_PROPERTY in root:
                del root[CHANGED_IN_TT_LAB_PROPERTY]

        for block, key, identity in [(material, tlm_blender.URI_PROPERTY, tlm_blender.BLENDER_ID_PROPERTY) for material in bpy.data.materials] + \
                [(image, material_settings.TEXTURE_URI_PROPERTY, material_settings.IMAGE_ID_PROPERTY) for image in bpy.data.images]:
            made = link.made.get(block.get(identity, ""))
            if made and not block.get(key):
                block[key] = made

        for material, _ in _linked_materials(link):
            if material_settings.is_managed(material) and material.get(tlm_blender.URI_PROPERTY):
                _marked[material.name] = now - QUIET

    for path in _released_here:
        _release(path)

    # Undo gives back the markers of replacements TT Lab took: they're taken all the same
    for root in _roots():
        if REPLACES_PROPERTY in root and (root.name, _normal(root.get(tlm_blender.PATH_PROPERTY, ""))) in _taken:
            del root[REPLACES_PROPERTY]


def register():
    if not bpy.app.timers.is_registered(tick):
        bpy.app.timers.register(tick, first_interval=1.0, persistent=True)

    for handlers in (bpy.app.handlers.undo_post, bpy.app.handlers.redo_post):
        if _undone not in handlers:
            handlers.append(_undone)


def unregister():
    if bpy.app.timers.is_registered(tick):
        bpy.app.timers.unregister(tick)

    for handlers in (bpy.app.handlers.undo_post, bpy.app.handlers.redo_post):
        if _undone in handlers:
            handlers.remove(_undone)

    drop_all()
