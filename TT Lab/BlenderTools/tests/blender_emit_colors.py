"""Checks the emit colors of rigid meshes drawn with the shaders lighting them, in Blender:

    blender --background --factory-startup --python tests/blender_emit_colors.py

The game's Standard Lit, Lit Environment Map and Lit Reflection Surface shaders light a rigid part by its emit colors, and the PS2
version lights a part without them with what the model drawn before it left there. The Twin Tech Material panel warns about a rigid
mesh drawn with one that has none and its Add Emit Colors gives it a copy of its vertex colors, a part drawn with one writes the
emit colors its mesh has, and exporting refuses a PS2 model with such a mesh while an Xbox model's is fine. Prints EMIT COLORS OK."""
import array
import json
import os
import shutil
import sys
import tempfile

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import gs_material, material_settings, material_ui, project as projects, tlm, tlm_blender, tlm_templates  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures")
SHINY = "res://PS2_Test/Material/Shiny"
MATTE = "res://PS2_Test/Material/Matte"
GREY = [0x7F / 255.0, 0x7F / 255.0, 0x7F / 255.0, 1.0]


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


def _write_json(path, value):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as file:
        json.dump(value, file, indent=2)


def _material_data(shader_type):
    shader = gs_material.new_shader(shader_type)
    return {"ActivatedShaders": 0, "Name": "MATERIAL", "DmaChainIndex": 2, "Shaders": [shader]}


def _material_asset(root, package, name, shader_type):
    folder = os.path.join(root, "assets", package, "Material")
    _write_json(os.path.join(folder, name + ".json"), {"URI": {"_uri": "res://%s/Material/%s" % (package, name)}, "Alias": name,
                                                      "Type": "TT_Lab.Assets.Graphics.Material, TT Lab", "Package": {"_uri": "res://" + package}})
    path = os.path.join(folder, name + ".data")
    _write_json(path, _material_data(shader_type))
    return path


def make_project(root):
    """A project of the PS2 version's packages, the Xbox version's and a mod made on the Xbox one, with a lit and an unlit material and
    the fixture model, whose material is lit."""
    _write_json(os.path.join(root, "Test.tson"), {"Name": "Test"})
    for package, dependencies in (("Global PS2_Test", []), ("PS2_Test", []), ("XBOX_Test", []), ("Xbox mod", ["res://XBOX_Test"])):
        _write_json(os.path.join(root, "assets", package, package + ".json"), {"Dependencies": [{"_uri": uri} for uri in dependencies],
                                                                               "URI": {"_uri": "res://" + package}})

    _material_asset(root, "PS2_Test", "Shiny", 2)
    _material_asset(root, "PS2_Test", "Matte", 1)
    _material_asset(root, "Global PS2_Test", "Crash Fur", 2)
    model = os.path.join(root, "assets", "Global PS2_Test", "OGI", "Crash.tlm")
    os.makedirs(os.path.dirname(model), exist_ok=True)
    shutil.copy(os.path.join(FIXTURES, "ogi.tlm"), model)
    return model


def clear():
    for collection in (bpy.data.objects, bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.actions, bpy.data.images, bpy.data.collections):
        for item in list(collection):
            collection.remove(item)


_ICONS = set(bpy.types.UILayout.bl_rna.functions["operator"].parameters["icon"].enum_items.keys())


class _Layout:
    """Stands in for a panel's layout, keeping the labels and operators it shows."""

    def __init__(self, shown):
        self.shown = shown
        self.use_property_split = False
        self.active = True
        self.alert = False
        self.enabled = True

    def row(self, **settings):
        return _Layout(self.shown)

    column = box = row

    def separator(self, **settings):
        pass

    def label(self, text="", icon="NONE", **settings):
        check(icon in _ICONS, "a panel shows the icon %s, which Blender doesn't have" % icon)
        self.shown.append(text)

    def prop(self, data, name, **settings):
        pass

    def operator(self, idname, icon="NONE", **settings):
        check(icon in _ICONS, "a panel shows the icon %s, which Blender doesn't have" % icon)
        self.shown.append(idname)
        return type("Properties", (), {})()

    def template_list(self, *arguments, **settings):
        pass

    def template_ID(self, *arguments, **settings):
        pass


def panel_shows(material, blender_object):
    shown = []
    context = type("Context", (), {"material": material, "object": blender_object, "scene": bpy.context.scene})()
    material_ui.TTT_PT_Material.draw(type("Drawer", (), {"layout": _Layout(shown)})(), context)
    return shown


def _colors(mesh, name):
    attribute = mesh.color_attributes.get(name)
    values = array.array("f", [0.0] * len(attribute.data) * 4)
    attribute.data.foreach_get("color_srgb", values)
    return [round(value * 255.0) for value in values]


def _remove(mesh, name):
    mesh.color_attributes.remove(mesh.color_attributes.get(name))


def check_made_in_blender(directory):
    clear()
    root = tlm_templates.new_ogi(bpy.context, "Box")
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    skin = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "skin")
    ps2_path = os.path.join(directory, "assets", "PS2_Test", "OGI", "Box.tlm")
    xbox_path = os.path.join(directory, "assets", "Xbox mod", "OGI", "Box.tlm")
    os.makedirs(os.path.dirname(ps2_path), exist_ok=True)
    material = bpy.data.materials.new("Shiny Box")
    body.data.materials.append(material)
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.material_use_settings()

    settings = material_settings.settings_of(material)
    check(settings.shaders[0].shader_type == "StandardUnlit", "a rigid part's new material isn't unlit")
    skin.data.materials.append(material)
    check(tlm_blender.export_problem(root, ps2_path) is None, "an unlit mesh without emit colors is refused")
    check(not any("emit colors" in text for text in panel_shows(material, body)), "the panel warns about the emit colors of an unlit material")

    settings.shaders[0].shader_type = "StandardLit"
    problem = tlm_blender.export_problem(root, ps2_path) or ""
    check("Body has no emit colors, which its material Shiny Box's Standard Lit shader lights" in problem, "a lit mesh without emit colors isn't refused: %s" % problem)
    check("has no emit colors" in (tlm_blender.export_problem(root) or ""), "a model outside of a project isn't the PS2 version's")
    check(tlm_blender.export_problem(root, xbox_path) is None, "an Xbox model's mesh is refused")
    check(not tlm_blender.is_rigid_part(skin) and skin not in material_ui._lacking_emit_colors(material), "a skin is taken for a rigid part")
    shown = panel_shows(material, body)
    check("A mesh drawn with it has no emit colors, which Standard Lit lights" in shown and "ttt.add_emit_colors" in shown,
          "the panel doesn't offer to add the emit colors: %s" % shown)
    check("ttt.add_vertex_colors" in shown, "the panel doesn't offer the vertex colors any more")

    # Without vertex colors the emit colors are the grey TT Lab writes for them
    with bpy.context.temp_override(material=material, object=body):
        check(bpy.ops.ttt.add_emit_colors.poll(), "Add Emit Colors is grayed out")
        bpy.ops.ttt.add_emit_colors()
        check(not bpy.ops.ttt.add_emit_colors.poll(), "Add Emit Colors stays pressable")

    emit = body.data.color_attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE)
    check(emit is not None and emit.domain == "POINT" and _colors(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE) == [0x7F, 0x7F, 0x7F, 0xFF] * len(body.data.vertices),
          "a mesh without vertex colors didn't get grey emit colors")
    check(tlm_blender.vertex_colors_of(body.data) is None, "the emit colors are taken for the vertex colors")
    check(tlm_blender.export_problem(root, ps2_path) is None, "a lit mesh with emit colors is refused")
    check(not any("emit colors" in text for text in panel_shows(material, body)), "the panel still warns about the emit colors")

    # A copy of the vertex colors, which stay the ones shown and rendered
    _remove(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE)
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.add_vertex_colors()

    painted = [((index * 37) % 256) / 255.0 for index in range(len(body.data.vertices) * 4)]
    body.data.color_attributes.get(tlm_blender.COLOR_ATTRIBUTE).data.foreach_set("color_srgb", painted)
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.add_emit_colors()

    attributes = body.data.color_attributes
    check(_colors(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE) == _colors(body.data, tlm_blender.COLOR_ATTRIBUTE), "the emit colors aren't the vertex colors")
    check(attributes.active_color_name == tlm_blender.COLOR_ATTRIBUTE and attributes[attributes.render_color_index].name == tlm_blender.COLOR_ATTRIBUTE,
          "the emit colors took the vertex colors' place")
    # Removing the active attribute leaves Blender's active and rendered ones on nothing, the vertex colors get them back
    attributes.active_color = attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE)
    attributes.render_color_index = attributes.find(tlm_blender.EMIT_COLOR_ATTRIBUTE)
    _remove(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE)
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.add_emit_colors()

    attributes = body.data.color_attributes
    check(attributes.active_color_name == tlm_blender.COLOR_ATTRIBUTE and attributes[attributes.render_color_index].name == tlm_blender.COLOR_ATTRIBUTE,
          "the emit colors added again are the ones painted or rendered")

    # Exported, the part has them; without them the export is refused and nothing is written
    for selected in bpy.context.selected_objects:
        selected.select_set(False)

    bpy.context.view_layer.objects.active = root
    bpy.context.view_layer.update()
    check(bpy.ops.ttt.export_tlm(filepath=ps2_path) == {"FINISHED"}, "the model wasn't exported")
    file = tlm.TlmFile.load(ps2_path)
    part = tlm.children(tlm.find_child(file.root, "rigid_bodies"), "body")[0]["mesh"]["parts"][0]
    check("emit_color" in part and list(file.read_view(part["emit_color"])) == list(file.read_view(part["color"])), "the part wasn't written with its emit colors")
    os.remove(ps2_path)
    _remove(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE)
    error = _error_of(bpy.ops.ttt.export_tlm, filepath=ps2_path)
    check("Can't export: Body has no emit colors" in error and not os.path.exists(ps2_path), "a lit mesh without emit colors got exported: %s" % error)
    os.makedirs(os.path.dirname(xbox_path), exist_ok=True)
    check(bpy.ops.ttt.export_tlm(filepath=xbox_path) == {"FINISHED"}, "an Xbox model without emit colors wasn't exported")

    # Colors of the face corners make emit colors of the face corners
    _remove(body.data, tlm_blender.COLOR_ATTRIBUTE)
    corners = body.data.color_attributes.new(tlm_blender.COLOR_ATTRIBUTE, "BYTE_COLOR", "CORNER")
    corners.data.foreach_set("color_srgb", [((index * 11) % 256) / 255.0 for index in range(len(body.data.loops) * 4)])
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.add_emit_colors()

    emit = body.data.color_attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE)
    check(emit.domain == "CORNER" and _colors(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE) == _colors(body.data, tlm_blender.COLOR_ATTRIBUTE),
          "the emit colors of corner colors aren't theirs")

    # Blender hands out no attribute values of a mesh being edited, the buttons set them in Object Mode
    _remove(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE)
    _remove(body.data, tlm_blender.COLOR_ATTRIBUTE)
    for selected in bpy.context.selected_objects:
        selected.select_set(False)

    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    with bpy.context.temp_override(material=material, object=body):
        bpy.ops.ttt.add_vertex_colors()
        bpy.ops.ttt.add_emit_colors()

    check(body.mode == "EDIT", "the buttons left Edit Mode")
    bpy.ops.object.mode_set(mode="OBJECT")
    check(_colors(body.data, tlm_blender.COLOR_ATTRIBUTE) == [0x7F, 0x7F, 0x7F, 0xFF] * len(body.data.vertices) and
          _colors(body.data, tlm_blender.EMIT_COLOR_ATTRIBUTE) == _colors(body.data, tlm_blender.COLOR_ATTRIBUTE), "the colors added in Edit Mode aren't TT Lab's")


def check_project_materials(directory):
    clear()
    project = projects.open_project(os.path.join(directory, "Test.tson"), refresh=True)
    # Without settings, a material is the project's
    plain = bpy.data.materials.new("Shiny")
    plain[tlm_blender.URI_PROPERTY] = SHINY
    check(tlm_blender.emit_lighting_shader(plain, project) == "Standard Lit", "a project material without settings isn't lit like the project's")
    check(tlm_blender.emit_lighting_shader(plain, None) is None, "a material of no project is taken for lit")
    made = bpy.data.materials.new("Made")
    check(tlm_blender.emit_lighting_shader(made, project) is None, "a material made in Blender without settings is taken for lit")

    # With settings, what was changed in Blender over the project's: what TT Lab changed since stays
    matte = bpy.data.materials.new("Matte")
    matte[tlm_blender.URI_PROPERTY] = MATTE
    material_settings.load(matte, project.materials[MATTE].read(), project)
    check(tlm_blender.emit_lighting_shader(matte, project) is None, "an unlit project material is taken for lit")
    _write_json(project.materials[MATTE].data_path, _material_data(12))
    check(material_settings.settings_of(matte).shaders[0].shader_type == "StandardUnlit", "Blender's settings changed with the project's")
    check(tlm_blender.emit_lighting_shader(matte, project) == "Lit Environment Map", "the project's lit shader isn't the material's")
    shiny = bpy.data.materials.new("Shiny settings")
    shiny[tlm_blender.URI_PROPERTY] = SHINY
    material_settings.load(shiny, project.materials[SHINY].read(), project)
    _write_json(project.materials[SHINY].data_path, _material_data(1))
    check(tlm_blender.emit_lighting_shader(shiny, project) is None, "a shader made unlit in TT Lab is still taken for lit")
    material_settings.settings_of(shiny).shaders[0].shader_type = "LitReflectionSurface"
    check(tlm_blender.emit_lighting_shader(shiny, project) == "Lit Reflection Surface", "a shader made lit in Blender isn't taken for lit")
    _write_json(project.materials[SHINY].data_path, _material_data(2))
    _write_json(project.materials[MATTE].data_path, _material_data(1))


def check_fixture(model):
    """The fixture's body has a part without emit colors drawn with the project's lit fur: imported, it gets its colors as them and
    writes them."""
    clear()
    projects.open_project(model, refresh=True)
    root = tlm_blender.import_file(bpy.context, model)
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    check(body.data.color_attributes.get(tlm_blender.EMIT_COLOR_ATTRIBUTE) is not None, "the body's emit colors weren't imported")
    check(tlm_blender.export_problem(root) is None, "the fixture is refused")
    fur = bpy.data.materials["Crash Fur"]
    check(not any("emit colors" in text for text in panel_shows(fur, body)), "the panel warns about the fixture's emit colors")
    original = tlm.TlmFile.load(model)
    original_parts = tlm.children(tlm.find_child(original.root, "rigid_bodies"), "body")[0]["mesh"]["parts"]
    check("emit_color" not in original_parts[1], "the fixture's second part has emit colors")
    path = os.path.join(os.path.dirname(model), "Crash exported.tlm")
    bpy.context.view_layer.update()
    tlm_blender.export_file(root, path)
    file = tlm.TlmFile.load(path)
    parts = tlm.children(tlm.find_child(file.root, "rigid_bodies"), "body")[0]["mesh"]["parts"]
    check(list(file.read_view(parts[0]["emit_color"])) == list(original.read_view(original_parts[0]["emit_color"])), "the first part's emit colors changed")
    check("emit_color" in parts[1] and list(file.read_view(parts[1]["emit_color"])) == list(file.read_view(parts[1]["color"])),
          "the part drawn with the lit fur wasn't written with its colors as its emit colors")


def check_scenery():
    clear()
    root = tlm_templates.new_scenery(bpy.context)
    descendants = tlm_blender._descendants(root)
    ground = next(child for child in descendants if child.type == "MESH" and tlm_blender.role_of(child) == "scenery_mesh")
    collision = next(child for child in descendants if tlm_blender.role_of(child) == "collision")
    material = bpy.data.materials.new("Lit Ground")
    ground.data.materials.append(material)
    with bpy.context.temp_override(material=material, object=ground):
        bpy.ops.ttt.material_use_settings()

    material_settings.settings_of(material).shaders[0].shader_type = "StandardLit"
    lit = tlm_blender.lit_without_emit_colors(root, "")
    check([name for name, _, _ in lit] == [ground.name], "the scenery's meshes lacking emit colors are %s" % lit)
    check(tlm_blender.is_rigid_part(ground) and not tlm_blender.is_rigid_part(collision), "the scenery's collision is taken for a rigid part")


def main():
    twin_tech_tools.register()
    directory = tempfile.mkdtemp()
    try:
        model = make_project(directory)
        check_made_in_blender(directory)
        check_project_materials(directory)
        check_fixture(model)
        check_scenery()
    finally:
        shutil.rmtree(directory)

    print("EMIT COLORS OK")


main()
