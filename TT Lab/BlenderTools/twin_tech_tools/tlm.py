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

"""TT Lab model files (.tlm): a JSON tree with its arrays in binary data next to it, see TLM.md.

Nothing here needs Blender. Arrays are `array.array`s, which Blender's foreach_get and foreach_set take as they are.
"""

import array
import json
import struct
import sys
import typing

MAGIC = b"TLM\0"
FORMAT_VERSION = 1

# Type of a view and the typecode of the array holding its values
TYPECODES = {"f32": "f", "i32": "i", "u32": "I", "i16": "h", "u16": "H", "u8": "B"}


class TlmError(Exception):
    pass


class TlmFile:
    def __init__(self, asset_type: str = "", name: str = "", json_tree: typing.Optional[typing.Dict[str, typing.Any]] = None,
                 binary: bytes = b""):
        self.json = json_tree if json_tree is not None else {"asset": {"type": asset_type, "name": name}, "materials": []}
        self._binary = bytearray(binary)

    @property
    def asset_type(self) -> str:
        return str(self.json.get("asset", {}).get("type", ""))

    @property
    def name(self) -> str:
        return str(self.json.get("asset", {}).get("name", ""))

    @property
    def root(self) -> typing.Optional[typing.Dict[str, typing.Any]]:
        return self.json.get("root")

    @root.setter
    def root(self, value: typing.Dict[str, typing.Any]) -> None:
        self.json["root"] = value

    @property
    def materials(self) -> typing.List[typing.Dict[str, typing.Any]]:
        return self.json.setdefault("materials", [])

    def write_view(self, values: typing.Iterable[typing.Any], view_type: str) -> typing.Dict[str, typing.Any]:
        """Puts the values into the binary data and gives the view pointing at them."""
        data = values if isinstance(values, array.array) and values.typecode == TYPECODES[view_type] else array.array(TYPECODES[view_type], values)
        self._binary.extend(b"\0" * (-len(self._binary) & 3))
        offset = len(self._binary)
        self._binary.extend(_little_endian(data).tobytes())
        return {"offset": offset, "count": len(data), "type": view_type}

    def read_view(self, view: typing.Any, view_type: typing.Optional[str] = None) -> array.array:
        """The values a view points at, empty without one."""
        if not isinstance(view, dict):
            return array.array(TYPECODES[view_type or "f32"])

        stored_type = view.get("type")
        if stored_type not in TYPECODES or view_type is not None and stored_type != view_type:
            raise TlmError("A view of %s values was read as %s" % (stored_type, view_type))

        result = array.array(TYPECODES[stored_type])
        offset = int(view.get("offset", 0))
        count = int(view.get("count", 0))
        end = offset + count * result.itemsize
        if offset < 0 or count < 0 or end > len(self._binary):
            raise TlmError("A view points past the end of the binary data")

        result.frombytes(bytes(self._binary[offset:end]))
        return _little_endian(result)

    def to_bytes(self) -> bytes:
        text = json.dumps(self.json, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        text += b" " * (-len(text) & 3)
        return MAGIC + struct.pack("<II", FORMAT_VERSION, len(text)) + text + struct.pack("<I", len(self._binary)) + bytes(self._binary)

    def save(self, path: str) -> None:
        with open(path, "wb") as file:
            file.write(self.to_bytes())

    @staticmethod
    def from_bytes(data: bytes) -> "TlmFile":
        if data[:4] != MAGIC:
            raise TlmError("Not a TT Lab model file")

        version, json_length = struct.unpack_from("<II", data, 4)
        if version != FORMAT_VERSION:
            raise TlmError("TT Lab model file of version %d, this add-on reads version %d" % (version, FORMAT_VERSION))

        json_end = 12 + json_length
        tree = json.loads(data[12:json_end].decode("utf-8"), parse_int=_parse_int)
        binary_length, = struct.unpack_from("<I", data, json_end)
        return TlmFile(json_tree=tree, binary=data[json_end + 4:json_end + 4 + binary_length])

    @staticmethod
    def load(path: str) -> "TlmFile":
        with open(path, "rb") as file:
            return TlmFile.from_bytes(file.read())


def _parse_int(text: str) -> typing.Any:
    # The game has negative zeros, which TT Lab writes as -0
    return -0.0 if text == "-0" else int(text)


def _little_endian(values: array.array) -> array.array:
    if sys.byteorder == "little" or values.itemsize == 1:
        return values

    swapped = array.array(values.typecode, values)
    swapped.byteswap()
    return swapped


# Nodes

def node(kind: str, name: str, data: typing.Optional[typing.Dict[str, typing.Any]] = None) -> typing.Dict[str, typing.Any]:
    result: typing.Dict[str, typing.Any] = {"kind": kind, "name": name}
    if data is not None:
        result["data"] = data

    return result


def children(tree_node: typing.Optional[typing.Dict[str, typing.Any]], kind: typing.Optional[str] = None) -> typing.List[typing.Dict[str, typing.Any]]:
    if not isinstance(tree_node, dict):
        return []

    return [child for child in tree_node.get("children", []) if isinstance(child, dict) and (kind is None or child.get("kind") == kind)]


def find_child(tree_node: typing.Optional[typing.Dict[str, typing.Any]], kind: str) -> typing.Optional[typing.Dict[str, typing.Any]]:
    found = children(tree_node, kind)
    return found[0] if len(found) > 0 else None


def traverse(tree_node: typing.Optional[typing.Dict[str, typing.Any]]) -> typing.Iterator[typing.Dict[str, typing.Any]]:
    """The node and every node under it, parents before their children."""
    if not isinstance(tree_node, dict):
        return

    yield tree_node
    for child in children(tree_node):
        yield from traverse(child)


def add_child(tree_node: typing.Dict[str, typing.Any], child: typing.Dict[str, typing.Any]) -> typing.Dict[str, typing.Any]:
    tree_node.setdefault("children", []).append(child)
    return child
