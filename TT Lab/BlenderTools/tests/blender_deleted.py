"""Exports the fixture model after deleting parts of it without purging them, in Blender:

    blender --background --factory-startup --python tests/blender_deleted.py

What's deleted stays in the file until Clean Up > Purge Unused takes it out: an animation nothing plays without a fake user (the bones'
settings of an imported one still point at it) and an object out of every scene. None of it gets exported. The Export button stays
pressable and says what keeps the scene from being exported. Prints DELETED OK."""
import os
import sys
import tempfile

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm, tlm_blender  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def _error_of(operator, **arguments):
    """What the operator reported as its error, it raises in a script."""
    try:
        operator(**arguments)
    except RuntimeError as error:
        return str(error)

    return ""


def main():
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    root = tlm_blender.import_file(bpy.context, os.path.join(FIXTURES, "ogi.tlm"))
    armature = tlm_blender.armature_of(root)
    check(sorted(action.name for action in tlm_blender.animations_of(root)) == ["Walk", "Wave"], "the fixture's animations aren't Walk and Wave")

    # An animation made in Blender for the model's bones, played and then dropped for another
    made = bpy.data.actions.new("Made In Blender")
    bag = tlm_blender._channelbag(made, made.slots.new("OBJECT", armature.name))
    fcurve = bag.fcurves.new('pose.bones["Joint 1"].location', index=0)
    fcurve.keyframe_points.insert(0, 0.0)
    fcurve.keyframe_points.insert(5, 1.0)
    tlm_blender.assign_action(armature, made)
    check([action.name for action in tlm_blender.animations_of(root)][-1] == "Made In Blender", "the action made in Blender isn't the model's")
    tlm_blender.assign_action(armature, bpy.data.actions["Walk"])
    check(made.users == 0, "the dropped action still has %d users" % made.users)
    # An imported animation deleted: its fake user cleared and nothing playing it, the bones' settings of it still point at it
    wave = bpy.data.actions["Wave"]
    wave.use_fake_user = False
    check(wave.users > 0 and tlm_blender.is_deleted(wave), "the deleted animation isn't taken for deleted (%d users)" % wave.users)
    check(not tlm_blender.is_deleted(bpy.data.actions["Walk"]), "the animation played is taken for deleted")
    check([action.name for action in tlm_blender.animations_of(root)] == ["Walk"], "the deleted animations are still the model's")

    # An object taken out of every scene stays in the file until it's purged
    exit_point = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "exit_point")
    for collection in list(exit_point.users_collection):
        collection.objects.unlink(exit_point)

    check(exit_point.name in bpy.data.objects and exit_point.parent is not None and len(exit_point.users_scene) == 0, "the exit point isn't out of the scene")
    check(exit_point not in tlm_blender._descendants(root), "the exit point out of the scene is still the model's")

    path = os.path.join(tempfile.gettempdir(), "ttt_deleted.tlm")
    bpy.context.view_layer.update()
    warnings = tlm_blender.export_file(root, path)
    check(any("Wave, Made In Blender of Crash weren't written" in warning for warning in warnings), "the deleted animations weren't said: %s" % warnings)
    file = tlm.TlmFile.load(path)
    animations = [animation["name"] for animation in tlm.find_child(file.root, "armature")["animations"]]
    check(animations == ["Walk"], "the file has the animations %s" % animations)
    check(tlm.children(tlm.find_child(file.root, "exit_points"), "exit_point") == [], "the file has the exit point out of the scene")
    check(len(tlm.children(tlm.find_child(file.root, "rigid_bodies"), "body")) == 1, "the file lost the body")
    os.remove(path)

    # The Export button is always pressable and says what keeps the scene from being exported
    for selected in bpy.context.selected_objects:
        selected.select_set(False)

    bpy.context.view_layer.objects.active = None
    check(bpy.ops.ttt.export_tlm.poll(), "the Export button is grayed out")
    check("Nothing is selected" in _error_of(bpy.ops.ttt.export_tlm, filepath=path), "exporting nothing didn't say why")
    loose = bpy.data.objects.new("Loose", None)
    bpy.context.scene.collection.objects.link(loose)
    bpy.context.view_layer.objects.active = loose
    check("Loose isn't part of a TT Lab model" in _error_of(bpy.ops.ttt.export_tlm, filepath=path), "exporting a loose object didn't say why")
    # Selected along with an object of a model, the model is what gets exported
    loose.select_set(True)
    root.select_set(True)
    check(bpy.ops.ttt.export_tlm(filepath=path) == {"FINISHED"} and os.path.exists(path), "the selected model wasn't exported")
    os.remove(path)
    bpy.data.objects.remove(armature)
    bpy.context.view_layer.objects.active = root
    check("has no armature" in _error_of(bpy.ops.ttt.export_tlm, filepath=path), "exporting an OGI without an armature didn't say why")
    check(not os.path.exists(path), "an OGI without an armature got exported")
    print("DELETED OK")


main()
