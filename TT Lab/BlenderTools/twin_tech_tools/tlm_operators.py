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

"""Importing and exporting TT Lab model files, and picking the project's materials."""

import os
import traceback
import typing

import bpy
from bpy_extras.io_utils import ExportHelper, ImportHelper

from . import checks
from . import collision_builder
from . import materials
from . import tlm
from . import tlm_blender
from . import tlm_scenery
from . import tlm_templates


class TTT_OT_ImportTlm(bpy.types.Operator, ImportHelper):
    """Imports a TT Lab model file. Models of a TT Lab project show the project's materials. A model the scene has of the file is put in
    its place after asking"""

    bl_idname = "ttt.import_tlm"
    bl_label = "Import TT Lab Model"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})
    as_copy: bpy.props.BoolProperty(name="Import as a Copy", description="Imports the model next to the one the scene has of the file, instead of "
                                                                         "putting it in that one's place")

    def execute(self, context):
        existing = tlm_blender.model_of_file(self.filepath)
        if existing is not None and not self.as_copy:
            return bpy.ops.ttt.reimport_tlm("INVOKE_DEFAULT", filepath=self.filepath, root=existing.name)

        try:
            root = tlm_blender.import_file(context, self.filepath)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't import %s: %s" % (self.filepath, error))
            return {"CANCELLED"}

        for selected in context.selected_objects:
            selected.select_set(False)

        root.select_set(True)
        context.view_layer.objects.active = root
        return {"FINISHED"}


class TTT_OT_EditFromTTLab(bpy.types.Operator):
    """Makes the blend file TT Lab's Edit in Blender edits a model in: the model imported into the scene and the scene saved where TT Lab
    says, without writing the model (it's as TT Lab has it). Linked once TT Lab is found"""

    bl_idname = "ttt.edit_from_tt_lab"
    bl_label = "Edit a TT Lab Model"
    bl_options = {"INTERNAL"}

    model: bpy.props.StringProperty(options={"HIDDEN"})
    blend: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        from . import save_hook

        try:
            root = tlm_blender.import_file(context, self.model)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't import %s: %s" % (self.model, error))
            return {"CANCELLED"}

        for selected in context.selected_objects:
            selected.select_set(False)

        root.select_set(True)
        context.view_layer.objects.active = root
        os.makedirs(os.path.dirname(self.blend) or ".", exist_ok=True)
        with save_hook.leaving_models_alone():
            bpy.ops.wm.save_as_mainfile(filepath=self.blend)

        return {"FINISHED"}


# How the kinds of model files are said, a blend file's model of the kind replaces a TT Lab model of it
_KIND_NAMES = {"Ogi": "game model", "Scenery": "scenery", "Skydome": "skydome", "Mesh": "model", "Model": "model", "SaveIcon": "save icon",
               "DynamicScenery": "dynamic scenery", "Collision": "collision", "RigidModel": "rigid model", "Skin": "skin", "BlendSkin": "blend skin"}
# The blend file's models the dialog offers, kept: Blender reads the strings of a dynamic enum's items after the callback returned
_candidate_items: typing.List[typing.Tuple[str, str, str]] = []


def _candidate_enum(self, context):
    from . import link_sync

    _candidate_items[:] = [(root.name, root.name, root.get(tlm_blender.PATH_PROPERTY, "") or "Made in Blender") for root in link_sync.candidates(self.asset_type)] \
        or [("NONE", "No model", "The blend file has no model of the kind")]
    return _candidate_items


class TTT_OT_TTLabRequest(bpy.types.Operator):
    """Keeps what TT Lab's Use an Existing Blend File asks for while Blender starts: until a model of the kind is picked, the scene's models of
    the kind aren't linked to TT Lab (they'd be their files' models meanwhile)"""

    bl_idname = "ttt.tt_lab_request"
    bl_label = "TT Lab's Request"
    bl_options = {"INTERNAL"}

    model: bpy.props.StringProperty(options={"HIDDEN"})
    asset_type: bpy.props.StringProperty(options={"HIDDEN"})
    name: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        from . import link_sync

        link_sync.request(self.model, self.asset_type, self.name)
        return {"FINISHED"}


class TTT_OT_UseForTTLabModel(bpy.types.Operator):
    """Puts a model of the blend file in a TT Lab model's place (TT Lab's Use an Existing Blend File): the model is given the TT Lab model's
    file and replaces it through TT Lab once it's linked, when the checks and TT Lab take it, and the blend file is saved, the model's from
    then on. Asks which model when the file has several of the kind"""

    bl_idname = "ttt.use_for_tt_lab_model"
    bl_label = "Replace TT Lab's Model"
    bl_options = {"UNDO", "INTERNAL"}

    model: bpy.props.StringProperty(options={"HIDDEN"})
    asset_type: bpy.props.StringProperty(options={"HIDDEN"})
    name: bpy.props.StringProperty(options={"HIDDEN"})
    root: bpy.props.EnumProperty(name="Model", description="The model of the blend file that replaces TT Lab's", items=_candidate_enum)

    def invoke(self, context, event):
        from . import link_sync

        link_sync.request(self.model, self.asset_type, self.name)
        candidates = link_sync.candidates(self.asset_type)
        # The Twin Tech Model panel's button says which
        if len(candidates) < 2 or self.properties.is_property_set("root"):
            return self.execute(context)

        own = tlm_blender.model_of_file(self.model)
        self.root = own.name if own in candidates else candidates[0].name
        return context.window_manager.invoke_props_dialog(self, width=480, title="Which model replaces %s?" % self.name, confirm_text="Replace")

    def draw(self, context):
        self.layout.label(text="The model picked replaces %s in TT Lab" % self.name)
        self.layout.prop(self, "root")

    def cancel(self, context):
        from . import link_sync
        from . import save_hook

        link_sync.drop_request()
        save_hook.tell("INFO", "Nothing replaces %s: select a model of the kind and use the Twin Tech Model panel's Replace %s in TT Lab" % (self.name, self.name))

    # Run without invoking (Blender in the background never invokes), the file's only model of the kind: an unset choice reads as its first
    def execute(self, context):
        from . import link_sync
        from . import save_hook

        candidates = link_sync.candidates(self.asset_type)
        picked = self.root if self.properties.is_property_set("root") else ""
        root = next((candidate for candidate in candidates if picked and candidate.name == picked), None) or (candidates[0] if len(candidates) == 1 else None)
        if root is None:
            link_sync.drop_request()
            kind = _KIND_NAMES.get(self.asset_type, self.asset_type + " model")
            save_hook.tell("ERROR", "%s has no %s to replace %s with: import or make one in it and use the Twin Tech Model panel's Replace %s in TT Lab"
                           % (os.path.basename(bpy.data.filepath) or "The blend file", kind, self.name, self.name) if not candidates
                           else "%s has %d %ss, pick the one that replaces %s" % (os.path.basename(bpy.data.filepath) or "The blend file", len(candidates), kind, self.name))
            return {"CANCELLED"}

        _in_object_mode(context)
        # Selected first: nothing fails once it's bound
        _select_only(context, root)
        link_sync.use_for(root, self.model, self.name)
        save_hook.tell("INFO", "%s replaces %s in TT Lab once Blender linked it" % (root.name, self.name))
        return {"FINISHED"}


def _link_problem(root, path):
    """The name of the TT Lab model whose model file the model can take the place of (Link to Another TT Lab Model), or why it can't: the
    file has to be a model of a project's assets of the model's kind, not the one it stands for."""
    from . import link_sync
    from . import project as projects

    if not path or not os.path.isfile(path):
        return None, "Pick a TT Lab model file (.tlm)"

    file_name = os.path.basename(path)
    asset = projects.model_asset_of(path)
    if asset is None:
        return None, "%s isn't a model of a TT Lab project: pick the model file of one in a project's assets folder" % file_name

    try:
        kind = str(tlm.TlmFile.read_json(path).get("asset", {}).get("type", ""))
    except (OSError, tlm.TlmError, ValueError) as error:
        return None, "%s can't be read: %s" % (file_name, error)

    wanted = root.get(tlm_blender.ROOT_PROPERTY, "")
    if kind != wanted:
        return None, "%s is a %s's model file, %s is a %s: pick a %s's" % (file_name, _KIND_NAMES.get(kind, kind + " model"), root.name,
                                                                         _KIND_NAMES.get(wanted, wanted + " model"), _KIND_NAMES.get(wanted, wanted + " model"))

    if link_sync._same_file(root.get(tlm_blender.PATH_PROPERTY, ""), path):
        return None, "%s stands for %s already" % (root.name, asset["name"])

    return asset["name"], None


# What the file browser's side panel last said of a pick, worked out again once the pick, its file or the model changes: it's drawn often
_link_checked: typing.Dict[typing.Tuple[typing.Any, ...], typing.Tuple[typing.Optional[str], typing.Optional[str]]] = {}


def _checked_link(root, path):
    try:
        status = os.stat(path)
        stamp = (status.st_mtime_ns, status.st_size)
    except OSError:
        stamp = None

    key = (root.name, root.get(tlm_blender.ROOT_PROPERTY, ""), root.get(tlm_blender.PATH_PROPERTY, ""), path, stamp)
    if key not in _link_checked:
        _link_checked.clear()
        _link_checked[key] = _link_problem(root, path)

    return _link_checked[key]


class TTT_OT_LinkToTTLabModel(bpy.types.Operator, ImportHelper):
    """Puts the model in the place of another TT Lab model, picked by its model file (.tlm) in a project's assets: the model is written over
    it through TT Lab once Blender linked it, and the blend file is that model's from then on. The model it stood for keeps what TT Lab has
    of it"""

    bl_idname = "ttt.link_to_tt_lab_model"
    bl_label = "Link to TT Lab Model"
    # Not registered: the redo panel would draw the side panel's text, saying the model stands for the file already
    bl_options = {"UNDO"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})
    root: bpy.props.StringProperty(options={"HIDDEN"})

    # Always offered: pressing it says what's wrong
    @classmethod
    def poll(cls, context):
        return True

    def _root(self, context):
        """The model named, else the active object's or the one the selection is of, and what's wrong when there's none."""
        root = bpy.data.objects.get(self.root) if self.root else None
        if root is not None and root.get(tlm_blender.ROOT_PROPERTY):
            return root, None

        root, problem = _model_to_export(context)
        return root, problem.replace("to export", "to link") if problem else None

    def invoke(self, context, event):
        from . import link_protocol

        root, problem = self._root(context)
        if root is None:
            self.report({"ERROR"}, "Can't link: %s" % problem)
            return {"CANCELLED"}

        self.root = root.name
        # The folder of the model's file, else the assets of the project TT Lab has open, else the blend file's: nothing picked yet
        path = root.get(tlm_blender.PATH_PROPERTY, "")
        running = link_protocol.any_tt_lab()
        project = running.get("project") if running is not None else None
        for folder in (os.path.dirname(path) if path and os.path.isabs(path) else "",
                       os.path.join(project.get("path", ""), "assets") if isinstance(project, dict) and project.get("path") else "",
                       os.path.dirname(bpy.data.filepath)):
            if folder and os.path.isdir(folder):
                self.filepath = os.path.join(folder, "")
                break

        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def draw(self, context):
        from . import checks_ui

        root, _ = self._root(context)
        if root is None:
            return

        width = max(24, int(context.region.width / 7.5) - 6) if context.region is not None else 40
        path = bpy.path.abspath(self.filepath)
        # The pick as the file browser has it (it hands the operator the file selected before it asks the operator's check)
        name, problem = _checked_link(root, path) if os.path.basename(path) else (None, None)
        box = self.layout.box()
        if problem is not None:
            checks_ui._wrapped(box, problem, width, "ERROR")
        elif name is not None:
            checks_ui._wrapped(box, "%s takes %s's place" % (root.name, name), width, "LINKED")
        else:
            checks_ui._wrapped(box, "Pick the model file of the TT Lab model %s takes the place of" % root.name, width, "INFO")

        checks_ui._wrapped(self.layout, "TT Lab gets it written over that model once Blender linked it, one step of that model's history, and the "
                                        "blend file is saved: it's that model's blend file from then on.", width, "BLANK1")
        if root.get(tlm_blender.PATH_PROPERTY):
            checks_ui._wrapped(self.layout, "The model it stands for now keeps what TT Lab has of it.", width, "BLANK1")

    def execute(self, context):
        from . import link_protocol
        from . import link_sync
        from . import project as projects

        root, problem = self._root(context)
        if root is None:
            self.report({"ERROR"}, "Can't link: %s" % problem)
            return {"CANCELLED"}

        path = bpy.path.abspath(self.filepath)
        name, problem = _link_problem(root, path)
        if problem is not None:
            self.report({"ERROR"}, problem)
            return {"CANCELLED"}

        _in_object_mode(context)
        _select_only(context, root)
        # Two models can't write one file: the scene's other model of it stands for none from now on
        others = [other.name for other in bpy.data.objects if other != root and other.get(tlm_blender.ROOT_PROPERTY) and not other.get("ttt_retarget_source")
                  and link_sync._same_file(other.get(tlm_blender.PATH_PROPERTY, ""), path)]
        link_sync.use_for(root, path, name)
        if others:
            self.report({"WARNING"}, "%s stood for %s, it's a model of no file now" % (", ".join(others), name))

        folder = projects.find_root(path)
        if link_protocol.tt_lab_of(folder, projects.uuid_of(folder)) is not None:
            self.report({"INFO"}, "%s takes %s's place in TT Lab once Blender linked it" % (root.name, name))
        else:
            self.report({"INFO"}, "%s takes %s's place in TT Lab once TT Lab has the project open" % (root.name, name))

        return {"FINISHED"}


class TTT_OT_ReimportTlm(bpy.types.Operator):
    """Puts the file's model in the place of the scene's model of it: what was changed in Blender since goes with it"""

    bl_idname = "ttt.reimport_tlm"
    bl_label = "Import the Model Again"
    bl_options = {"REGISTER", "UNDO", "INTERNAL"}

    filepath: bpy.props.StringProperty(options={"HIDDEN"})
    root: bpy.props.StringProperty(options={"HIDDEN"})

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event, title="Import %s again?" % os.path.basename(self.filepath),
                                                     message="%s is put in its place, what was changed in Blender since goes" % self.root,
                                                     confirm_text="Import Again", icon="WARNING")

    def execute(self, context):
        root = bpy.data.objects.get(self.root)
        if root is None or not root.get(tlm_blender.ROOT_PROPERTY):
            self.report({"ERROR"}, "%s isn't a model of the scene any more" % self.root)
            return {"CANCELLED"}

        from . import link_sync

        _in_object_mode(context)
        try:
            # A linked model as TT Lab has it
            new_root = link_sync.take_tt_labs(context, root, self.filepath)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't import %s: %s" % (self.filepath, error))
            return {"CANCELLED"}

        if new_root is None:
            self.report({"ERROR"}, "TT Lab didn't give %s: it's as it was, try again" % self.root)
            return {"CANCELLED"}

        _select_only(context, new_root)
        self.report({"INFO"}, "%s is the file's model again" % new_root.name)
        return {"FINISHED"}


def _model_to_export(context):
    """The model the export writes, the active object's or else the one the selection is of, and what's wrong when there's none."""
    root = tlm_blender.find_root(context.object)
    if root is not None:
        return root, None

    roots = []
    for selected in context.selected_objects:
        found = tlm_blender.find_root(selected)
        if found is not None and found not in roots:
            roots.append(found)

    if len(roots) == 1:
        return roots[0], None

    if len(roots) > 1:
        return None, "Several models are selected (%s): select the one to export, or click it last" % ", ".join(found.name for found in roots)

    if context.object is None:
        return None, "Nothing is selected: select the model to export, its root or anything under it"

    return None, ("%s isn't part of a TT Lab model: a model is what's under the root an import or Add > Twin Tech makes. Select the model, or put "
                  "%s under a model's root (an armature and its meshes under an OGI's)" % (context.object.name, context.object.name))


class TTT_OT_ExportTlm(bpy.types.Operator, ExportHelper):
    """Exports the selected TT Lab model back into a TT Lab model file, the file it came from by default"""

    bl_idname = "ttt.export_tlm"
    bl_label = "Export TT Lab Model"
    bl_options = {"REGISTER"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})

    # Always offered: pressing it says what keeps the scene from being exported
    @classmethod
    def poll(cls, context):
        return True

    def _check(self, context, path=None):
        """The model to export to the path, its own file by default, None when something keeps it from being exported (checks)."""
        root, problem = _model_to_export(context)
        if problem is not None:
            self.report({"ERROR"}, "Can't export: %s" % problem)
            return None

        materials.make_drawn_twin_tech(root)
        refused = checks.refused(root, path)
        for found in refused[1:]:
            self.report({"ERROR"}, found.text)

        if refused:
            more = " (and %d more, the Twin Tech Checks panel lists them)" % (len(refused) - 1) if len(refused) > 1 else ""
            self.report({"ERROR"}, "Can't export: %s%s" % (refused[0].text, more))
            return None

        return root

    def invoke(self, context, event):
        root = self._check(context)
        if root is None:
            return {"CANCELLED"}

        source = root.get(tlm_blender.PATH_PROPERTY, "")
        if source:
            self.filepath = source

        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        _in_object_mode(context)
        root = self._check(context, self.filepath)
        if root is None:
            return {"CANCELLED"}

        context.view_layer.update()
        try:
            warnings = tlm_blender.export_file(root, self.filepath)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't export %s: %s" % (os.path.basename(self.filepath), error))
            return {"CANCELLED"}
        except Exception as error:  # noqa: BLE001 - a setup nothing checks for yet, said instead of Blender's Python error
            traceback.print_exc()
            self.report({"ERROR"}, "Couldn't export %s: %s (%s), the system console has the details" % (os.path.basename(self.filepath), error, type(error).__name__))
            return {"CANCELLED"}

        for warning in warnings:
            self.report({"WARNING"}, warning)

        self.report({"INFO"}, "Exported %s" % os.path.basename(self.filepath))
        return {"FINISHED"}


def _select_only(context, blender_object):
    for selected in context.selected_objects:
        selected.select_set(False)

    blender_object.select_set(True)
    context.view_layer.objects.active = blender_object


def _in_object_mode(context):
    if context.object is not None and context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


class TTT_OT_NewOgi(bpy.types.Operator):
    """Makes a new OGI model to build a game object's model in: its root, an armature of joint 0, a box (its rigid body) and the holders
    the exit points and collision hulls go in. Rig meshes to the armature the way Blender rigs them (Ctrl+P), add bones and animations as
    needed and export it over an OGI's file in a TT Lab project to make that OGI the model"""

    bl_idname = "ttt.new_ogi"
    bl_label = "OGI Model"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_ogi(context)
        _select_only(context, root)
        return {"FINISHED"}


class TTT_OT_NewSkydome(bpy.types.Operator):
    """Makes a new sky to build a level's sky in: its root and a dome drawn with its vertex colors and a sky's material. Export it over a
    skydome's file in a TT Lab project to make that skydome the sky"""

    bl_idname = "ttt.new_skydome"
    bl_label = "Skydome"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_skydome(context)
        _select_only(context, root)
        return {"FINISHED"}


class TTT_OT_NewModel(bpy.types.Operator):
    """Makes a new model sceneries and LODs place: its root and a box with a picture. Export it over a mesh's file in a TT Lab project to make
    that mesh the model"""

    bl_idname = "ttt.new_model"
    bl_label = "Model"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_model(context)
        _select_only(context, root)
        return {"FINISHED"}


class TTT_OT_NewScenery(bpy.types.Operator):
    """Makes a new scenery to build a level's scenery in: its root with lighting on, the tree node the game culls it with holding a
    ground mesh, an ambient and a directional light, the ground again as the collision with a placeholder surface and an empty
    dynamic scenery. Build on it and export it over a scenery's file in a TT Lab project to make that scenery the level's"""

    bl_idname = "ttt.new_scenery"
    bl_label = "Scenery"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_scenery(context)
        _select_only(context, root)
        return {"FINISHED"}


class TTT_OT_NewSaveIcon(bpy.types.Operator):
    """Makes a new PS2 memory card icon: its root with the game's icon's settings, a box mesh with a material made in Blender whose
    picture becomes the icon's 128x128 texture. Add shape keys for its shapes and keys on them for its animation, and export it over the
    startup folder's Crash.ico file in a TT Lab project"""

    bl_idname = "ttt.new_save_icon"
    bl_label = "PS2 Save Icon"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_save_icon(context)
        _select_only(context, root)
        return {"FINISHED"}


class TTT_MT_Add(bpy.types.Menu):
    """New models to build in Blender, laid out the way TT Lab's are"""

    bl_idname = "TTT_MT_add"
    bl_label = "Twin Tech"

    def draw(self, context):
        self.layout.operator(TTT_OT_NewOgi.bl_idname, icon="OUTLINER_OB_ARMATURE")
        self.layout.operator(TTT_OT_NewSkydome.bl_idname, icon="WORLD_DATA")
        self.layout.operator(TTT_OT_NewModel.bl_idname, icon="MESH_CUBE")
        self.layout.separator()
        self.layout.operator(TTT_OT_NewScenery.bl_idname, icon="WORLD")
        self.layout.operator(TTT_OT_NewSaveIcon.bl_idname, icon="IMAGE_DATA")


_surface_items_cache = []


def _surface_items(self, context):
    """The collision's surfaces first, then the file's other surface materials, the placeholder surface when there's none"""
    root = tlm_blender.find_root(context.object) if context is not None else None
    collision = tlm_scenery.collision_of(root) if root is not None else None
    names = [material.name for material in collision.data.materials if material is not None] if collision is not None else []
    names += sorted(material.name for material in bpy.data.materials if material.users > 0 and tlm_scenery.is_surface(material) and material.name not in names)
    if not names:
        names.append(tlm_templates.DEFAULT_SURFACE)

    _surface_items_cache[:] = [(name, name, "Every triangle made goes on this surface") for name in names]
    return _surface_items_cache


class TTT_OT_GenerateCollision(bpy.types.Operator):
    """Makes the scenery's collision out of the meshes it draws, the selected object's and every mesh under it or the whole
    scenery's (the closest level of LODs, never the dynamic scenery), every triangle on one surface. It's as coarse as the game's:
    the game only collides the player with 32 triangles at a time and slows Crash down to a fifth where there are more, so meshes
    become their convex hulls where those stay close and the rest is made coarser. Corners closer than the weld distance become one
    vertex and flat triangles are left out, adding to the collision leaves out the triangles it already has"""

    bl_idname = "ttt.generate_collision"
    bl_label = "Generate Collision"
    bl_options = {"REGISTER", "UNDO"}

    source: bpy.props.EnumProperty(name="From", items=[
        ("SELECTED", "Selected Object", "The selected object's mesh and every mesh under it"),
        ("SCENERY", "Whole Scenery", "Every mesh of the scenery"),
    ], default="SELECTED")
    mode: bpy.props.EnumProperty(name="Collision", items=[
        ("REPLACE", "Replace", "The collision is made anew out of the meshes"),
        ("ADD", "Add", "The meshes' triangles are added to the collision, but the ones it has"),
    ], default="REPLACE")
    surface: bpy.props.EnumProperty(name="Surface", items=_surface_items, description="The collision surface every triangle made goes on")
    weld: bpy.props.FloatProperty(name="Weld Distance", default=0.001, min=0.0, soft_max=0.1, precision=4,
                                  description="Corners closer than this, in the game's units, become one vertex")
    flip: bpy.props.BoolProperty(name="Wound Like The Game's", default=True,
                                 description="Turns the triangles around, the game winds its collision so a triangle's normal points into the solid (a floor's down)")
    tolerance: bpy.props.FloatProperty(name="Tolerance", default=collision_builder.TOLERANCE, min=0.0, soft_max=0.5, precision=3,
                                       description="How far, in the game's units, the collision may be from the meshes. 0 with a hull distance of 0 keeps every triangle")
    hull_distance: bpy.props.FloatProperty(name="Hull Distance", default=collision_builder.HULL_DISTANCE, min=0.0, soft_max=1.0, precision=3,
                                           description="A mesh, or a part of one, becomes its convex hull while the hull stays this close to it: gaps narrower than about twice it get filled (Crash is 1.2 units across). 0 makes no hulls")
    coarser_where_crowded: bpy.props.BoolProperty(name="Coarser Where Crowded", default=True,
                                                  description="Makes a mesh coarser still while Crash standing on it would touch more triangles than the game takes")

    @classmethod
    def poll(cls, context):
        root = tlm_blender.find_root(context.object)
        return root is not None and root.get(tlm_blender.KIND_PROPERTY) == "scenery"

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        root = tlm_blender.find_root(context.object)
        if root is None:
            self.report({"ERROR"}, "Select an object of a scenery")
            return {"CANCELLED"}

        start = root if self.source == "SCENERY" else context.object
        if not tlm_scenery.collision_sources(start):
            self.report({"ERROR"}, "%s has no mesh the scenery draws" % start.name)
            return {"CANCELLED"}

        _in_object_mode(context)
        surface = bpy.data.materials.get(self.surface)
        if not tlm_scenery.is_surface(surface):
            surface = tlm_scenery.placeholder_surface(self.surface or tlm_templates.DEFAULT_SURFACE)

        result, crowding = tlm_scenery.generate_collision(root, start, surface, self.mode == "REPLACE", self.weld, self.flip, self.tolerance,
                                                          self.hull_distance, self.coarser_where_crowded)
        self.report({"INFO"}, "%d triangles on %s made of the meshes' %d: %d convex hulls, %d meshes made coarser where crowded, %d layers lying on others left out, %d triangles already there and %d flat ones"
                    % (len(result.triangles), surface.name, result.sources, result.hulls, result.coarsened, result.covered, result.skipped, result.dropped))
        if crowding.places:
            self.report({"WARNING"}, "Crash would touch up to %d collision triangles at %d places, the most at (%.1f, %.1f, %.1f) in the game's space: the game only takes %d at a time and slows him down where there are more. Leave out small meshes there or raise the tolerance"
                        % (crowding.most, crowding.places, *crowding.worst, collision_builder.MOST_TRIANGLES))

        return {"FINISHED"}


class TTT_PT_Model(bpy.types.Panel):
    bl_label = "Twin Tech Model"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"

    @classmethod
    def poll(cls, context):
        return tlm_blender.find_root(context.object) is not None

    def draw(self, context):
        from . import link_sync
        from . import project as projects
        from . import save_hook

        layout = self.layout
        root = tlm_blender.find_root(context.object)
        path = root.get(tlm_blender.PATH_PROPERTY, "")
        layout.label(text=os.path.basename(path) if path else root.name, icon="FILE_3D")
        folder = projects.find_root(path) if path else None
        if link_sync.is_linked(root):
            layout.label(text="Linked to TT Lab: what changes goes there, syncing writes the model", icon="LINKED")
            if root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and link_sync.replacing(root) is None:
                layout.label(text="Changed in TT Lab since: syncing asks what to do with it", icon="ERROR")
        elif folder is not None:
            layout.label(text="TT Lab doesn't have the project open: syncing writes the model into its file", icon="UNLINKED")

        if path:
            row = layout.row(align=True)
            row.operator(save_hook.TTT_OT_SyncWithTTLab.bl_idname, icon="FILE_REFRESH")
            preferences = save_hook.preferences()
            if preferences is not None:
                row.prop(preferences, "sync_on_save", text="")

        request = link_sync.last_request()
        if request is not None and root.get(tlm_blender.ROOT_PROPERTY) == request["asset_type"] and link_sync.replacing(root) is None \
                and not (path and _same_path(path, request["model"])):
            replace = layout.operator(TTT_OT_UseForTTLabModel.bl_idname, text="Replace %s in TT Lab" % request["name"], icon="FILE_REFRESH")
            replace.model, replace.asset_type, replace.name, replace.root = request["model"], request["asset_type"], request["name"], root.name
        elif link_sync.replacing(root) is not None:
            layout.label(text="Replaces %s in TT Lab once TT Lab takes it" % link_sync.replacing(root), icon="FILE_REFRESH")

        link = layout.operator(TTT_OT_LinkToTTLabModel.bl_idname, text="Link to Another TT Lab Model..." if folder is not None else "Link to a TT Lab Model...",
                               icon="LINKED")
        link.root = root.name
        row = layout.row(align=True)
        row.operator(TTT_OT_ExportTlm.bl_idname, icon="EXPORT")
        row.operator(TTT_OT_OpenInTTLab.bl_idname, icon="WINDOW", text="")
        if folder is None and root.get(tlm_blender.ROOT_PROPERTY) in NEW_ASSET_TYPES:
            layout.operator(TTT_OT_AddToTTLab.bl_idname, icon="ADD")
        if root.get(tlm_blender.KIND_PROPERTY) == "scenery":
            layout.operator(TTT_OT_GenerateCollision.bl_idname, icon="MOD_PHYSICS")


def _same_path(path, other):
    return os.path.normcase(os.path.abspath(path)) == os.path.normcase(os.path.abspath(other))


# The kinds of models made in Blender TT Lab makes assets of
NEW_ASSET_TYPES = ("Ogi", "Skydome", "Mesh")
# The folders the running TT Lab offers, for the dialog's choice
_new_asset_folders: typing.List[typing.Tuple[str, str, str]] = []


def _connect_to_running_tt_lab(running=None):
    """A link to the running TT Lab with a project open (the newest, else the one of the discovery file given), welcomed, or why there's
    none."""
    from . import link_client
    from . import link_protocol

    running = link_protocol.any_tt_lab() if running is None else running
    if running is None or not running.get("project"):
        return None, "No TT Lab with a project open runs"

    try:
        client = link_client.Client(int(running["port"]))
    except (OSError, KeyError, ValueError) as error:
        return None, "TT Lab didn't answer: %s" % error

    welcome = client.ask({"type": "hello", "token": running.get("token", ""), "protocol": link_protocol.PROTOCOL, "blend": "", "blender": bpy.app.version_string,
                          "addon": tlm_blender.ADDON_VERSION})
    if welcome is None or welcome[0].get("type") != "welcome":
        client.close()
        return None, "TT Lab didn't link: %s" % (welcome[0].get("text", "") if welcome else "no answer")

    return client, (running.get("project") or {}).get("name", "")


class TTT_OT_AddToTTLab(bpy.types.Operator):
    """Makes the model made in Blender an asset of the project the running TT Lab has open, in the folder picked: its materials and pictures
    become the model's own, and the model is linked to TT Lab from then on"""

    bl_idname = "ttt.add_to_tt_lab"
    bl_label = "Add to TT Lab's Project"
    bl_options = {"REGISTER"}

    name: bpy.props.StringProperty(name="Name", description="The new asset's name, plain ASCII")
    folder: bpy.props.EnumProperty(name="Folder", description="Where in the project the model goes",
                                   items=lambda self, context: _new_asset_folders or [("", "No folder", "")])

    @classmethod
    def poll(cls, context):
        from . import link_protocol

        root = tlm_blender.find_root(context.object)
        if root is None or root.get(tlm_blender.ROOT_PROPERTY) not in NEW_ASSET_TYPES:
            cls.poll_message_set("TT Lab makes assets of a game model, a skydome or a model made in Blender")
            return False

        running = link_protocol.any_tt_lab()
        if running is None or not running.get("project"):
            cls.poll_message_set("Open the project in TT Lab first")
            return False

        return True

    def invoke(self, context, event):
        client, why = _connect_to_running_tt_lab()
        if client is None:
            self.report({"ERROR"}, why)
            return {"CANCELLED"}

        answer = client.ask({"type": "new_model_targets"})
        client.close()
        folders = answer[0].get("folders", []) if answer is not None else []
        _new_asset_folders[:] = [(folder["uri"], folder["label"], folder["uri"]) for folder in folders if isinstance(folder, dict) and folder.get("uri")]
        if not _new_asset_folders:
            self.report({"ERROR"}, "TT Lab's project has no folder models are made in")
            return {"CANCELLED"}

        root = tlm_blender.find_root(context.object)
        self.name = root.name
        return context.window_manager.invoke_props_dialog(self, width=480, title="Add to %s" % why)

    def execute(self, context):
        from . import link_protocol
        from . import link_sync

        root = tlm_blender.find_root(context.object)
        name = self.name.strip()
        if not name or not name.isascii():
            self.report({"ERROR"}, "Give the model a name of plain ASCII")
            return {"CANCELLED"}

        running = link_protocol.any_tt_lab()
        client, why = _connect_to_running_tt_lab(running)
        if client is None:
            self.report({"ERROR"}, why)
            return {"CANCELLED"}

        # The model is the project's from now on, like a model exported into its files or put in a model's place: what it draws of another
        # project (an imported model's materials and pictures) is made its own, what other projects' models draw too copied for it. Any
        # file in the project's folder tells separate the project
        into = os.path.join(running["project"].get("path", ""), "assets", name + ".tlm")
        link_sync.separate(root, into, before_settings=True)
        materials.make_drawn_twin_tech(root)
        link_sync.separate(root, into, before_settings=False)
        file, _, warnings = tlm_blender.build_file(root, "")
        answer = client.ask({"type": "create_model", "name": name, "folder": self.folder}, file.to_bytes(), timeout=60.0)
        client.close()
        if answer is None or answer[0].get("type") != "created":
            self.report({"ERROR"}, "TT Lab didn't add it: %s" % (answer[0].get("text", "") if answer else "no answer"))
            return {"CANCELLED"}

        # The model it stood for keeps its file: TT Lab's Edit in Blender forgets the blend file for it, and what Blender kept of it (its
        # revision, what TT Lab said) isn't of the new model
        previous = root.get(tlm_blender.PATH_PROPERTY, "")
        link_sync.unbind(root)
        if previous and not link_sync._same_file(previous, answer[0]["path"]):
            link_sync._release(previous)

        root[tlm_blender.PATH_PROPERTY] = answer[0]["path"]
        for warning in warnings:
            self.report({"WARNING"}, warning)

        self.report({"INFO"}, "TT Lab has the model now, linked once Blender finds it")
        return {"FINISHED"}


class TTT_OT_OpenInTTLab(bpy.types.Operator):
    """Shows the model in TT Lab: the TT Lab that has its project open, else a running one opens the project (asking about its own unsaved
    changes first), else TT Lab starts with it"""

    bl_idname = "ttt.open_in_tt_lab"
    bl_label = "Open in TT Lab"
    bl_options = {"REGISTER"}

    @classmethod
    def poll(cls, context):
        from . import project as projects

        root = tlm_blender.find_root(context.object)
        path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
        if not path or projects.find_root(path) is None:
            cls.poll_message_set("The model isn't a model of a TT Lab project: import it from one")
            return False

        return True

    def execute(self, context):
        import subprocess

        from . import link_client
        from . import link_protocol
        from . import link_sync
        from . import project as projects

        root = tlm_blender.find_root(context.object)
        path = root[tlm_blender.PATH_PROPERTY]
        link = link_sync.link_of(root)
        if link is not None and root.get(link_sync.MODEL_URI_PROPERTY):
            link.client.send({"type": "open_asset", "uri": root[link_sync.MODEL_URI_PROPERTY]})
            return {"FINISHED"}

        folder = projects.find_root(path)
        # The TT Lab that has the project open, else any (it opens the project)
        running = (link_protocol.tt_lab_of(folder, projects.uuid_of(folder)) if folder else None) or link_protocol.any_tt_lab()
        if running is not None:
            try:
                client = link_client.Client(int(running["port"]))
            except (OSError, KeyError, ValueError) as error:
                self.report({"ERROR"}, "TT Lab didn't answer: %s" % error)
                return {"CANCELLED"}

            import time

            answers = []
            client.send({"type": "hello", "token": running.get("token", ""), "protocol": link_protocol.PROTOCOL, "blend": "", "blender": bpy.app.version_string,
                         "addon": tlm_blender.ADDON_VERSION})
            client.send({"type": "open_project", "project": folder, "path": path}, answered=lambda header, payload: answers.append(header))
            # Closed with its answer unread, the socket would be reset before TT Lab read the question
            end = time.time() + 3.0
            while not answers and not client.closed and time.time() < end:
                client.take()
                time.sleep(0.02)

            client.close()
            if answers and answers[0].get("type") == "error":
                self.report({"ERROR"}, "TT Lab didn't open the project: %s" % answers[0].get("text", ""))
                return {"CANCELLED"}
            self.report({"INFO"}, "TT Lab opens the project, asking about its own unsaved changes first")
            return {"FINISHED"}

        command = link_protocol.tt_lab_command()
        if command is None:
            self.report({"ERROR"}, "No TT Lab ran on this computer yet: start TT Lab once, Blender starts it from then on")
            return {"CANCELLED"}

        try:
            subprocess.Popen(command + [folder, "--open-file", path], start_new_session=True)
        except OSError as error:
            self.report({"ERROR"}, "TT Lab didn't start: %s" % error)
            return {"CANCELLED"}

        self.report({"INFO"}, "TT Lab starts with the project")
        return {"FINISHED"}


def _menu_import(self, context):
    self.layout.operator(TTT_OT_ImportTlm.bl_idname, text="TT Lab Model (.tlm)")


def _menu_export(self, context):
    self.layout.operator(TTT_OT_ExportTlm.bl_idname, text="TT Lab Model (.tlm)")


def _menu_add(self, context):
    self.layout.separator()
    self.layout.menu(TTT_MT_Add.bl_idname, icon="FILE_3D")


def register():
    bpy.types.TOPBAR_MT_file_import.append(_menu_import)
    bpy.types.TOPBAR_MT_file_export.append(_menu_export)
    bpy.types.VIEW3D_MT_add.append(_menu_add)


def unregister():
    bpy.types.VIEW3D_MT_add.remove(_menu_add)
    bpy.types.TOPBAR_MT_file_import.remove(_menu_import)
    bpy.types.TOPBAR_MT_file_export.remove(_menu_export)
