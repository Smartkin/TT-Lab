"""Checks the Twin Tech materials in Blender against a project made up in a temporary folder:

    blender --background --factory-startup --python tests/blender_materials.py [-- fixtures/ogi_materials_from_blender.tlm]

Importing the fixture model gives its material the project's settings and draws them, exporting it unchanged writes no settings,
what's changed in Blender goes over what TT Lab changed meanwhile, the shader animation's keys come back as the game's frames,
materials made in Blender go into the file with their settings and pictures, and a render shows the colors the game would.
Given a path, the model with its material's animation keyed, a picture painted and a new name is written there, TT Lab's
TlmFixtureTests read it as ogi_materials_from_blender.tlm. Prints MATERIALS OK."""
import copy
import json
import os
import shutil
import sys
import tempfile

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import gs_material, material_settings, material_ui, png, tlm, tlm_blender  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")
PACKAGE = "Global PS2_Test"
FUR = "res://%s/Material/Crash Fur" % PACKAGE
FUR_TEXTURE = "res://%s/Texture/Fur" % PACKAGE
GLOSS_TEXTURE = "res://%s/Texture/Gloss" % PACKAGE


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def _picture(path, color, size=8):
    image = bpy.data.images.new("picture", size, size, alpha=True)
    image.pixels = [channel / 255.0 for channel in color] * (size * size)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def _asset(folder, name, kind, extension, payload):
    os.makedirs(folder, exist_ok=True)
    metadata = {"URI": {"_uri": "res://%s/%s/%s" % (PACKAGE, os.path.basename(folder), name)}, "Type": "TT_Lab.Assets.Graphics.%s, TT Lab" % kind,
                "Alias": name, "Package": {"_uri": "res://" + PACKAGE}}
    with open(os.path.join(folder, name + ".json"), "w", encoding="utf-8") as file:
        json.dump(metadata, file)

    path = os.path.join(folder, name + extension)
    if extension == ".data":
        with open(path, "w", encoding="utf-8") as file:
            json.dump(payload, file, indent=2)
    else:
        _picture(path, payload)

    return path


def fur_material(animated=False):
    lit = gs_material.new_shader(2)
    lit.update(TxtMapping=1, TextureId={"_uri": FUR_TEXTURE}, LodParamK=65467, UnusedValue=4, LeftoverVector=[0x7FC00001, 1, 2, 3])
    gloss = gs_material.new_shader(22)
    gloss.update(TxtMapping=1, TextureId={"_uri": GLOSS_TEXTURE}, ABlending=1, AlphaRegSettingsIndex=1, ZValueDrawingMask=1, XScrollSettings=2,
                 UvScrollSpeed=[0, 0, gs_material.bits_of_float(0.5), 0])
    if animated:
        gloss["XScrollSettings"] = 1
        gloss["Animation"] = {
            "Header": 10 << 16 | 4, "TotalFrames": 4,
            "AnimationSettings": [{"TranslateX": 0, "TranslateY": 1, "ColorR": 1, "ColorG": 1, "ColorB": 1, "ColorA": 1, "StaticTransformationIndex": 0,
                                   "AnimationTransformationIndex": 0}],
            "StaticTransformations": [{"PureValue": raw} for raw in (0, 4096, 4096, 4096, 4096)],
            "AnimatedTransformations": [{"Count": 1, "Transforms": [{"PureValue": frame * 1024}]} for frame in range(4)],
        }

    return {"ActivatedShaders": 0x40002, "Name": "CRASH_FUR", "DmaChainIndex": 3, "Shaders": [lit, gloss]}


def make_project(root, animated=False):
    os.makedirs(root, exist_ok=True)
    with open(os.path.join(root, "Test.tson"), "w", encoding="utf-8") as file:
        file.write("{}")

    assets = os.path.join(root, "assets", PACKAGE)
    material_path = _asset(os.path.join(assets, "Material"), "Crash Fur", "Material", ".data", fur_material(animated))
    _asset(os.path.join(assets, "Texture"), "Fur", "Texture", ".png", (200, 100, 50, 255))
    _asset(os.path.join(assets, "Texture"), "Gloss", "Texture", ".png", (40, 40, 40, 255))
    model = os.path.join(assets, "OGI", "Crash.tlm")
    os.makedirs(os.path.dirname(model))
    shutil.copy(os.path.join(FIXTURES, "ogi.tlm"), model)
    return model, material_path


def clear():
    for collection in (bpy.data.objects, bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.actions, bpy.data.images, bpy.data.collections):
        for item in list(collection):
            collection.remove(item)


def read_json(path):
    with open(path, encoding="utf-8") as file:
        return json.load(file)


def write_json(path, value):
    with open(path, "w", encoding="utf-8") as file:
        json.dump(value, file, indent=2)


def export(root, path):
    bpy.context.view_layer.update()
    tlm_blender.export_file(root, path)
    return tlm.TlmFile.load(path).materials


def check_import_and_merge(directory):
    model, material_path = make_project(directory)
    root = tlm_blender.import_file(bpy.context, model)
    check(bpy.context.scene.view_settings.view_transform == "Standard", "the view isn't Standard after importing")
    material = bpy.data.materials["Crash Fur"]
    settings = material_settings.settings_of(material)
    check(settings.managed, "the project's material has no settings")
    check((settings.game_name, settings.render_bucket, len(settings.shaders)) == ("CRASH_FUR", "BUCKET_3", 2), "the material's values aren't the project's")
    lit, gloss = settings.shaders
    check((lit.shader_type, lit.texture_mapping, lit.lod_k, lit.texture) == ("StandardLit", True, -4.3125, FUR_TEXTURE), "the first shader isn't the project's")
    check((gloss.shader_type, gloss.blending, gloss.blend_preset, gloss.depth_write, gloss.scroll_u) == ("UnlitEnvironmentMap", True, "Add", "NOT_UPDATE", "Linear"),
          "the second shader isn't the project's")
    check(lit.image is not None and lit.image.get(material_settings.TEXTURE_URI_PROPERTY) == FUR_TEXTURE, "the fur texture's picture wasn't loaded")
    check(material.surface_render_method == "BLENDED", "a blended material isn't drawn blended")
    kinds = {node.bl_idname for node in material.node_tree.nodes}
    for kind in ("ShaderNodeEmission", "ShaderNodeBsdfTransparent", "ShaderNodeTexImage", "ShaderNodeVectorTransform", "ShaderNodeValue"):
        check(kind in kinds, "the material's nodes have no %s" % kind)

    check(any(driver.driver.expression == "frame * fps_base / fps" for driver in material.node_tree.animation_data.drivers), "the scroll doesn't follow the time")

    # Nothing changed, nothing to take into the project
    path = os.path.join(directory, "export.tlm")
    entry = export(root, path)[0]
    check(entry == {"name": "Crash Fur", "uri": FUR}, "an unchanged material went into the file with %s" % sorted(entry))

    # Changed in Blender while TT Lab changed others: Blender's changes go over the project's
    theirs = read_json(material_path)
    theirs["Shaders"][1]["FixedAlphaValue"] = 77
    write_json(material_path, theirs)
    lit.blending = True
    gloss.alpha_reference = 50
    settings.game_name = "FUR"
    data = export(root, path)[0]["data"]
    check(data["Name"] == "FUR" and data["Shaders"][0]["ABlending"] == 1 and data["Shaders"][1]["AlphaValueToBeComparedTo"] == 50,
          "Blender's changes aren't in the file")
    check(data["Shaders"][1]["FixedAlphaValue"] == 77 and data["Shaders"][0]["LeftoverVector"] == [0x7FC00001, 1, 2, 3], "the project's values didn't stay")
    check(material_ui.is_changed_in_blender(material), "the material isn't changed in Blender")

    # TT Lab took them: the next export sends nothing and the project's is the base
    write_json(material_path, data)
    entry = export(root, path)[0]
    check("data" not in entry, "a material the project has went into the file again")
    check(not material_ui.is_changed_in_blender(material), "the material is still changed after TT Lab took it")
    theirs = read_json(material_path)
    theirs["Shaders"][0]["ABlending"] = 0
    write_json(material_path, theirs)
    check("data" not in export(root, path)[0], "Blender took back a change made in TT Lab")

    # A picture made in Blender goes into the file with the material
    picture = bpy.data.images.new("Painted", 4, 4)
    picture.pixels = [0.5, 0.25, 1.0, 1.0] * 16
    lit.image = picture
    check(lit.texture == "" and lit.texture_mapping, "picking a picture didn't make it the texture")
    entry = export(root, path)[0]
    check(entry["data"]["Shaders"][0]["Image"] == 0 and entry["data"]["Shaders"][0]["TextureId"] == {"_uri": "res://EMPTY"}, "the picture isn't the shader's")
    check(entry["images"][0]["name"] == "Painted" and entry["images"][0]["blender_id"], "the picture isn't in the file")
    material_settings.texture_image(material_ui.projects.open_project(model), FUR_TEXTURE)
    lit.image = material_settings.texture_image(material_ui.projects.open_project(model), FUR_TEXTURE)
    check(lit.texture == FUR_TEXTURE, "picking the project's picture again didn't link its texture")
    return root


def check_shader_list(root, directory):
    material = bpy.data.materials["Crash Fur"]
    settings = material_settings.settings_of(material)
    material_settings.add_shader(material)
    check(len(settings.shaders) == 3 and settings.shaders[2].source == -1, "the added shader isn't new")
    material_settings.add_shader(material)
    material_settings.add_shader(material)
    check(len(settings.shaders) == gs_material.MAX_SHADERS, "a material got more than %d shaders" % gs_material.MAX_SHADERS)
    material_settings.remove_shader(material, 3)
    material_settings.move_shader(material, 2, -1)
    check([item.shader_type for item in settings.shaders] == ["StandardLit", "StandardLit", "UnlitEnvironmentMap"], "the shaders didn't move")
    data = export(root, os.path.join(directory, "export.tlm"))[0]["data"]
    check([item["ShaderType"] for item in data["Shaders"]] == [2, 2, 22], "the file doesn't have Blender's shaders")
    check(data["Shaders"][2]["XScrollSettings"] == 2, "the moved shader lost its values")


def check_animation(directory, fixture=None):
    clear()
    model, material_path = make_project(directory, animated=True)
    root = tlm_blender.import_file(bpy.context, model)
    material = bpy.data.materials["Crash Fur"]
    gloss = material_settings.settings_of(material).shaders[1]
    check(gloss.has_animation and (gloss.anim_fps, gloss.anim_frames) == (10, 4), "the animation's timing isn't the project's")
    curve = material_settings.track_curve(material, 1, 0)
    check(curve is not None and material_settings.track_curve(material, 1, 2) is None, "only the U track is keyframed")
    step = material_settings.scene_fps() / 10
    check([tuple(point.co) for point in curve.keyframe_points] == [(frame * step, frame * 0.25 if frame < 4 else 0.0) for frame in range(5)],
          "the keys aren't on the game's frames: %s" % [tuple(point.co) for point in curve.keyframe_points])
    check(curve.modifiers[0].type == "CYCLES", "the animation doesn't loop")
    check(any(driver.driver.variables[0].targets[0].data_path == "ttt_material.shaders[1].anim_u" for driver in material.node_tree.animation_data.drivers),
          "the preview doesn't follow the U track")
    check(not any(action.name.startswith("Crash Fur") for action in tlm_blender.animations_of(root)), "the shader animation is taken for the model's")
    path = os.path.join(directory, "export.tlm")
    check("data" not in export(root, path)[0], "an unchanged animation went into the file")

    curve.keyframe_points[2].co.y = 0.75
    curve.update()
    animation = export(root, path)[0]["data"]["Shaders"][1]["Animation"]
    check([frame["Transforms"][0]["PureValue"] for frame in animation["AnimatedTransformations"]] == [0, 1024, 3072, 3072],
          "the keyed values aren't the file's: %s" % animation["AnimatedTransformations"])
    check(animation["Header"] == 10 << 16 | 4, "the animation's timing changed")
    # What TT Lab's fixture test reads: the keyed animation, a picture painted in Blender on the first shader and a new name
    picture = bpy.data.images.new("Painted", 4, 4)
    picture.pixels = [1.0, 0.0, 0.0, 1.0] * 16
    material_settings.settings_of(material).shaders[0].image = picture
    material_settings.settings_of(material).game_name = "FUR"
    file_path = fixture or path
    entry = export(root, file_path)[0]
    check(entry["data"]["Shaders"][0]["Image"] == 0 and entry["images"][0]["name"] == "Painted", "the painted picture isn't in the file")
    # Painted and never saved, its pixels as Blender has them
    width, height, pixels = png.decode(tlm.TlmFile.load(file_path).read_view(entry["images"][0]["png"], "u8").tobytes())
    check((width, height, pixels[:4]) == (4, 4, bytes((255, 0, 0, 255))), "the painted picture isn't red: %r" % pixels[:4])

    # A track keyframed in Blender is animated, the shader's keys move with it
    gloss.keyframe_insert("anim_r", frame=0)
    material_settings.move_shader(material, 1, -1)
    check(material_settings.track_curve(material, 0, 2) is not None and material_settings.track_curve(material, 1, 0) is None, "the keys didn't move with the shader")
    material_settings.remove_shader(material, 0)
    check(material_settings.track_curve(material, 0, 0) is None, "the keys of a shader taken out stayed")


class _Layout:
    """Stands in for a panel's layout: what it shows has to be a property, operator or list the add-on has."""

    def __init__(self):
        self.use_property_split = False
        self.active = True
        self.alert = False
        self.enabled = True

    def row(self, **settings):
        return _Layout()

    column = box = row

    def separator(self, **settings):
        pass

    def label(self, **settings):
        pass

    def prop(self, data, name, **settings):
        check(name in data.bl_rna.properties, "a panel shows %s, which %s doesn't have" % (name, data))

    def operator(self, idname, **settings):
        module, name = idname.split(".")
        check(hasattr(getattr(bpy.ops, module), name), "a panel shows the operator %s, which doesn't exist" % idname)
        return type("Properties", (), {})()

    def template_list(self, list_type, list_id, data, name, active_data, active_name, **settings):
        check(hasattr(bpy.types, list_type) and name in data.bl_rna.properties and active_name in active_data.bl_rna.properties, "a list shows what isn't there")

    def template_ID(self, data, name, **settings):
        self.prop(data, name)


def check_panels(material, blender_object):
    """Draws every Twin Tech material panel the way Blender would, which background mode never does."""
    context = type("Context", (), {"material": material, "object": blender_object, "scene": bpy.context.scene})()
    panels = [material_ui.TTT_PT_Material, material_ui.TTT_PT_MaterialShader, material_ui.TTT_PT_MaterialBlending, material_ui.TTT_PT_MaterialAlphaTest,
              material_ui.TTT_PT_MaterialDepth, material_ui.TTT_PT_MaterialScroll, material_ui.TTT_PT_MaterialAnimation]
    for panel in panels:
        check(panel.poll(context), "%s isn't shown for a Twin Tech material" % panel.__name__)
        drawer = type("Drawer", (), {"layout": _Layout(), "shader": staticmethod(getattr(panel, "shader", None) or (lambda context: None)),
                                     "field": staticmethod(getattr(panel, "field", None) or (lambda *arguments, **settings: None))})()
        panel.draw(drawer, context)
        if hasattr(panel, "draw_header"):
            panel.draw_header(drawer, context)


def check_made_in_blender(directory):
    clear()
    mesh = bpy.data.meshes.new("Plane")
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0)], [], [(0, 1, 2)])
    plane = bpy.data.objects.new("Plane", mesh)
    bpy.context.scene.collection.objects.link(plane)
    material = bpy.data.materials.new("Painted")
    mesh.materials.append(material)
    bpy.context.view_layer.objects.active = plane
    with bpy.context.temp_override(material=material, object=plane):
        bpy.ops.ttt.material_use_settings()

    settings = material_settings.settings_of(material)
    check(settings.managed and settings.shaders[0].shader_type == "StandardUnlit" and settings.shaders[0].receives_shadows, "a new material isn't TT Lab's")
    with bpy.context.temp_override(material=material, object=plane):
        bpy.ops.ttt.add_vertex_colors()

    check(mesh.color_attributes.get("Color") is not None, "the mesh didn't get vertex colors")
    check_panels(material, plane)
    settings.shaders[0].has_animation = True
    check(settings.shaders[0].anim_fps == 30 and settings.shaders[0].anim_b == 1.0, "a new animation isn't TT Lab's")
    check_panels(material, plane)
    picture = bpy.data.images.new("Wood", 4, 4)
    settings.shaders[0].image = picture
    file = tlm.TlmFile("RigidModel", "Plane")
    materials = tlm_blender._FileMaterials(file)
    check(materials.index_of(material) == 0, "the material isn't the file's first")
    entry = file.materials[0]
    check(entry["blender_id"] == material[tlm_blender.BLENDER_ID_PROPERTY] and entry["data"]["Shaders"][0]["Image"] == 0 and len(entry["images"]) == 1,
          "a material made in Blender isn't in the file with its settings and picture: %s" % sorted(entry))
    # Read back, it's the same material
    path = os.path.join(directory, "made.tlm")
    file.root = tlm.node("rigid_model", "Plane")
    file.save(path)
    del material[tlm_blender.BLENDER_ID_PROPERTY]
    again = tlm_blender._embedded_material(tlm.TlmFile.load(path), tlm.TlmFile.load(path).materials[0])
    check(material_settings.settings_of(again).managed and material_settings.settings_of(again).shaders[0].image is not None, "the file's material lost its settings")


def check_render(directory):
    """The game's colors on a plane facing up under an orthographic camera, over black: the fur texture (200, 100, 50) times grey vertex
    colors of 0x80 unlit, and lit by the default lights from above; mixed by a vertex alpha of 0x80; and a gloss texture (40, 40, 40)
    added over the unlit pass, which the GS adds in the display's values."""
    clear()
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    tlm_blender.use_standard_view(bpy.context)
    scene.render.resolution_x = scene.render.resolution_y = 32
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.0, 0.0, 0.0)
    # New worlds draw a grey background with their nodes
    if scene.world.node_tree is not None:
        for node in scene.world.node_tree.nodes:
            if node.type == "BACKGROUND":
                node.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.0
    camera.location = (0.0, 0.0, 5.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    pictures = {}
    for name, color in (("fur", (200, 100, 50, 255)), ("gloss", (40, 40, 40, 255))):
        _picture(os.path.join(directory, name + ".png"), color)
        pictures[name] = bpy.data.images.load(os.path.join(directory, name + ".png"))

    lit = min(1.0, 128 / 255.0 * (0.75 + 0.5))
    cases = {
        "unlit": ([gs_material.new_shader(1)], 1.0, [200 * 128 // 255, 100 * 128 // 255, 50 * 128 // 255]),
        "lit": ([gs_material.new_shader(2)], 1.0, [round(value * lit) for value in (200, 100, 50)]),
        "mixed": ([dict(gs_material.new_shader(1), ABlending=1)], 128 / 255.0, [round(value * 128 / 255.0 * 128 / 255.0) for value in (200, 100, 50)]),
        "glossed": ([gs_material.new_shader(1), dict(gs_material.new_shader(1), ABlending=1, AlphaRegSettingsIndex=1)], 1.0,
                    [round(value * 128 / 255.0) + round(40 * 128 / 255.0) for value in (200, 100, 50)]),
    }
    results = {}
    for name, (shaders, alpha, expected) in cases.items():
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(-2, -2, 0), (2, -2, 0), (2, 2, 0), (-2, 2, 0)], [], [(0, 1, 2, 3)])
        mesh.uv_layers.new(name="UVMap")
        material_ui.add_default_colors(mesh)
        mesh.color_attributes["Color"].data.foreach_set("color_srgb", [128 / 255.0, 128 / 255.0, 128 / 255.0, alpha] * 4)
        plane = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(plane)
        material = bpy.data.materials.new(name)
        mesh.materials.append(material)
        for shader in shaders:
            shader["TxtMapping"] = 1

        material_settings.load(material, {"Name": name, "DmaChainIndex": 2, "Shaders": shaders})
        for item, picture in zip(material_settings.settings_of(material).shaders, ("fur", "gloss")):
            item.image = pictures[picture]

        for other in scene.objects:
            other.hide_render = other.type == "MESH" and other != plane

        scene.render.filepath = os.path.join(directory, name + ".png")
        bpy.ops.render.render(write_still=True)
        rendered = bpy.data.images.load(scene.render.filepath)
        middle = (16 * 32 + 16) * 4
        results[name] = [round(value * 255) for value in rendered.pixels[middle:middle + 3]]
        check(all(abs(a - b) <= 2 for a, b in zip(results[name], expected)), "%s renders %s, the game shows %s" % (name, results[name], expected))

    check_environment_map(directory, scene, results)
    print("rendered", results)


def check_environment_map(directory, scene, results):
    """A sphere drawn with an environment map of a picture red on its left half and blue on its right: the game reads the picture by the
    half vector on the screen's axes, the right of the sphere as the console shows it blue. Blender shows the console's frame mirrored,
    so its right is red."""
    split = bpy.data.images.new("split", 8, 8, alpha=True)
    split.pixels = [channel for row in range(8) for column in range(8) for channel in ((1.0, 0.0, 0.0, 1.0) if column < 4 else (0.0, 0.0, 1.0, 1.0))]
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.4, segments=48, ring_count=24)
    sphere = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    material_ui.add_default_colors(sphere.data)
    sphere.data.color_attributes["Color"].data.foreach_set("color_srgb", [128 / 255.0, 128 / 255.0, 128 / 255.0, 1.0] * len(sphere.data.vertices))
    material = bpy.data.materials.new("environment")
    sphere.data.materials.append(material)
    material_settings.load(material, {"Name": "ENV", "DmaChainIndex": 2, "Shaders": [dict(gs_material.new_shader(22), TxtMapping=1)]})
    material_settings.settings_of(material).shaders[0].image = split
    for other in scene.objects:
        other.hide_render = other.type == "MESH" and other != sphere

    scene.render.filepath = os.path.join(directory, "environment.png")
    bpy.ops.render.render(write_still=True)
    pixels = bpy.data.images.load(scene.render.filepath).pixels[:]
    left, right = [[round(value * 255) for value in pixels[(16 * 32 + column) * 4:(16 * 32 + column) * 4 + 3]] for column in (8, 24)]
    results["environment"] = (left, right)
    check(right[0] > 100 and right[2] < 20 and left[2] > 100 and left[0] < 20, "the environment map reads its picture the wrong way round: %s" % (results["environment"],))


def main():
    twin_tech_tools.register()
    directory = tempfile.mkdtemp(prefix="ttt_materials_")
    try:
        clear()
        root = check_import_and_merge(os.path.join(directory, "merge"))
        check_shader_list(root, os.path.join(directory, "merge"))
        arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        check_animation(os.path.join(directory, "animation"), os.path.abspath(arguments[0]) if arguments else None)
        check_made_in_blender(directory)
        check_render(directory)
    finally:
        shutil.rmtree(directory, ignore_errors=True)

    print("MATERIALS OK")


main()
