"""Puts models of a blend file in the place of TT Lab models (TT Lab's Use an Existing Blend File), against a TT Lab stood in for by a process
of its own that can hold, refuse and push answers (fake_tt_lab.py), in Blender:

    blender --background --factory-startup --python tests/blender_use_blend.py

Checks what a request holds back, an answer for the file a model had, a replacement TT Lab refuses and the save that writes it once fixed
(the blend file keeping the revision TT Lab gave), a save while no TT Lab has the project open, pictures that can't be read, paths of
another system, TT Lab's use_for and released pushes, undo giving back what TT Lab said, what the replacing write names and another project's
model made this project's (what the scene's other models draw copied for it), by Add to TT Lab's Project too. Prints USE BLEND OK."""
import hashlib
import json
import os
import shutil
import sys
import tempfile
import time

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
STATE = tempfile.mkdtemp(prefix="ttt_use_state_")
os.environ["XDG_STATE_HOME"] = STATE

import twin_tech_tools  # noqa: E402
from twin_tech_tools import gs_material, link_sync, material_settings, material_sync, materials, replace, save_hook, tlm, tlm_blender, tlm_operators  # noqa: E402
from fake_tt_lab import FakeTTLab, _png  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")
PACKAGE = "Global PS2_Test"
UUID = "11111111-2222-3333-4444-555555555555"


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def uri_of(path):
    return "res://%s/OGI/%s" % (PACKAGE, os.path.splitext(os.path.basename(path))[0])


def tick_until(condition, seconds=8.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        link_sync._last_look[0] = 0.0
        link_sync.tick()
        time.sleep(0.02)

    return bool(condition())


def wait_for(condition, seconds=5.0):
    end = time.time() + seconds
    while not condition() and time.time() < end:
        time.sleep(0.02)

    return bool(condition())


def told_after(fake, count):
    """The blend_saved TT Lab got after the first count of them, once it got it: a save sends it without waiting for an answer."""
    check(wait_for(lambda: len(fake.of("blend_saved")) > count), "the save didn't tell TT Lab")
    return fake.of("blend_saved")[-1][0]


def told():
    return [text for _, text in save_hook._told]


def make_project(root):
    os.makedirs(os.path.join(root, "assets", PACKAGE, "OGI"), exist_ok=True)
    with open(os.path.join(root, "Test.tson"), "w", encoding="utf-8") as file:
        json.dump({"UUID": UUID, "Name": "Test"}, file)

    paths = {}
    for name in ("Crash", "Target", "Other"):
        paths[name] = os.path.join(root, "assets", PACKAGE, "OGI", name + ".tlm")
        shutil.copy(os.path.join(FIXTURES, "ogi.tlm"), paths[name])

    return paths


def make_copy(root, uuid, fixture="ogi.tlm"):
    """A project of the UUID whose Crash is the fixture's model."""
    os.makedirs(os.path.join(root, "assets", PACKAGE, "OGI"), exist_ok=True)
    with open(os.path.join(root, "Test.tson"), "w", encoding="utf-8") as file:
        json.dump({"UUID": uuid, "Name": "Test"}, file)

    path = os.path.join(root, "assets", PACKAGE, "OGI", "Crash.tlm")
    shutil.copy(os.path.join(FIXTURES, fixture), path)
    return path


def fur_of(root):
    return next(material for material, _ in materials._drawn(root).values() if material.get(tlm_blender.URI_PROPERTY) == "res://%s/Material/Crash Fur" % PACKAGE)


def sha256_of(path):
    with open(path, "rb") as file:
        return hashlib.sha256(file.read()).hexdigest()


def show_with_picture(material, texture):
    """Shows the material as its project's material drawing its project's texture of the URI, which TT Lab confirmed."""
    material_settings.load(material, gs_material.new_material(material.name, skin=True, textured=True), None, known=True)
    picture = bpy.data.images.new(texture.rsplit("/", 1)[-1], 4, 4)
    picture[material_settings.TEXTURE_URI_PROPERTY] = texture
    picture[material_settings.PNG_CONFIRMED_PROPERTY] = "confirmed"
    material_settings.settings_of(material).shaders[0].image = picture
    return picture


def add_project_material(root, data, color):
    """The project's Crash Fur, drawing its Fur texture of one color (a material and texture asset as TT Lab writes them)."""
    for folder, name, kind, extension in (("Material", "Crash Fur", "Material", ".data"), ("Texture", "Fur", "Texture", ".png")):
        directory = os.path.join(root, "assets", PACKAGE, folder)
        os.makedirs(directory, exist_ok=True)
        with open(os.path.join(directory, name + ".json"), "w", encoding="utf-8") as file:
            json.dump({"URI": {"_uri": "res://%s/%s/%s" % (PACKAGE, folder, name)}, "Type": "TT_Lab.Assets.Graphics.%s, TT Lab" % kind, "Alias": name,
                       "Package": {"_uri": "res://" + PACKAGE}}, file)

        if extension == ".data":
            with open(os.path.join(directory, name + extension), "w", encoding="utf-8") as file:
                json.dump(data, file)
        else:
            with open(os.path.join(directory, name + extension), "wb") as file:
                file.write(_png(4, 4, color))


def settings_snapshot(material):
    """What Blender shows of the material and the base it keeps."""
    return json.dumps({"shown": material_settings.shown(material), "base": material_settings.settings_of(material).base}, sort_keys=True,
                      default=lambda value: getattr(value, "__dict__", str(value)))


def pictures_of(material):
    return [item.image for item in material_settings.settings_of(material).shaders if item.image is not None] if material_settings.is_managed(material) else []


def fresh():
    link_sync.drop_all()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    for blocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images, bpy.data.actions):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)

    save_hook._told.clear()
    for scene in bpy.data.scenes:
        scene.pop(link_sync.RELEASED_PROPERTY, None)


def model(path, name):
    root = tlm_blender.import_file(bpy.context, path)
    root.name = name
    return root


def check_requests_hold_models_back(fake, paths):
    """While a model of the kind isn't picked, the scene's models of the kind aren't linked and nothing is made for them."""
    fresh()
    root = model(paths["Crash"], "Custom")
    bpy.ops.ttt.tt_lab_request(model=paths["Target"], asset_type="Ogi", name="Target")
    before = len(fake.of("link_model"))
    tick_until(lambda: False, 0.5)
    link = link_sync.link_of(root)
    check(not any(header["path"] == paths["Crash"] for header, _ in fake.of("link_model")[before:]) and (link is None or root.name not in link.models),
          "a model of the kind was linked while the choice was pending")
    check(bpy.ops.ttt.use_for_tt_lab_model(model=paths["Target"], asset_type="Ogi", name="Target") == {"FINISHED"}, "nothing was put in place")
    check(link_sync._request[0] is None and link_sync.replacing(root) == "Target", "the request wasn't taken")
    check(tick_until(lambda: link_sync.replacing(root) is None), "the model wasn't written in the target's place")
    write = fake.of("write_model")[-1]
    check(write[0]["replacing"] and write[0]["force"] and write[0]["uri"] == uri_of(paths["Target"]), "the write was %s" % write[0])
    print("requests hold models back: ok")


def check_stale_answers(fake, paths, directory):
    """An error for the file the model had before it was put in place doesn't cancel the replacement."""
    fresh()
    gone = os.path.join(os.path.dirname(paths["Crash"]), "Gone.tlm")
    fake.add_unknown(gone)
    root = model(paths["Crash"], "Custom")
    root[tlm_blender.PATH_PROPERTY] = gone
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "stale.blend"))
    fake.hold("link_model")
    # The link asks about the file it had
    check(tick_until(lambda: any(header["path"] == gone for header, _ in fake.of("link_model"))), "the model's file wasn't asked about")
    link_sync.use_for(root, paths["Target"], "Target")
    fake.release("link_model")
    check(tick_until(lambda: link_sync.replacing(root) is None), "the replacement was cancelled by the answer for the file the model had")
    check(not any("didn't link" in text for text in told()), "told %s" % told())
    fake.discard_unknown(gone)
    print("stale answers: ok")


def check_refused_then_saved(fake, paths, directory):
    """Refused, nothing is saved and the model stays put in place; the save once it's fixed writes it as a replacement and the blend file
    keeps the revision TT Lab gave it."""
    fresh()
    blend = os.path.join(directory, "refused.blend")
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=blend)
    saved = os.path.getmtime(blend)
    fake.refuse_next_writes(1)
    writes = len(fake.of("write_model"))
    saves = len(fake.of("save"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: len(fake.of("write_model")) > writes and not link_sync._finishing and link_sync.TT_LAB_PROBLEMS_PROPERTY in root), "the replacement wasn't sent")
    tick_until(lambda: False, 0.4)
    check(link_sync.replacing(root) == "Target" and link_sync._replacing[root.name].attempted, "the refused model isn't put in place any more")
    # Answered, TT Lab got everything sent before
    check(link_sync._ask(link_sync.link_of(root), {"type": "ping"}) is not None, "TT Lab didn't answer")
    check(os.path.getmtime(blend) == saved and len(fake.of("save")) == saves, "a refused replacement saved")
    check(any("needs 11 exit points" in text or "11 exit points" in text for text in told()), "told %s" % told())
    # Fixed: the save writes it as a replacement, and keeps the revision TT Lab answers
    bpy.ops.wm.save_mainfile()
    write = fake.of("write_model")[-1][0]
    check(write["replacing"] and write["force"] and write["name"] == "Custom" and write["blend"] == blend, "the save wrote %s" % write)
    check(link_sync.replacing(root) is None, "the model is still put in place after TT Lab took it")
    revision = root.get(link_sync.REVISION_PROPERTY)
    bpy.ops.wm.open_mainfile(filepath=blend)
    reopened = bpy.data.objects["Custom"]
    check(reopened.get(link_sync.REVISION_PROPERTY) == revision and revision not in ("", "r0", None), "the blend file kept revision %s, TT Lab gave %s"
          % (reopened.get(link_sync.REVISION_PROPERTY), revision))
    print("refused then saved: ok")


def check_offline_saves(paths, directory):
    """While no TT Lab has the project open, saving leaves the target's file alone and keeps what's released to tell."""
    fresh()
    blend = os.path.join(directory, "offline.blend")
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=blend)
    target = open(paths["Target"], "rb").read()
    link_sync.use_for(root, paths["Target"], "Target")
    bpy.ops.wm.save_mainfile()
    check(open(paths["Target"], "rb").read() == target, "the target's file was written past TT Lab")
    check(link_sync.replacing(root) == "Target" and link_sync.released() == [paths["Crash"]], "the save forgot what it waits for")
    check(any("once TT Lab has the project open" in text for text in told()), "told %s" % told())
    print("offline saves: ok")
    return blend


def check_offline_then_linked(fake, paths, blend):
    """TT Lab having the project again, the model put in place is written, and the save tells what it released."""
    root = bpy.data.objects["Custom"]
    check(tick_until(lambda: link_sync.replacing(root) is None and not link_sync._finishing), "the waiting model wasn't written once linked")
    check(wait_for(lambda: any(header.get("released") == [paths["Crash"]] for header, _ in fake.of("blend_saved"))), "the release wasn't told")
    check(link_sync.released() == [], "still releasing %s" % link_sync.released())
    print("offline then linked: ok")


def check_unreadable_pictures(fake, paths):
    """A picture whose file isn't here is left out of what's made, once."""
    fresh()
    root = model(paths["Crash"], "Custom")
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    painted = bpy.data.materials.new("Faraway")
    painted.use_nodes = True
    texture = painted.node_tree.nodes.new("ShaderNodeTexImage")
    picture = bpy.data.images.new("Faraway", 4, 4)
    picture.source = "FILE"
    picture.filepath = os.path.join(tempfile.gettempdir(), "ttt_not_here_%d.png" % os.getpid())
    texture.image = picture
    painted.node_tree.links.new(texture.outputs["Color"], painted.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    body.data.materials.clear()
    body.data.materials.append(painted)
    pictures = len(fake.of("create_picture"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: painted.get(tlm_blender.URI_PROPERTY)), "the material wasn't made without its picture")
    tick_until(lambda: False, 1.0)
    check(len(fake.of("create_picture")) == pictures, "a picture that can't be read was sent %d times" % (len(fake.of("create_picture")) - pictures))
    check(sum("can't be read" in text for text in told()) == 1, "told %s" % told())
    check(tick_until(lambda: link_sync.replacing(root) is None), "the model wasn't written without its picture")
    print("unreadable pictures: ok")


def check_other_systems_paths(paths):
    """A model of another system's file is in no project and isn't written into a file named after its path."""
    fresh()
    root = model(paths["Crash"], "Weapon")
    windows_path = "D:\\Mods\\Proj\\assets\\PS2\\OGI\\Weapon.tlm"
    root[tlm_blender.PATH_PROPERTY] = windows_path
    check(link_sync.projects.find_root(windows_path) is None, "a Windows path is in a project")
    refused = link_sync.write_models()
    check(any("isn't a file of this computer" in text for text in refused), "refused %s" % refused)
    check(not os.path.exists(windows_path) and not os.path.exists(os.path.join(os.getcwd(), windows_path)), "a file named after the path was written")
    print("other systems' paths: ok")


def check_pushes(fake, paths, directory):
    """TT Lab's use_for of a blend file this Blender has open puts its model in place here; released, a model stands for none."""
    fresh()
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "pushed.blend"))
    check(tick_until(lambda: link_sync.is_linked(root) and root.name in link_sync.link_of(root).models), "the model didn't link")
    fake.push({"type": "use_for", "model": paths["Target"], "asset_type": "Ogi", "name": "Target"})
    check(tick_until(lambda: root.get(tlm_blender.PATH_PROPERTY) == paths["Target"]), "the pushed use_for put nothing in place")
    check(tick_until(lambda: link_sync.replacing(root) is None), "the pushed replacement wasn't written")
    other = model(paths["Other"], "Elsewhere")
    check(tick_until(lambda: link_sync.link_of(other) is not None and other.name in link_sync.link_of(other).models), "the other model didn't link")
    fake.push({"type": "released", "uri": uri_of(paths["Other"]), "name": "Other", "by": "Reimu of reimu.blend"})
    check(tick_until(lambda: tlm_blender.PATH_PROPERTY not in other), "the released model still stands for it")
    check(any("Reimu of reimu.blend took its place" in text for text in told()), "told %s" % told())
    print("pushes: ok")


def check_undo_gives_back(fake, paths):
    """What undo takes back of what TT Lab said (a model's URI and revision, what it made's URIs) comes back; a model undo gave back its
    earlier file is that file's model again."""
    fresh()
    root = model(paths["Crash"], "Custom")
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    painted = bpy.data.materials.new("Painted")
    body.data.materials.clear()
    body.data.materials.append(painted)
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: link_sync.replacing(root) is None and painted.get(tlm_blender.URI_PROPERTY)), "the model wasn't put in place")
    uri, revision, made = root[link_sync.MODEL_URI_PROPERTY], root[link_sync.REVISION_PROPERTY], painted[tlm_blender.URI_PROPERTY]
    # What memory undo does to them
    for block, key in ((root, link_sync.MODEL_URI_PROPERTY), (root, link_sync.REVISION_PROPERTY), (painted, tlm_blender.URI_PROPERTY)):
        del block[key]

    # And the marker of the replacement TT Lab took
    root[link_sync.REPLACES_PROPERTY] = json.dumps({"name": "Target", "path": paths["Target"]})
    link_sync._undone()
    check((root.get(link_sync.MODEL_URI_PROPERTY), root.get(link_sync.REVISION_PROPERTY), painted.get(tlm_blender.URI_PROPERTY)) == (uri, revision, made),
          "undo kept %s" % [root.get(link_sync.MODEL_URI_PROPERTY), root.get(link_sync.REVISION_PROPERTY), painted.get(tlm_blender.URI_PROPERTY)])
    check(link_sync.REPLACES_PROPERTY not in root and link_sync.replacing(root) is None, "undo gave back a replacement TT Lab took")
    root[tlm_blender.PATH_PROPERTY] = paths["Crash"]
    link_sync._undone()
    check(root.name not in link_sync.link_of(root).models if link_sync.link_of(root) else True, "the model still stands for the target")
    print("undo gives back: ok")


def check_what_the_write_names(fake, paths):
    """The replacing write names what it draws by the URIs TT Lab gave, none of another project's."""
    fresh()
    root = model(paths["Crash"], "Custom")
    skinned = [child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) in tlm_blender.SKINNED_KINDS]
    foreign = skinned[0].data.materials[0].copy()
    foreign.name = "Foreign"
    foreign[tlm_blender.URI_PROPERTY] = "res://Elsewhere/Material/Foreign"
    # The skin and the blend skin draw it, the body what it drew
    for child in skinned:
        child.data = child.data.copy()
        for index in range(len(child.data.materials)):
            child.data.materials[index] = foreign
    root[tlm_blender.PATH_PROPERTY] = os.path.join(tempfile.gettempdir(), "Elsewhere", "assets", "PS2", "OGI", "Crash.tlm")
    writes = len(fake.of("write_model"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: len(fake.of("write_model")) > writes), "nothing was written")
    named = [entry.get("uri") for entry in tlm.TlmFile.from_bytes(fake.of("write_model")[-1][1]).materials]
    check("res://%s/Material/Foreign" % PACKAGE in named and not any(uri and uri.startswith("res://Elsewhere/") for uri in named), "the write names %s" % named)
    made = [header for header, _ in fake.of("create_material") if header["name"] == "Foreign"]
    check(made and fake.received.index((made[-1], b"")) < fake.received.index(fake.of("write_model")[-1]) if made else False, "written before the material was made")
    print("what the write names: ok")


def check_other_projects_models(fake, paths, directory):
    """A model of another project draws that project's materials and pictures, whatever this project has at their URIs (two projects of a
    disc share the game's): they're made the model's own, what another model of the scene draws as well copied for it, the other model
    keeping its project's. A model of a copy of this project (its UUID) draws this project's."""
    fresh()
    elsewhere = make_copy(os.path.join(directory, "Elsewhere"), "99999999-8888-7777-6666-555555555555")
    fur_uri, picture_uri = "res://%s/Material/Crash Fur" % PACKAGE, "res://%s/Texture/Fur" % PACKAGE
    root, keeper = model(elsewhere, "Custom"), model(elsewhere, "Keeper")
    fur = bpy.data.materials["Crash Fur"]
    check(fur.get(tlm_blender.URI_PROPERTY) == fur_uri and "Crash Fur" in materials._drawn(root) and "Crash Fur" in materials._drawn(keeper),
          "the models don't share the project's material")
    picture = show_with_picture(fur, picture_uri)
    # The model's own material made in Blender, drawing the picture the other model's material draws
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    body.data = body.data.copy()
    paint = bpy.data.materials.new("Paint")
    paint.use_nodes = True
    texture = paint.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = picture
    paint.node_tree.links.new(texture.outputs["Color"], paint.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    body.data.materials.clear()
    body.data.materials.append(paint)
    # The shared material's shader animation: the copy gets one of its own
    fur.animation_data_create()
    animation = bpy.data.actions.new("Fur Animation")
    slot = animation.slots.new(id_type="MATERIAL", name=fur.name)
    fur.animation_data.action = animation
    fur.animation_data.action_slot = slot
    # TT Lab's Target draws this project's Crash Fur and Fur, other settings and another picture at the same URIs (the projects' names are
    # the same), and the other model's material has a change its project's TT Lab doesn't have yet
    picture[material_settings.PNG_CONFIRMED_PROPERTY] = link_sync._png_hash(picture)
    confirmed = picture[material_settings.PNG_CONFIRMED_PROPERTY]
    material_settings.settings_of(fur).render_bucket = "BUCKET_8"
    theirs = gs_material.new_material("CRASH_FUR", skin=True, textured=True)
    theirs["DmaChainIndex"] = 6
    theirs["Shaders"][0]["TextureId"] = {"_uri": picture_uri}
    fake.answer_link(paths["Target"], [{"uri": fur_uri, "name": "Crash Fur", "data": theirs}], [{"uri": picture_uri, "name": "Fur", "hash": "a" * 64}])
    keeper_settings, keeper_pixels = settings_snapshot(fur), list(picture.pixels)
    sent_before, asked_before = len(fake.of("update_material")), len(fake.of("get_picture"))
    materials_made, pictures_made = len(fake.of("create_material")), len(fake.of("create_picture"))
    saves_told = len(fake.of("blend_saved"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: link_sync.replacing(root) is None and not link_sync._finishing), "the other project's model wasn't put in place")
    # The other project's file it had isn't this TT Lab's to forget, and the other model holds that project still
    saved = told_after(fake, saves_told)
    check(saved["released"] == [] and saved["released_projects"] == [], "the blend file told %s" % saved)

    def keeper_is_its_projects(when):
        # Answered, TT Lab got everything sent before
        check(link_sync._ask(link_sync.link_of(root), {"type": "ping"}) is not None, "%s, TT Lab didn't answer" % when)
        check(settings_snapshot(fur) == keeper_settings, "%s, the other model's material got this project's settings" % when)
        check(list(picture.pixels) == keeper_pixels and picture.get(material_settings.PNG_CONFIRMED_PROPERTY) == confirmed,
              "%s, the other model's picture got this project's" % when)
        check(not any(header.get("uri") == fur_uri for header, _ in fake.of("update_material")[sent_before:]),
              "%s, the other model's change went to this project's material" % when)
        check(not any(header.get("uri") == picture_uri for header, _ in fake.of("get_picture")[asked_before:]),
              "%s, this project's picture was asked for the other model's" % when)

    keeper_is_its_projects("linked")

    drawn = [material for material, _ in materials._drawn(root).values()]
    check(fur not in drawn and paint in drawn, "the model draws %s" % [material.name for material in drawn])
    check(fur.get(tlm_blender.URI_PROPERTY) == fur_uri and pictures_of(fur) == [picture] and picture.get(material_settings.TEXTURE_URI_PROPERTY) == picture_uri
          and picture.get(material_settings.PNG_CONFIRMED_PROPERTY) == confirmed, "the other model's material or picture isn't its project's any more")
    own = {image for material in drawn for image in pictures_of(material)}
    check(len(own) == 1 and picture not in own, "the model's materials draw %s" % [image.name for image in own])
    target = uri_of(paths["Target"])
    made = [header for header, _ in fake.of("create_material")[materials_made:]]
    check(sorted(header["name"] for header in made) == sorted(material.name for material in drawn) and all(header["owner"] == target for header in made),
          "made the materials %s" % made)
    made_pictures = [header for header, _ in fake.of("create_picture")[pictures_made:]]
    check([header["name"] for header in made_pictures] == [image.name for image in own] and made_pictures[0]["owner"] == target, "made the pictures %s" % made_pictures)
    replaced = [payload for header, payload in fake.of("write_model") if header.get("replacing")][-1]
    named = [entry.get("uri") for entry in tlm.TlmFile.from_bytes(replaced).materials]
    check(fur_uri not in named and sorted(named) == sorted(material.get(tlm_blender.URI_PROPERTY) for material in drawn), "the write names %s" % named)
    copy = next(material for material in drawn if material.name.startswith("Crash Fur"))
    check(copy.animation_data is not None and copy.animation_data.action not in (None, animation) and fur.animation_data.action == animation,
          "the copy's animation is the other model's material's")
    # What TT Lab tells of its project's Crash Fur and Fur isn't of the other model's; the model's own still take it (the rename last, all
    # taken once it's taken)
    other = gs_material.new_material("CRASH_FUR", skin=True, textured=True)
    other["DmaChainIndex"] = 10
    copy_uri = copy[tlm_blender.URI_PROPERTY]
    for push in ({"type": "material_changed", "uri": fur_uri, "data": other}, {"type": "picture_changed", "uri": picture_uri, "hash": "b" * 64},
                 {"type": "renamed", "uri": fur_uri, "new_uri": fur_uri + " 2", "name": "Crash Fur 2"}, {"type": "deleted", "uri": picture_uri},
                 {"type": "deleted", "uri": fur_uri}, {"type": "renamed", "uri": copy_uri, "new_uri": copy_uri + " Renamed", "name": copy.name + " Renamed"}):
        fake.push(push)

    check(tick_until(lambda: copy.get(tlm_blender.URI_PROPERTY) == copy_uri + " Renamed"), "the model's own material wasn't renamed")
    check(fur.name == "Crash Fur" and fur.get(tlm_blender.URI_PROPERTY) == fur_uri and picture.get(material_settings.TEXTURE_URI_PROPERTY) == picture_uri,
          "TT Lab's rename or deletion reached the other model's material or picture")
    keeper_is_its_projects("told")
    # A model of a third project of the same name draws its own project's, not the other model's of the same URIs
    third = make_copy(os.path.join(directory, "Third"), "33333333-4444-5555-6666-777777777777")
    add_project_material(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(third)))), theirs, (10, 200, 30, 255))
    imported = model(third, "Third")
    its = [material for material, _ in materials._drawn(imported).values() if material.get(tlm_blender.URI_PROPERTY) == fur_uri]
    check(len(its) == 1 and its[0] != fur and material_settings.settings_of(its[0]).render_bucket == "BUCKET_6",
          "the third project's model draws %s" % [material.name for material in its])
    check(pictures_of(its[0]) and pictures_of(its[0])[0] != picture and tuple(round(value * 255) for value in pictures_of(its[0])[0].pixels[:4]) == (10, 200, 30, 255),
          "the third project's model draws the other model's picture")
    keeper_is_its_projects("imported")
    third_pictures = pictures_of(its[0])
    tlm_blender.remove_model(imported)
    for material in its:
        bpy.data.materials.remove(material)

    for image in third_pictures:
        bpy.data.images.remove(image)

    # This project's model drawing its Crash Fur next to the other model's: TT Lab's change of it is its, saving confirms only its
    mine = model(paths["Other"], "Mine")
    check(tick_until(linked(mine)), "this project's model didn't link")
    own_fur = next((material for material, _ in materials._drawn(mine).values() if material.get(tlm_blender.URI_PROPERTY) == fur_uri), None)
    check(own_fur is not None and own_fur != fur, "this project's model draws the other model's material")
    # Drawing this project's Fur, which only the other model's picture is of in the scene: TT Lab's picture asked for
    textured = json.loads(json.dumps(other))
    textured["Shaders"][0]["TextureId"] = {"_uri": picture_uri}
    fake.push({"type": "material_changed", "uri": fur_uri, "data": textured})
    check(tick_until(lambda: material_settings.settings_of(own_fur).render_bucket == "BUCKET_10" and pictures_of(own_fur)),
          "this project's material didn't take TT Lab's change")
    own_picture = pictures_of(own_fur)[0]
    check(own_picture != picture and tuple(round(value * 255) for value in own_picture.pixels[:4]) == (200, 100, 50, 255),
          "this project's material draws the other model's picture")
    asked_before = len(fake.of("get_picture"))
    bpy.ops.wm.save_mainfile()
    keeper_is_its_projects("saved")
    # What TT Lab doesn't have any more is made again of this project's material, the other model's stays its project's
    link_sync._make_again(link_sync.link_of(mine), mine, {fur_uri, picture_uri})
    check(not own_fur.get(tlm_blender.URI_PROPERTY) and not own_picture.get(material_settings.TEXTURE_URI_PROPERTY), "this project's weren't made again")
    check(fur.get(tlm_blender.URI_PROPERTY) == fur_uri and picture.get(material_settings.TEXTURE_URI_PROPERTY) == picture_uri, "the other model's were made again")
    tlm_blender.remove_model(mine)
    # Without the other model, the TT Labs forget the blend file for that project's models (and this TT Lab for the model taken out)
    tlm_blender.remove_model(keeper)
    saves_told = len(fake.of("blend_saved"))
    bpy.ops.wm.save_mainfile()
    saved = told_after(fake, saves_told)
    check(saved["released"] == [paths["Other"]] and saved["released_projects"] == ["99999999-8888-7777-6666-555555555555"], "the blend file told %s" % saved)
    check(link_sync.released() == [], "still releasing %s" % link_sync.released())

    # A copy of this project's model draws this project's
    fresh()
    root = model(make_copy(os.path.join(directory, "Copy"), UUID), "Custom")
    fur = bpy.data.materials["Crash Fur"]
    picture = show_with_picture(fur, picture_uri)
    made = len(fake.of("create_material")) + len(fake.of("create_picture"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: link_sync.replacing(root) is None and not link_sync._finishing), "the copy's model wasn't put in place")
    check(len(fake.of("create_material")) + len(fake.of("create_picture")) == made, "the project's material or picture was made again for its copy's model")
    check(fur.get(tlm_blender.URI_PROPERTY) == fur_uri and picture.get(material_settings.TEXTURE_URI_PROPERTY) == picture_uri, "the copy's model lost the project's URIs")
    # Whose project's the scene's materials are: a copy's model is the project's, what another project's models draw as well is shared
    fresh()
    ours, copied, theirs_model = model(paths["Crash"], "Ours"), model(make_copy(os.path.join(directory, "Copy2"), UUID), "Copied"), model(elsewhere, "Theirs")
    check(set(materials._drawn(copied)) == set(materials._drawn(ours)), "the copy's model doesn't draw the project's materials")
    check(not set(material.as_pointer() for material, _ in materials._drawn(theirs_model).values()) & set(material.as_pointer() for material, _ in materials._drawn(ours).values()),
          "the other project's model draws this project's materials")
    materials.make_own(bpy.data.materials["Crash Fur"], copied)
    shared = bpy.data.materials.new("Shared")
    for drawing in (ours, theirs_model):
        part = next(child for child in tlm_blender._descendants(drawing) if child.type == "MESH" and tlm_blender.role_of(child) == "body")
        part.data.materials.append(shared)
        part.data.polygons[0].material_index = len(part.data.materials) - 1

    blocks = materials.ProjectBlocks(project_root_of(paths["Crash"]))
    check(sorted(root.name for root in blocks.roots()[0]) == ["Copied", "Ours"] and [root.name for root in blocks.roots()[1]] == ["Theirs"],
          "the project's models are %s, the others %s" % ([root.name for root in blocks.roots()[0]], [root.name for root in blocks.roots()[1]]))
    theirs_only = [material for material, _ in materials._drawn(theirs_model).values() if material != shared]
    check(theirs_only and all(blocks.is_theirs(material) and not blocks.is_ours(material) for material in theirs_only),
          "the other project's materials are %s" % [material.name for material in theirs_only])
    check(blocks.is_shared(shared) and blocks.is_ours(shared) and not blocks.is_theirs(shared), "the shared material isn't shared")
    check(all(blocks.is_ours(material) and not blocks.is_theirs(material) for drawing in (ours, copied) for material, _ in materials._drawn(drawing).values()),
          "this project's models' materials aren't its")
    # Of the candidates of a URI the project's is picked, never the other project's
    fur_theirs = next(material for material in theirs_only if material.get(tlm_blender.URI_PROPERTY) == fur_uri)
    fur_ours = next(material for material, _ in materials._drawn(ours).values() if material.get(tlm_blender.URI_PROPERTY) == fur_uri)
    check(blocks.pick([fur_theirs, fur_ours]) == fur_ours and materials.ProjectBlocks(project_root_of(elsewhere)).pick([fur_ours, fur_theirs]) == fur_theirs,
          "picked another project's material")
    print("other projects' models: ok")


def check_added_to_the_project(fake, paths, directory):
    """Add to TT Lab's Project of another project's model makes what it draws of that project its own (the file sent names none of its
    URIs), the scene's other model of it keeping them, and the model links as the new one, which TT Lab didn't change. A model of this project
    added again leaves the model it stood for: unlinked, its file released."""
    fresh()
    elsewhere = make_copy(os.path.join(directory, "Added"), "99999999-8888-7777-6666-444444444444")
    fur_uri, picture_uri = "res://%s/Material/Crash Fur" % PACKAGE, "res://%s/Texture/Fur" % PACKAGE
    root, keeper = model(elsewhere, "Imported"), model(elsewhere, "Keeper")
    fur = bpy.data.materials["Crash Fur"]
    picture = show_with_picture(fur, picture_uri)
    folder = "res://%s/OGI" % PACKAGE
    tlm_operators._new_asset_folders[:] = [(folder, "OGI", folder)]

    def add(blender_object, name):
        bpy.context.view_layer.objects.active = blender_object
        made = len(fake.of("create_model"))
        check(bpy.ops.ttt.add_to_tt_lab(name=name, folder=folder) == {"FINISHED"}, "%s wasn't added" % blender_object.name)
        sent = fake.of("create_model")[made:]
        check(len(sent) == 1, "TT Lab was asked to add %d models" % len(sent))
        added = os.path.join(project_root_of(paths["Crash"]), "assets", PACKAGE, "OGI", name + ".tlm")
        check(blender_object.get(tlm_blender.PATH_PROPERTY) == added, "the model's file is %s" % blender_object.get(tlm_blender.PATH_PROPERTY))
        check(tick_until(linked(blender_object)) and link_sync.link_of(blender_object).models[blender_object.name] == uri_of(added)
              and not blender_object.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "the added model didn't link as TT Lab has it")
        return json.dumps(tlm.TlmFile.from_bytes(sent[0][1]).json)

    written = add(root, "Added")
    check(fur_uri not in written and picture_uri not in written, "the file sent names the other project's material or picture")
    drawn = [material for material, _ in materials._drawn(root).values()]
    check(fur not in drawn and not any(material.get(tlm_blender.URI_PROPERTY) for material in drawn), "the model draws %s" % [material.name for material in drawn])
    check(picture not in {image for material in drawn for image in pictures_of(material)}, "the model draws the other project's picture")
    check(fur in [material for material, _ in materials._drawn(keeper).values()] and fur.get(tlm_blender.URI_PROPERTY) == fur_uri
          and picture.get(material_settings.TEXTURE_URI_PROPERTY) == picture_uri, "the other model lost its project's material or picture")

    # This project's linked model added as another one
    fresh()
    mine = model(paths["Other"], "Mine")
    check(tick_until(linked(mine)), "this project's model didn't link")
    unlinked = len(fake.of("unlink_model"))
    add(mine, "Another")
    check([header["uri"] for header, _ in fake.of("unlink_model")[unlinked:]] == [uri_of(paths["Other"])], "the model it stood for wasn't unlinked")
    check(link_sync.released() == [paths["Other"]], "released %s" % link_sync.released())
    for name in ("Added", "Another"):
        os.remove(os.path.join(project_root_of(paths["Crash"]), "assets", PACKAGE, "OGI", name + ".tlm"))

    print("added to the project: ok")


def project_root_of(path):
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(path))))


def linked(root):
    # This model linked, not another of its name before it
    return lambda: link_sync.is_linked(root) and root.name in link_sync.link_of(root).models \
        and link_sync.link_of(root).uids.get(root.name) == root.get(tlm_blender.UID_PROPERTY)


def check_conflicts_during_saves(fake, paths, directory):
    """A conflict TT Lab answers while a save waits for it marks the model, and the save asks about it once it's done; undo leaves the mark.
    Taking TT Lab's imports TT Lab's model as it has it, nothing changed since."""
    fresh()
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "conflict.blend"))
    check(tick_until(linked(root)), "the model didn't link")
    fake.conflict_next_writes(1)
    if bpy.app.timers.is_registered(save_hook._ask):
        bpy.app.timers.unregister(save_hook._ask)

    saves_told = len(fake.of("blend_saved"))
    bpy.ops.wm.save_mainfile()
    check(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and root.name in link_sync.link_of(root).changed, "the conflict didn't mark the model")
    check(bpy.app.timers.is_registered(save_hook._ask), "the save didn't ask about the model TT Lab changed")
    bpy.app.timers.unregister(save_hook._ask)
    # The blend file isn't the model's as TT Lab has it
    saved = told_after(fake, saves_told)
    check(saved["models"] == [], "the save told %s" % saved)
    # What memory undo does to the mark: given back
    del root[link_sync.CHANGED_IN_TT_LAB_PROPERTY]
    link_sync._undone()
    check(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "undo took TT Lab's change back")
    gets = len(fake.of("get_model"))
    new_root = link_sync.take_tt_labs(bpy.context, root, paths["Crash"])
    check(len(fake.of("get_model")) == gets + 1, "TT Lab's model wasn't asked for")
    check(new_root.get(link_sync.REVISION_PROPERTY) == "r-tt-lab" and new_root.get(link_sync.MODEL_URI_PROPERTY) == uri_of(paths["Crash"])
          and not new_root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "TT Lab's model isn't the model now")
    writes, saves_told = len(fake.of("write_model")), len(fake.of("blend_saved"))
    bpy.ops.wm.save_mainfile()
    written = [header for header, _ in fake.of("write_model")[writes:]]
    check(written and written[-1]["revision"] == "r-tt-lab" and not written[-1]["force"], "the save after taking TT Lab's wrote %s" % written)
    saved = told_after(fake, saves_told)
    check(saved["models"] == [uri_of(paths["Crash"])], "the save told %s" % saved)
    print("conflicts during saves: ok")


def check_closed_files_messages(fake, paths):
    """What waited to be taken was told of the file that closed: dropped with it, and a link that closed tells nothing."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    stale = link_sync.link_of(root)
    use_for = {"type": "use_for", "model": paths["Target"], "asset_type": "Ogi", "name": "Target"}
    link_sync._waiting.append((stale, use_for, b""))
    link_sync.drop_all()
    check(not link_sync._waiting, "what waited stayed for the next file")
    link_sync._waiting.append((stale, use_for, b""))
    link_sync.tick()
    check(link_sync.replacing(root) is None and link_sync._request[0] is None, "a closed link's message was taken")
    print("closed files' messages: ok")


def check_stale_replacements(fake, paths):
    """Undone before TT Lab took it (linked as the target's, refused), a replacement goes with the marker undo takes back: the model is its
    earlier file's again and links again, the target's released; renamed while it waits, it's the model's still."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    fake.refuse_next_writes(1)
    link_sync.use_for(root, paths["Target"], "Target")

    def refused():
        entry, link = link_sync._replacing.get(root.name), link_sync.link_of(root)
        return link is not None and link.models.get(root.name) == uri_of(paths["Target"]) and entry is not None and entry.attempted and not entry.is_in_flight()

    check(tick_until(refused), "the replacement wasn't linked and refused")
    unlinks = len(fake.of("unlink_model"))
    # What memory undo does: the file the model had, no marker
    root[tlm_blender.PATH_PROPERTY] = paths["Crash"]
    del root[link_sync.REPLACES_PROPERTY]
    link_sync._undone()
    check(root.name not in link_sync._replacing and root.name not in link_sync.link_of(root).models, "undo left the model the target's")
    check(wait_for(lambda: len(fake.of("unlink_model")) > unlinks) and fake.of("unlink_model")[-1][0]["uri"] == uri_of(paths["Target"]),
          "the target's model wasn't released")
    check(paths["Target"] in link_sync.released(), "the target's file isn't released")
    writes = len(fake.of("write_model"))
    check(tick_until(linked(root)), "the model didn't link")
    tick_until(lambda: False, 0.5)
    check(not any(header.get("replacing") for header, _ in fake.of("write_model")[writes:]), "the undone replacement was written")
    check(root.get(link_sync.MODEL_URI_PROPERTY) == uri_of(paths["Crash"]), "the model isn't its file's")
    link_sync.use_for(root, paths["Target"], "Target")
    root.name = "Renamed"
    check(link_sync.replacing(root) == "Target", "renamed, it doesn't replace Target any more")
    check(tick_until(lambda: link_sync.replacing(root) is None), "the renamed replacement wasn't written")
    check(fake.of("write_model")[-1][0]["replacing"] and fake.of("write_model")[-1][0]["name"] == "Renamed", "written as %s" % fake.of("write_model")[-1][0])
    print("stale replacements: ok")


def check_replacements_not_changed(fake, paths):
    """A model waiting to replace TT Lab's isn't marked changed by TT Lab's changes of that model: it's written over them."""
    fresh()
    root = model(paths["Crash"], "Custom")
    fake.refuse_next_writes(1)
    link_sync.use_for(root, paths["Target"], "Target")

    def refused():
        entry = link_sync._replacing.get(root.name)
        return link_sync.replacing(root) == "Target" and entry is not None and entry.attempted and not entry.is_in_flight()

    check(tick_until(refused), "the replacement wasn't refused")
    fake.push({"type": "model_changed", "uri": uri_of(paths["Target"]), "revision": "r9"})
    tick_until(lambda: False, 0.5)
    check(not root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "the waiting replacement was marked changed in TT Lab")
    print("replacements not changed: ok")


def check_replacements_in_flight(fake, paths):
    """A save while TT Lab takes a model in its model's place doesn't write it again: once answered it's TT Lab's, the blend file saved."""
    fresh()
    root = model(paths["Crash"], "Custom")
    writes = len(fake.of("write_model"))
    fake.hold("write_model")
    link_sync.use_for(root, paths["Target"], "Target")

    def sent():
        entry = link_sync._replacing.get(root.name)
        return link_sync.replacing(root) == "Target" and entry is not None and entry.is_in_flight()

    check(tick_until(sent), "the replacement wasn't sent")
    wait = link_sync.ANSWERS_WAIT
    link_sync.ANSWERS_WAIT = 0.3
    try:
        bpy.ops.wm.save_mainfile()
    finally:
        link_sync.ANSWERS_WAIT = wait

    replacing = [header for header, _ in fake.of("write_model")[writes:] if header.get("replacing")]
    check(len(replacing) == 1, "written in its place %d times" % len(replacing))
    check(any("TT Lab is taking it" in text for text in told()), "told %s" % told())
    fake.release("write_model")
    check(tick_until(lambda: link_sync.replacing(root) is None and not link_sync._finishing), "the answer wasn't taken")
    # Taken after the save, the blend file was saved again: opened again it doesn't replace the target once more
    bpy.ops.wm.open_mainfile(filepath=bpy.data.filepath)
    check(link_sync.REPLACES_PROPERTY not in bpy.data.objects["Custom"], "the blend file kept the replacement TT Lab took")
    print("replacements in flight: ok")


def check_deleted_models_released(fake, paths, directory):
    """A linked model taken out of the scene is unlinked, and the blend file's save releases its file."""
    fresh()
    root = model(paths["Other"], "Gone")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "deleted.blend"))
    check(tick_until(linked(root)), "the model didn't link")
    unlinks = len(fake.of("unlink_model"))
    tlm_blender.remove_model(root)
    check(tick_until(lambda: len(fake.of("unlink_model")) > unlinks), "TT Lab wasn't told the model left")
    check(fake.of("unlink_model")[-1][0]["uri"] == uri_of(paths["Other"]), "unlinked %s" % fake.of("unlink_model")[-1][0])
    saves_told = len(fake.of("blend_saved"))
    bpy.ops.wm.save_mainfile()
    saved = told_after(fake, saves_told)
    check(paths["Other"] in saved["released"], "the save told %s" % saved)
    print("deleted models released: ok")


def check_deleted_pictures(fake, paths):
    """A texture deleted in TT Lab while Blender draws its picture: the picture is made again."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    texture = "res://%s/Texture/Fur" % PACKAGE
    picture = show_with_picture(bpy.data.materials["Crash Fur"], texture)
    pictures = len(fake.of("create_picture"))
    fake.push({"type": "deleted", "uri": texture})
    check(tick_until(lambda: len(fake.of("create_picture")) > pictures and picture.get(material_settings.TEXTURE_URI_PROPERTY)), "the deleted picture wasn't made again")
    check(fake.of("create_picture")[-1][0]["name"] == picture.name, "made %s" % fake.of("create_picture")[-1][0])
    print("deleted pictures: ok")


def check_several_candidates(fake, paths):
    """Run without asking (in the background), Use an Existing Blend File of a file with several models of the kind takes none."""
    fresh()
    first, second = model(paths["Crash"], "First"), model(paths["Other"], "Second")
    result = bpy.ops.ttt.use_for_tt_lab_model("EXEC_DEFAULT", True, model=paths["Target"], asset_type="Ogi", name="Target")
    check(result == {"CANCELLED"} and link_sync.replacing(first) is None and link_sync.replacing(second) is None, "one of several was taken")
    check(any("has 2 " in text for text in told()), "told %s" % told())
    print("several candidates: ok")


def check_pending_replacements_kept(fake, paths, directory):
    """A replacement TT Lab didn't take yet is kept with the blend file: opened again it still waits, and a save without TT Lab leaves the
    target's file alone."""
    fresh()
    away = make_copy(os.path.join(directory, "Away"), "77777777-6666-5555-4444-333333333333")
    target = os.path.join(os.path.dirname(away), "Target.tlm")
    shutil.copy(away, target)
    root = model(paths["Crash"], "Custom")
    link_sync.use_for(root, target, "Target")
    blend = os.path.join(directory, "pending.blend")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=blend)
    before = open(target, "rb").read()
    bpy.ops.wm.open_mainfile(filepath=blend)
    root = bpy.data.objects["Custom"]
    check(link_sync.replacing(root) == "Target", "opened again, the replacement doesn't wait")
    save_hook._told.clear()
    bpy.ops.wm.save_mainfile()
    check(open(target, "rb").read() == before, "the target's file was written past TT Lab")
    check(any("once TT Lab has the project open" in text for text in told()), "told %s" % told())
    print("pending replacements kept: ok")


def check_split_copies_by_project(directory):
    """Two projects of one name draw materials of the same URIs: they aren't copies of each other (the timer made one of them a material made
    in Blender); a copy within one project's model still is."""
    fresh()
    first = model(make_copy(os.path.join(directory, "SplitA"), "aaaaaaaa-0000-0000-0000-000000000001"), "SplitA")
    second = model(make_copy(os.path.join(directory, "SplitB"), "aaaaaaaa-0000-0000-0000-000000000002"), "SplitB")
    furs = [fur_of(first), fur_of(second)]
    check(furs[0] != furs[1], "the two projects' models draw one material")
    check(materials.split_copies() == [] and all(fur.get(tlm_blender.URI_PROPERTY) for fur in furs), "another project's material was split")
    copy = furs[0].copy()
    body = next(child for child in tlm_blender._descendants(first) if child.type == "MESH" and tlm_blender.role_of(child) == "body")
    body.data = body.data.copy()
    body.data.materials.append(copy)
    body.data.polygons[0].material_index = len(body.data.materials) - 1
    check(materials.split_copies() == [copy] and not copy.get(tlm_blender.URI_PROPERTY) and furs[0].get(tlm_blender.URI_PROPERTY), "the copy wasn't split")
    # The first project's model deleted, not purged: its material left over isn't the second's original
    for blender_object in [first] + tlm_blender._descendants(first):
        bpy.data.objects.remove(blender_object)

    check(materials.split_copies() == [] and furs[1].get(tlm_blender.URI_PROPERTY), "the other project's leftover split this project's material")
    print("split copies by project: ok")


def check_walks_alike(paths):
    """The walks through an index of the objects' children (objects_walked, made once) find what Object.children finds, objects out of
    every scene left out alike."""
    fresh()
    root = model(paths["Crash"], "Custom")
    gone = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    for collection in list(gone.users_collection):
        collection.objects.unlink(gone)

    def walk():
        return ([blender_object.name for blender_object in tlm_blender._descendants(root)],
                [(blender_object.name, tlm_blender.in_scene(blender_object)) for blender_object in bpy.data.objects],
                [[child.name for child in tlm_blender.live_children(blender_object)] for blender_object in bpy.data.objects])

    plain = walk()
    with tlm_blender.objects_walked():
        indexed = walk()

    check(plain == indexed, "the walks differ: %s against %s" % (plain, indexed))
    check((gone.name, False) in plain[1] and not any(gone.name in children for children in plain[2]), "an object out of the scene was walked")
    print("walks alike: ok")


def check_lookups_prefer_the_project(fake, paths, directory):
    """A leftover of another project's model of the same URIs (deleted, not purged) isn't what TT Lab's link renames: the project's model's
    is, and nothing renames it back."""
    fresh()
    leftover = model(make_copy(os.path.join(directory, "Leftover"), "bbbbbbbb-0000-0000-0000-000000000001"), "Leftover")
    root = model(paths["Crash"], "Custom")
    theirs, ours = fur_of(leftover), fur_of(root)
    check(theirs != ours, "this project's model draws the other project's material")
    for blender_object in [leftover] + tlm_blender._descendants(leftover):
        bpy.data.objects.remove(blender_object)

    check(tick_until(linked(root)), "the model didn't link")
    renames = len(fake.of("rename"))
    fur_uri = ours[tlm_blender.URI_PROPERTY]
    fake.push({"type": "renamed", "uri": fur_uri, "new_uri": fur_uri, "name": "Bandicoot Fur"})
    check(tick_until(lambda: ours.name == "Bandicoot Fur"), "the project's material wasn't renamed")
    tick_until(lambda: False, 0.5)
    check(theirs.name != "Bandicoot Fur" and not any(header.get("uri") == fur_uri for header, _ in fake.of("rename")[renames:]),
          "the leftover was renamed or TT Lab's material renamed back")
    print("lookups prefer the project: ok")


def check_confirmed_pictures_are_the_projects(directory):
    """TT Lab took what an export sent (it wrote the file again without exported_by): the pictures sent are confirmed on the model's own
    material's, not another project's of the same texture."""
    fresh()
    texture = "res://%s/Texture/Fur" % PACKAGE
    path = make_copy(os.path.join(directory, "PicturesB"), "cccccccc-0000-0000-0000-000000000002")
    other = model(make_copy(os.path.join(directory, "PicturesA"), "cccccccc-0000-0000-0000-000000000001"), "PicturesA")
    root = model(path, "PicturesB")
    theirs, ours = show_with_picture(fur_of(other), texture), show_with_picture(fur_of(root), texture)
    # Painted in Blender, exported without TT Lab
    ours.pixels = [0.5] * len(ours.pixels)
    painted = link_sync._png_hash(ours)
    tlm_blender.export_file(root, path)
    written = tlm.TlmFile.load(path)
    check(material_sync.EXPORTED_BY_KEY in written.json, "the export isn't the add-on's")
    # What TT Lab writes once it read it
    del written.json[material_sync.EXPORTED_BY_KEY]
    written.save(path)
    tlm_blender.export_file(root, path)
    check(ours.get(material_settings.PNG_CONFIRMED_PROPERTY) == painted, "the model's picture isn't confirmed")
    check(theirs.get(material_settings.PNG_CONFIRMED_PROPERTY) == "confirmed", "the other project's picture got the model's confirmed")
    print("confirmed pictures are the project's: ok")


def check_revisions_of_files(fake, paths):
    """Imported, a model's revision is its file's; exported without TT Lab, what was written. Linked, TT Lab says it has it."""
    fresh()
    path = os.path.join(os.path.dirname(paths["Crash"]), "Revisions.tlm")
    shutil.copy(paths["Crash"], path)
    root = model(path, "Revisions")
    check(root.get(link_sync.REVISION_PROPERTY) == "file:" + sha256_of(path), "imported, the revision is %s" % root.get(link_sync.REVISION_PROPERTY))
    tlm_blender.export_file(root, path)
    check(root.get(link_sync.REVISION_PROPERTY) == "exported:" + sha256_of(path), "exported, the revision is %s" % root.get(link_sync.REVISION_PROPERTY))
    check(tick_until(linked(root)), "the model didn't link")
    check(not root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and root.get(link_sync.REVISION_PROPERTY) == "r0", "TT Lab didn't take what was written for its model")
    tlm_blender.remove_model(root)
    os.remove(path)
    print("revisions of files: ok")


def check_model_changes_told(fake, paths):
    """TT Lab telling a model changed with the revision Blender has (a name, a save of what it wrote) changes nothing, another revision marks
    it; moved, the model is its new place's; deleted, it's a model of no file."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    uri = uri_of(paths["Crash"])
    fake.push({"type": "model_changed", "uri": uri, "revision": root[link_sync.REVISION_PROPERTY]})
    tick_until(lambda: False, 0.5)
    check(not root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "a change to what Blender has marked the model")
    fake.push({"type": "model_changed", "uri": uri, "revision": "r-elsewhere"})
    check(tick_until(lambda: root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY)), "TT Lab's change didn't mark the model")
    fresh()
    root = model(paths["Other"], "Mover")
    check(tick_until(linked(root)), "the model didn't link")
    moved = os.path.join(os.path.dirname(os.path.dirname(paths["Other"])), "Characters", "OGI", "Other.tlm")
    moved_uri = "res://%s/Characters/OGI/Other" % PACKAGE
    fake.push({"type": "moved", "uri": uri_of(paths["Other"]), "new_uri": moved_uri, "path": moved})
    check(tick_until(lambda: root.get(tlm_blender.PATH_PROPERTY) == moved), "the moved model isn't its new place's")
    link = link_sync.link_of(root)
    check(root.get(link_sync.MODEL_URI_PROPERTY) == moved_uri and link.models.get(root.name) == moved_uri and link.paths.get(root.name) == moved,
          "the moved model is %s" % [root.get(link_sync.MODEL_URI_PROPERTY), link.models.get(root.name)])
    # What memory undo does: the file and URI it had before the move, which is its new place's still
    root[tlm_blender.PATH_PROPERTY] = paths["Other"]
    root[link_sync.MODEL_URI_PROPERTY] = uri_of(paths["Other"])
    link_sync._undone()
    check(root.get(tlm_blender.PATH_PROPERTY) == moved and root.get(link_sync.MODEL_URI_PROPERTY) == moved_uri and link.models.get(root.name) == moved_uri,
          "undo took the move back: %s" % [root.get(tlm_blender.PATH_PROPERTY), root.get(link_sync.MODEL_URI_PROPERTY), link.models.get(root.name)])
    fake.push({"type": "deleted", "uri": moved_uri})
    check(tick_until(lambda: tlm_blender.PATH_PROPERTY not in root), "the deleted model still has its file")
    check(root.name not in link.models and any("was deleted in TT Lab" in text for text in told()), "told %s" % told())
    print("model changes told: ok")


def check_edit_selects_the_links_model(fake, paths, directory):
    """TT Lab's Edit in Blender selects its project's model, not another project's of the same URI."""
    fresh()
    theirs = model(make_copy(os.path.join(directory, "EditOther"), "dddddddd-0000-0000-0000-000000000001"), "A Theirs")
    theirs[link_sync.MODEL_URI_PROPERTY] = uri_of(paths["Crash"])
    ours = model(paths["Crash"], "B Ours")
    check(tick_until(linked(ours)), "the model didn't link")
    bpy.context.view_layer.objects.active = None
    fake.push({"type": "edit_in_blender", "uri": uri_of(paths["Crash"])})
    check(tick_until(lambda: bpy.context.view_layer.objects.active == ours), "Edit in Blender selected %s" % bpy.context.view_layer.objects.active)
    check(not theirs.select_get(), "the other project's model was selected")
    print("edit selects the link's model: ok")


def check_other_systems_models_used(fake, paths):
    """A model whose file is another system's path is no other project's: put in a model's place, what it draws stays the project's."""
    fresh()
    root = model(paths["Crash"], "Weapon")
    root[tlm_blender.PATH_PROPERTY] = "D:\\Mods\\Test\\assets\\%s\\OGI\\Crash.tlm" % PACKAGE
    drawn = {material.name: material.get(tlm_blender.URI_PROPERTY) for material, _ in materials._drawn(root).values()}
    made = len(fake.of("create_material"))
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: link_sync.replacing(root) is None and not link_sync._finishing), "the model wasn't put in place")
    check({material.name: material.get(tlm_blender.URI_PROPERTY) for material, _ in materials._drawn(root).values()} == drawn and len(fake.of("create_material")) == made,
          "what the model draws was made its own")
    print("other systems' models used: ok")


def check_overwrite_forces_only_whats_asked(fake, paths):
    """Write Blender's Over Them forces the models the question asked about, not one TT Lab changed while it was up."""
    fresh()
    asked, later = model(paths["Crash"], "Asked"), model(paths["Other"], "Later")
    check(tick_until(linked(asked)) and tick_until(linked(later)), "the models didn't link")
    for root in (asked, later):
        fake.push({"type": "model_changed", "uri": uri_of(root[tlm_blender.PATH_PROPERTY]), "revision": "r-elsewhere"})

    check(tick_until(lambda: asked.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and later.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY)), "the models weren't marked")
    writes = len(fake.of("write_model"))
    check(bpy.ops.ttt.resolve_tt_lab_changes("EXEC_DEFAULT", choice="OVERWRITE", names=json.dumps(["Asked"])) == {"FINISHED"}, "nothing was written")
    written = [header for header, _ in fake.of("write_model")[writes:]]
    check([(header["uri"], header["force"]) for header in written] == [(uri_of(paths["Crash"]), True)], "wrote %s" % written)
    check(tick_until(lambda: not asked.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY)) and later.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "the marks are wrong")
    print("overwrite forces only what's asked: ok")


def check_refused_links_of_replacements(fake, paths, directory):
    """TT Lab has no model of the file a model was put in the place of: it's a model of no file, a save writes nothing into that file."""
    fresh()
    root = model(paths["Crash"], "Custom")
    with open(paths["Target"], "rb") as file:
        before = file.read()

    fake.add_unknown(paths["Target"])
    try:
        link_sync.use_for(root, paths["Target"], "Target")
        check(tick_until(lambda: tlm_blender.PATH_PROPERTY not in root), "the model still has the target's file")
        check(any("It's a model of no file now" in text for text in told()), "told %s" % told())
        writes = len(fake.of("write_model"))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "unlinkable.blend"))
        with open(paths["Target"], "rb") as file:
            check(file.read() == before and len(fake.of("write_model")) == writes, "the target's file was written")
    finally:
        fake.discard_unknown(paths["Target"])

    print("refused links of replacements: ok")


def check_undoing_take(fake, paths, directory):
    """Taking TT Lab's undone, the model Blender had is back as TT Lab's change marked it: the next save asks again."""
    fresh()
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "undo_take.blend"))
    check(tick_until(linked(root)), "the model didn't link")
    fake.conflict_next_writes(1)
    bpy.ops.wm.save_mainfile()
    check(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "the conflict didn't mark the model")
    if bpy.app.timers.is_registered(save_hook._ask):
        bpy.app.timers.unregister(save_hook._ask)

    uid, revision = root[tlm_blender.UID_PROPERTY], root[link_sync.REVISION_PROPERTY]
    new_root = link_sync.take_tt_labs(bpy.context, root, paths["Crash"])
    check(new_root is not None and new_root.get(link_sync.REVISION_PROPERTY) == "r-tt-lab", "TT Lab's model wasn't taken")
    check(tick_until(linked(new_root)), "TT Lab's model didn't link")
    # What memory undo does: the model before, under the name
    new_root[tlm_blender.UID_PROPERTY] = uid
    new_root[link_sync.REVISION_PROPERTY] = revision
    new_root[link_sync.CHANGED_IN_TT_LAB_PROPERTY] = True
    link_sync._undone()
    tick_until(lambda: False, 0.5)
    check(new_root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and new_root.get(link_sync.REVISION_PROPERTY) == revision, "undo of Take TT Lab's lost TT Lab's change")
    writes = len(fake.of("write_model"))
    bpy.ops.wm.save_mainfile()
    check(not any(header["uri"] == uri_of(paths["Crash"]) for header, _ in fake.of("write_model")[writes:]), "the save wrote over TT Lab's change")
    check(bpy.app.timers.is_registered(save_hook._ask), "the save didn't ask again")
    bpy.app.timers.unregister(save_hook._ask)
    print("undoing take: ok")


def check_undoing_use_for(fake, paths):
    """A model put in another's place and undone is its file's model again with what it had: TT Lab's change of that model since stays
    marked once it links again."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    revision = root[link_sync.REVISION_PROPERTY]
    fake.revise(paths["Crash"], "r-saved-in-tt-lab")
    fake.push({"type": "model_changed", "uri": uri_of(paths["Crash"]), "revision": "r-saved-in-tt-lab"})
    check(tick_until(lambda: root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY)), "TT Lab's change didn't mark the model")
    link_sync.use_for(root, paths["Target"], "Target")
    check(tick_until(lambda: link_sync.link_of(root) is not None and link_sync.link_of(root).models.get(root.name) == uri_of(paths["Target"]))
          or link_sync.replacing(root) is None, "the model wasn't put in place")
    # What memory undo does: the file, revision and mark it had, no marker
    root[tlm_blender.PATH_PROPERTY] = paths["Crash"]
    root[link_sync.REVISION_PROPERTY] = revision
    root[link_sync.CHANGED_IN_TT_LAB_PROPERTY] = True
    if link_sync.REPLACES_PROPERTY in root:
        del root[link_sync.REPLACES_PROPERTY]

    link_sync._undone()
    check(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY) and root.get(link_sync.REVISION_PROPERTY) == revision, "undo lost what the model had")
    check(tick_until(lambda: link_sync.link_of(root) is not None and link_sync.link_of(root).models.get(root.name) == uri_of(paths["Crash"])), "the model didn't link again")
    tick_until(lambda: False, 0.3)
    check(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "linked again, TT Lab's change isn't marked")
    print("undoing use_for: ok")


def check_late_takes(fake, paths):
    """TT Lab not giving its model in time: the model stays as it was, the file isn't taken for TT Lab's."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    fake.hold("get_model")
    wait = link_sync.TAKE_WAIT
    link_sync.TAKE_WAIT = 0.5
    try:
        revision = root[link_sync.REVISION_PROPERTY]
        check(link_sync.take_tt_labs(bpy.context, root, paths["Crash"]) is None, "a model was taken")
        check(bpy.data.objects.get("Custom") == root and root.get(link_sync.REVISION_PROPERTY) == revision, "the model changed")
        check(any("TT Lab didn't give Custom" in text for text in told()), "told %s" % told())
    finally:
        link_sync.TAKE_WAIT = wait
        fake.release("get_model")

    tick_until(lambda: False, 0.3)
    check(bpy.data.objects.get("Custom") == root and root.get(link_sync.REVISION_PROPERTY) == revision, "the late answer was taken")
    print("late takes: ok")


def check_retargeted_models_keep_their_link(fake, paths, directory):
    """The rig's model taking a linked model's place (Retarget and Replace) is that model as TT Lab knows it: its URI, revision and marks."""
    fresh()
    root = model(paths["Crash"], "Custom")
    check(tick_until(linked(root)), "the model didn't link")
    said = (root[link_sync.REVISION_PROPERTY], bool(root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY)))
    rig = model(make_copy(os.path.join(directory, "Rig"), "eeeeeeee-0000-0000-0000-000000000001", "ogi_template.tlm"), "Rig")
    replace._take_place(bpy.context, root, rig, root)
    link = link_sync.link_of(rig)

    def what_tt_lab_said():
        return rig.get(link_sync.REVISION_PROPERTY), bool(rig.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY))

    check(rig.name == "Custom" and what_tt_lab_said() == said and rig.get(link_sync.MODEL_URI_PROPERTY) == uri_of(paths["Crash"]),
          "the rig's model is %s" % [rig.name, what_tt_lab_said(), rig.get(link_sync.MODEL_URI_PROPERTY)])
    check(link is not None and link.uids.get("Custom") == rig.get(tlm_blender.UID_PROPERTY), "the link doesn't know the rig's model")
    link_sync._undone()
    check(what_tt_lab_said() == said, "an undo took what TT Lab said of it: %s against %s" % (what_tt_lab_said(), said))
    print("retargeted models keep their link: ok")


def check_released_files_kept(fake, paths, directory):
    """A linked model taken out releases its file: an undo or a save that fails keeps it for the next save to tell."""
    fresh()
    root = model(paths["Other"], "Gone")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "released.blend"))
    check(tick_until(linked(root)), "the model didn't link")
    tlm_blender.remove_model(root)
    check(tick_until(lambda: paths["Other"] in link_sync.released()), "the file wasn't released")
    # What memory undo does to the scene's list
    for scene in bpy.data.scenes:
        scene.pop(link_sync.RELEASED_PROPERTY, None)

    link_sync._undone()
    check(paths["Other"] in link_sync.released(), "undo lost the released file")
    try:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "not here", "released.blend"))
    except RuntimeError:
        pass

    check(paths["Other"] in link_sync.released(), "the failed save lost the released file")
    saves_told = len(fake.of("blend_saved"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "released.blend"))
    check(paths["Other"] in told_after(fake, saves_told)["released"] and link_sync.released() == [], "the save didn't tell the released file")
    print("released files kept: ok")


def check_saves_without_tt_lab(fake, paths):
    """TT Lab quitting while a save waits for its answers: the save goes on at once."""
    fresh()
    root = model(paths["Other"], "Custom")
    check(tick_until(linked(root)) and not root.get(link_sync.CHANGED_IN_TT_LAB_PROPERTY), "the model didn't link as TT Lab has it")
    fake.close_on("write_model")
    writes = len(fake.of("write_model"))
    wait = link_sync.ANSWERS_WAIT
    link_sync.ANSWERS_WAIT = 4.0
    try:
        started = time.time()
        bpy.ops.wm.save_mainfile()
        took = time.time() - started
    finally:
        link_sync.ANSWERS_WAIT = wait

    check(len(fake.of("write_model")) > writes, "the save didn't write the model")
    check(took < 2.0, "the save waited %.1f s for a link that closed" % took)
    print("saves without TT Lab: ok")


def check_sync_settings(fake, paths, directory):
    """Saving syncs the models with TT Lab as the add-on's preferences say: Only Manually writes nothing and asks nothing (TT Lab saves the
    picture painted, which it has already), Prompt to Sync on Save writes nothing and asks once it's saved (Sync writing the model and saving the blend file again, which asks nothing more), and Sync
    with TT Lab writes it without saving the blend file. A copy saved and the add-on's own saves ask nothing, and with no model of a file
    there's nothing to sync."""
    fresh()
    # Answered as written: a conflict an earlier check asked for can still be waiting for a write
    fake.conflict_next_writes(0)
    fake.refuse_next_writes(0)
    check(not bpy.ops.ttt.sync_with_tt_lab.poll(), "Sync with TT Lab is offered without a model of a file")
    root = model(paths["Other"], "Synced")
    blend = os.path.join(directory, "sync_settings.blend")

    def asked():
        return bpy.app.timers.is_registered(save_hook._ask_to_sync)

    def writes(action):
        before = len(fake.of("write_model"))
        action()
        return len(fake.of("write_model")) - before

    try:
        save_hook.SYNC_ON_SAVE = "ASK"
        with save_hook.leaving_models_alone():
            bpy.ops.wm.save_as_mainfile(filepath=blend)
        check(not asked(), "the add-on's own save asked")
        check(tick_until(linked(root)), "the model didn't link")
        # A picture painted is TT Lab's already: saving without syncing has TT Lab save it, closing Blender then doesn't take it back
        picture_uri = "res://%s/Texture/Fur" % PACKAGE
        picture = show_with_picture(fur_of(root), picture_uri)
        picture.pixels[0] = 0.25
        check(tick_until(lambda: picture.get(material_settings.PNG_CONFIRMED_PROPERTY) == link_sync._png_hash(picture)), "the picture painted didn't go to TT Lab")
        saves = len(fake.of("save"))
        save_hook.SYNC_ON_SAVE = "MANUAL"
        check(writes(bpy.ops.wm.save_mainfile) == 0 and not asked(), "saving with Only Manually wrote the model or asked")
        check(any(picture_uri in header.get("uris", []) for header, _ in fake.of("save")[saves:]), "saving without syncing didn't have TT Lab save the picture painted")
        save_hook.SYNC_ON_SAVE = "ASK"
        check(writes(bpy.ops.wm.save_mainfile) == 0, "saving with Prompt to Sync on Save wrote the model before asking")
        check(asked(), "saving with Prompt to Sync on Save didn't ask")
        bpy.app.timers.unregister(save_hook._ask_to_sync)
        saved = os.stat(blend).st_mtime_ns
        time.sleep(0.05)
        check(writes(bpy.ops.ttt.sync_after_save) == 1, "Sync didn't write the model")
        check(os.stat(blend).st_mtime_ns != saved, "the blend file wasn't saved again after syncing")
        check(not asked(), "saving the blend file again asked again")
        # The blend file saved again keeps the revision TT Lab gave the model written (read from a copy: Blender loads nothing of the open file)
        written = os.path.join(directory, "sync_settings_written.blend")
        shutil.copy(blend, written)
        with bpy.data.libraries.load(written) as (_, loaded):
            loaded.objects = [root.name]

        kept = loaded.objects[0]
        try:
            check(root.get(link_sync.REVISION_PROPERTY) and kept.get(link_sync.REVISION_PROPERTY) == root.get(link_sync.REVISION_PROPERTY),
                  "the blend file keeps the revision %r, TT Lab gave %r" % (kept.get(link_sync.REVISION_PROPERTY), root.get(link_sync.REVISION_PROPERTY)))
        finally:
            bpy.data.objects.remove(kept)

        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "sync_settings_copy.blend"), copy=True)
        check(not asked(), "saving a copy asked")
        saved = os.stat(blend).st_mtime_ns
        check(writes(bpy.ops.ttt.sync_with_tt_lab) == 1, "Sync with TT Lab didn't write the model")
        check(os.stat(blend).st_mtime_ns == saved, "Sync with TT Lab saved the blend file")
    finally:
        save_hook.SYNC_ON_SAVE = "ALWAYS"
        if asked():
            bpy.app.timers.unregister(save_hook._ask_to_sync)

    print("sync settings: ok")


def check_reimported_models_link(fake, paths):
    """A linked model deleted and its file imported again under its name before the link looks again is another model: it links its own."""
    fresh()
    root = model(paths["Other"], "Again")
    check(tick_until(linked(root)), "the model didn't link")
    links = len(fake.of("link_model"))
    tlm_blender.remove_model(root)
    again = model(paths["Other"], "Again")
    check(again.name == "Again", "imported as %s" % again.name)
    check(tick_until(linked(again)) and len(fake.of("link_model")) > links, "the model imported again didn't link")
    print("reimported models link: ok")


def check_take_gives_tt_labs_materials(fake, paths, directory):
    """Taking TT Lab's model takes what TT Lab has of its materials too (its unsaved changes), nothing of the files' sent back."""
    fresh()
    root = model(paths["Crash"], "Custom")
    with save_hook.leaving_models_alone():
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory, "take_materials.blend"))
    check(tick_until(linked(root)), "the model didn't link")
    fur_uri = fur_of(root)[tlm_blender.URI_PROPERTY]
    theirs = gs_material.new_material("CRASH_FUR", skin=True, textured=False)
    theirs["DmaChainIndex"] = 6
    fake.answer_link(paths["Crash"], [{"uri": fur_uri, "name": "Crash Fur", "data": theirs}], [])
    fake.conflict_next_writes(1)
    bpy.ops.wm.save_mainfile()
    if bpy.app.timers.is_registered(save_hook._ask):
        bpy.app.timers.unregister(save_hook._ask)

    updates = len(fake.of("update_material"))
    new_root = link_sync.take_tt_labs(bpy.context, root, paths["Crash"])
    check(new_root is not None, "TT Lab's model wasn't taken")
    check(tick_until(lambda: material_settings.settings_of(fur_of(new_root)).render_bucket == "BUCKET_6"), "the material isn't TT Lab's")
    link_sync._undone()
    tick_until(lambda: False, 0.6)
    check(not any(header.get("uri") == fur_uri for header, _ in fake.of("update_material")[updates:]), "the files' material went back to TT Lab")
    fake.answer_link(paths["Crash"], [], [])
    print("take gives TT Lab's materials: ok")


def main():
    twin_tech_tools.register()
    # Saving writes the models, as Always on Save does: these checks are of what's written
    save_hook.SYNC_ON_SAVE = "ALWAYS"
    directory = tempfile.mkdtemp(prefix="ttt_use_")
    project_root = os.path.join(directory, "Test")
    paths = make_project(project_root)
    fake = None
    try:
        check_other_systems_paths(paths)
        blend = check_offline_saves(paths, directory)
        fake = FakeTTLab(STATE, project_root, UUID, PACKAGE, tlm_blender.ADDON_VERSION)
        check_offline_then_linked(fake, paths, blend)
        check_requests_hold_models_back(fake, paths)
        check_stale_answers(fake, paths, directory)
        check_refused_then_saved(fake, paths, directory)
        check_unreadable_pictures(fake, paths)
        check_pushes(fake, paths, directory)
        check_undo_gives_back(fake, paths)
        check_what_the_write_names(fake, paths)
        check_other_projects_models(fake, paths, directory)
        check_added_to_the_project(fake, paths, directory)
        check_conflicts_during_saves(fake, paths, directory)
        check_closed_files_messages(fake, paths)
        check_stale_replacements(fake, paths)
        check_replacements_not_changed(fake, paths)
        check_replacements_in_flight(fake, paths)
        check_deleted_models_released(fake, paths, directory)
        check_deleted_pictures(fake, paths)
        check_several_candidates(fake, paths)
        check_pending_replacements_kept(fake, paths, directory)
        check_split_copies_by_project(directory)
        check_walks_alike(paths)
        check_confirmed_pictures_are_the_projects(directory)
        check_lookups_prefer_the_project(fake, paths, directory)
        check_revisions_of_files(fake, paths)
        check_model_changes_told(fake, paths)
        check_edit_selects_the_links_model(fake, paths, directory)
        check_other_systems_models_used(fake, paths)
        check_overwrite_forces_only_whats_asked(fake, paths)
        check_refused_links_of_replacements(fake, paths, directory)
        check_undoing_take(fake, paths, directory)
        check_undoing_use_for(fake, paths)
        check_late_takes(fake, paths)
        check_retargeted_models_keep_their_link(fake, paths, directory)
        check_released_files_kept(fake, paths, directory)
        check_take_gives_tt_labs_materials(fake, paths, directory)
        check_reimported_models_link(fake, paths)
        check_saves_without_tt_lab(fake, paths)
        check_sync_settings(fake, paths, directory)
    finally:
        link_sync.drop_all()
        if fake is not None:
            fake.close()

        shutil.rmtree(directory, ignore_errors=True)
        shutil.rmtree(STATE, ignore_errors=True)

    print("USE BLEND OK")


main()
