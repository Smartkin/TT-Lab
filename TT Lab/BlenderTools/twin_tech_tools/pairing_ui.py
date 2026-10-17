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

"""The Twin Tech Animation Retargeting panel: the rig taking the model's place, the table of the joint each of its bones stands for, which
it keeps, and Retarget and Replace (replace)."""

import os
import textwrap
import typing

import bpy
from bpy_extras.io_utils import ImportHelper

from . import pairing
from . import replace
from . import tlm
from . import tlm_blender


class TTT_PairRow(bpy.types.PropertyGroup):
    bone: bpy.props.StringProperty(name="Bone", description="A bone of the rig")
    joint: bpy.props.StringProperty(name="Joint", description="The joint of the model the bone stands for: it plays its animations. None makes "
                                                              "it a new joint, at rest. A bone stands for a child of the joint its parent stands for")


class TTT_ShapeRow(bpy.types.PropertyGroup):
    shape: bpy.props.StringProperty(name="Shape Key", description="A shape key of the rig's blend skin")
    source: bpy.props.StringProperty(name="Model's Shape Key", description="The model's shape key whose facial animations it plays, none for none")


def _is_rig(root_holder: bpy.types.Object, candidate: bpy.types.Object) -> bool:
    if candidate.type != "ARMATURE" or not tlm_blender.in_scene(candidate):
        return False

    root = tlm_blender.find_root(candidate)
    return not replace.is_source(root)


def _model(context: bpy.types.Context) -> typing.Optional[bpy.types.Object]:
    root = tlm_blender.find_root(getattr(context, "object", None))
    return root if root is not None and root.get(tlm_blender.KIND_PROPERTY) == "ogi" and not replace.is_source(root) else None


def rig_of(root: bpy.types.Object) -> typing.Optional[bpy.types.Object]:
    """The rig taking the model's place: the one picked, a model made by retargeting its own armature."""
    rig = root.ttt_retarget_rig
    if rig is not None and rig.name in bpy.data.objects and _is_rig(root, rig):
        return rig

    return tlm_blender.armature_of(root) if replace.source_of(root) != root else None


def _wrapped(layout: typing.Any, text: str, icon: str, width: int = 60) -> None:
    column = layout.column(align=True)
    for index, line in enumerate(textwrap.wrap(text, width)):
        column.label(text=line, icon=icon if index == 0 else "BLANK1")


class TTT_UL_Pairs(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_property, index=0, flt_flag=0):
        root = _model(context)
        source = tlm_blender.armature_of(replace.source_of(root)) if root is not None else None
        row = layout.row(align=True)
        row.label(text=item.bone, icon="BONE_DATA")
        if source is not None:
            row.prop_search(item, "joint", source.data, "bones", text="", icon="GROUP_BONE")
        else:
            row.prop(item, "joint", text="")


class TTT_UL_ShapePairs(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_property, index=0, flt_flag=0):
        row = layout.row(align=True)
        row.label(text=item.shape, icon="SHAPEKEY_DATA")
        row.prop(item, "source", text="")


class TTT_OT_ImportRig(bpy.types.Operator, ImportHelper):
    """Imports a TT Lab model file as the rig taking this model's place"""

    bl_idname = "ttt.import_rig"
    bl_label = "Import a Rig"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})

    def execute(self, context):
        root = _model(context)
        if root is None:
            self.report({"ERROR"}, "Select an object of an OGI model")
            return {"CANCELLED"}

        try:
            imported = tlm_blender.import_file(context, self.filepath)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't import %s: %s" % (self.filepath, error))
            return {"CANCELLED"}

        rig = tlm_blender.armature_of(imported)
        if imported.get(tlm_blender.KIND_PROPERTY) != "ogi" or rig is None:
            tlm_blender.remove_model(imported)
            self.report({"ERROR"}, "%s isn't an OGI with an armature" % os.path.basename(self.filepath))
            return {"CANCELLED"}

        # The file's own animations go, the model's are the ones it plays from now on
        for action in tlm_blender.animations_of(imported):
            bpy.data.actions.remove(action)

        root.ttt_retarget_rig = rig
        replace.guess(tlm_blender.armature_of(replace.source_of(root)), rig)
        # The panel is the model's
        for selected in context.selected_objects:
            selected.select_set(False)

        root.select_set(True)
        context.view_layer.objects.active = root
        return {"FINISHED"}


class TTT_OT_GuessPairs(bpy.types.Operator):
    """Fills the table in again: bones of the joints' indexes or names stand for them, the rest are guessed by which way they and what's under
    them rest from their parents, the rig's bones of no joint new joints"""

    bl_idname = "ttt.guess_pairs"
    bl_label = "Guess the Pairs"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        root = _model(context)
        return root is not None and rig_of(root) is not None

    def execute(self, context):
        root = _model(context)
        pairs = replace.guess(tlm_blender.armature_of(replace.source_of(root)), rig_of(root))
        self.report({"INFO"}, "%d of %d bones stand for joints" % (sum(1 for joint in pairs.values() if joint), len(pairs)))
        return {"FINISHED"}


class TTT_OT_RetargetReplace(bpy.types.Operator):
    """The rig takes the model's place, playing its animations: its bones become the joints the table pairs them with (the rest new joints),
    the model's exit points and hulls follow their joints' bones and the model is kept hidden, to change the table and retarget again.
    Export the model afterwards"""

    bl_idname = "ttt.retarget_replace"
    bl_label = "Retarget and Replace"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        root = _model(context)
        rig = rig_of(root) if root is not None else None
        if rig is None:
            cls.poll_message_set("Pick the rig taking the model's place")
            return False

        found = replace.problems(replace.source_of(root), rig)
        if found:
            cls.poll_message_set(found[0])
            return False

        return True

    def execute(self, context):
        root = _model(context)
        rig = rig_of(root)
        if context.object is not None and context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

        shapes = {row.shape: (row.source or None) for row in rig.data.ttt_shape_pairs} or None
        try:
            model, count, notes = replace.replace(context, root, rig, shapes)
        except ValueError as error:
            self.report({"ERROR"}, "Nothing was retargeted: %s" % error)
            return {"CANCELLED"}

        for selected in context.selected_objects:
            selected.select_set(False)

        model.select_set(True)
        context.view_layer.objects.active = model
        for note in notes:
            self.report({"WARNING"}, note)

        self.report({"INFO"}, "%s plays %d retargeted animations" % (model.name, count))
        return {"FINISHED"}


class TTT_OT_PairShapes(bpy.types.Operator):
    """Fills the shape keys' table in again: of the same name, then in their order"""

    bl_idname = "ttt.pair_shapes"
    bl_label = "Pair the Shape Keys"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        root = _model(context)
        return root is not None and rig_of(root) is not None

    def execute(self, context):
        root = _model(context)
        rig = rig_of(root)
        rig_root = tlm_blender.find_root(rig)
        pairs = pairing.pair_shapes(replace.shape_names(replace.source_of(root)), replace.shape_names(rig_root))
        rows = rig.data.ttt_shape_pairs
        rows.clear()
        for shape, source in pairs.items():
            row = rows.add()
            row.shape = shape
            row.source = source or ""

        return {"FINISHED"}


class TTT_PT_Retargeting(bpy.types.Panel):
    """Plays the model's animations on another rig, which takes the model's place"""

    bl_label = "Twin Tech Animation Retargeting"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"
    bl_options = {"DEFAULT_CLOSED"}

    @classmethod
    def poll(cls, context):
        return _model(context) is not None

    def draw(self, context):
        layout = self.layout
        root = _model(context)
        source = replace.source_of(root)
        source_armature = tlm_blender.armature_of(source)
        if source_armature is None:
            layout.label(text="%s has no armature to retarget from" % root.name, icon="ERROR")
            return

        if source != root:
            layout.label(text="Retargeted from %s" % source.name, icon="LINKED")

        layout.label(text="%d animations of %s" % (len(tlm_blender.animations_of(source)), source.name), icon="ACTION")
        row = layout.row(align=True)
        row.prop(root, "ttt_retarget_rig", text="Rig")
        row.operator(TTT_OT_ImportRig.bl_idname, text="", icon="FILE_3D")
        rig = rig_of(root)
        if rig is None:
            layout.label(text="Pick the armature taking the model's place, or import a model file's", icon="INFO")
            return

        rows = rig.data.ttt_pairs
        if len(rows) == 0:
            layout.label(text="The table is empty: guess the pairs", icon="INFO")

        layout.template_list("TTT_UL_Pairs", "", rig.data, "ttt_pairs", rig.data, "ttt_pairs_index", rows=8)
        layout.operator(TTT_OT_GuessPairs.bl_idname, icon="AUTO")
        for problem in replace.problems(source, rig):
            _wrapped(layout, problem, "ERROR")

        if replace.shape_names(source) and replace.shape_names(tlm_blender.find_root(rig)):
            layout.label(text="Shape Keys", icon="SHAPEKEY_DATA")
            layout.template_list("TTT_UL_ShapePairs", "", rig.data, "ttt_shape_pairs", rig.data, "ttt_shape_pairs_index", rows=3)
            layout.operator(TTT_OT_PairShapes.bl_idname, icon="AUTO")

        for note in replace.notes_of(root):
            _wrapped(layout, note, "INFO")

        layout.operator(TTT_OT_RetargetReplace.bl_idname, icon="ARMATURE_DATA")


def register():
    bpy.types.Armature.ttt_pairs = bpy.props.CollectionProperty(type=TTT_PairRow)
    bpy.types.Armature.ttt_pairs_index = bpy.props.IntProperty(options={"HIDDEN"})
    bpy.types.Armature.ttt_shape_pairs = bpy.props.CollectionProperty(type=TTT_ShapeRow)
    bpy.types.Armature.ttt_shape_pairs_index = bpy.props.IntProperty(options={"HIDDEN"})
    bpy.types.Object.ttt_retarget_rig = bpy.props.PointerProperty(type=bpy.types.Object, name="Rig", poll=_is_rig,
                                                                  description="The armature taking the model's place, playing its animations")


def unregister():
    for owner, name in ((bpy.types.Armature, "ttt_pairs"), (bpy.types.Armature, "ttt_pairs_index"), (bpy.types.Armature, "ttt_shape_pairs"),
                        (bpy.types.Armature, "ttt_shape_pairs_index"), (bpy.types.Object, "ttt_retarget_rig")):
        if hasattr(owner, name):
            delattr(owner, name)
