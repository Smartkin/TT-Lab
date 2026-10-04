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

import bpy
from bpy_extras.io_utils import ExportHelper, ImportHelper

from . import collision_builder
from . import project as projects
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


class TTT_OT_ExportTlm(bpy.types.Operator, ExportHelper):
    """Exports the selected TT Lab model back into a TT Lab model file, the file it came from by default"""

    bl_idname = "ttt.export_tlm"
    bl_label = "Export TT Lab Model"
    bl_options = {"REGISTER"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})

    @classmethod
    def poll(cls, context):
        return tlm_blender.find_root(context.object) is not None

    def invoke(self, context, event):
        root = tlm_blender.find_root(context.object)
        source = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
        if source:
            self.filepath = source

        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        root = tlm_blender.find_root(context.object)
        if root is None:
            self.report({"ERROR"}, "Select an object of a TT Lab model")
            return {"CANCELLED"}

        if context.object is not None and context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

        context.view_layer.update()
        try:
            warnings = tlm_blender.export_file(root, self.filepath)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't export %s: %s" % (os.path.basename(self.filepath), error))
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
    """Makes a new OGI model to build a game object's model in: its root, an armature whose only bone is joint 0, a box skin the
    joint deforms and the holders the rigid bodies, exit points and collision hulls go under. Replace the box with the model's mesh,
    add bones and animations as needed, keep the root's bounding box around the skin and export it over an OGI's file in a TT Lab
    project to make that OGI the model"""

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


class TTT_MT_Add(bpy.types.Menu):
    """New models to build in Blender, laid out the way TT Lab's are"""

    bl_idname = "TTT_MT_add"
    bl_label = "Twin Tech"

    def draw(self, context):
        self.layout.operator(TTT_OT_NewOgi.bl_idname, icon="OUTLINER_OB_ARMATURE")
        self.layout.operator(TTT_OT_NewScenery.bl_idname, icon="WORLD")


class TTT_OT_RetargetAndReplace(bpy.types.Operator, ImportHelper):
    """Imports another TT Lab model file and puts it in this model's place, playing this model's animations: its bones get this
    model's joint names, indexes and settings (a bone with one of the joint indexes is that joint while the file's skeleton is this
    model's, then bones named like this model's, then the hierarchy in order, the rest new joints), every animation of this model
    becomes one of the new model's, in which the matched bones turn from their own rests as much as this model's do on every frame and
    move as far, and this model is removed with its own animations. The file's own animations are dropped, the new model takes this
    model's file and name. Export the model afterwards"""

    bl_idname = "ttt.retarget_and_replace"
    bl_label = "Retarget Another TLM And Replace The Current Model"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".tlm"
    filter_glob: bpy.props.StringProperty(default="*.tlm", options={"HIDDEN"})
    match: bpy.props.EnumProperty(
        name="Match Bones By",
        items=[
            (retarget.MATCH_AUTO, "Automatically", "Joint indexes when the file's skeleton is this model's (every joint they share has the same parent), names and then the hierarchy otherwise"),
            (retarget.MATCH_INDEX, "Joint Indexes", "A bone with one of this model's joint indexes is that joint, whatever its place: for a copy of this model's skeleton"),
            (retarget.MATCH_NAME, "Names, Then The Hierarchy", "Indexes are left out, exporting numbers any rig's bones: bones named like this model's joints are those, the rest by their place in the hierarchy"),
        ],
        default=retarget.MATCH_AUTO)

    @classmethod
    def poll(cls, context):
        root = tlm_blender.find_root(context.object)
        return root is not None and root.get(tlm_blender.KIND_PROPERTY) == "ogi" and tlm_blender.armature_of(root) is not None

    def execute(self, context):
        root = tlm_blender.find_root(context.object)
        if root is None:
            self.report({"ERROR"}, "Select an object of a TT Lab model")
            return {"CANCELLED"}

        _in_object_mode(context)
        name = root.name
        try:
            replaced, count, matched = tlm_blender.retarget_and_replace(context, root, self.filepath, self.match)
        except (OSError, tlm.TlmError, ValueError) as error:
            self.report({"ERROR"}, "Couldn't put %s in the model's place: %s" % (self.filepath, error))
            return {"CANCELLED"}

        _select_only(context, replaced)
        how = ", ".join("%d by %s" % (matched[way], label) for way, label in ((retarget.BY_INDEX, "joint index"), (retarget.BY_NAME, "name"),
                                                                             (retarget.BY_ORDER, "the hierarchy"), (retarget.NEW, "none, new joints")) if matched.get(way))
        self.report({"INFO"}, "%s is now %s, playing %d retargeted animations; bones matched %s" % (name, os.path.basename(self.filepath), count, how))
        return {"FINISHED"}


_surface_items_cache = []


def _surface_items(self, context):
    """The collision's surfaces first, then the file's other surface materials, the placeholder surface when there's none"""
    root = tlm_blender.find_root(context.object) if context is not None else None
    collision = tlm_scenery.collision_of(root) if root is not None else None
    names = [material.name for material in collision.data.materials if material is not None] if collision is not None else []
    names += sorted(material.name for material in bpy.data.materials if tlm_scenery.is_surface(material) and material.name not in names)
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
    """Plays the model's animations on another model: the other model's file takes this one's place with its joints named like
    this model's and this model's animations retargeted to it"""

    bl_label = "Twin Tech Animation Retargeting"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"
    bl_options = {"DEFAULT_CLOSED"}

    @classmethod
    def poll(cls, context):
        root = tlm_blender.find_root(context.object)
        return root is not None and root.get(tlm_blender.KIND_PROPERTY) == "ogi"

    def draw(self, context):
        layout = self.layout
        root = tlm_blender.find_root(context.object)
        armature = tlm_blender.armature_of(root)
        if armature is None:
            layout.label(text="%s has no armature to retarget from" % root.name, icon="ERROR")
            return

        count = len(tlm_blender.animations_of(root))
        layout.label(text="%d animations of %s" % (count, root.name), icon="ACTION")
        column = layout.column(align=True)
        column.label(text="Another model file takes this model's place,")
        column.label(text="playing them retargeted to its joints")
        layout.operator(TTT_OT_RetargetAndReplace.bl_idname, icon="FILE_3D")


def _project_of(context):
    root = tlm_blender.find_root(context.object)
    path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
    if not path:
        path = bpy.data.filepath

    return projects.open_project(path) if path else None, root


_material_items_cache = []


def _material_items(self, context):
    project, root = _project_of(context)
    if project is None:
        _material_items_cache[:] = [("NONE", "No TT Lab project", "The model isn't part of a TT Lab project")]
        return _material_items_cache

    path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
    package = projects.package_of(project.root, path) if path else None
    platform = projects.platform_of(package) if package else None
    _material_items_cache[:] = [(material.uri, material.label, material.uri) for material in project.materials_of_packages([])
                                if platform is None or projects.platform_of(material.package) in (platform, None)]
    return _material_items_cache


class TTT_OT_PickProjectMaterial(bpy.types.Operator):
    """Draws the material with one of the project's materials"""

    bl_idname = "ttt.pick_project_material"
    bl_label = "Pick Project Material"
    bl_options = {"REGISTER", "UNDO"}
    bl_property = "material"

    material: bpy.props.EnumProperty(name="Material", items=_material_items)

    @classmethod
    def poll(cls, context):
        return context.material is not None

    def invoke(self, context, event):
        context.window_manager.invoke_search_popup(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        if self.material == "NONE":
            return {"CANCELLED"}

        project, _ = _project_of(context)
        material = context.material
        material[tlm_blender.URI_PROPERTY] = self.material
        project_material = project.materials.get(self.material) if project is not None else None
        if project_material is not None:
            material.name = project_material.name

        tlm_blender.show_project_material(material, project)
        return {"FINISHED"}


class TTT_OT_ReloadProject(bpy.types.Operator):
    """Reads the project's materials and textures again, after they were changed in TT Lab"""

    bl_idname = "ttt.reload_project"
    bl_label = "Reload Project Materials"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        root = tlm_blender.find_root(context.object)
        path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
        project = projects.open_project(path, refresh=True) if path else None
        if project is None:
            self.report({"WARNING"}, "The model isn't part of a TT Lab project")
            return {"CANCELLED"}

        for material in bpy.data.materials:
            if material.get(tlm_blender.URI_PROPERTY):
                tlm_blender.show_project_material(material, project)

        for image in bpy.data.images:
            if image.filepath:
                image.reload()

        return {"FINISHED"}


class TTT_PT_ProjectMaterial(bpy.types.Panel):
    bl_label = "Twin Tech Project Material"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "material"

    @classmethod
    def poll(cls, context):
        return context.material is not None and tlm_blender.find_root(context.object) is not None

    def draw(self, context):
        layout = self.layout
        material = context.material
        uri = material.get(tlm_blender.URI_PROPERTY, "")
        if uri:
            layout.label(text=uri, icon="MATERIAL")
        else:
            layout.label(text="Made in Blender, TT Lab adds it to the project", icon="INFO")

        row = layout.row()
        row.operator(TTT_OT_PickProjectMaterial.bl_idname, icon="VIEWZOOM")
        row.operator(TTT_OT_ReloadProject.bl_idname, icon="FILE_REFRESH", text="")


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
