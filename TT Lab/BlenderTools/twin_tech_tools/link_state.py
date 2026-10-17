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

"""Linking a material again, without Blender: what Blender changed since its snapshot of TT Lab's (gs_material.changes) goes over what TT
Lab has now, the way TT Lab takes it (BlenderAssets.WithChanges), and that's what both have from then on."""

import copy
import typing

from . import gs_material


def apply(theirs: typing.Dict[str, typing.Any], data: typing.Dict[str, typing.Any], changed: typing.Optional[typing.List[typing.List[typing.Any]]],
          sources: typing.Optional[typing.List[int]]) -> typing.Dict[str, typing.Any]:
    """TT Lab's material with Blender's changes: its shaders in Blender's order from their sources (-1 Blender's own), then the changed
    paths' values set. Without changed all of Blender's."""
    if changed is None:
        return copy.deepcopy(data)

    result = copy.deepcopy(theirs)
    if sources is not None:
        their_shaders = gs_material._shaders(theirs)
        blender_shaders = gs_material._shaders(data)
        result["Shaders"] = [copy.deepcopy(their_shaders[source] if 0 <= source < len(their_shaders) else blender_shaders[index])
                             for index, source in enumerate(sources) if index < len(blender_shaders)]

    for path in changed:
        found, value = _find(data, path)
        if found:
            _set(result, path, copy.deepcopy(value))

    return result


def _find(root: typing.Any, path: typing.List[typing.Any]) -> typing.Tuple[bool, typing.Any]:
    node = root
    for step in path:
        if isinstance(node, dict) and isinstance(step, str) and step in node:
            node = node[step]
        elif isinstance(node, list) and isinstance(step, int) and 0 <= step < len(node):
            node = node[step]
        else:
            return False, None

    return True, node


def _set(root: typing.Any, path: typing.List[typing.Any], value: typing.Any) -> None:
    if not path:
        return

    found, parent = _find(root, path[:-1])
    last = path[-1]
    if not found:
        return

    if isinstance(parent, dict) and isinstance(last, str):
        parent[last] = value
    elif isinstance(parent, list) and isinstance(last, int) and 0 <= last < len(parent):
        parent[last] = value


def relink(base: typing.Optional[typing.Dict[str, typing.Any]], known: bool, shown: typing.Dict[str, typing.Any], theirs: typing.Dict[str, typing.Any]
           ) -> typing.Tuple[typing.Dict[str, typing.Any], typing.Optional[typing.Dict[str, typing.Any]]]:
    """What the material is once it's linked again: TT Lab's with what Blender changed since the base over it, and what to send TT Lab for
    it (gs_material.changes' data, changed and shader sources), none when Blender changed nothing."""
    data, changed, sources = gs_material.changes(base, shown, known)
    if changed is not None and not changed and sources is None:
        return copy.deepcopy(theirs), None

    return apply(theirs, data, changed, sources), {"data": data, "changed": changed, "shader_sources": sources}
