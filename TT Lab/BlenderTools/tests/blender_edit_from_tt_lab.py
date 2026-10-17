"""What TT Lab's Edit in Blender runs in Blender for a model without a blend file (BlenderEditing.ImportExpression without its timer), in
Blender:

    blender --background --factory-startup --python tests/blender_edit_from_tt_lab.py

The model is imported into an empty scene saved where TT Lab says, its file left as it is. Prints EDIT OK."""
import os
import shutil
import sys
import tempfile

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import checks, tlm_blender  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    twin_tech_tools.register()
    directory = tempfile.mkdtemp(prefix="ttt_edit_")
    try:
        model = os.path.join(directory, "Crash.tlm")
        shutil.copy(os.path.join(FIXTURES, "ogi.tlm"), model)
        with open(model, "rb") as file:
            before = file.read()

        blend = os.path.join(directory, "source", "Crash.blend")
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.ttt.edit_from_tt_lab(model=model, blend=blend)

        check(os.path.exists(blend) and bpy.data.filepath == blend, "the blend file wasn't saved where TT Lab said")
        roots = [blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY)]
        check(len(roots) == 1 and roots[0].get(tlm_blender.PATH_PROPERTY) == model, "the scene has %s" % [root.name for root in roots])
        check(bpy.context.view_layer.objects.active == roots[0], "the model isn't the active object")
        # Loading a file drops the handlers that aren't persistent, the checks and new materials went unlooked at
        check(checks.note_change in bpy.app.handlers.depsgraph_update_post, "loading the file dropped the checks' handler")
        with open(model, "rb") as file:
            check(file.read() == before, "saving wrote the model")
    finally:
        shutil.rmtree(directory, ignore_errors=True)

    print("EDIT OK")


main()
