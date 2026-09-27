"""Stand-ins for the parts of Blender the add-on touches, so its logic can be tested without Blender."""

import importlib
import os
import sys
import types

ADDON_DIRECTORY = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "twin_tech_tools")


class Action:
    def __init__(self, name):
        self.name = name
        self.custom = {}

    def __contains__(self, key):
        return key in self.custom

    def __getitem__(self, key):
        return self.custom[key]

    def __setitem__(self, key, value):
        self.custom[key] = value


class Actions(list):
    """bpy.data.actions, iterable and looked up by name."""

    def get(self, name):
        return next((action for action in self if action.name == name), None)


class FlagsEntry:
    def __init__(self):
        self.action = None
        self.independent_scaling = False
        self.uses_additional_rotation = False
        self.imported = False
        self.imported_independent_scaling = False
        self.imported_uses_additional_rotation = False


class FlagsCollection(list):
    def add(self):
        entry = FlagsEntry()
        self.append(entry)
        return entry

    # Blender's collections remove by index
    def remove(self, index):
        del self[index]


class Bone:
    def __init__(self, name):
        self.name = name
        self.inherit_scale = "FULL"
        self.ttt_animation_flags = FlagsCollection()


class IdProperty(dict):
    """Blender's ID properties hand dictionaries out as objects with to_dict."""

    def to_dict(self):
        return dict(self)


class Collection(list):
    """A collection property, adding items made by its factory."""

    def __init__(self, factory):
        super().__init__()
        self.factory = factory

    def add(self):
        item = self.factory()
        self.append(item)
        return item

    def clear(self):
        del self[:]

    def remove(self, index):
        del self[index]


class Group:
    """The add-on's properties of a TwinTech type, made from the schema the way the add-on makes them."""

    def __init__(self, twin_type):
        self.twin_type = twin_type
        for field in twin_type.fields:
            if field.kind == "flags":
                setattr(self, field.attr, set())
                setattr(self, field.attr + "_other", "0")
            elif field.kind == "box":
                setattr(self, field.attr, (0.0,) * 8)
                setattr(self, field.attr + "_stored", False)
            elif field.kind == "vec4s":
                setattr(self, field.attr, Collection(lambda: types.SimpleNamespace(value=(0.0, 0.0, 0.0, 0.0))))
            elif field.kind == "list":
                setattr(self, field.attr, Collection(lambda item=field.item: Group(item)))
                setattr(self, field.attr + "_index", 0)
            else:
                setattr(self, field.attr, field.default_value())


class Container:
    """An element's ttt property group."""

    def __init__(self, twin_types):
        self.types = twin_types
        self.type = "NONE"
        self.unknown = ""
        self.show_advanced = False
        for twin_type in twin_types:
            setattr(self, twin_type.attr, Group(twin_type))


class Element(dict):
    """An object, bone, material or scene: custom properties and the add-on's properties."""

    def __init__(self, twin_types, **custom_properties):
        super().__init__(custom_properties)
        self.ttt = Container(twin_types)


def _fake_properties_module():
    """Stands in for twintech_properties, which makes Blender's property groups."""
    twintech = importlib.import_module("twin_tech_tools.twintech")
    module = types.ModuleType("twin_tech_tools.twintech_properties")
    module.PROPERTY = "ttt"

    def get(element):
        element = getattr(element, "bone", element)
        return getattr(element, "ttt", None)

    def read(element, extras):
        container = get(element)
        return twintech.read_container(container, container.types, extras) if container is not None else False

    def write(element):
        container = get(element)
        return twintech.write_container(container, container.types) if container is not None else None

    module.get, module.read, module.write = get, read, write
    return module


def load_addon_module(name):
    """Imports a module of the add-on without running the add-on's __init__, which needs Blender."""
    if "bpy" not in sys.modules:
        bpy = types.ModuleType("bpy")
        bpy.data = types.SimpleNamespace(actions=Actions())
        bpy.context = types.SimpleNamespace(scene=None)
        sys.modules["bpy"] = bpy

    if "twin_tech_tools" not in sys.modules:
        package = types.ModuleType("twin_tech_tools")
        package.__path__ = [ADDON_DIRECTORY]
        sys.modules["twin_tech_tools"] = package

    if "twin_tech_tools.twintech_properties" not in sys.modules:
        sys.modules["twin_tech_tools.twintech_properties"] = _fake_properties_module()
        sys.modules["twin_tech_tools"].twintech_properties = sys.modules["twin_tech_tools.twintech_properties"]

    return importlib.import_module("twin_tech_tools." + name)
