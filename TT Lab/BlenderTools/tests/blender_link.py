"""Links the fixture model of a project made up in a temporary folder to a TT Lab stood in for by a process of its own (fake_tt_lab.py), in Blender:

    blender --background --factory-startup --python tests/blender_link.py

The model links to the TT Lab with its project open, a setting changed in Blender goes there once it stayed for a moment, what TT Lab
changes comes in as an undo step, a material and picture made in Blender are made in TT Lab, and saving the blend file writes the model
through TT Lab and saves what Blender made and changed. Then a copy of the model drawing another project's material is put in another
model's place (TT Lab's Use an Existing Blend File): the material is made the model's own, the copy written over the model and the blend
file saved. Prints LINK OK."""
import json
import os
import shutil
import sys
import tempfile
import time

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
STATE = tempfile.mkdtemp(prefix="ttt_link_state_")
os.environ["XDG_STATE_HOME"] = STATE

import twin_tech_tools  # noqa: E402
from twin_tech_tools import gs_material, link_sync, material_settings, save_hook, tlm, tlm_blender  # noqa: E402
from fake_tt_lab import FakeTTLab  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")
PACKAGE = "Global PS2_Test"
FUR = "res://%s/Material/Crash Fur" % PACKAGE
UUID = "11111111-2222-3333-4444-555555555555"


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def tlm_blender_png_hash(image):
    import hashlib

    return hashlib.sha256(tlm_blender._png_bytes(image) or b"").hexdigest()


def tick_until(condition, seconds=5.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        link_sync._last_look[0] = 0.0 if not link_sync._links else link_sync._last_look[0]
        link_sync.tick()
        time.sleep(0.05)

    return condition()


def make_project(root):
    os.makedirs(root, exist_ok=True)
    with open(os.path.join(root, "Test.tson"), "w", encoding="utf-8") as file:
        json.dump({"UUID": UUID, "Name": "Test"}, file)

    model = os.path.join(root, "assets", PACKAGE, "OGI", "Crash.tlm")
    os.makedirs(os.path.dirname(model))
    shutil.copy(os.path.join(FIXTURES, "ogi.tlm"), model)
    folder = os.path.join(root, "assets", PACKAGE, "Material")
    os.makedirs(folder)
    with open(os.path.join(folder, "Crash Fur.json"), "w", encoding="utf-8") as file:
        json.dump({"URI": {"_uri": FUR}, "Type": "TT_Lab.Assets.Graphics.Material, TT Lab", "Alias": "Crash Fur", "Package": {"_uri": "res://" + PACKAGE}}, file)

    with open(os.path.join(folder, "Crash Fur.data"), "w", encoding="utf-8") as file:
        json.dump(gs_material.new_material("CRASH_FUR", skin=True, textured=False), file)

    return model


def clear_replacements():
    """The scene's models stand for the files they have, none waiting to replace another."""
    for blender_object in bpy.data.objects:
        link_sync._drop_replacement(blender_object)


def use_for_another_model(fake, root, model, project_root, directory):
    """TT Lab's Use an Existing Blend File: a copy of the model drawing another project's material is put in the place of another model."""
    from twin_tech_tools import materials

    target = os.path.join(project_root, "assets", PACKAGE, "OGI", "Target.tlm")
    target_uri = "res://%s/OGI/Target" % PACKAGE
    custom = tlm_blender.import_file(bpy.context, model)
    custom.name = "Custom"
    skin = next(child for child in tlm_blender._descendants(custom) if tlm_blender.role_of(child) == "skin")
    foreign = bpy.data.materials["Crash Fur"].copy()
    foreign.name = "Foreign"
    foreign[tlm_blender.URI_PROPERTY] = "res://Elsewhere/Material/Foreign"
    skin.data = skin.data.copy()
    for index, slot in enumerate(skin.data.materials):
        if slot is not None and slot.name == "Crash Fur":
            skin.data.materials[index] = foreign

    # What can take a game model's place: the scene's game models, not a retarget's source or another kind of model
    source = bpy.data.objects.new("Source", None)
    source[tlm_blender.ROOT_PROPERTY] = "Ogi"
    source["ttt_retarget_source"] = True
    sky = bpy.data.objects.new("Sky", None)
    sky[tlm_blender.ROOT_PROPERTY] = "Skydome"
    for blender_object in (source, sky):
        bpy.context.scene.collection.objects.link(blender_object)

    check([candidate.name for candidate in link_sync.candidates("Ogi")] == [root.name, "Custom"], "the candidates are %s" % link_sync.candidates("Ogi"))
    check(bpy.ops.ttt.use_for_tt_lab_model(model=target, asset_type="SaveIcon", name="Icon") == {"CANCELLED"}, "a save icon was put in place")

    check(bpy.ops.ttt.use_for_tt_lab_model("EXEC_DEFAULT", True, model=target, asset_type="Ogi", name="Target", root="Custom") == {"FINISHED"}, "nothing was put in place")
    check(custom[tlm_blender.PATH_PROPERTY] == target and link_sync.replacing(custom) == "Target", "the copy isn't the target's")
    # The model it's a copy of is the blend file's still
    check(link_sync.released() == [], "released %s" % link_sync.released())
    check(tick_until(lambda: fake.of("write_model")[1:] and fake.of("blend_saved")[1:], 10.0), "the copy wasn't written in the target's place")
    linked = [header for header, _ in fake.of("link_model") if header["path"] == target][0]
    check("res://Elsewhere/Material/Foreign" in linked["materials"], "the link didn't say what the copy draws: %s" % linked)
    made = [header for header, _ in fake.of("create_material") if header["name"] == "Foreign"]
    check(made and made[0]["owner"] == target_uri, "the other project's material wasn't made the target's own: %s" % made)
    check(foreign.get(tlm_blender.URI_PROPERTY) == "res://%s/Material/Foreign" % PACKAGE, "the material didn't learn its URI")
    received = [header for header, _ in fake.received]
    replacing = [index for index, header in enumerate(received) if header["type"] == "write_model" and header.get("replacing")]
    check(len(replacing) == 1, "the copy was written in the target's place %d times" % len(replacing))
    write = received[replacing[0]]
    check(write["uri"] == target_uri and write["force"] and write["name"] == "Custom" and write["blend"] == bpy.data.filepath, "the copy was written as %s" % write)
    later = received[replacing[0] + 1:]
    check(any(header["type"] == "save" for header in later), "what Blender made wasn't saved after the copy was taken")
    # The blend file's save once it was taken writes the other models like any save: the one it's a copy of, not the copy again
    rewritten = [header["uri"] for header in later if header["type"] == "write_model"]
    check(rewritten == ["res://%s/OGI/Crash" % PACKAGE], "the save after the copy was taken wrote %s" % rewritten)
    check(fake.of("blend_saved")[-1][0]["released"] == [], "the blend file released %s" % fake.of("blend_saved")[-1][0])
    check(custom.get(link_sync.REVISION_PROPERTY) == "r2" and link_sync.replacing(custom) is None, "the copy isn't the target's model now")

    # Putting the first model in the target's place too: the copy stands for no model, the first one's file isn't the blend file's any more
    link_sync.use_for(root, target, "Target")
    check(tlm_blender.PATH_PROPERTY not in custom, "two models write the target's file")
    end = time.time() + 5.0
    while len(fake.of("unlink_model")) < 2 and time.time() < end:
        time.sleep(0.02)

    # The first model stands for no model until it's linked as the target's
    unlinked = [header["uri"] for header, _ in fake.of("unlink_model")]
    check(unlinked == [target_uri, "res://%s/OGI/Crash" % PACKAGE], "unlinked %s" % unlinked)
    check(link_sync.released() == [model], "released %s" % link_sync.released())
    clear_replacements()

    # Two models of the scene standing for one of TT Lab's: the one given another model's file leaves the other linked
    first, second = tlm_blender.import_file(bpy.context, model), tlm_blender.import_file(bpy.context, model)
    crash = "res://%s/OGI/Crash" % PACKAGE
    check(tick_until(lambda: first.get(link_sync.MODEL_URI_PROPERTY) == crash and second.get(link_sync.MODEL_URI_PROPERTY) == crash), "the copies didn't link")
    before = len(fake.of("unlink_model"))
    link_sync.use_for(first, os.path.join(project_root, "assets", PACKAGE, "OGI", "Other.tlm"), "Other")
    link_sync.use_for(second, os.path.join(project_root, "assets", PACKAGE, "OGI", "Third.tlm"), "Third")
    end = time.time() + 5.0
    while len(fake.of("unlink_model")) < before + 1 and time.time() < end:
        time.sleep(0.02)

    time.sleep(0.2)
    unlinked = [header["uri"] for header, _ in fake.of("unlink_model")][before:]
    check(unlinked == [crash], "unlinked %s while another model stood for it" % unlinked)
    clear_replacements()


def main():
    twin_tech_tools.register()
    # Saving writes the models, as Always on Save does: these checks are of what's written
    save_hook.SYNC_ON_SAVE = "ALWAYS"
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    directory = tempfile.mkdtemp(prefix="ttt_link_")
    fake = None
    try:
        project_root = os.path.join(directory, "Test")
        model = make_project(project_root)
        fake = FakeTTLab(STATE, project_root, UUID, PACKAGE, tlm_blender.ADDON_VERSION, link_revision="r1",
                         link_materials=[{"uri": FUR, "name": "Crash Fur", "data": gs_material.new_material("CRASH_FUR", skin=True, textured=False)}],
                         write_revision="r2")
        root = tlm_blender.import_file(bpy.context, model)

        check(tick_until(lambda: link_sync.is_linked(root) and fake.of("link_model")), "the model didn't link")
        check(fake.of("hello")[0][0]["token"] == "secret" and fake.of("link_model")[0][0]["path"] == model, "the hello or link isn't the model's")
        check(tick_until(lambda: root.get(link_sync.MODEL_URI_PROPERTY) == "res://%s/OGI/Crash" % PACKAGE), "the model didn't learn its URI")

        # A setting changed in Blender goes once it stayed for a moment
        material = bpy.data.materials["Crash Fur"]
        material_settings.settings_of(material).render_bucket = "BUCKET_6"
        check(tick_until(lambda: fake.of("update_material")), "the changed setting wasn't sent")
        update = fake.of("update_material")[-1][0]
        check(update["uri"] == FUR and update["changed"] == [["DmaChainIndex"]] and update["data"]["DmaChainIndex"] == 6, "the update is %s" % update)

        # What TT Lab changes comes in
        theirs = json.loads(json.dumps(fake.material))
        theirs["DmaChainIndex"] = 6
        theirs["Shaders"][0]["FixedAlphaValue"] = 77
        fake.push({"type": "material_changed", "uri": FUR, "data": theirs})
        check(tick_until(lambda: material_settings.settings_of(material).shaders[0].fixed_alpha == 77), "TT Lab's change didn't come in")
        check(material_settings.settings_of(material).render_bucket == "BUCKET_6", "Blender's change was lost")
        sent_before = len(fake.of("update_material"))
        tick_until(lambda: False, 0.6)
        check(len(fake.of("update_material")) == sent_before, "what came from TT Lab went back to it")

        # Textured, the material keeps drawing the scene's picture of its texture, and one the scene has none of (TT Lab's, not saved yet)
        # is TT Lab's picture
        fur_texture, unseen = "res://%s/Texture/Fur" % PACKAGE, "res://%s/Texture/Unseen" % PACKAGE
        picture = bpy.data.images.new("Fur", 4, 4)
        picture[material_settings.TEXTURE_URI_PROPERTY] = fur_texture
        picture[material_settings.PNG_CONFIRMED_PROPERTY] = tlm_blender_png_hash(picture)
        # A picture of Blender's TT Lab made a texture of keeps its Blender ID
        picture[material_settings.IMAGE_ID_PROPERTY] = "fur-picture"
        textured = json.loads(json.dumps(theirs))
        textured["Shaders"][0]["TextureId"] = {"_uri": fur_texture}
        textured["Shaders"][0]["FixedAlphaValue"] = 78
        fake.push({"type": "material_changed", "uri": FUR, "data": textured})
        check(tick_until(lambda: material_settings.settings_of(material).shaders[0].fixed_alpha == 78), "the textured change didn't come in")
        check(material_settings.settings_of(material).shaders[0].image == picture, "the material lost its picture")
        # The same again is no change of picture: no undo step
        again = link_sync._take(link_sync.link_of(root), {"type": "material_changed", "uri": FUR, "data": json.loads(json.dumps(textured))}, b"")
        check(again is None, "TT Lab's texture of the picture was taken for another picture: %s" % again)
        textured = json.loads(json.dumps(textured))
        textured["Shaders"][0]["TextureId"] = {"_uri": unseen}
        fake.push({"type": "material_changed", "uri": FUR, "data": textured})
        check(tick_until(lambda: material_settings.settings_of(material).shaders[0].image is not None
                         and material_settings.settings_of(material).shaders[0].image.get(material_settings.TEXTURE_URI_PROPERTY) == unseen), "TT Lab's picture wasn't drawn")
        check(any(header["uri"] == unseen for header, _ in fake.of("get_picture")), "TT Lab's picture wasn't asked for")
        # The scene's picture of the texture made of Blender's is TT Lab's texture: relinking takes nothing for a change of picture
        sent_before = len(fake.of("update_material"))
        tick_until(lambda: False, 0.6)
        check(len(fake.of("update_material")) == sent_before, "what came from TT Lab went back to it")

        # A material and picture made in Blender are made in TT Lab, the picture first
        body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
        made = bpy.data.materials.new("Painted")
        made.use_nodes = True
        texture = made.node_tree.nodes.new("ShaderNodeTexImage")
        picture = bpy.data.images.new("Wood", 4, 4)
        texture.image = picture
        made.node_tree.links.new(texture.outputs["Color"], made.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
        # Every face of the body draws with it
        body.data.materials.clear()
        body.data.materials.append(made)
        from twin_tech_tools import materials
        materials.make_drawn_twin_tech(root)
        check(tick_until(lambda: made.get(tlm_blender.URI_PROPERTY)), "the material made in Blender wasn't made in TT Lab")
        kinds = [header["type"] for header, _ in fake.received if header["type"].startswith("create_")]
        check(kinds == ["create_picture", "create_material"], "made %s" % kinds)
        check(fake.of("create_picture")[0][1].startswith(b"\x89PNG"), "the picture didn't go with its PNG")
        # The node tree was made again of the settings, the picture is the image itself
        check(picture.get(material_settings.TEXTURE_URI_PROPERTY) == "res://%s/Texture/Wood" % PACKAGE, "the picture didn't learn its texture")

        # Saving the blend file writes the model through TT Lab and saves what Blender made and changed, never into the model's file
        with open(model, "rb") as file:
            before = file.read()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "scene.blend"))
        check(tick_until(lambda: fake.of("save") and root.get(link_sync.REVISION_PROPERTY) == "r2"), "saving didn't write the model through TT Lab")
        write = fake.of("write_model")[0]
        check(write[0]["uri"] == "res://%s/OGI/Crash" % PACKAGE and tlm.TlmFile.from_bytes(write[1]).asset_type == "Ogi", "the written model isn't the model")
        saved = fake.of("save")[0][0]["uris"]
        check(FUR in saved and "res://%s/Material/Painted" % PACKAGE in saved and "res://%s/Texture/Wood" % PACKAGE in saved, "saved %s" % saved)
        with open(model, "rb") as file:
            check(file.read() == before, "the model's file was written in Blender")

        use_for_another_model(fake, root, model, project_root, directory)
    finally:
        link_sync.drop_all()
        if fake is not None:
            fake.close()

        shutil.rmtree(directory, ignore_errors=True)
        shutil.rmtree(STATE, ignore_errors=True)

    print("LINK OK")


main()
