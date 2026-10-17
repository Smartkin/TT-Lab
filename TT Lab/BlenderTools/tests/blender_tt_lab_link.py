"""Links a model of the project a TT Lab has open (TT Lab.Tests' BlenderLinkEndToEndTests start one and run this) and edits it the way a
user does, in Blender:

    blender --background --factory-startup --python tests/blender_tt_lab_link.py -- <model file> <blend file> <result file>

Changes the model's material, paints a new material with a new picture onto its body, renames it and saves the blend file, which writes
the model through TT Lab. Writes what it saw into the result file as JSON for the test to check TT Lab against."""
import json
import os
import sys
import time

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import link_sync, material_settings, materials, save_hook, tlm_blender  # noqa: E402


def tick_until(condition, seconds=30.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        # Background Blender runs no timers
        link_sync._last_look[0] = 0.0 if not link_sync._links else link_sync._last_look[0]
        link_sync.tick()
        time.sleep(0.05)

    return bool(condition())


def settled(root):
    link = link_sync.link_of(root)
    return link is not None and not link_sync._marked and not link.sending and not link.making and not link.client.pending()


def main():
    model_path, blend_path, result_path = sys.argv[sys.argv.index("--") + 1:][:3]
    twin_tech_tools.register()
    # Saving writes the models, as Always on Save does: these checks are of what's written
    save_hook.SYNC_ON_SAVE = "ALWAYS"
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    result = {}
    try:
        root = tlm_blender.import_file(bpy.context, model_path)
        result["linked"] = tick_until(lambda: link_sync.is_linked(root) and bool(root.get(link_sync.MODEL_URI_PROPERTY)))
        result["uri"] = root.get(link_sync.MODEL_URI_PROPERTY, "")

        material = next(material for material, _ in materials._drawn(root).values() if material_settings.is_managed(material))
        material_settings.settings_of(material).render_bucket = "BUCKET_6"
        result["changed"] = material.get(tlm_blender.URI_PROPERTY, "")
        # Sent once it stayed the same for a moment
        tick_until(lambda: False, link_sync.QUIET + 0.2)
        result["sent"] = tick_until(lambda: settled(root))

        body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
        painted = bpy.data.materials.new("Painted")
        painted.use_nodes = True
        texture = painted.node_tree.nodes.new("ShaderNodeTexImage")
        picture = bpy.data.images.new("Wood", 64, 64)
        picture.pixels = [0.5, 0.25, 0.125, 1.0] * (64 * 64)
        texture.image = picture
        painted.node_tree.links.new(texture.outputs["Color"], painted.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
        body.data.materials.clear()
        body.data.materials.append(painted)
        materials.make_drawn_twin_tech(root)
        result["made"] = tick_until(lambda: bool(painted.get(tlm_blender.URI_PROPERTY)))
        result["material"] = painted.get(tlm_blender.URI_PROPERTY, "")
        result["picture"] = picture.get(material_settings.TEXTURE_URI_PROPERTY, "")

        # Renamed in Blender, renamed in TT Lab
        painted.name = "Varnished"
        result["renamed"] = tick_until(lambda: link_sync.link_of(root).names.get(result["material"]) == "Varnished" and settled(root))

        revision = root.get(link_sync.REVISION_PROPERTY, "")
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)
        result["written"] = tick_until(lambda: root.get(link_sync.REVISION_PROPERTY, "") not in ("", revision) and settled(root))
        result["revision"] = root.get(link_sync.REVISION_PROPERTY, "")
        result["problems"] = link_sync.tt_lab_problems(root)
    except Exception as error:  # noqa: BLE001 - the test reads why
        import traceback

        result["error"] = traceback.format_exc() or str(error)
    finally:
        with open(result_path, "w", encoding="utf-8") as file:
            json.dump(result, file)


main()
