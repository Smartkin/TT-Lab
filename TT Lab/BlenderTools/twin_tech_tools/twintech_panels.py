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

"""Panels editing what TT Lab keeps of objects, bones and materials, drawn from the schema."""

import re
import typing

import bpy

from . import schema
from . import twintech
from . import twintech_properties as properties

_PATH_PART = re.compile(r"^(\w+)(?:\[(\d+)\])?$")

# Hints for elements without a type, TT Lab still makes something out of them
_NONE_HINTS = {
    "Object": "Meshes TT Lab doesn't know become placed meshes in a scenery, and rigid bodies in the Rigid Bodies of a model",
    "Material": "Materials of TT Lab's projects are shown in the Twin Tech Project Material panel",
    "Bone": "Bones added in Blender become new joints",
}


def resolve(context: typing.Any, path: str) -> typing.Any:
    """Follows a path like object.ttt.scenery_lod.collisions[2].points from the context."""
    value = context
    for part in path.split("."):
        match = _PATH_PART.match(part)
        if match is None or value is None:
            return None

        value = getattr(value, match.group(1), None)
        if match.group(2) is not None and value is not None:
            index = int(match.group(2))
            value = value[index] if 0 <= index < len(value) else None

    return value


def _item_label(twin_type: schema.TwinType, item: typing.Any, index: int) -> str:
    if twin_type is schema.TREE_NODE:
        return "%d: %s, parent %d, slot %d" % (index, item.kind, item.parent, item.slot)

    return "%d" % index


class TTT_UL_Items(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_property, index=0, flt_flag=0):
        layout.label(text=_item_label(item.twin_type, item, index))


class TTT_OT_ListAdd(bpy.types.Operator):
    """Adds an item to the list"""

    bl_idname = "ttt.list_add"
    bl_label = "Add"
    bl_options = {"REGISTER", "UNDO", "INTERNAL"}

    path: bpy.props.StringProperty()

    def execute(self, context):
        collection = resolve(context, self.path)
        if collection is None:
            return {"CANCELLED"}

        collection.add()
        owner = resolve(context, self.path.rsplit(".", 1)[0])
        index_name = self.path.rsplit(".", 1)[1] + "_index"
        if owner is not None and hasattr(owner, index_name):
            setattr(owner, index_name, len(collection) - 1)

        return {"FINISHED"}


class TTT_OT_ListRemove(bpy.types.Operator):
    """Removes the item from the list"""

    bl_idname = "ttt.list_remove"
    bl_label = "Remove"
    bl_options = {"REGISTER", "UNDO", "INTERNAL"}

    path: bpy.props.StringProperty()
    index: bpy.props.IntProperty()

    def execute(self, context):
        collection = resolve(context, self.path)
        if collection is None or not 0 <= self.index < len(collection):
            return {"CANCELLED"}

        collection.remove(self.index)
        owner = resolve(context, self.path.rsplit(".", 1)[0])
        index_name = self.path.rsplit(".", 1)[1] + "_index"
        if owner is not None and hasattr(owner, index_name):
            setattr(owner, index_name, min(getattr(owner, index_name), len(collection) - 1))

        return {"FINISHED"}


class TTT_OT_ListMove(bpy.types.Operator):
    """Moves the item in the list, the order of lists is the order the game gets them in"""

    bl_idname = "ttt.list_move"
    bl_label = "Move"
    bl_options = {"REGISTER", "UNDO", "INTERNAL"}

    path: bpy.props.StringProperty()
    index: bpy.props.IntProperty()
    direction: bpy.props.IntProperty(default=1)

    def execute(self, context):
        collection = resolve(context, self.path)
        target = self.index + self.direction
        if collection is None or not 0 <= self.index < len(collection) or not 0 <= target < len(collection):
            return {"CANCELLED"}

        collection.move(self.index, target)
        owner = resolve(context, self.path.rsplit(".", 1)[0])
        index_name = self.path.rsplit(".", 1)[1] + "_index"
        if owner is not None and hasattr(owner, index_name):
            setattr(owner, index_name, target)

        return {"FINISHED"}


def _draw_field(layout: typing.Any, group: typing.Any, field: schema.Field, path: str) -> None:
    kind = field.kind
    if kind == "list":
        _draw_list(layout, group, field, path)
    elif kind == "vec4s":
        _draw_vectors(layout, group, field, path)
    elif kind == "box":
        box = layout.box()
        box.prop(group, field.attr + "_stored", text="Keep %s" % field.label)
        column = box.column(align=True)
        column.enabled = getattr(group, field.attr + "_stored")
        for label, start in (("Min", 0), ("Max", 4)):
            row = column.row(align=True)
            row.label(text=label)
            for index in range(start, start + 4):
                row.prop(group, field.attr, index=index, text="")
    elif kind == "matrix":
        column = layout.column(align=True)
        column.label(text=field.label)
        for row_index in range(4):
            row = column.row(align=True)
            for column_index in range(4):
                row.prop(group, field.attr, index=row_index * 4 + column_index, text="")
    elif kind in ("vec4", "color", "ivec3"):
        column = layout.column(align=True)
        column.label(text=field.label)
        column.row(align=True).prop(group, field.attr, text="")
    else:
        layout.prop(group, field.attr)


def _draw_fields(layout: typing.Any, group: typing.Any, twin_type: schema.TwinType, path: str, show_advanced: bool) -> None:
    for field in twin_type.fields:
        if not field.advanced:
            _draw_field(layout, group, field, "%s.%s" % (path, field.attr))

    advanced = [field for field in twin_type.fields if field.advanced]
    if show_advanced and len(advanced) > 0:
        box = layout.box()
        box.label(text="Kept as the game had them", icon="LOCKED")
        for field in advanced:
            _draw_field(box, group, field, "%s.%s" % (path, field.attr))


def _draw_list(layout: typing.Any, group: typing.Any, field: schema.Field, path: str) -> None:
    box = layout.box()
    box.label(text=field.label)
    row = box.row()
    collection = getattr(group, field.attr)
    row.template_list("TTT_UL_Items", path, group, field.attr, group, field.attr + "_index", rows=3)
    buttons = row.column(align=True)
    buttons.operator(TTT_OT_ListAdd.bl_idname, text="", icon="ADD").path = path
    active = getattr(group, field.attr + "_index")
    if 0 <= active < len(collection):
        remove = buttons.operator(TTT_OT_ListRemove.bl_idname, text="", icon="REMOVE")
        remove.path, remove.index = path, active
        up = buttons.operator(TTT_OT_ListMove.bl_idname, text="", icon="TRIA_UP")
        up.path, up.index, up.direction = path, active, -1
        down = buttons.operator(TTT_OT_ListMove.bl_idname, text="", icon="TRIA_DOWN")
        down.path, down.index, down.direction = path, active, 1
        _draw_fields(box, collection[active], field.item, "%s[%d]" % (path, active), True)


def _draw_vectors(layout: typing.Any, group: typing.Any, field: schema.Field, path: str) -> None:
    column = layout.column(align=True)
    header = column.row()
    header.label(text=field.label)
    header.operator(TTT_OT_ListAdd.bl_idname, text="", icon="ADD").path = path
    for index, item in enumerate(getattr(group, field.attr)):
        row = column.row(align=True)
        row.prop(item, "value", text="")
        remove = row.operator(TTT_OT_ListRemove.bl_idname, text="", icon="X")
        remove.path, remove.index = path, index


def _draw_container(layout: typing.Any, element: typing.Any, element_path: str, element_name: str) -> None:
    container = properties.get(element)
    if container is None:
        return

    layout.use_property_split = False
    layout.prop(container, "type")
    if container.type == schema.NONE_TYPE:
        if container.unknown:
            layout.label(text="Keeps TT Lab data this add-on doesn't know", icon="INFO")
        else:
            layout.label(text=_NONE_HINTS[element_name], icon="INFO")

        return

    twin_type = schema.find_type(container.types, container.type)
    if twin_type is None:
        return

    if twin_type.description:
        layout.label(text=twin_type.description, icon="INFO")

    _draw_fields(layout, getattr(container, twin_type.attr), twin_type, "%s.%s.%s" % (element_path, properties.PROPERTY, twin_type.attr), container.show_advanced)
    if any(field.advanced for field in twin_type.fields):
        layout.prop(container, "show_advanced", icon="LOCKED")

    if container.unknown:
        layout.prop(container, "unknown")


class TTT_PT_ObjectTwinTech(bpy.types.Panel):
    bl_label = "Twin Tech"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"

    @classmethod
    def poll(cls, context):
        return context.object is not None

    def draw(self, context):
        _draw_container(self.layout, context.object, "object", "Object")


class TTT_PT_BoneTwinTech(bpy.types.Panel):
    bl_label = "Twin Tech Joint"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "bone"

    @classmethod
    def poll(cls, context):
        return context.bone is not None

    def draw(self, context):
        _draw_container(self.layout, context.bone, "bone", "Bone")


class TTT_PT_MaterialTwinTech(bpy.types.Panel):
    bl_label = "Twin Tech Material"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "material"

    @classmethod
    def poll(cls, context):
        return context.material is not None

    def draw(self, context):
        _draw_container(self.layout, context.material, "material", "Material")
