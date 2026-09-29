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

"""What TT Lab keeps in the data of its model files' nodes, and how it converts to the properties the add-on edits.

The add-on keeps a node's values as {"TwinTech": {"Type": ..., <fields>}}, the type coming from the node's kind. Every type and
its fields are described here once: their key in the data, the property the add-on shows for them and how values convert both
ways. Nothing here needs Blender, so the conversions can be tested without it.
"""

import json
import math
import os
import re
import struct
import typing

KEY = "TwinTech"
TYPE_KEY = "Type"
NONE_TYPE = "NONE"

with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "schema.json"), encoding="utf-8") as _file:
    _SCHEMA = json.load(_file)

ENUMS: typing.Dict[str, typing.List[typing.Tuple[str, int]]] = {name: [(item[0], int(item[1])) for item in items] for name, items in _SCHEMA["enums"].items()}
FOG_COLORS: typing.List[typing.Tuple[str, typing.Tuple[int, int, int, int]]] = [(name, tuple(rgba)) for name, rgba in _SCHEMA["fogColors"]]
LIGHTS_AMOUNT = 128


def snake_case(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).lower()


def title(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", name).replace("_", " ")


class Field:
    """One value of a type, stored under its key in the extras and shown as a property."""

    def __init__(self, key: str, kind: str, label: typing.Optional[str] = None, description: str = "", enum: typing.Optional[str] = None,
                 item: typing.Optional["TwinType"] = None, default: typing.Any = None, minimum: typing.Optional[int] = None,
                 maximum: typing.Optional[int] = None, advanced: bool = False):
        self.key = key
        self.attr = snake_case(key)
        self.kind = kind
        self.label = label or title(key)
        self.description = description
        self.enum = enum
        self.item = item
        self.default = default
        self.minimum = minimum
        self.maximum = maximum
        # Values the game had that TT Lab uses to stay exact, they're kept but shown apart from the rest
        self.advanced = advanced

    def enum_items(self) -> typing.List[typing.Tuple[str, int]]:
        return ENUMS[self.enum] if self.enum is not None else []

    def default_value(self) -> typing.Any:
        if self.default is not None:
            return self.default

        return {
            "int": 0, "uint": 0, "byte": 0, "short": 0, "ushort": 0, "float": 0.0, "bool": False, "string": "",
            "enum": self.enum_items()[0][0] if self.enum is not None else "", "fog": "0",
            "vec4": (0.0, 0.0, 0.0, 0.0), "color": (1.0, 1.0, 1.0, 1.0), "ivec3": (0, 0, 0),
            "matrix": tuple(1.0 if i % 5 == 0 else 0.0 for i in range(16)), "ints": "", "floats": "", "lights": "",
            "vec4s": [], "box": ((0.0,) * 8, False), "list": [],
        }[self.kind]

    def from_extras(self, value: typing.Any) -> typing.Any:
        """Converts a value of the extras into what the field's property holds."""
        value = plain(value)
        if value is None:
            return self.default_value()

        kind = self.kind
        if kind in ("int", "uint", "byte", "short", "ushort"):
            number = to_int(value, self.default_value())
            return to_signed32(number) if kind == "uint" else number

        if kind == "float":
            return to_float(value, self.default_value())

        if kind == "bool":
            return to_bool(value)

        if kind == "string":
            return str(value)

        if kind == "enum":
            return to_enum_name(self.enum_items(), value, self.default_value())

        if kind == "fog":
            index = to_int(value, 0)
            return str(index) if 0 <= index < len(FOG_COLORS) else "0"

        if kind in ("vec4", "color", "matrix"):
            size = 16 if kind == "matrix" else 4
            numbers = [to_float(number, 0.0) for number in as_list(value)]
            return tuple(numbers) if len(numbers) == size else self.default_value()

        if kind == "ivec3":
            numbers = [to_signed32(to_int(number, 0)) for number in as_list(value)]
            return tuple(numbers) if len(numbers) == 3 else self.default_value()

        if kind == "ints":
            return format_ints(to_int(number, 0) for number in as_list(value))

        if kind == "floats":
            return format_floats(to_float(number, 0.0) for number in as_list(value))

        if kind == "lights":
            return format_ranges(index for index, flag in enumerate(as_list(value)) if to_bool(flag))

        if kind == "vec4s":
            numbers = [to_float(number, 0.0) for number in as_list(value)]
            return [tuple(numbers[i:i + 4]) for i in range(0, len(numbers) - 3, 4)]

        if kind == "box":
            numbers = [to_float(number, 0.0) for number in as_list(value)]
            return (tuple(numbers), True) if len(numbers) == 8 else self.default_value()

        if kind == "list":
            return [self.item.read_values(item)[0] for item in indexed(value)]

        raise ValueError(kind)

    def to_extras(self, value: typing.Any) -> typing.Any:
        """Converts what the field's property holds into its value in the extras."""
        kind = self.kind
        if kind in ("int", "uint", "byte", "short", "ushort"):
            return int(value)

        if kind == "float":
            return float(value)

        if kind == "bool":
            return bool(value)

        if kind in ("string", "enum"):
            return str(value)

        if kind == "fog":
            return int(value)

        if kind in ("vec4", "color", "matrix"):
            return [float(number) for number in value]

        if kind == "ivec3":
            return [int(number) for number in value]

        if kind == "ints":
            return parse_ints(value)

        if kind == "floats":
            return parse_floats(value)

        if kind == "lights":
            enabled = set(parse_ranges(value))
            return [index in enabled for index in range(LIGHTS_AMOUNT)]

        if kind == "vec4s":
            return [float(number) for vector in value for number in vector]

        if kind == "box":
            numbers, stored = value
            # No box makes TT Lab work it out from the mesh
            return [float(number) for number in numbers] if stored else []

        if kind == "list":
            return {str(index): self.item.write_values(item, include_type=False) for index, item in enumerate(value)}

        raise ValueError(kind)


class TwinType:
    """A type of element TT Lab writes, told apart by the Type in its extras."""

    def __init__(self, name: str, label: str, fields: typing.List[Field], description: str = ""):
        self.name = name
        self.label = label
        self.fields = fields
        self.description = description
        self.attr = snake_case(name)
        self.by_key = {field.key: field for field in fields}

    def read_values(self, twin: typing.Any) -> typing.Tuple[typing.Dict[str, typing.Any], typing.Dict[str, typing.Any]]:
        """The values of the fields in a TwinTech dictionary, and the keys this add-on doesn't know."""
        twin = plain(twin)
        if not isinstance(twin, dict):
            twin = {}

        values = {field.attr: field.from_extras(twin.get(field.key)) for field in self.fields}
        unknown = {key: value for key, value in twin.items() if key != TYPE_KEY and key not in self.by_key}
        return values, unknown

    def write_values(self, values: typing.Dict[str, typing.Any], unknown: typing.Optional[typing.Dict[str, typing.Any]] = None,
                     include_type: bool = True) -> typing.Dict[str, typing.Any]:
        result: typing.Dict[str, typing.Any] = {}
        if include_type:
            result[TYPE_KEY] = self.name

        result.update(unknown or {})
        for field in self.fields:
            result[field.key] = field.to_extras(values[field.attr]) if field.attr in values else field.to_extras(field.default_value())

        return result


# Conversions of values that went through Blender's custom properties or were written by hand

def plain(value: typing.Any) -> typing.Any:
    """Turns Blender's ID property groups and arrays into dictionaries and lists."""
    if hasattr(value, "to_dict"):
        return value.to_dict()

    if hasattr(value, "to_list"):
        return value.to_list()

    return value


def as_list(value: typing.Any) -> typing.List[typing.Any]:
    value = plain(value)
    if isinstance(value, (list, tuple)):
        return list(value)

    if isinstance(value, dict):
        return [value[key] for key in sorted(value, key=index_order)]

    return []


def index_order(key: typing.Any) -> typing.Tuple[int, typing.Any]:
    text = str(key)
    return (0, int(text)) if text.lstrip("-").isdigit() else (1, text)


def indexed(value: typing.Any) -> typing.List[typing.Dict[str, typing.Any]]:
    """Items of a list written as a dictionary keyed by their index, lists of objects can't be custom properties."""
    return [plain(item) for item in as_list(value) if isinstance(plain(item), dict)]


def to_int(value: typing.Any, fallback: int) -> int:
    if isinstance(value, bool):
        return int(value)

    if isinstance(value, int):
        return value

    if isinstance(value, float):
        return int(round(value)) if math.isfinite(value) else fallback

    if isinstance(value, str):
        text = value.strip()
        try:
            return int(text, 16) if text.lower().startswith("0x") else int(text)
        except ValueError:
            try:
                return int(round(float(text)))
            except ValueError:
                return fallback

    return fallback


def to_signed32(value: int) -> int:
    """The signed integer with the same 32 bits, TT Lab writes unsigned values that way."""
    value &= 0xFFFFFFFF
    return value - 0x100000000 if value > 0x7FFFFFFF else value


def to_float(value: typing.Any, fallback: float) -> float:
    if isinstance(value, bool):
        return float(value)

    if isinstance(value, (int, float)):
        return float(value)

    if isinstance(value, str):
        try:
            return float(value)
        except ValueError:
            return fallback

    return fallback


def to_float32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def to_bool(value: typing.Any) -> bool:
    if isinstance(value, str):
        return value.strip().lower() in ("true", "1", "yes", "on")

    return bool(value)


def to_enum_name(items: typing.List[typing.Tuple[str, int]], value: typing.Any, fallback: str) -> str:
    if isinstance(value, str):
        for name, _ in items:
            if name.lower() == value.strip().lower():
                return name

        value = to_int(value, None)  # type: ignore[arg-type]

    if isinstance(value, (int, float)) and not isinstance(value, bool):
        for name, number in items:
            if number == int(value):
                return name

    return fallback


def format_ints(values: typing.Iterable[int]) -> str:
    return ", ".join(str(value) for value in values)


def parse_ints(text: str) -> typing.List[int]:
    return [to_int(part, 0) for part in re.split(r"[\s,;]+", text.strip()) if part != ""]


def format_floats(values: typing.Iterable[float]) -> str:
    return ", ".join(repr(value) for value in values)


def parse_floats(text: str) -> typing.List[float]:
    return [to_float(part, 0.0) for part in re.split(r"[\s,;]+", text.strip()) if part != ""]


def format_ranges(indexes: typing.Iterable[int]) -> str:
    """Indexes as ranges, 0-3, 7."""
    parts = []
    start = previous = None
    for index in sorted(set(indexes)):
        if start is None:
            start = previous = index
        elif index == previous + 1:
            previous = index
        else:
            parts.append(str(start) if start == previous else "%d-%d" % (start, previous))
            start = previous = index

    if start is not None:
        parts.append(str(start) if start == previous else "%d-%d" % (start, previous))

    return ", ".join(parts)


def parse_ranges(text: str) -> typing.List[int]:
    result = []
    for part in re.split(r"[\s,;]+", text.strip()):
        if part == "":
            continue

        bounds = part.split("-", 1)
        start = to_int(bounds[0], -1)
        end = to_int(bounds[1], -1) if len(bounds) == 2 else start
        result.extend(index for index in range(start, end + 1) if 0 <= index < LIGHTS_AMOUNT)

    return result


# The types TT Lab writes

TREE_NODE = TwinType("SceneryTreeNode", "Tree node", [
    Field("LightsEnabler", "lights", label="Lights", description="Lights that light what the node holds, like 0-3, 7"),
    Field("Kind", "enum", enum="SceneryType", default="Leaf", description="TT Lab works it out from the tree nodes under this one", advanced=True),
    Field("Slot", "int", minimum=0, maximum=7, description="Which of its parent's 8 children this node is", advanced=True),
    Field("BoundsMin", "vec4", label="Bounds Min", description="Smallest corner of the node's cell, the game finds the node holding a box by it. Grows to hold what's placed in it",
          advanced=True),
    Field("BoundsMax", "vec4", label="Bounds Max", description="Biggest corner of the node's cell, grows to hold what's placed in it",
          advanced=True),
    Field("BoundsCenter", "vec4", label="Center And Radius", description="Center of the cell with its radius in W, the game never reads it", advanced=True),
    Field("BoundsHalfSize", "vec4", label="Half Size", description="Half the size of the cell, the game never reads it", advanced=True),
    Field("TreeDepth", "uint", description="The tools' depth of the tree, only the root has it and the game never uses it", advanced=True),
    Field("SceneryTypes", "ints", description="Kinds of the node's children, TT Lab works them out from the tree", advanced=True),
], "A box of the tree the game culls the scenery with. What's under it is culled with it, TT Lab moves meshes that left its box to the "
   "node they're in")


def _light_fields() -> typing.List[Field]:
    return [
        Field("Order", "int", description="Position among the lights of its kind"),
        Field("Color", "color"),
        Field("Intensity", "float", default=1.0, description="Multiplies the color"),
        Field("Enabled", "bool", default=True, description="Set on every light of the game, which never reads it", advanced=True),
        Field("PositionW", "float", default=1.0, advanced=True),
        Field("BoundsMin", "vec4", default=(0.0, 0.0, 0.0, 1.0), description="Bounds the tools kept, the game works them out again", advanced=True),
        Field("BoundsMax", "vec4", default=(0.0, 0.0, 0.0, 1.0), description="Bounds the tools kept, the game works them out again", advanced=True),
    ]


SCENE_TYPES: typing.List["TwinType"] = []

JOINT = TwinType("Joint", "Joint", [
        Field("Index", "int", minimum=0, description="Which of the game's joints the bone is, skins and animations refer to joints by it"),
        Field("ReactId", "byte", default=255, description="255 for joints that don't react"),
        Field("AdditionalAnimationRotation", "vec4", default=(0.0, 0.0, 0.0, 1.0), description="Rotation animations can add, as a quaternion"),
        Field("ChildrenAmt2", "int", label="Second Children Amount", advanced=True),
        Field("UnusedRotation", "vec4", advanced=True),
        Field("LocalTranslation", "vec4", description="Kept while the bone isn't moved", advanced=True),
        Field("LocalRotation", "vec4", description="Kept while the bone isn't turned", advanced=True),
        Field("WorldTranslation", "vec4", description="Kept while the bone isn't moved", advanced=True),
        Field("InverseBindMatrix", "matrix", description="Kept while the bone isn't moved", advanced=True),
    ], "One of the joints of a model's skeleton")

OBJECT_TYPES = [
    TwinType("Ogi", "OGI", [
        Field("BoundingBoxMin", "vec4", default=(0.0, 0.0, 0.0, 1.0)),
        Field("BoundingBoxMax", "vec4", default=(1.0, 1.0, 1.0, 1.0)),
    ], "A model with a skeleton: its armature, skin, shape, rigid bodies, exit points and collision hulls are under it"),
    TwinType("Skin", "Skin", [], "A mesh the armature deforms"),
    TwinType("BlendSkin", "Blend skin", [], "A mesh the armature deforms, whose shape keys are the shapes the facial animations blend"),
    TwinType("Body", "Rigid body", [Field("Order", "int", description="Position among the model's rigid bodies")],
             "A mesh following the bone its Child Of constraint targets"),
    TwinType("Scenery", "Scenery", [
        Field("FogColor", "fog"),
        Field("HasLighting", "bool"),
        Field("UnusedByte", "byte", description="Read into the chunk and never used, 0 on every level but one", advanced=True),
        Field("LightOrder", "ints", description="Index and kind of every light in the order the Xbox version lists them", advanced=True),
    ], "A level's scenery: the tree it's culled with, the lights, the collision and the dynamic scenery are under it"),
    TwinType("Skydome", "Skydome", [], "The sky of a level, its meshes are under it"),
    TwinType("Model", "Model", [], "A model's geometry, its parts have no materials"),
    TwinType("RigidModel", "Rigid model", [], "A model drawn with materials"),
    TwinType("Mesh", "Mesh", [], "A model placed by scenery, LODs and skydomes"),
    TwinType("SceneryMesh", "Scenery mesh", [
        Field("Order", "int", description="Position among the meshes of its tree node, new meshes go last", advanced=True),
        Field("Matrix", "matrix", description="Transform as the game has it, the object's replaces it once moved", advanced=True),
        Field("BoundingBox", "box", description="Box the game culls the mesh with, TT Lab works it out when it doesn't hold the mesh", advanced=True),
    ], "A mesh placed in the scenery, culled with the tree node it's under"),
    TwinType("SceneryLod", "Scenery LOD", [
        Field("Order", "int", description="Position among the LODs of its tree node, new LODs go last", advanced=True),
        Field("Matrix", "matrix", description="Transform as the game has it, the object's replaces it once moved", advanced=True),
        Field("LodType", "enum", enum="LodType", default="COMPRESSED"),
        Field("MinDrawDistance", "int"),
        Field("MaxDrawDistance", "int", default=65535),
        Field("ModelsDrawDistances", "ivec3", description="Distances the level meshes switch at"),
        Field("BoundingBox", "box", description="Box the game culls the LOD with, TT Lab works it out when it doesn't hold the LOD", advanced=True),
    ], "Meshes the game switches between by distance, every mesh under the LOD's object is a level"),
    TwinType("LodMesh", "LOD level", [Field("Level", "int", description="Which of the LOD's meshes this is, 0 is the closest")]),
    TwinType("AmbientLight", "Ambient light", _light_fields(), "Lights everything with its color times its intensity"),
    TwinType("DirectionalLight", "Directional light", _light_fields() + [
        Field("Leftover", "short", description="Never read by the game", advanced=True),
        Field("Direction", "vec4", description="Direction as the game has it, the object's rotation replaces it once turned", advanced=True),
    ], "Lights from one direction: the arrow points at where the light comes from"),
    TwinType("PointLight", "Point light", _light_fields() + [
        Field("AttenuationPower", "short", description="How many times the distance attenuation 25 / (d² + 25) multiplies the intensity, 0 to 2 in the game's levels"),
    ], "Lights what's around it, fading with the distance"),
    TwinType("NegativeLight", "Spot light", _light_fields() + [
        Field("ConeAngle", "uint", default=16384, description="Angle of the whole cone lit at full intensity, in 65536ths of a turn (182 per degree)"),
        Field("FalloffAngle", "uint", default=910, description="Angle the light fades out over past the cone, in 65536ths of a turn (182 per degree)"),
        Field("AttenuationPower", "ushort", description="How many times the distance attenuation 25 / (d² + 25) multiplies the intensity"),
        Field("SpotExponent", "ushort", description="Power the cosine of the angle to the axis is raised to within the cone, 0 in the game's levels"),
        Field("Direction", "vec4", description="Direction as the game has it, the object's rotation replaces it once turned", advanced=True),
        Field("InnerConeCosine", "float", description="Cosine the game lights with, made from the cone angle when it changed", advanced=True),
        Field("OuterConeCosine", "float", description="Cosine the game lights with, made from the angles when they changed", advanced=True),
    ], "A spot light (the tools' negative light): the arrow points where it shines"),
    TwinType("ExitPoint", "Exit point", [Field("Id", "uint", label="ID")],
             "A point other objects attach to, following the bone its Child Of constraint targets"),
    TwinType("CollisionHull", "Collision hull", [], "A convex mesh the game collides the model with, following the bone its Child Of constraint targets or "
                                                    "the model when it has none. The planes and axes the game reads are worked out from its faces"),
    TwinType("DynamicScenery", "Dynamic scenery", [], "Holds the dynamic models"),
    TwinType("DynamicSceneryModel", "Dynamic model", [
        Field("Order", "int", description="Position among the dynamic models"),
        Field("LodFlag", "byte"),
        Field("BoundingBoxMin", "vec4", default=(0.0, 0.0, 0.0, 1.0)),
        Field("BoundingBoxMax", "vec4", default=(1.0, 1.0, 1.0, 1.0)),
    ], "A scenery mesh moving by its animation, its collision hulls are under it"),
    TwinType("SkydomeMesh", "Skydome mesh", [Field("Order", "int", description="Position among the skydome's meshes")]),
    TwinType("Collision", "Collision", [
        Field("UnusedVertexes", "ints", advanced=True),
        Field("UnusedPositions", "floats", advanced=True),
    ], "The level's collision, every material is a surface"),
    TREE_NODE,
]



BONE_TYPES = [JOINT]

MATERIAL_TYPES = [
    TwinType("CollisionSurface", "Collision surface", [
        Field("Surface", "string", description="URI of the collision surface asset, materials named after a surface use that surface otherwise"),
    ]),
]

SUBTYPES: typing.List[TwinType] = []


def find_type(types: typing.List[TwinType], name: typing.Any) -> typing.Optional[TwinType]:
    return next((twin_type for twin_type in types if twin_type.name == name), None)


def read_twin(types: typing.List[TwinType], extras: typing.Any) -> typing.Tuple[str, typing.Dict[str, typing.Any], str]:
    """The type, the values and the JSON of what the add-on doesn't know, out of an element's extras or custom properties.

    A type the add-on doesn't know is kept as it is in the JSON, with a type of NONE.
    """
    extras = plain(extras)
    twin = plain(extras.get(KEY)) if isinstance(extras, dict) else None
    if not isinstance(twin, dict):
        return NONE_TYPE, {}, ""

    twin_type = find_type(types, twin.get(TYPE_KEY))
    if twin_type is None:
        return NONE_TYPE, {}, json.dumps(twin)

    values, unknown = twin_type.read_values(twin)
    return twin_type.name, values, json.dumps(unknown) if len(unknown) > 0 else ""


def write_twin(types: typing.List[TwinType], type_name: str, values: typing.Dict[str, typing.Any], unknown_json: str) -> typing.Optional[typing.Dict[str, typing.Any]]:
    """The TwinTech dictionary of an element, None when it has none."""
    unknown = load_json(unknown_json)
    twin_type = find_type(types, type_name)
    if twin_type is None:
        return unknown if TYPE_KEY in unknown else None

    return twin_type.write_values(values, unknown)


def load_json(text: str) -> typing.Dict[str, typing.Any]:
    if not text:
        return {}

    try:
        value = json.loads(text)
    except ValueError:
        return {}

    return value if isinstance(value, dict) else {}
