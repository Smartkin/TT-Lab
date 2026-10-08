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

bl_info = {
    "name": "Twin Tech Tools",
    "author": "Smyshliaev \"Smartkin\" Vladislav",
    "description": "Blender tools to work with TwinTech Lab(TT-Lab) files",
    "blender": (4, 5, 0),
    "version": (0, 7, 0),
    "location": "Sidebar",
    "category": "Object",
    "warning": "",
}

# Wrap in try-except here so that we can use hatch's dynamic version detection
# without this failing. See the `[tool.hatch.version]` section in `pyproject.toml`.
import sys

# Installing the add-on again in a running Blender only imports this module again, the modules it's made of would keep running their
# old code until Blender restarts
for _module in [name for name in sys.modules if name.startswith(__name__ + ".")]:
    del sys.modules[_module]
    # The package keeps its submodules as attributes too, importing one again would find the old one there
    globals().pop(_module[len(__name__) + 1:].split(".")[0], None)

try:
    from . import auto_load
    from .properties_loader import register_props_for_blender_types, unregister_props_for_blender_types
    from .animator import subscribe_to_blender, unsubscribe_from_blender

    auto_load.init()

    def register():
        auto_load.register()
        register_props_for_blender_types()
        subscribe_to_blender()

    def unregister():
        unsubscribe_from_blender()
        unregister_props_for_blender_types()
        auto_load.unregister()

except ImportError as e:
    print(e.msg)
