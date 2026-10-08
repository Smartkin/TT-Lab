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

import bpy
from bpy_extras.io_utils import ExportHelper, ImportHelper

from . import collision_builder
from . import retarget
from . import tlm
from . import tlm_blender
from . import tlm_scenery
from . import tlm_templates


class TTT_OT_ImportTlm(bpy.types.Operator, ImportHelper):
    """Imports a TT Lab model file. Models of a TT Lab project show the project's materials"""

    bl_idname = "ttt.import_tlm"
    bl_label = "Import TT Lab Model"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})

    def execute(self, context):
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

    def _check(self, context):
        root, problem = _model_to_export(context)
        problem = problem or tlm_blender.export_problem(root)
        if problem is not None:
            self.report({"ERROR"}, "Can't export: %s" % problem)
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
        root = self._check(context)
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
    """Makes a new OGI model to build a game object's model in: its root, an armature of joint 0 and joint 1, a box rigid body on joint 1,
    an empty skin and blend skin the armature deforms and the holders the exit points and collision hulls go under. Replace the box or
    fill the skin with the model's meshes, add bones and animations as needed, keep the root's bounding box around the meshes and export
    it over an OGI's file in a TT Lab project to make that OGI the model"""

    bl_idname = "ttt.new_ogi"
    bl_label = "OGI Model"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _in_object_mode(context)
        root = tlm_templates.new_ogi(context)
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
        self.layout.operator(TTT_OT_NewScenery.bl_idname, icon="WORLD")
        self.layout.operator(TTT_OT_NewSaveIcon.bl_idname, icon="IMAGE_DATA")


_MATCH_ITEMS = [
    (retarget.MATCH_AUTO, "Automatically", "Joint indexes when the target's skeleton is the source's (every joint they share has the same parent), names and then the hierarchy otherwise"),
    (retarget.MATCH_INDEX, "Joint Indexes", "A bone with one of the source's joint indexes is that joint, whatever its place: for a copy of the source's skeleton"),
    (retarget.MATCH_NAME, "Names, Then The Hierarchy", "Indexes are left out, exporting numbers any rig's bones: bones named like the source's joints are those, the rest by their place in the hierarchy"),
    (retarget.MATCH_NAME_ONLY, "Name Only", "Bones named like the source's joints are those joints and nothing else matches: the target's other bones stay as they are, new "
                                         "joints keeping their names. The matched bones have to be under each other like the joints are, other bones may be in between"),
]


def _is_armature(self, blender_object):
    return blender_object.type == "ARMATURE"


class TTT_OT_Retarget(bpy.types.Operator):
    """Plays the source armature's animations on the target armature: the target's bones get the source's joint names, indexes and
    settings (by joint index while the target's skeleton is the source's, then by name, then by the hierarchy in order, the rest new
    joints), every animation of the source becomes the target's under the same name and ID, in which the matched bones turn from their own
    rests as much as the source's do on every frame and move as far, and the source's own animations go. Export the target's model
    afterwards"""

    bl_idname = "ttt.retarget"
    bl_label = "Perform Retargeting"
    bl_options = {"REGISTER", "UNDO"}

    # Always offered: pressing it says what's missing
    @classmethod
    def poll(cls, context):
        return True

    def execute(self, context):
        scene = context.scene
        source, target = scene.ttt_retarget_source, scene.ttt_retarget_target
        problem = tlm_blender.retarget_problem(source, target)
        if problem is not None:
            self.report({"ERROR"}, "Can't retarget: %s" % problem)
            return {"CANCELLED"}

        _in_object_mode(context)
        source_name, target_name = source.name, target.name
        try:
            count, matched, taken = tlm_blender.retarget_armature(context, source, target, scene.ttt_retarget_match)
        except ValueError as error:
            self.report({"ERROR"}, "Can't retarget: %s" % error)
            return {"CANCELLED"}

        how = ", ".join("%d by %s" % (matched[way], label) for way, label in ((retarget.BY_INDEX, "joint index"), (retarget.BY_NAME, "name"),
                                                                             (retarget.BY_ORDER, "the hierarchy"), (retarget.NEW, "none, new joints"),
                                                                             (retarget.KEPT, "none, kept as they are")) if matched.get(way))
        self.report({"INFO"}, "%s plays the %d animations of %s now; bones matched %s" % (target_name, count, source_name, how))
        if taken:
            self.report({"WARNING"}, "Other actions are named %s already, so these kept Blender's numbers" % ", ".join(taken))

        return {"FINISHED"}


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
        layout = self.layout
        root = tlm_blender.find_root(context.object)
        path = root.get(tlm_blender.PATH_PROPERTY, "")
        layout.label(text=os.path.basename(path) if path else root.name, icon="FILE_3D")
        layout.operator(TTT_OT_ExportTlm.bl_idname, icon="EXPORT")
        if root.get(tlm_blender.KIND_PROPERTY) == "scenery":
            layout.operator(TTT_OT_GenerateCollision.bl_idname, icon="MOD_PHYSICS")


class TTT_PT_Retargeting(bpy.types.Panel):
    """Plays one armature's animations on another: the target's bones become the source's joints and the source's animations the
    target's, retargeted to its bones"""

    bl_label = "Twin Tech Animation Retargeting"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"
    bl_options = {"DEFAULT_CLOSED"}

    @classmethod
    def poll(cls, context):
        return context.object is not None

    def draw(self, context):
        layout = self.layout
        scene = context.scene
        layout.prop(scene, "ttt_retarget_source", icon="ARMATURE_DATA")
        layout.prop(scene, "ttt_retarget_target", icon="ARMATURE_DATA")
        layout.prop(scene, "ttt_retarget_match")
        source = scene.ttt_retarget_source
        if source is not None and source.type == "ARMATURE":
            count = len(tlm_blender._owned_actions(tlm_blender.find_root(source), source, [bone.name for bone in source.data.bones]))
            layout.label(text="%d animations of %s" % (count, source.name), icon="ACTION")

        layout.operator(TTT_OT_Retarget.bl_idname, icon="PLAY")


def _menu_import(self, context):
    self.layout.operator(TTT_OT_ImportTlm.bl_idname, text="TT Lab Model (.tlm)")


def _menu_export(self, context):
    self.layout.operator(TTT_OT_ExportTlm.bl_idname, text="TT Lab Model (.tlm)")


def _menu_add(self, context):
    self.layout.separator()
    self.layout.menu(TTT_MT_Add.bl_idname, icon="FILE_3D")


def register():
    bpy.types.Scene.ttt_retarget_source = bpy.props.PointerProperty(type=bpy.types.Object, name="Source Armature", poll=_is_armature,
                                                                    description="The armature playing the animations, they go to the target")
    bpy.types.Scene.ttt_retarget_target = bpy.props.PointerProperty(type=bpy.types.Object, name="Target Armature", poll=_is_armature,
                                                                    description="The armature to play the source's animations, its bones become the source's joints")
    bpy.types.Scene.ttt_retarget_match = bpy.props.EnumProperty(name="Match Bones By", items=_MATCH_ITEMS, default=retarget.MATCH_AUTO)
    bpy.types.TOPBAR_MT_file_import.append(_menu_import)
    bpy.types.TOPBAR_MT_file_export.append(_menu_export)
    bpy.types.VIEW3D_MT_add.append(_menu_add)


def unregister():
    bpy.types.VIEW3D_MT_add.remove(_menu_add)
    bpy.types.TOPBAR_MT_file_import.remove(_menu_import)
    bpy.types.TOPBAR_MT_file_export.remove(_menu_export)
    for name in ("ttt_retarget_source", "ttt_retarget_target", "ttt_retarget_match"):
        if hasattr(bpy.types.Scene, name):
            delattr(bpy.types.Scene, name)
