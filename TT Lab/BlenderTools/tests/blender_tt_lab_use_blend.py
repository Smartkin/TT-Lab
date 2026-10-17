"""Puts the model of an existing blend file in the place of a model of the project a TT Lab has open, the way TT Lab's Use an Existing Blend
File has Blender do it (TT Lab.Tests' BlenderLinkEndToEndTests start one and run this):

    blender --background --factory-startup --python tests/blender_tt_lab_use_blend.py -- <model file> <target model file> <blend file> <result file>

Makes a blend file of the first model, made bigger and painted with a material and picture made in Blender and a material of another
project, opens it again, runs the operators TT Lab's expression runs for the target model (ttt.tt_lab_request, then
ttt.use_for_tt_lab_model; the expression itself waits for Blender's window, which a Blender in the background has none of, and only its
text is checked, BlenderEditingTests) and waits until TT Lab took it and the blend file is saved. Writes what it saw into the result file as
JSON for the test to check TT Lab against."""
import json
import os
import sys
import time

import bpy
import mathutils

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import link_sync, material_settings, materials, save_hook, tlm_blender  # noqa: E402

FOREIGN = "res://Elsewhere/Material/Foreign"


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


def make_blend_file(model_path, blend_path):
    """The blend file a modder made of the model: bigger, painted with a picture of their own and a material of another project of theirs."""
    root = tlm_blender.import_file(bpy.context, model_path)
    root.name = "Custom"
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    body.data.transform(mathutils.Matrix.Scale(2.0, 4))
    checker = body.data.materials[0]
    foreign = checker.copy()
    foreign.name = "Foreign"
    foreign[tlm_blender.URI_PROPERTY] = FOREIGN
    painted = bpy.data.materials.new("Painted")
    painted.use_nodes = True
    texture = painted.node_tree.nodes.new("ShaderNodeTexImage")
    picture = bpy.data.images.new("Wood", 64, 64)
    picture.pixels = [0.5, 0.25, 0.125, 1.0] * (64 * 64)
    picture.pack()
    texture.image = picture
    painted.node_tree.links.new(texture.outputs["Color"], painted.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    body.data.materials.clear()
    body.data.materials.append(painted)
    body.data.materials.append(foreign)
    for index, polygon in enumerate(body.data.polygons):
        polygon.material_index = index % 2

    materials.make_drawn_twin_tech(root)
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)


def main():
    model_path, target_path, blend_path, result_path = sys.argv[sys.argv.index("--") + 1:][:4]
    twin_tech_tools.register()
    # Saving writes the models, as Always on Save does: these checks are of what's written
    save_hook.SYNC_ON_SAVE = "ALWAYS"
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    result = {}
    try:
        make_blend_file(model_path, blend_path)
        made = os.path.getmtime(blend_path)
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.wm.open_mainfile(filepath=blend_path)

        # What TT Lab's expression runs: the request at once, the operator once Blender's window is up
        bpy.ops.ttt.tt_lab_request(model=target_path, asset_type="Ogi", name="Target")
        bpy.ops.ttt.use_for_tt_lab_model("INVOKE_DEFAULT", True, model=target_path, asset_type="Ogi", name="Target")
        root = bpy.data.objects["Custom"]
        result["path"] = root.get(tlm_blender.PATH_PROPERTY, "")
        result["released"] = link_sync.released()
        result["replacing"] = link_sync.replacing(root) == "Target"
        result["replaced"] = tick_until(lambda: link_sync.replacing(root) is None and link_sync.TT_LAB_PROBLEMS_PROPERTY in root and settled(root)
                                        and os.path.getmtime(blend_path) > made)
        result["uri"] = root.get(link_sync.MODEL_URI_PROPERTY, "")
        result["problems"] = link_sync.tt_lab_problems(root)

        # What the blend file keeps: the model is the target's, what it draws the project's (closing it drops nothing TT Lab saved)
        bpy.ops.wm.open_mainfile(filepath=blend_path)
        root = bpy.data.objects["Custom"]
        result["saved_path"] = root.get(tlm_blender.PATH_PROPERTY, "")
        result["saved_revision"] = root.get(link_sync.REVISION_PROPERTY, "")
        result["painted"] = bpy.data.materials["Painted"].get(tlm_blender.URI_PROPERTY, "")
        result["foreign"] = bpy.data.materials["Foreign"].get(tlm_blender.URI_PROPERTY, "")
        result["picture"] = bpy.data.images["Wood"].get(material_settings.TEXTURE_URI_PROPERTY, "")
    except Exception as error:  # noqa: BLE001 - the test reads why
        import traceback

        result["error"] = traceback.format_exc() or str(error)
    finally:
        with open(result_path, "w", encoding="utf-8") as file:
            json.dump(result, file)


main()
