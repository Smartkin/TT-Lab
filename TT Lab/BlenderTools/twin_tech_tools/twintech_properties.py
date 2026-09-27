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

"""Properties holding what TT Lab keeps in the extras of objects, bones, materials and scenes, made from the schema.

Every object, bone and material gets a `ttt` group with the TwinTech type of the element and a group of properties for every
type. Importing TT Lab model files fills them from the data of the nodes, exporting writes them back.
"""

import typing

import bpy

from . import schema
from . import twintech

PROPERTY = "ttt"

# Blender element the properties are on, and the types it can be
ELEMENT_TYPES = (
    (bpy.types.Object, "Object", schema.OBJECT_TYPES),
    (bpy.types.Material, "Material", schema.MATERIAL_TYPES),
    (bpy.types.Bone, "Bone", schema.BONE_TYPES),
)


class TTT_Vector4(bpy.types.PropertyGroup):
    value: bpy.props.FloatVectorProperty(name="Value", size=4, precision=5)


def _range(field: schema.Field, minimum: typing.Optional[int], maximum: typing.Optional[int]) -> typing.Dict[str, int]:
    limits = {}
    minimum = field.minimum if field.minimum is not None else minimum
    maximum = field.maximum if field.maximum is not None else maximum
    if minimum is not None:
        limits["min"] = minimum

    if maximum is not None:
        limits["max"] = maximum

    return limits


def _properties(field: schema.Field, item_groups: typing.Dict[str, type]) -> typing.Dict[str, typing.Any]:
    """The Blender properties of a field, the ones some kinds need next to the value included."""
    kind = field.kind
    common = {"name": field.label, "description": field.description}
    default = field.default_value()
    if kind in ("int", "uint"):
        return {field.attr: bpy.props.IntProperty(default=default, **_range(field, None, None), **common)}

    if kind in ("byte", "short", "ushort"):
        limits = {"byte": (0, 255), "short": (-32768, 32767), "ushort": (0, 65535)}[kind]
        return {field.attr: bpy.props.IntProperty(default=default, **_range(field, *limits), **common)}

    if kind == "float":
        return {field.attr: bpy.props.FloatProperty(default=default, precision=5, **common)}

    if kind == "bool":
        return {field.attr: bpy.props.BoolProperty(default=default, **common)}

    if kind in ("string", "ints", "floats", "lights"):
        return {field.attr: bpy.props.StringProperty(default=default, **common)}

    if kind == "enum":
        items = [(name, name, "%s (%d)" % (name, value)) for name, value in field.enum_items()]
        return {field.attr: bpy.props.EnumProperty(items=items, default=default, **common)}

    if kind == "fog":
        items = [(str(index), name, "Fog color %d, RGBA %s" % (index, rgba)) for index, (name, rgba) in enumerate(schema.FOG_COLORS)]
        return {field.attr: bpy.props.EnumProperty(items=items, default=default, **common)}

    if kind in ("vec4", "color", "matrix"):
        size = 16 if kind == "matrix" else 4
        extra = {"subtype": "COLOR", "soft_min": 0.0, "soft_max": 1.0} if kind == "color" else {}
        return {field.attr: bpy.props.FloatVectorProperty(size=size, default=default, precision=5, **extra, **common)}

    if kind == "ivec3":
        return {field.attr: bpy.props.IntVectorProperty(size=3, default=default, **common)}

    if kind == "vec4s":
        return {field.attr: bpy.props.CollectionProperty(type=TTT_Vector4, **common)}

    if kind == "box":
        return {
            field.attr: bpy.props.FloatVectorProperty(size=8, precision=5, **common),
            field.attr + "_stored": bpy.props.BoolProperty(name="Keep Box", description="Off makes TT Lab work the box out from the mesh", default=False),
        }

    if kind == "list":
        return {
            field.attr: bpy.props.CollectionProperty(type=item_groups[field.item.name], **common),
            field.attr + "_index": bpy.props.IntProperty(name="Active", default=0),
        }

    raise ValueError(kind)


def _make_group(class_name: str, twin_type: schema.TwinType, item_groups: typing.Dict[str, type]) -> type:
    annotations: typing.Dict[str, typing.Any] = {}
    for field in twin_type.fields:
        annotations.update(_properties(field, item_groups))

    return type(class_name, (bpy.types.PropertyGroup,), {"__annotations__": annotations, "__module__": __name__, "twin_type": twin_type})


def _make_container(element_name: str, types: typing.List[schema.TwinType], groups: typing.Dict[str, type]) -> type:
    items = [(schema.NONE_TYPE, "None", "Not something of TT Lab's")] + [(twin_type.name, twin_type.label, twin_type.description) for twin_type in types]
    annotations: typing.Dict[str, typing.Any] = {
        "type": bpy.props.EnumProperty(name="Twin Tech Type", items=items, default=schema.NONE_TYPE,
                                       description="What TT Lab makes out of it, None leaves it to TT Lab to guess"),
        "unknown": bpy.props.StringProperty(name="Unknown Values", description="What TT Lab wrote that the add-on doesn't know, as JSON. It's written back as it is"),
        "show_advanced": bpy.props.BoolProperty(name="Game Values", description="Shows the values TT Lab keeps as the game had them"),
    }
    for twin_type in types:
        annotations[twin_type.attr] = bpy.props.PointerProperty(type=groups[twin_type.name])

    return type("TTT_%sTwinTech" % element_name, (bpy.types.PropertyGroup,), {"__annotations__": annotations, "__module__": __name__, "types": types})


# Items of lists first, the groups of the types use them
_ITEM_GROUPS: typing.Dict[str, type] = {}
for _item_type in schema.SUBTYPES:
    _ITEM_GROUPS[_item_type.name] = _make_group("TTT_Item%s" % _item_type.name, _item_type, _ITEM_GROUPS)
    globals()[_ITEM_GROUPS[_item_type.name].__name__] = _ITEM_GROUPS[_item_type.name]

CONTAINERS: typing.Dict[str, type] = {}
for _blender_type, _element_name, _types in ELEMENT_TYPES:
    _groups = {}
    for _twin_type in _types:
        _groups[_twin_type.name] = _make_group("TTT_%s%s" % (_element_name, _twin_type.name), _twin_type, _ITEM_GROUPS)
        globals()[_groups[_twin_type.name].__name__] = _groups[_twin_type.name]

    CONTAINERS[_element_name] = _make_container(_element_name, _types, _groups)
    globals()[CONTAINERS[_element_name].__name__] = CONTAINERS[_element_name]


def register():
    for blender_type, element_name, _ in ELEMENT_TYPES:
        setattr(blender_type, PROPERTY, bpy.props.PointerProperty(type=CONTAINERS[element_name]))


def unregister():
    for blender_type, _, _ in ELEMENT_TYPES:
        if hasattr(blender_type, PROPERTY):
            delattr(blender_type, PROPERTY)


def get(element: typing.Any) -> typing.Any:
    """The TwinTech properties of an object, bone or material. Pose bones use their bone's."""
    element = getattr(element, "bone", element)
    return getattr(element, PROPERTY, None)


def read(element: typing.Any, extras: typing.Any) -> bool:
    """Fills the element's properties from {"TwinTech": {"Type": ..., <data>}}. Returns whether it had anything of TT Lab's."""
    container = get(element)
    if container is None:
        return False

    return twintech.read_container(container, container.types, extras)


def write(element: typing.Any) -> typing.Optional[typing.Dict[str, typing.Any]]:
    """The TwinTech dictionary of the element, None when it has none."""
    container = get(element)
    if container is None:
        return None

    return twintech.write_container(container, container.types)
