"""Makes a model in Blender from the add-on's template and adds it to the project a TT Lab has open (TT Lab.Tests'
BlenderLinkEndToEndTests start one and run this), in Blender:

    blender --background --factory-startup --python tests/blender_tt_lab_new_model.py -- <folder label> <blend file> <result file>

The model becomes an asset of the folder, links to TT Lab, Sync with TT Lab writes it through TT Lab and the blend file is saved. Writes what it saw into the
result file as JSON for the test to check TT Lab against."""
import json
import os
import sys
import time

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import link_sync, tlm_blender, tlm_operators, tlm_templates  # noqa: E402


def tick_until(condition, seconds=30.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        link_sync._last_look[0] = 0.0 if not link_sync._links else link_sync._last_look[0]
        link_sync.tick()
        time.sleep(0.05)

    return bool(condition())


def main():
    label, blend_path, result_path = sys.argv[sys.argv.index("--") + 1:][:3]
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    result = {}
    try:
        root = tlm_templates.new_model(bpy.context, "Crate")
        bpy.context.view_layer.objects.active = root
        client, project = tlm_operators._connect_to_running_tt_lab()
        result["project"] = project
        answer = client.ask({"type": "new_model_targets"})
        client.close()
        folders = answer[0]["folders"]
        tlm_operators._new_asset_folders[:] = [(folder["uri"], folder["label"], folder["uri"]) for folder in folders]
        folder = next(folder["uri"] for folder in folders if folder["label"] == label)
        bpy.ops.ttt.add_to_tt_lab(name="Crate", folder=folder)
        result["path"] = root.get(tlm_blender.PATH_PROPERTY, "")
        result["linked"] = tick_until(lambda: link_sync.is_linked(root) and bool(root.get(link_sync.MODEL_URI_PROPERTY)))
        result["uri"] = root.get(link_sync.MODEL_URI_PROPERTY, "")
        link = link_sync.link_of(root)
        material = bpy.data.materials["Crate"]
        result["material_linked"] = tick_until(lambda: bool(material.get(tlm_blender.URI_PROPERTY)) and not link.making and not link.client.pending())
        result["material"] = material.get(tlm_blender.URI_PROPERTY, "")
        # Saving asks whether to sync (the default), which Blender in the background can't: synced with the button, then saved
        bpy.ops.ttt.sync_with_tt_lab()
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)
        result["written"] = tick_until(lambda: bool(root.get(link_sync.REVISION_PROPERTY)) and not link.client.pending())
    except Exception as error:  # noqa: BLE001 - the test reads why
        import traceback

        result["error"] = traceback.format_exc() or str(error)
    finally:
        with open(result_path, "w", encoding="utf-8") as file:
            json.dump(result, file)


main()
