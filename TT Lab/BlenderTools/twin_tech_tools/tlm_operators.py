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

from . import project as projects
from . import tlm
from . import tlm_blender


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
        tlm_blender.export_file(root, self.filepath)
        self.report({"INFO"}, "Exported %s" % os.path.basename(self.filepath))
        return {"FINISHED"}


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


def register():
    bpy.types.TOPBAR_MT_file_import.append(_menu_import)
    bpy.types.TOPBAR_MT_file_export.append(_menu_export)


def unregister():
    bpy.types.TOPBAR_MT_file_import.remove(_menu_import)
    bpy.types.TOPBAR_MT_file_export.remove(_menu_export)
