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

"""What a material sent into model files TT Lab hasn't read yet, kept on the material as JSON, without Blender.

Writing a model sends what Blender changed of its materials since their base (gs_material.changes). TT Lab reads the file, takes it and
writes the file again without it, so the next export into the same file finds that and takes what was sent there as the base: what TT
Lab has. A file TT Lab didn't read yet is written over with everything changed since the base, which stays."""

import json
import os
import typing

# The key at the top of the files the add-on writes, TT Lab's never have it
EXPORTED_BY_KEY = "exported_by"


def normal_path(path: str) -> str:
    return os.path.normcase(os.path.abspath(path))


def records(text: str) -> typing.Dict[str, typing.Any]:
    try:
        result = json.loads(text) if text else {}
    except ValueError:
        return {}

    return result if isinstance(result, dict) else {}


def with_sent(text: str, path: str, sequence: int, data: typing.Dict[str, typing.Any], pictures: typing.Dict[str, str]) -> str:
    """The records with what was sent into the file: the settings, their number among the material's sends and the pictures' SHA-256."""
    result = records(text)
    result[normal_path(path)] = {"seq": sequence, "data": data, "pictures": pictures}
    return json.dumps(result)


def taken(text: str, path: str, base_sequence: int) -> typing.Tuple[str, typing.Optional[typing.Dict[str, typing.Any]]]:
    """The records without what was sent into the file TT Lab read, and that record while it's newer than the base (TT Lab can read files
    in another order than they were written in)."""
    result = records(text)
    record = result.pop(normal_path(path), None)
    if not isinstance(record, dict) or int(record.get("seq", 0)) <= base_sequence:
        record = None

    return json.dumps(result), record


def read_by_tt_lab(json_tree: typing.Optional[typing.Dict[str, typing.Any]]) -> bool:
    """Whether the file is one TT Lab wrote, after reading what the add-on wrote there if it did."""
    return isinstance(json_tree, dict) and EXPORTED_BY_KEY not in json_tree
