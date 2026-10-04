"""Makes the add-on's OGI and scenery templates and exports them, in Blender:

    blender --background --factory-startup --python tests/blender_templates.py -- fixtures/ogi_template.tlm fixtures/scenery_template.tlm

TT Lab's tests (TlmFixtureTests) read what it exported. Prints TEMPLATES OK when both templates have what the add-on's own imports
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
    check(_kinds(root) == ["armature", "collision_hulls", "exit_points", "rigid_bodies", "skin"], "the template holds %s" % _kinds(root))
    armature = tlm_blender.armature_of(root)
    # One joint and no exit points and the game would draw no skin
    check([bone.name for bone in armature.data.bones] == ["Joint 0", "Joint 1"], "the armature's bones are %s" % [bone.name for bone in armature.data.bones])
    check(armature.data.bones["Joint 1"].parent == armature.data.bones["Joint 0"], "joint 1 isn't under joint 0")
    for index, bone_name in enumerate(("Joint 0", "Joint 1")):
        joint = twintech_properties.get(armature.data.bones[bone_name])
        check(joint.type == "Joint" and int(joint.joint.index) == index, "%s isn't joint %d" % (bone_name, index))
        check(armature.pose.bones[bone_name].rotation_mode == "QUATERNION", "%s doesn't rotate by quaternions" % bone_name)
    skin = next(child for child in root.children if tlm_blender.role_of(child) == "skin")
    check(len(skin.data.vertices) == 8 and len(skin.data.polygons) == 6, "the skin isn't a box")
    check(skin.data.uv_layers.active is not None, "the skin has no UVs")
    check([group.name for group in skin.vertex_groups] == ["Joint 1"] and all(len(vertex.groups) == 1 for vertex in skin.data.vertices), "the skin isn't weighted to joint 1")
    check(any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in skin.modifiers), "the armature doesn't deform the skin")
    check(len(bpy.data.actions) == 0, "the template has animations")
    check(root.matrix_world.to_quaternion().angle > 1.0, "the root doesn't turn the model Z up")

    warnings = tlm_blender.export_file(root, target)
    file = tlm.TlmFile.load(target)
    check(file.asset_type == "Ogi" and file.root.get("kind") == "ogi", "the file isn't an OGI")
    joints = tlm.find_child(file.root, "armature")["joints"]
    check([(joint["index"], joint["parent"]) for joint in joints] == [(0, -1), (1, 0)], "the file's joints are %s" % joints)
    skins = tlm.children(file.root, "skin")
    check(len(skins) == 1 and len(skins[0]["mesh"]["parts"]) == 1, "the file has no skin of one part")
    part = skins[0]["mesh"]["parts"][0]
    check(part["vertices"] >= 8 and part["material"] == -1, "the skin's part is %s" % {key: value for key, value in part.items() if not isinstance(value, dict)})
    check(all(joint == 1 for joint in file.read_view(part["group_joints"], "i32")[0::4]), "the skin isn't on joint 1 in the file")
    check(file.root["data"]["BoundingBoxMax"][1] == 1.0, "the bounding box isn't around the skin: %s" % file.root["data"])
    return warnings


def _scenery(target):
    bpy.ops.ttt.new_scenery()
    root = bpy.context.object
    check(root is not None and root.get(tlm_blender.ROOT_PROPERTY) == "Scenery" and root.get(tlm_blender.KIND_PROPERTY) == "scenery", "the scenery template isn't a model root")
    check(twintech_properties.get(root).type == "Scenery", "the scenery isn't one")
    check(tuple(twintech_properties.get(root).scenery.bounds_max) == (200.0, 100.0, 200.0), "the scenery's box isn't a new chunk's")
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
    check(pointed.y > 0.9, "the directional light doesn't come from above: %s" % pointed)

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


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ogi_target, scenery_target = arguments
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    warnings = _ogi(os.path.abspath(ogi_target)) + _scenery(os.path.abspath(scenery_target))
    check(len([blender_object for blender_object in bpy.data.objects if blender_object.get(tlm_blender.ROOT_PROPERTY)]) == 2, "the scene doesn't hold both templates")
    print("TEMPLATES OK, warnings: %s" % (warnings or "none"))


main()
