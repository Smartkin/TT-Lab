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

import bpy

from bpy.types import PropertyGroup


class ObjectAsBoneSettings(PropertyGroup):
    animationLink: bpy.props.PointerProperty(type=bpy.types.Action)
    independentScaling: bpy.props.BoolProperty(name="Independent scaling", default=False)

class NodeHasIndependentScaling(PropertyGroup):
    links: bpy.props.CollectionProperty(type=ObjectAsBoneSettings)

    def as_dict(self):
        return {item.animationLink.name: item.independentScaling for item in self.items}

    def from_dict(self, data):
        self.links.clear()
        for k, v in data.items():
            if k != "NodeHasIndependentScaling":
                continue

            animationPairs: dict[str, bool] = v.to_dict()
            for action_name, enabled in animationPairs.items():
                action = bpy.data.actions.get(action_name)
                if action is None:
                    continue
                
                entry = self.links.add()
                entry.animationLink = action
                entry.independentScaling = enabled