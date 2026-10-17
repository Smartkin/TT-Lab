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

"""The Twin Tech Checks panel: everything checks finds in the model, with what selects the objects it's about and fixes it."""

import textwrap
import typing

import bpy

from . import checks
from . import material_ui
from . import tlm_blender
from . import twintech_properties as properties

# Problems' subjects go to the operators as one string
_SEPARATOR = "\x1f"


def _objects(names: str) -> typing.List[bpy.types.Object]:
    return [bpy.data.objects[name] for name in names.split(_SEPARATOR) if name in bpy.data.objects]


class TTT_OT_SelectProblem(bpy.types.Operator):
    """Selects the objects the problem is about"""

    bl_idname = "ttt.select_problem"
    bl_label = "Select"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        found = _objects(self.subjects)
        if not found:
            self.report({"WARNING"}, "None of them is an object of the scene")
            return {"CANCELLED"}

        for selected in context.selected_objects:
            selected.select_set(False)

        for blender_object in found:
            if blender_object.visible_get():
                blender_object.select_set(True)

        context.view_layer.objects.active = found[0]
        return {"FINISHED"}


class TTT_OT_ConvertToMesh(bpy.types.Operator):
    """Converts the objects to meshes, which the model can have"""

    bl_idname = "ttt.convert_to_mesh"
    bl_label = "Convert to Meshes"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        found = [blender_object for blender_object in _objects(self.subjects) if blender_object.type in ("CURVE", "SURFACE", "META", "FONT", "CURVES", "POINTCLOUD")]
        if not found:
            self.report({"ERROR"}, "None of them converts to a mesh: delete them or move them out of the model's root")
            return {"CANCELLED"}

        if context.object is not None and context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

        for selected in context.selected_objects:
            selected.select_set(False)

        for blender_object in found:
            blender_object.select_set(True)

        context.view_layer.objects.active = found[0]
        bpy.ops.object.convert(target="MESH")
        return {"FINISHED"}


class TTT_OT_MakeBlendSkin(bpy.types.Operator):
    """Makes the mesh part of the blend skin, whose meshes' shape keys are the shapes the facial animations blend"""

    bl_idname = "ttt.make_blend_skin"
    bl_label = "Make It the Blend Skin"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        for blender_object in _objects(self.subjects):
            container = properties.get(blender_object)
            if container is not None:
                container.type = "BlendSkin"

            if blender_object.get(tlm_blender.KIND_PROPERTY) == "skin":
                blender_object[tlm_blender.KIND_PROPERTY] = "shape"

        return {"FINISHED"}


class TTT_OT_SplitMaterial(bpy.types.Operator):
    """Gives the rigid parts drawn with the material a copy of it of their own: the skin keeps the material, the copy draws like rigid parts"""

    bl_idname = "ttt.split_material"
    bl_label = "Split Into Skin and Rigid"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        from . import materials

        names = [name for name in self.subjects.split(_SEPARATOR) if name in bpy.data.materials]
        if not names:
            self.report({"ERROR"}, "The material isn't there any more")
            return {"CANCELLED"}

        root = tlm_blender.find_root(context.object)
        for name in names:
            copy = materials.split_rigid(bpy.data.materials[name], root)
            if copy is not None:
                self.report({"INFO"}, "The rigid parts draw with %s now" % copy.name)

        return {"FINISHED"}


class TTT_OT_AddEmitColorsTo(bpy.types.Operator):
    """Gives the meshes emit colors, a copy of their vertex colors"""

    bl_idname = "ttt.add_emit_colors_to"
    bl_label = "Add Emit Colors"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        meshes = [blender_object for blender_object in _objects(self.subjects) if blender_object.type == "MESH"]
        with material_ui._out_of_edit_mode(meshes):
            for blender_object in meshes:
                material_ui.add_emit_colors(blender_object.data)

        return {"FINISHED"}


class TTT_OT_RemoveChildOf(bpy.types.Operator):
    """Removes the objects' Child Of constraints to bones, leaving them where they are: they follow what they're parented to"""

    bl_idname = "ttt.remove_child_of"
    bl_label = "Remove Its Child Of"
    bl_options = {"REGISTER", "UNDO"}

    subjects: bpy.props.StringProperty(options={"HIDDEN"})

    def execute(self, context):
        # Where it is with the armatures at rest, which is what's exported: a pose's scales would be baked into it
        armatures = {armature: armature.data.pose_position for armature in bpy.data.objects if armature.type == "ARMATURE"}
        for armature in armatures:
            armature.data.pose_position = "REST"

        context.view_layer.update()
        try:
            for blender_object in _objects(self.subjects):
                world = blender_object.matrix_world.copy()
                for constraint in [constraint for constraint in blender_object.constraints
                                   if constraint.type == "CHILD_OF" and constraint.target is not None and constraint.target.type == "ARMATURE"]:
                    blender_object.constraints.remove(constraint)

                context.view_layer.update()
                blender_object.matrix_world = world
        finally:
            for armature, position in armatures.items():
                armature.data.pose_position = position

        return {"FINISHED"}


# What fixes a problem the checks name: their own operators take the problem's subjects
_FIXES = {"ttt.add_emit_colors": TTT_OT_AddEmitColorsTo.bl_idname}


def _wrapped(layout: typing.Any, text: str, width: int, icon: str) -> None:
    column = layout.column(align=True)
    for index, line in enumerate(textwrap.wrap(text, max(24, width))):
        column.label(text=line, icon=icon if index == 0 else "BLANK1")


def draw_problems(layout: typing.Any, context: bpy.types.Context, found: typing.List[checks.Problem], width: typing.Optional[int] = None) -> None:
    characters = width if width is not None else max(24, int(context.region.width / 7.5) - 6)
    for problem in found:
        box = layout.box()
        _wrapped(box, problem.text, characters, "ERROR" if problem.refused else "INFO")
        row = box.row(align=True)
        subjects = _SEPARATOR.join(problem.subjects)
        if problem.subjects and any(name in bpy.data.objects for name in problem.subjects):
            row.operator(TTT_OT_SelectProblem.bl_idname, icon="RESTRICT_SELECT_OFF").subjects = subjects

        if problem.fix:
            operator = _FIXES.get(problem.fix, problem.fix)
            row.operator(operator, icon="MODIFIER").subjects = subjects


class TTT_PT_Checks(bpy.types.Panel):
    """What keeps the model from being written into its file or TT Lab, and what it's written without"""

    bl_label = "Twin Tech Checks"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"

    @classmethod
    def poll(cls, context):
        return tlm_blender.find_root(context.object) is not None

    def draw_header(self, context):
        found = checks.cached(tlm_blender.find_root(context.object))
        refused = sum(1 for problem in found if problem.refused)
        self.layout.label(text="", icon="ERROR" if refused else ("INFO" if found else "CHECKMARK"))

    def draw(self, context):
        layout = self.layout
        found = checks.cached(tlm_blender.find_root(context.object))
        if not found:
            layout.label(text="Nothing keeps the model from being written", icon="CHECKMARK")
            return

        refused = [problem for problem in found if problem.refused]
        if refused:
            layout.label(text="%d %s it from being written" % (len(refused), "problem keeps" if len(refused) == 1 else "problems keep"), icon="CANCEL")

        draw_problems(layout, context, found)
