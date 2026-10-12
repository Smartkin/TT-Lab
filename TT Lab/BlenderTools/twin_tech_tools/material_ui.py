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

"""The Twin Tech Material panel: every setting TT Lab edits of a material and its shaders, grayed out where the game doesn't read
it, and the operators picking the project's materials and textures."""

import array
import contextlib
import typing
import uuid

import bpy

from . import gs_material
from . import material_settings
from . import project as projects
from . import tlm_blender

_SKINNED_ROLES = ("skin", "shape")


def project_of(context: bpy.types.Context) -> typing.Tuple[typing.Optional[projects.Project], typing.Optional[str]]:
    """The project of the active object's model (or of the blend file) and the version of the game its package is for."""
    root = tlm_blender.find_root(getattr(context, "object", None))
    path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
    project = projects.open_project(path or bpy.data.filepath) if path or bpy.data.filepath else None
    if project is None:
        return None, None

    package = projects.package_of(project.root, path) if path else None
    return project, projects.platform_of(package) if package else None


def _material(context: bpy.types.Context) -> typing.Optional[bpy.types.Material]:
    """The material the properties editor shows, the active object's otherwise (operators run from anywhere)."""
    material = getattr(context, "material", None)
    active = getattr(context, "object", None)
    if material is None and active is not None:
        material = active.active_material

    return material


def _users(material: bpy.types.Material) -> typing.List[bpy.types.Object]:
    return [blender_object for blender_object in bpy.data.objects
            if blender_object.type == "MESH" and any(slot.material == material for slot in blender_object.material_slots)]


def _draws_skins(material: bpy.types.Material) -> bool:
    return any(tlm_blender.role_of(blender_object) in _SKINNED_ROLES for blender_object in _users(material))


def _is_global(context: bpy.types.Context) -> bool:
    root = tlm_blender.find_root(getattr(context, "object", None))
    path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
    project = projects.open_project(path) if path else None
    package = projects.package_of(project.root, path) if project is not None else None
    return package is not None and package.split("://", 1)[-1].lower().startswith("global")


def _shown(item: typing.Any) -> typing.Dict[str, typing.Any]:
    values = {field.prop: getattr(item, field.prop) for field in gs_material.SHADER_FIELDS}
    values["has_animation"] = item.has_animation
    return values


class TTT_OT_MaterialUseSettings(bpy.types.Operator):
    """Draws the material and writes it into model files with Twin Tech settings: the project material's, or the ones TT Lab gives
    a material made in Blender. Its nodes are made again. When the project material's settings aren't found, it asks whether to make it
    a new material with those instead"""

    bl_idname = "ttt.material_use_settings"
    bl_label = "Use Twin Tech Settings"
    bl_options = {"REGISTER", "UNDO"}

    new_material: bpy.props.BoolProperty(options={"HIDDEN", "SKIP_SAVE"},
                                         description="A project material whose settings aren't found becomes a new material with the ones TT Lab gives "
                                                     "a material made in Blender (TT Lab adds it to the project, the project's material stays as it is)")

    @classmethod
    def poll(cls, context):
        return _material(context) is not None

    def invoke(self, context, event):
        material = _material(context)
        problem = _project_settings(material, context)[2] if material.get(tlm_blender.URI_PROPERTY) and not self.new_material else ""
        if not problem:
            return self.execute(context)

        self.new_material = True
        return context.window_manager.invoke_confirm(self, event, title="%s's settings weren't found" % material.name,
                                                     message="%s. Make it a new material?" % (problem[0].upper() + problem[1:]),
                                                     confirm_text="Make a New Material", icon="QUESTION")

    def execute(self, context):
        material = _material(context)
        uri = material.get(tlm_blender.URI_PROPERTY, "")
        if uri:
            project, data, problem = _project_settings(material, context)
            if data is not None:
                material_settings.load(material, data, project)
                return {"FINISHED"}

            if not self.new_material:
                self.report({"ERROR"}, "The settings of %s weren't found: %s" % (uri, problem))
                return {"CANCELLED"}

            # With the link kept the file would bring the new settings for the project's material, which a project without it drops for
            # a placeholder and one with it takes whole
            _detach(material)
            self.report({"INFO"}, "%s is a new material, which TT Lab adds to the project" % material.name)

        material_settings.make(material, skin=_draws_skins(material), global_package=_is_global(context))
        return {"FINISHED"}


def _project_settings(material: bpy.types.Material, context: bpy.types.Context
                      ) -> typing.Tuple[typing.Optional[projects.Project], typing.Optional[typing.Dict[str, typing.Any]], str]:
    """The project and the settings of the project material the material links, None and why when they can't be read."""
    project, _ = project_of(context)
    if project is None:
        return None, None, "the model isn't part of a TT Lab project here"

    project_material = project.materials.get(material.get(tlm_blender.URI_PROPERTY, ""))
    if project_material is None:
        return project, None, "the project %s doesn't have it" % project.name

    data = project_material.read()
    return project, data, "" if data is not None else "its file in the project %s can't be read" % project.name


class TTT_OT_MaterialShaderAdd(bpy.types.Operator):
    """Adds a shader, a pass drawn over the others"""

    bl_idname = "ttt.material_shader_add"
    bl_label = "Add Shader"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if not material_settings.is_managed(_material(context)):
            return False

        if len(material_settings.settings_of(_material(context)).shaders) >= gs_material.MAX_SHADERS:
            cls.poll_message_set("The game keeps a material's shaders in %d slots" % gs_material.MAX_SHADERS)
            return False

        return True

    def execute(self, context):
        material_settings.add_shader(_material(context))
        return {"FINISHED"}


class TTT_OT_MaterialShaderRemove(bpy.types.Operator):
    """Takes the shader out"""

    bl_idname = "ttt.material_shader_remove"
    bl_label = "Remove Shader"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return material_settings.is_managed(_material(context)) and len(material_settings.settings_of(_material(context)).shaders) > 0

    def execute(self, context):
        settings = material_settings.settings_of(_material(context))
        material_settings.remove_shader(_material(context), settings.active_shader)
        return {"FINISHED"}


class TTT_OT_MaterialShaderMove(bpy.types.Operator):
    """Moves the shader, earlier ones are drawn first"""

    bl_idname = "ttt.material_shader_move"
    bl_label = "Move Shader"
    bl_options = {"REGISTER", "UNDO"}

    direction: bpy.props.IntProperty(default=-1)

    @classmethod
    def poll(cls, context):
        return material_settings.is_managed(_material(context)) and len(material_settings.settings_of(_material(context)).shaders) > 1

    def execute(self, context):
        settings = material_settings.settings_of(_material(context))
        material_settings.move_shader(_material(context), settings.active_shader, self.direction)
        return {"FINISHED"}


_texture_items_cache: typing.List[typing.Tuple[str, str, str]] = []


def _texture_items(self, context):
    project, platform = project_of(context)
    if project is None:
        _texture_items_cache[:] = [("NONE", "No TT Lab project", "The model isn't part of a TT Lab project")]
    else:
        _texture_items_cache[:] = [(texture.uri, texture.label, texture.uri) for texture in project.textures_of_platform(platform)]

    return _texture_items_cache


class TTT_OT_PickProjectTexture(bpy.types.Operator):
    """Draws the shader with one of the project's textures"""

    bl_idname = "ttt.pick_project_texture"
    bl_label = "Pick Project Texture"
    bl_options = {"REGISTER", "UNDO"}
    bl_property = "texture"

    texture: bpy.props.EnumProperty(name="Texture", items=_texture_items)

    @classmethod
    def poll(cls, context):
        return material_settings.is_managed(_material(context)) and len(material_settings.settings_of(_material(context)).shaders) > 0

    def invoke(self, context, event):
        context.window_manager.invoke_search_popup(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        if self.texture == "NONE":
            return {"CANCELLED"}

        project, _ = project_of(context)
        image = material_settings.texture_image(project, self.texture)
        if image is None:
            self.report({"ERROR"}, "The picture of %s can't be read" % self.texture)
            return {"CANCELLED"}

        settings = material_settings.settings_of(_material(context))
        settings.shaders[settings.active_shader].image = image
        return {"FINISHED"}


_material_items_cache: typing.List[typing.Tuple[str, str, str]] = []


def _material_items(self, context):
    project, platform = project_of(context)
    if project is None:
        _material_items_cache[:] = [("NONE", "No TT Lab project", "The model isn't part of a TT Lab project")]
        return _material_items_cache

    _material_items_cache[:] = [(material.uri, material.label, material.uri) for material in project.materials_of_packages([])
                                if platform is None or projects.platform_of(material.package) in (platform, None)]
    return _material_items_cache


class TTT_OT_PickProjectMaterial(bpy.types.Operator):
    """Makes the material one of the project's materials, with its settings"""

    bl_idname = "ttt.pick_project_material"
    bl_label = "Pick Project Material"
    bl_options = {"REGISTER", "UNDO"}
    bl_property = "material"

    material: bpy.props.EnumProperty(name="Material", items=_material_items)

    @classmethod
    def poll(cls, context):
        return _material(context) is not None

    def invoke(self, context, event):
        context.window_manager.invoke_search_popup(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        if self.material == "NONE":
            return {"CANCELLED"}

        project, _ = project_of(context)
        project_material = project.materials.get(self.material) if project is not None else None
        data = project_material.read() if project_material is not None else None
        if data is None:
            self.report({"ERROR"}, "The project's %s can't be read" % self.material)
            return {"CANCELLED"}

        material = _material(context)
        material[tlm_blender.URI_PROPERTY] = self.material
        material.name = project_material.name
        material_settings.load(material, data, project)
        return {"FINISHED"}


class TTT_OT_MaterialRevert(bpy.types.Operator):
    """Takes the project's settings of the material again, what was changed in Blender is lost"""

    bl_idname = "ttt.material_revert"
    bl_label = "Revert to the Project's"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return material_settings.is_managed(_material(context)) and bool(_material(context).get(tlm_blender.URI_PROPERTY))

    def execute(self, context):
        material = _material(context)
        project, _ = project_of(context)
        uri = material[tlm_blender.URI_PROPERTY]
        project_material = project.materials.get(uri) if project is not None else None
        data = project_material.read() if project_material is not None else None
        if data is None:
            self.report({"ERROR"}, "The project's %s can't be read" % uri)
            return {"CANCELLED"}

        material_settings.load(material, data, project)
        return {"FINISHED"}


class TTT_OT_ReloadProject(bpy.types.Operator):
    """Reads the project's materials and textures again, after they were changed in TT Lab. Materials changed in Blender keep their
    changes, Revert takes the project's again"""

    bl_idname = "ttt.reload_project"
    bl_label = "Reload Project Materials"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        root = tlm_blender.find_root(getattr(context, "object", None))
        path = root.get(tlm_blender.PATH_PROPERTY, "") if root is not None else ""
        project = projects.open_project(path or bpy.data.filepath, refresh=True) if path or bpy.data.filepath else None
        if project is None:
            self.report({"WARNING"}, "The model isn't part of a TT Lab project")
            return {"CANCELLED"}

        for image in bpy.data.images:
            if image.filepath and image.packed_file is None:
                image.reload()

        kept = []
        for material in bpy.data.materials:
            if not material.get(tlm_blender.URI_PROPERTY):
                continue

            if material_settings.is_managed(material) and is_changed_in_blender(material, project):
                kept.append(material.name)
                continue

            tlm_blender.show_project_material(material, project)

        if kept:
            self.report({"INFO"}, "%s keep what was changed in Blender" % ", ".join(kept))

        return {"FINISHED"}


def is_changed_in_blender(material: bpy.types.Material, project: typing.Optional[projects.Project] = None) -> bool:
    """Whether the material's settings differ from the project's it was read from."""
    base = material_settings.base_of(material)
    return gs_material.is_changed(gs_material.merge_material(base, base, material_settings.shown(material, [], project))[0], base)


class TTT_OT_MaterialDetach(bpy.types.Operator):
    """Makes the material one of Blender's with the same settings: TT Lab adds it to the project as a new material, the project's
    material stays as it is"""

    bl_idname = "ttt.material_detach"
    bl_label = "Make a New Material"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return material_settings.is_managed(_material(context)) and bool(_material(context).get(tlm_blender.URI_PROPERTY))

    def execute(self, context):
        # Sent whole from now on, over the project material's values the add-on doesn't show
        _detach(_material(context))
        return {"FINISHED"}


def _detach(material: bpy.types.Material) -> None:
    """Makes a project's material one of Blender's, which TT Lab adds to the project as a new material."""
    del material[tlm_blender.URI_PROPERTY]
    material[tlm_blender.BLENDER_ID_PROPERTY] = uuid.uuid4().hex


class TTT_OT_AddVertexColors(bpy.types.Operator):
    """Gives the meshes drawn with the material that have no vertex colors the ones TT Lab writes for them: grey of 0x7F, opaque.
    The game draws the texture times them"""

    bl_idname = "ttt.add_vertex_colors"
    bl_label = "Add Vertex Colors"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return _material(context) is not None and any(_lacks_colors(blender_object) for blender_object in _users(_material(context)))

    def execute(self, context):
        lacking = [blender_object for blender_object in _users(_material(context)) if _lacks_colors(blender_object)]
        with _out_of_edit_mode(lacking):
            for blender_object in lacking:
                # Linked duplicates share the mesh
                if _lacks_colors(blender_object):
                    add_default_colors(blender_object.data)

        return {"FINISHED"}


@contextlib.contextmanager
def _out_of_edit_mode(objects: typing.List[bpy.types.Object]) -> typing.Iterator[None]:
    """Blender hands out no attribute values of a mesh being edited: they're set in Object Mode, which goes back to Edit Mode after."""
    editing = any(blender_object.data.is_editmode for blender_object in objects)
    if editing:
        bpy.ops.object.mode_set(mode="OBJECT")

    try:
        yield
    finally:
        if editing:
            bpy.ops.object.mode_set(mode="EDIT")


def _lacks_colors(blender_object: bpy.types.Object) -> bool:
    return blender_object.type == "MESH" and blender_object.data.color_attributes.get(tlm_blender.COLOR_ATTRIBUTE) is None


def add_default_colors(mesh: bpy.types.Mesh) -> None:
    colors = mesh.color_attributes.new(tlm_blender.COLOR_ATTRIBUTE, "BYTE_COLOR", "POINT")
    colors.data.foreach_set("color_srgb", [0x7F / 255.0, 0x7F / 255.0, 0x7F / 255.0, 1.0] * len(mesh.vertices))
    mesh.color_attributes.active_color = colors
    mesh.color_attributes.render_color_index = mesh.color_attributes.find(tlm_blender.COLOR_ATTRIBUTE)


class TTT_OT_AddEmitColors(bpy.types.Operator):
    """Gives the rigid meshes drawn with the material that have no emit colors a copy of their vertex colors as them (the EmitColor
    attribute). The game's Standard Lit, Lit Environment Map and Lit Reflection Surface shaders light a rigid model by its emit colors, not
    its vertex colors: paint EmitColor to light it in other colors, painting Color afterwards doesn't change them"""

    bl_idname = "ttt.add_emit_colors"
    bl_label = "Add Emit Colors"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        material = _material(context)
        return material is not None and len(_lacking_emit_colors(material)) > 0

    def execute(self, context):
        lacking = _lacking_emit_colors(_material(context))
        with _out_of_edit_mode(lacking):
            for blender_object in lacking:
                # Linked duplicates share the mesh
                if blender_object.data.color_attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE) is None:
                    add_emit_colors(blender_object.data)

        return {"FINISHED"}


def _lacking_emit_colors(material: bpy.types.Material) -> typing.List[bpy.types.Object]:
    """The rigid meshes of PS2 models drawn with the material that have no emit colors. The Xbox version's models have one color, which its
    shaders light."""
    versions: typing.Dict[str, str] = {}
    result = []
    for blender_object in _users(material):
        if blender_object.data.color_attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE) is not None or not tlm_blender.is_rigid_part(blender_object):
            continue

        path = tlm_blender.find_root(blender_object).get(tlm_blender.PATH_PROPERTY, "")
        if path not in versions:
            versions[path] = projects.version_of_file(path)

        if versions[path] != "XBOX":
            result.append(blender_object)

    return result


def add_emit_colors(mesh: bpy.types.Mesh) -> None:
    """A copy of the mesh's vertex colors as its emit colors, the grey TT Lab writes for a mesh without colors when it has none. The
    vertex colors stay the ones painted and rendered."""
    colors = tlm_blender.vertex_colors_of(mesh)
    domain = colors.domain if colors is not None else "POINT"
    values = array.array("f", [0.0] * len(colors.data) * 4) if colors is not None else None
    if colors is not None:
        colors.data.foreach_get("color_srgb", values)

    attributes = mesh.color_attributes
    # A new attribute can move the others' data, they're looked up again
    attributes.new(tlm_blender.EMIT_COLOR_ATTRIBUTE, "BYTE_COLOR", domain)
    attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE).data.foreach_set("color_srgb", values if values is not None else
                                                                      [0x7F / 255.0, 0x7F / 255.0, 0x7F / 255.0, 1.0] * len(mesh.vertices))
    # Removing the active attribute leaves Blender's active and rendered ones pointing at nothing, or at the emit colors once they're added
    colors = tlm_blender.vertex_colors_of(mesh)
    if colors is None:
        return

    if attributes.active_color is None or attributes.active_color_name == tlm_blender.EMIT_COLOR_ATTRIBUTE:
        attributes.active_color = colors

    rendered = attributes.render_color_index
    if not 0 <= rendered < len(attributes) or attributes[rendered].name == tlm_blender.EMIT_COLOR_ATTRIBUTE:
        attributes.render_color_index = attributes.find(colors.name)


def _emit_lighting_shader(material: bpy.types.Material, context: bpy.types.Context) -> typing.Optional[str]:
    """The label of the material's first shader type lighting rigid parts by their emit colors, of the settings the panel shows: its own,
    the project material's for a material without them."""
    settings = material_settings.settings_of(material)
    if settings.managed:
        shader_types = [item.shader_type for item in settings.shaders]
    else:
        uri = material.get(tlm_blender.URI_PROPERTY, "")
        project = project_of(context)[0] if uri else None
        project_material = project.materials.get(uri) if project is not None else None
        shader_types = [shader.get("ShaderType") for shader in project_material.shaders] if project_material is not None else []

    shader_type = next((shader_type for shader_type in shader_types if gs_material.lights_emit_colors(shader_type)), None)
    return gs_material.type_label(shader_type) if shader_type is not None else None


def _draw_emit_colors_warning(layout: typing.Any, material: bpy.types.Material, context: bpy.types.Context) -> None:
    """Exporting refuses a PS2 model's rigid mesh drawn with a shader lighting its emit colors without them."""
    shader = _emit_lighting_shader(material, context)
    if shader is None or len(_lacking_emit_colors(material)) == 0:
        return

    warning = layout.row()
    warning.alert = True
    warning.label(text="A mesh drawn with it has no emit colors, which %s lights" % shader, icon="ERROR")
    layout.operator(TTT_OT_AddEmitColors.bl_idname, icon="LIGHT")


class TTT_UL_Shaders(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_property, index=0, flt_flag=0):
        label = gs_material.ENUM_LABELS["ShaderType"].get(item.shader_type, (item.shader_type, ""))[0]
        notes = []
        if item.blending:
            notes.append(gs_material.ENUM_LABELS["AlphaBlendPresets"].get(item.blend_preset, (item.blend_preset, ""))[0] if not item.custom_blend else "own blend")

        if item.alpha_test:
            notes.append("alpha test")

        if item.has_animation:
            notes.append("animated")

        text = "%d  %s" % (index, label) + ("  (%s)" % ", ".join(notes) if notes else "")
        layout.label(text=text, icon="TEXTURE" if item.texture_mapping else "SHADING_SOLID")


class TTT_PT_Material(bpy.types.Panel):
    bl_label = "Twin Tech Material"
    bl_idname = "TTT_PT_material"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "material"

    @classmethod
    def poll(cls, context):
        return _material(context) is not None

    def draw(self, context):
        layout = self.layout
        material = _material(context)
        settings = material_settings.settings_of(material)
        uri = material.get(tlm_blender.URI_PROPERTY, "")
        if not settings.managed:
            column = layout.column()
            if uri:
                column.label(text=uri, icon="MATERIAL")
                column.label(text="Drawn without the project material's settings", icon="INFO")
            else:
                column.label(text="Made in Blender: TT Lab makes it an unlit material of its picture", icon="INFO")

            row = layout.row()
            row.operator(TTT_OT_MaterialUseSettings.bl_idname, icon="SHADING_TEXTURE")
            row.operator(TTT_OT_PickProjectMaterial.bl_idname, icon="VIEWZOOM", text="")
            _draw_emit_colors_warning(layout, material, context)
            return

        box = layout.box()
        if uri:
            box.label(text=uri, icon="MATERIAL")
            if is_changed_in_blender(material, project_of(context)[0]):
                box.label(text="Changed in Blender: exporting the model changes the project's material", icon="MODIFIER")
        else:
            box.label(text="Made in Blender: TT Lab adds it to the project", icon="INFO")

        row = box.row(align=True)
        row.operator(TTT_OT_PickProjectMaterial.bl_idname, icon="VIEWZOOM")
        if uri:
            row.operator(TTT_OT_MaterialRevert.bl_idname, icon="LOOP_BACK", text="")
            row.operator(TTT_OT_MaterialDetach.bl_idname, icon="DUPLICATE", text="")

        row.operator(TTT_OT_ReloadProject.bl_idname, icon="FILE_REFRESH", text="")

        if any(_lacks_colors(blender_object) for blender_object in _users(material)):
            warning = layout.row()
            warning.alert = True
            warning.label(text="A mesh drawn with it has no vertex colors, it shows black", icon="ERROR")
            layout.operator(TTT_OT_AddVertexColors.bl_idname, icon="GROUP_VCOL")

        _draw_emit_colors_warning(layout, material, context)
        layout.use_property_split = True
        layout.prop(settings, "game_name")
        layout.prop(settings, "render_bucket")
        row = layout.row()
        row.template_list("TTT_UL_Shaders", "", settings, "shaders", settings, "active_shader", rows=2, maxrows=gs_material.MAX_SHADERS)
        buttons = row.column(align=True)
        buttons.operator(TTT_OT_MaterialShaderAdd.bl_idname, text="", icon="ADD")
        buttons.operator(TTT_OT_MaterialShaderRemove.bl_idname, text="", icon="REMOVE")
        buttons.separator()
        buttons.operator(TTT_OT_MaterialShaderMove.bl_idname, text="", icon="TRIA_UP").direction = -1
        buttons.operator(TTT_OT_MaterialShaderMove.bl_idname, text="", icon="TRIA_DOWN").direction = 1
        if len(settings.shaders) == 0:
            layout.label(text="Without shaders the game draws nothing of it", icon="ERROR")
        elif _draws_skins(material) and settings.shaders[0].shader_type != "LitSkinnedModel":
            warning = layout.row()
            warning.alert = True
            warning.label(text="A skin is drawn with it: the game only draws skins right with Lit Skinned Model first", icon="ERROR")


class _ShaderPanel:
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "material"
    bl_parent_id = "TTT_PT_material"

    @classmethod
    def poll(cls, context):
        if not material_settings.is_managed(_material(context)):
            return False

        settings = material_settings.settings_of(_material(context))
        return 0 <= settings.active_shader < len(settings.shaders)

    @staticmethod
    def shader(context) -> typing.Any:
        settings = material_settings.settings_of(_material(context))
        return settings.shaders[settings.active_shader]

    @staticmethod
    def field(layout: typing.Any, item: typing.Any, prop: str, shown: typing.Dict[str, typing.Any], **settings: typing.Any) -> None:
        row = layout.row()
        row.active = gs_material.read_when(shown, prop)
        row.prop(item, prop, **settings)


class TTT_PT_MaterialShader(_ShaderPanel, bpy.types.Panel):
    bl_label = "Shader"

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        shown = _shown(item)
        layout.prop(item, "shader_type")
        description = gs_material.ENUM_LABELS["ShaderType"].get(item.shader_type, ("", ""))[1]
        if description:
            layout.label(text=description, icon="INFO")

        layout.prop(item, "texture_mapping")
        column = layout.column()
        column.active = item.texture_mapping
        column.template_ID(item, "image", open="image.open")
        row = column.row(align=True)
        row.operator(TTT_OT_PickProjectTexture.bl_idname, icon="VIEWZOOM")
        if item.texture:
            column.label(text=item.texture, icon="TEXTURE")
        elif item.image is not None:
            column.label(text="Goes into the model file, TT Lab makes it a texture", icon="IMAGE_DATA")

        if gs_material.texture_source(item.shader_type) == "frame":
            column.label(text="This type draws the frame behind it, not the texture", icon="INFO")

        column.prop(item, "texture_filter")
        column.prop(item, "texture_coordinates")
        column.prop(item, "lod_k")
        column.prop(item, "lod_l")
        layout.separator()
        layout.prop(item, "shading")
        layout.prop(item, "receives_shadows")
        layout.prop(item, "fog")
        layout.prop(item, "context")
        layout.prop(item, "anti_aliasing")
        self.field(layout, item, "int_param", shown)
        self.field(layout, item, "float_param", shown)


class TTT_PT_MaterialBlending(_ShaderPanel, bpy.types.Panel):
    bl_label = "Blending"

    def draw_header(self, context):
        self.layout.prop(self.shader(context), "blending", text="")

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        shown = _shown(item)
        layout.active = item.blending
        self.field(layout, item, "blend_preset", shown)
        layout.prop(item, "custom_blend")
        for prop in ("blend_a", "blend_b", "blend_c", "blend_d", "fixed_alpha"):
            self.field(layout, item, prop, shown)


class TTT_PT_MaterialAlphaTest(_ShaderPanel, bpy.types.Panel):
    bl_label = "Alpha Test"
    bl_options = {"DEFAULT_CLOSED"}

    def draw_header(self, context):
        self.layout.prop(self.shader(context), "alpha_test", text="")

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        shown = _shown(item)
        column = layout.column()
        column.active = item.alpha_test
        column.prop(item, "alpha_test_method")
        column.prop(item, "alpha_reference")
        column.prop(item, "alpha_fail")
        layout.prop(item, "destination_alpha_test")
        self.field(layout, item, "destination_alpha_mode", shown)


class TTT_PT_MaterialDepth(_ShaderPanel, bpy.types.Panel):
    bl_label = "Depth"
    bl_options = {"DEFAULT_CLOSED"}

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        self.field(layout, item, "depth_test", _shown(item))
        layout.prop(item, "depth_write")


class TTT_PT_MaterialScroll(_ShaderPanel, bpy.types.Panel):
    bl_label = "Scroll"
    bl_options = {"DEFAULT_CLOSED"}

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        shown = _shown(item)
        layout.prop(item, "scroll_u")
        layout.prop(item, "scroll_v")
        column = layout.column(align=True)
        column.active = gs_material.read_when(shown, "uv_scroll")
        for index, label in enumerate(("U Phase", "V Phase", "U Speed", "V Speed")):
            column.prop(item, "uv_scroll", index=index, text=label)


class TTT_PT_MaterialAnimation(_ShaderPanel, bpy.types.Panel):
    bl_label = "Animation"
    bl_options = {"DEFAULT_CLOSED"}

    def draw_header(self, context):
        self.layout.prop(self.shader(context), "has_animation", text="")

    def draw(self, context):
        layout = self.layout
        layout.use_property_split = True
        item = self.shader(context)
        shown = _shown(item)
        column = layout.column()
        column.active = item.has_animation
        column.prop(item, "anim_fps")
        column.prop(item, "anim_frames")
        if item.has_animation:
            step = material_settings.scene_fps() / item.anim_fps if item.anim_fps > 0 else 0.0
            column.label(text="Keyframe a track to animate it: the game's frame n is the scene's frame %.3g n" % step, icon="INFO")

        tracks = column.column(align=True)
        for prop in material_settings.TRACK_PROPS:
            tracks.prop(item, prop)

        self.field(layout, item, "animation_drives_color", shown)
        column = layout.column(align=True)
        column.active = gs_material.read_when(shown, "shader_color")
        for index, label in enumerate(("Shader Color X", "Y", "Z", "W")):
            column.prop(item, "shader_color", index=index, text=label)
