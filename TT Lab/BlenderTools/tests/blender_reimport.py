"""Imports the fixture model twice, the second time in the first one's place, in Blender:

    blender --background --factory-startup --python tests/blender_reimport.py

Prints REIMPORT OK when the scene has the model once, its animations without Blender's numbers, and a copy only when asked for."""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm_blender  # noqa: E402

FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "ogi.tlm")


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def roots():
    return [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY) and tlm_blender.in_scene(blender_object)]


def main():
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    first = tlm_blender.import_file(bpy.context, FIXTURE)
    name = first.name
    actions = sorted(action.name for action in tlm_blender.animations_of(first))
    first.location = (1.0, 2.0, 3.0)
    check(tlm_blender.model_of_file(FIXTURE) == first, "the model isn't found by its file")

    again = tlm_blender.reimport(bpy.context, first, FIXTURE)
    check(roots() == [again] and again.name == name, "the scene has %s" % [root.name for root in roots()])
    check(tuple(again.location) == (0.0, 0.0, 0.0), "the model kept what was changed in Blender")
    check(sorted(action.name for action in tlm_blender.animations_of(again)) == actions, "the animations are %s" % sorted(action.name for action in bpy.data.actions))
    check(not any(action.name.endswith(".001") for action in bpy.data.actions), "actions got Blender's numbers: %s" % [action.name for action in bpy.data.actions])
    check(not any(blender_object.name.endswith(".001") for blender_object in tlm_blender._descendants(again)), "objects got Blender's numbers")

    # Asked for, a copy comes next to it
    copy = tlm_blender.import_file(bpy.context, FIXTURE)
    check(len(roots()) == 2 and copy != again, "the copy isn't there")
    print("REIMPORT OK")


main()
