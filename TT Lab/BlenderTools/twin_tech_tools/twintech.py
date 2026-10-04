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

"""Moves values between the data of TT Lab model files' nodes and the add-on's properties.

A container is an element's `ttt` group: its `type`, the JSON of what the add-on doesn't know in `unknown`, and a group of
properties for every type, named after the type. The functions only use attributes and the collections' add and clear, so
they work on Blender's properties and on stand-ins alike.
"""

import typing

from . import schema


def assign(group: typing.Any, twin_type: schema.TwinType, values: typing.Dict[str, typing.Any]) -> None:
    """Sets the properties of a type's group to the values read from the extras."""
    for field in twin_type.fields:
        value = values.get(field.attr, field.default_value())
        kind = field.kind
        if kind == "box":
            numbers, stored = value
            setattr(group, field.attr, tuple(numbers) if stored else (0.0,) * 8)
            setattr(group, field.attr + "_stored", stored)
        elif kind == "vec4s":
            collection = getattr(group, field.attr)
            collection.clear()
            for vector in value:
                collection.add().value = tuple(vector)
        elif kind == "list":
            collection = getattr(group, field.attr)
            collection.clear()
            for item_values in value:
                assign(collection.add(), field.item, item_values)
        else:
            setattr(group, field.attr, tuple(value) if isinstance(value, (list, tuple)) else value)


def collect(group: typing.Any, twin_type: schema.TwinType) -> typing.Dict[str, typing.Any]:
    """The values of a type's group, to be written into the extras."""
    values: typing.Dict[str, typing.Any] = {}
    for field in twin_type.fields:
        kind = field.kind
        value = getattr(group, field.attr)
        if kind == "box":
            values[field.attr] = (tuple(value), bool(getattr(group, field.attr + "_stored")))
        elif kind == "vec4s":
            values[field.attr] = [tuple(item.value) for item in value]
        elif kind == "list":
            values[field.attr] = [collect(item, field.item) for item in value]
        elif kind in ("vec3", "vec4", "color", "matrix", "ivec3"):
            values[field.attr] = tuple(value)
        else:
            values[field.attr] = value

    return values


def read_container(container: typing.Any, types: typing.List[schema.TwinType], extras: typing.Any) -> bool:
    """Fills the container from the element's extras or custom properties. Returns whether they had anything of TT Lab's."""
    type_name, values, unknown = schema.read_twin(types, extras)
    if type_name == schema.NONE_TYPE and unknown == "":
        return False

    container.type = type_name
    container.unknown = unknown
    twin_type = schema.find_type(types, type_name)
    if twin_type is not None:
        assign(getattr(container, twin_type.attr), twin_type, values)

    return True


def write_container(container: typing.Any, types: typing.List[schema.TwinType]) -> typing.Optional[typing.Dict[str, typing.Any]]:
    twin_type = schema.find_type(types, container.type)
    values = collect(getattr(container, twin_type.attr), twin_type) if twin_type is not None else {}
    return schema.write_twin(types, container.type, values, container.unknown)
