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

"""Syncing writes the blend file's models: through TT Lab while they're linked (and saves what Blender made and changed in the project),
into their own files without it, Edit Mode left meanwhile. The Twin Tech Model panel's Sync with TT Lab syncs, and saving the blend file
syncs too, asks once it's saved or leaves it to the button, as the add-on's preferences say (sync_on_save): writing every model takes a
while. Every save has TT Lab save what Blender made and changed of materials and pictures, which TT Lab has already (save_assets). A model refused by the checks isn't written and the status bar says why (tell), a blend file being saved is saved anyway. A model
TT Lab changed since Blender read it is only written once the user says what to do with it, asked right after the sync (save handlers
can't wait). Opening another blend file drops what the scene made or changed in TT Lab and didn't sync."""

import json
import os
import typing

import bpy
from bpy.app.handlers import persistent

from . import link_sync
from . import project as projects
from . import tlm_blender

_refused: typing.List[str] = []
# Saving a blend file made of the models as they are (TT Lab's Edit in Blender) writes nothing of them (True), or not the ones named (a model
# TT Lab just took in its model's place)
_leaving_models: typing.List[typing.Any] = [None]
# What the save tells the TT Labs linked of the files released, by link: the blend file is saved without them
_telling: typing.List[typing.Tuple[typing.Any, typing.List[str]]] = []
_telling_projects: typing.List[str] = []


class leaving_models_alone:
    """Saves within it don't write the models, or the ones named."""

    def __init__(self, names: typing.Optional[typing.Collection[str]] = None):
        self.names = names

    def __enter__(self):
        _leaving_models[0] = set(self.names) if self.names is not None else True

    def __exit__(self, *_):
        _leaving_models[0] = None


# The released files the save tells of, out of the blend file it writes: put back when the save fails
_forgotten: typing.List[str] = []
# Whether the save in progress wrote the models (Always on Save)
_wrote = [False]

SYNC_CHOICES = [
    ("ALWAYS", "Always on Save", "Saving the blend file writes the models through TT Lab (into their files without it) and saves what Blender made and "
     "changed in the project, which takes a while with big models"),
    ("ASK", "Prompt to Sync on Save", "Once the blend file is saved a popup asks whether to sync the models with TT Lab, and saves it again after"),
    ("MANUAL", "Only Manually", "Saving only saves the blend file, the Twin Tech Model panel's Sync with TT Lab writes the models")]
# What saving does when the add-on's preferences aren't there (the add-on run from a script, like its tests)
SYNC_ON_SAVE = "ASK"


class TTT_AddonPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__

    sync_on_save: bpy.props.EnumProperty(name="Sync with TT Lab", items=SYNC_CHOICES, default="ASK",
                                         description="When the models are written through TT Lab (into their files without it)")

    def draw(self, context):
        self.layout.prop(self, "sync_on_save")


def preferences() -> typing.Optional[TTT_AddonPreferences]:
    addon = bpy.context.preferences.addons.get(__package__)
    return addon.preferences if addon is not None else None


def sync_on_save() -> str:
    """What saving the blend file does with its models: ALWAYS writes them, ASK asks once it's saved, MANUAL leaves them to the button."""
    found = preferences()
    return found.sync_on_save if found is not None else SYNC_ON_SAVE


def _is_main_file(path: typing.Any) -> bool:
    """Whether the blend file saved is the one open, not a copy of it."""
    return not isinstance(path, str) or not path or os.path.normcase(os.path.abspath(path)) == os.path.normcase(os.path.abspath(bpy.data.filepath))


@persistent
def _before_save(*_: typing.Any) -> None:
    leaving = _leaving_models[0]
    _wrote[0] = leaving is not True and sync_on_save() == "ALWAYS"
    # A replacement TT Lab takes while the models are written is written with the blend file, one taken later saves the blend file again
    link_sync._blend_saving[0] = True
    try:
        _refused[:] = link_sync.write_models(skip=leaving or ()) if _wrote[0] else []
        if not _wrote[0] and leaving is not True:
            link_sync.save_assets()
    finally:
        link_sync._blend_saving[0] = False

    # The files released that the TT Labs linked hear of go out of the blend file (they're told once it's saved), the others stay in it
    # for a TT Lab of their project to hear of once it's linked: each TT Lab of its own project's, every one of the projects the blend file
    # has no model of any more
    released = link_sync.released()
    links = [link for link in link_sync._links.values() if link.welcomed and not link.client.closed]
    _telling[:] = [(link, [path for path in released if projects.find_root(path) == link.root_folder]) for link in links]
    _telling_projects[:] = link_sync.released_projects(released) if links else []
    told = [path for _, paths in _telling for path in paths]
    told += [path for path in released if projects.find_root(path) is not None and projects.uuid_of(projects.find_root(path)) in _telling_projects]
    _forgotten[:] = told
    link_sync.forget_released(told)


@persistent
def _save_failed(*_: typing.Any) -> None:
    """The blend file wasn't written: what it had of the files released is kept for the next save to tell."""
    for path in _forgotten:
        link_sync._release(path)

    _forgotten.clear()
    _telling.clear()
    _telling_projects.clear()


@persistent
def _after_save(path: typing.Any = None, *_: typing.Any) -> None:
    # TT Lab's Edit in Blender opens this blend file for the models linked from now on, and not for the ones it no longer has
    # Every save, the blend file's path (a Save As) and the models it has as TT Lab has them
    for link, paths in list(_telling):
        if link.welcomed and not link.client.closed:
            link.client.send({"type": "blend_saved", "path": bpy.data.filepath, "released": paths, "released_projects": list(_telling_projects),
                              "models": link_sync.standing_for(link)})

    _telling.clear()
    _telling_projects.clear()
    _forgotten.clear()
    if _wrote[0]:
        _wrote[0] = False
        _tell_written(list(_refused))
    elif _leaving_models[0] is None and sync_on_save() == "ASK" and _is_main_file(path) and link_sync._roots():
        # Asked once the save is done: the add-on's own saves and copies of the blend file ask nothing
        ask_to_sync()


def _tell_written(refused: typing.List[str]) -> None:
    """Says what kept models from being written and asks what to do with the ones TT Lab changed since: a conflict TT Lab answered while
    the models were written marks the model only."""
    for text in refused:
        tell("WARNING", text)

    if any(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and link_sync.replacing(root) is None for root in link_sync._roots()):
        ask_about_changes()


def sync() -> typing.List[str]:
    """Writes the scene's models (through TT Lab while they're linked, into their files without it) and saves what Blender made and changed
    in the project. Returns what kept models from being written, which is said too."""
    refused = link_sync.write_models()
    _tell_written(refused)
    return refused


# What's said in the status bar and the Info editor once the timer can: the level of each and its text
_told: typing.List[typing.Tuple[str, str]] = []


def tell(level: str, text: str) -> None:
    """Says it in the status bar and the Info editor (and the console): reports of an operator Python runs to its end only reach the
    console, a modal operator's go there."""
    print("Twin Tech: %s" % text)
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if len(windows) == 0:
        # Nothing reports them (Blender in the background), the last ones are kept for what looks
        _told.append((level, text))
        del _told[:-100]
        return

    if not _told:
        bpy.app.timers.register(_tell, first_interval=0.1)

    _told.append((level, text))


def _tell() -> None:
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if windows and _told:
        with bpy.context.temp_override(window=windows[0]):
            bpy.ops.ttt.tell_unwritten("INVOKE_DEFAULT")

    return None


def ask_to_sync() -> None:
    """Asks whether to sync the models with TT Lab, once the timer can: the save in progress can't wait for an answer. Blender in the
    background asks nothing."""
    if not bpy.app.timers.is_registered(_ask_to_sync):
        bpy.app.timers.register(_ask_to_sync, first_interval=0.1)


def _ask_to_sync() -> None:
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if windows and link_sync._roots():
        with bpy.context.temp_override(window=windows[0]):
            bpy.ops.ttt.sync_after_save("INVOKE_DEFAULT")

    return None


def ask_about_changes() -> None:
    """Asks what to do with the models TT Lab changed since Blender read them, once the timer can."""
    if not bpy.app.timers.is_registered(_ask):
        bpy.app.timers.register(_ask, first_interval=0.1)


def _ask() -> None:
    windows = bpy.context.window_manager.windows if bpy.context.window_manager is not None else []
    if not windows:
        return None

    with bpy.context.temp_override(window=windows[0]):
        bpy.ops.ttt.resolve_tt_lab_changes("INVOKE_DEFAULT")

    return None


@persistent
def _before_load(*_: typing.Any) -> None:
    link_sync.drop_all()


class TTT_OT_ResolveTTLabChanges(bpy.types.Operator):
    """TT Lab changed models since Blender read them: write Blender's over them, take TT Lab's (the model imported again in its place) or
    leave them for now"""

    bl_idname = "ttt.resolve_tt_lab_changes"
    bl_label = "Models Changed in TT Lab"
    bl_options = {"REGISTER", "UNDO"}

    choice: bpy.props.EnumProperty(name="Changed in TT Lab", items=[
        ("OVERWRITE", "Write Blender's Over Them", "TT Lab gets the models as Blender has them, its changes to them go"),
        ("TAKE", "Take TT Lab's", "The models are imported again from TT Lab, what Blender changed of them since goes"),
        ("SKIP", "Leave Them", "Nothing is written, asked again at the next sync")], default="SKIP")

    # The models asked about, as the question showed them (JSON): one TT Lab changed while it was up isn't answered for
    names: bpy.props.StringProperty(options={"HIDDEN", "SKIP_SAVE"})

    def invoke(self, context, event):
        self.names = json.dumps([root.name for root in self._changed()])
        return context.window_manager.invoke_props_dialog(self, width=420)

    @staticmethod
    def _changed():
        # A model put in a TT Lab model's place is written over it whatever TT Lab changed of it
        return [root for root in link_sync._roots() if root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and link_sync.replacing(root) is None]

    def _asked(self):
        names = json.loads(self.names) if self.names else None
        return [root for root in self._changed() if names is None or root.name in names]

    def draw(self, context):
        layout = self.layout
        for root in self._asked():
            layout.label(text=root.name, icon="OBJECT_DATA")

        layout.prop(self, "choice", expand=True)

    def execute(self, context):
        roots = self._asked()
        if self.choice == "SKIP" or not roots:
            return {"CANCELLED"}

        if self.choice == "OVERWRITE":
            # Only these are forced: the save asking wrote the others, which a change TT Lab made since would conflict for
            for root in roots:
                link_sync.overwriting(root)

            for text in link_sync.write_models(force=True, only={root.name for root in roots}):
                tell("WARNING", text)

            return {"FINISHED"}

        for root in roots:
            path = root.get(tlm_blender.PATH_PROPERTY, "")
            if path:
                # Not given, it's said why and stays as it was
                link_sync.take_tt_labs(context, root, path)

        return {"FINISHED"}


class TTT_OT_SyncWithTTLab(bpy.types.Operator):
    """Writes the scene's models through the TT Lab that has their project open (into their files without it) and saves what Blender made and
    changed in the project. Saving the blend file does it too, or asks, as the add-on's preferences say"""

    bl_idname = "ttt.sync_with_tt_lab"
    bl_label = "Sync with TT Lab"
    # An undo step: what TT Lab answers (the models' revisions) is kept with the blend file, which Blender then asks to save
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if not link_sync._roots():
            cls.poll_message_set("The scene has no model of a model file: import one, or add it to TT Lab's project")
            return False

        return True

    def execute(self, context):
        count = len(link_sync._roots())
        refused = sync()
        if refused:
            self.report({"WARNING"}, "%d of %d models synced with TT Lab: %s" % (count - len(refused), count, refused[0]))
        else:
            self.report({"INFO"}, "%d %s synced with TT Lab" % (count, "model" if count == 1 else "models"))

        return {"FINISHED"}


class TTT_OT_SyncAfterSave(bpy.types.Operator):
    """The blend file is saved: its models synced with TT Lab (written through TT Lab, into their files without it, and what Blender made and
    changed in the project saved) and the blend file saved again, keeping what TT Lab answered"""

    bl_idname = "ttt.sync_after_save"
    bl_label = "Sync with TT Lab"
    bl_options = {"INTERNAL"}

    # The models listed, more go in a line
    MOST_LISTED = 8

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self, width=420, title="Sync with TT Lab?", confirm_text="Sync")

    def draw(self, context):
        layout = self.layout
        roots = link_sync._roots()
        layout.label(text="Write the models:")
        for root in roots[:self.MOST_LISTED]:
            layout.label(text=root.name if link_sync.is_linked(root) else "%s, into its file" % root.name,
                         icon="LINKED" if link_sync.is_linked(root) else "FILE_3D")

        if len(roots) > self.MOST_LISTED:
            layout.label(text="and %d more" % (len(roots) - self.MOST_LISTED))

        found = preferences()
        if found is not None:
            layout.prop(found, "sync_on_save", text="Saving")

    def execute(self, context):
        sync()
        if bpy.data.filepath:
            with leaving_models_alone():
                bpy.ops.wm.save_mainfile()

        return {"FINISHED"}


class TTT_OT_TellUnwritten(bpy.types.Operator):
    """Says what happened to TT Lab's models: which ones the blend file's save didn't write and why, a model put in a TT Lab model's place"""

    bl_idname = "ttt.tell_unwritten"
    bl_label = "Twin Tech"
    bl_options = {"INTERNAL"}

    def execute(self, context):
        told = list(_told)
        _told.clear()
        for level, text in told:
            self.report({level}, text)

        return {"FINISHED"}

    # Reports of an operator Python runs to its end only go to the console, a modal one's to the status bar and the Info editor
    def invoke(self, context, event):
        self._timer = context.window_manager.event_timer_add(0.0, window=context.window)
        context.window_manager.modal_handler_add(self)
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        if event.type != "TIMER":
            return {"PASS_THROUGH"}

        context.window_manager.event_timer_remove(self._timer)
        return self.execute(context)


class TTT_OT_SaveScene(bpy.types.Operator):
    """Saves the blend file. A new one of a TT Lab project's model goes into the project's source folder, unless another place is picked"""

    bl_idname = "ttt.save_scene"
    bl_label = "Save"
    bl_options = {"INTERNAL"}

    def invoke(self, context, event):
        if bpy.data.filepath:
            bpy.ops.wm.save_mainfile("INVOKE_DEFAULT")
            return {"FINISHED"}

        root = next((root for root in bpy.data.objects if root.get(tlm_blender.ROOT_PROPERTY) and root.get(tlm_blender.PATH_PROPERTY)
                     and projects.find_root(root[tlm_blender.PATH_PROPERTY]) is not None), None)
        if root is None:
            bpy.ops.wm.save_mainfile("INVOKE_DEFAULT")
            return {"FINISHED"}

        folder = os.path.join(projects.find_root(root[tlm_blender.PATH_PROPERTY]), "source")
        os.makedirs(folder, exist_ok=True)
        bpy.ops.wm.save_as_mainfile("INVOKE_DEFAULT", filepath=os.path.join(folder, bpy.path.clean_name(root.name) + ".blend"))
        return {"FINISHED"}


_keymaps: typing.List[typing.Tuple[typing.Any, typing.Any]] = []


def register():
    for handlers, handler in ((bpy.app.handlers.save_pre, _before_save), (bpy.app.handlers.save_post, _after_save), (bpy.app.handlers.save_post_fail, _save_failed),
                              (bpy.app.handlers.load_pre, _before_load)):
        if handler not in handlers:
            handlers.append(handler)

    # Ctrl+S of a new scene suggests the project's source folder
    window_manager = bpy.context.window_manager
    keyconfig = window_manager.keyconfigs.addon if window_manager is not None else None
    if keyconfig is not None:
        keymap = keyconfig.keymaps.new(name="Window", space_type="EMPTY")
        _keymaps.append((keymap, keymap.keymap_items.new(TTT_OT_SaveScene.bl_idname, "S", "PRESS", ctrl=True)))


def unregister():
    for handlers, handler in ((bpy.app.handlers.save_pre, _before_save), (bpy.app.handlers.save_post, _after_save), (bpy.app.handlers.save_post_fail, _save_failed),
                              (bpy.app.handlers.load_pre, _before_load)):
        if handler in handlers:
            handlers.remove(handler)

    for keymap, item in _keymaps:
        keymap.keymap_items.remove(item)

    _keymaps.clear()
