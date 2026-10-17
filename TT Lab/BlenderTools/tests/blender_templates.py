"""Makes the add-on's OGI, scenery, save icon, skydome and model templates and exports them, in Blender:

    blender --background --factory-startup --python tests/blender_templates.py -- fixtures/ogi_template.tlm fixtures/scenery_template.tlm fixtures/save_icon_template.tlm fixtures/skydome_template.tlm fixtures/model_template.tlm

TT Lab's tests (TlmFixtureTests) read what it exported. Prints TEMPLATES OK when the templates have what the add-on's own imports
have and export as models."""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm, tlm_blender, tlm_scenery, twintech_properties  # noqa: E402


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def _kinds(root):
    return sorted(tlm_blender.role_of(child) for child in tlm_blender._descendants(root))


def _ogi(target):
    bpy.ops.ttt.new_ogi()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "Ogi" and root.get(tlm_blender.KIND_PROPERTY) == "ogi", "the OGI template isn't a model root")
    check(root.select_get() and tlm_blender.find_root(root) == root, "the root isn't selected")
    check(twintech_properties.get(root).type == "Ogi", "the root has no OGI type")
    # The box is a rigid body of joint 0 by Blender's rigging: a mesh the armature neither deforms nor carries
    check(_kinds(root) == ["armature", "body", "collision_hulls", "exit_points"], "the template holds %s" % _kinds(root))
    armature = tlm_blender.armature_of(root)
    check([bone.name for bone in armature.data.bones] == ["Joint 0"], "the armature's bones are %s" % [bone.name for bone in armature.data.bones])
    joint = twintech_properties.get(armature.data.bones["Joint 0"])
    check(joint.type == "Joint" and int(joint.joint.index) == 0, "the bone isn't joint 0")
    check(armature.pose.bones["Joint 0"].rotation_mode == "QUATERNION", "the bone doesn't rotate by quaternions")
    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    check(len(body.data.vertices) == 8 and len(body.data.polygons) == 6 and body.data.uv_layers.active is not None, "the body isn't a box with UVs")
    check(twintech_properties.get(body).type in ("", "NONE") and not body.constraints, "the body is typed or constrained: Blender's rigging decides")
    bpy.context.view_layer.update()
    # Blender's Z is the game's up
    heights = [(root.matrix_world.inverted() @ body.matrix_world @ vertex.co).z for vertex in body.data.vertices]
    check(abs(min(heights)) < 1e-5 and abs(max(heights) - 1.0) < 1e-5, "the body doesn't stand on the ground: %s" % heights)
    check(len(bpy.data.actions) == 0, "the template has animations")
    check(root.matrix_world.to_quaternion().angle < 1e-6, "the root is turned, models are in Blender's axes")

    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "Ogi" and file.root.get("kind") == "ogi", "the file isn't an OGI")
    joints = tlm.find_child(file.root, "armature")["joints"]
    check([(joint["index"], joint["parent"]) for joint in joints] == [(0, -1)], "the file's joints are %s" % joints)
    check(tlm.children(file.root, "skin") == [] and tlm.children(file.root, "shape") == [], "the file has a skin or a blend skin")
    bodies = tlm.children(tlm.find_child(file.root, "rigid_bodies"), "body")
    check(len(bodies) == 1 and bodies[0]["joint"] == 0 and "translation" not in bodies[0], "the file's bodies are %s" % [{key: value for key, value in body.items() if key != "mesh"} for body in bodies])
    check(len(bodies[0]["mesh"]["parts"]) == 1 and bodies[0]["mesh"]["parts"][0]["material"] == -1, "the body isn't one part without a material")
    check(file.root["data"]["BoundingBoxMax"][1] == 1.0, "the bounding box isn't around the box: %s" % file.root["data"])
    return warnings


def _skydome(target):
    bpy.ops.ttt.new_skydome()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "Skydome" and root.get(tlm_blender.KIND_PROPERTY) == "skydome", "the skydome template isn't a model root")
    check(_kinds(root) == ["skydome_mesh"], "the template holds %s" % _kinds(root))
    dome = tlm_blender._descendants(root)[0]
    bpy.context.view_layer.update()
    # Its faces face the camera inside it
    dome.data.calc_loop_triangles()
    inward = sum(1 for triangle in dome.data.loop_triangles if triangle.normal.dot(triangle.center) < 0)
    check(inward == len(dome.data.loop_triangles), "%d of the dome's %d triangles face out" % (len(dome.data.loop_triangles) - inward, len(dome.data.loop_triangles)))

    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "Skydome" and file.root.get("kind") == "skydome", "the file isn't a skydome")
    meshes = [node for node in tlm.traverse(file.root) if node.get("kind") == "skydome_mesh"]
    check(len(meshes) == 1 and meshes[0]["data"]["Order"] == 0, "the file's sky meshes are %s" % [node.get("name") for node in meshes])
    shader = file.materials[0]["data"]["Shaders"][0]
    check((shader["ShaderType"], file.materials[0]["data"]["DmaChainIndex"], shader["DepthTest"]) == (10, 0, 1), "the sky's material isn't a sky's: %s" % shader)
    return warnings


def _model(target):
    bpy.ops.ttt.new_model()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "Mesh" and root.get(tlm_blender.KIND_PROPERTY) == "mesh", "the model template isn't a model root")
    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "Mesh" and len(file.root["mesh"]["parts"]) == 1, "the file isn't a mesh of one part")
    material = file.materials[0]
    check(material["data"]["Shaders"][0]["ShaderType"] == 1 and len(material.get("images", [])) == 1, "the model's material isn't an unlit one of its picture: %s" % material.get("data"))
    return warnings


def _scenery(target):
    bpy.ops.ttt.new_scenery()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "Scenery" and root.get(tlm_blender.KIND_PROPERTY) == "scenery", "the scenery template isn't a model root")
    check(twintech_properties.get(root).type == "Scenery", "the scenery isn't one")
    # Shown in Blender's axes, Z up
    check(tuple(twintech_properties.get(root).scenery.bounds_max) == (200.0, 200.0, 100.0), "the scenery's box isn't a new chunk's")
    check(_kinds(root) == ["ambient_light", "collision", "directional_light", "dynamic_scenery", "lights", "scenery_lods", "scenery_mesh", "scenery_meshes"],
          "the template holds %s" % _kinds(root))
    ground = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "scenery_mesh")
    check(tlm_blender.role_of(ground.parent) == "scenery_meshes", "the ground isn't in the Meshes")
    check(len(ground.data.polygons) == 1 and len(ground.data.vertices) == 4, "the ground isn't a square")
    collision = next(child for child in root.children if tlm_blender.role_of(child) == "collision")
    surface = collision.data.materials[0]
    check(surface is not None and surface.name == "SURF_DEFAULT_0" and twintech_properties.get(surface).type == "CollisionSurface", "the collision has no surface material")
    check(surface.get(tlm_scenery.SURFACE_PROPERTY) == "", "the surface names a project asset")
    sun = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "directional_light")
    pointed = (root.matrix_world.inverted() @ sun.matrix_world).to_quaternion() @ __import__("mathutils").Vector((0.0, 0.0, 1.0))
    check(pointed.z > 0.9, "the directional light doesn't come from above: %s" % pointed)

    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "Scenery" and file.root.get("kind") == "scenery", "the file isn't a scenery")
    kinds = sorted(node.get("kind") for node in tlm.traverse(file.root))
    check(kinds == ["ambient_light", "collision", "directional_light", "dynamic_scenery", "lights", "scenery", "scenery_lods", "scenery_mesh", "scenery_meshes"],
          "the file holds %s" % kinds)
    check(file.root["data"]["BoundsMin"] == [-200.0, -100.0, -200.0], "the file's scenery box is %s" % file.root["data"])
    collision_node = tlm.find_child(file.root, "collision")
    check(len(collision_node["surfaces"]) == 1 and collision_node["surfaces"][0]["name"] == "SURF_DEFAULT_0", "the file's collision has surfaces %s" % collision_node["surfaces"])
    mesh_node = next(node for node in tlm.traverse(file.root) if node.get("kind") == "scenery_mesh")
    check(len(mesh_node["mesh"]["parts"]) == 1, "the ground has %d parts" % len(mesh_node["mesh"]["parts"]))
    return warnings


def _save_icon(target):
    bpy.ops.ttt.new_save_icon()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "SaveIcon" and root.get(tlm_blender.KIND_PROPERTY) == "save_icon", "the save icon template isn't a model root")
    check(twintech_properties.get(root).type == "SaveIcon", "the root has no save icon type")
    check(_kinds(root) == ["icon_mesh"], "the template holds %s" % _kinds(root))
    icon = tlm_blender._descendants(root)[0]
    check(len(icon.data.vertices) == 8 and len(icon.data.polygons) == 6 and icon.data.uv_layers.active is not None, "the icon isn't a box with UVs")
    colors = icon.data.color_attributes.get(tlm_blender.COLOR_ATTRIBUTE)
    check(colors is not None and abs(colors.data[0].color_srgb[0] - 128.0 / 255.0) < 1e-3, "the icon's vertex colors aren't the console's full 0x80")
    material = icon.data.materials[0]
    image = next(node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE")
    check(material.get(tlm_blender.BLENDER_ID_PROPERTY) and tuple(image.size) == (128, 128) and image.packed_file is not None, "the icon has no packed 128x128 picture")

    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "SaveIcon" and file.root.get("kind") == "save_icon", "the file isn't a save icon")
    data = file.root["data"]
    check(data["FileId"] == 0x10000 and data["TextureType"] == 6 and data["FrameLength"] == 1, "the icon's header is %s" % data)
    part = file.root["mesh"]["parts"][0]
    check(part["material"] == 0 and file.materials[0].get("image") is not None, "the icon's material isn't in the file with its picture")
    frames = [(frame["shape"], list(file.read_view(frame["keys"], "f32"))) for frame in file.root["animation"]["frames"]]
    check(frames == [(0, [0.0, 1.0])], "the icon's animation is %s" % frames)
    return warnings


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ogi_target, scenery_target, save_icon_target, skydome_target, model_target = arguments
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    warnings = _ogi(os.path.abspath(ogi_target)) + _scenery(os.path.abspath(scenery_target)) + _save_icon(os.path.abspath(save_icon_target)) + \
        _skydome(os.path.abspath(skydome_target)) + _model(os.path.abspath(model_target))
    check(len([blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY)]) == 5, "the scene doesn't hold the templates")
    print("TEMPLATES OK, warnings: %s" % (warnings or "none"))


main()
