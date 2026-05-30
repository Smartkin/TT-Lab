
import bpy
import typing
from bpy.app.handlers import persistent
from .properties import *

def register_props_for_blender_types():
    bpy.types.Object.NodeHasIndependentScaling = bpy.props.PointerProperty(type=NodeHasIndependentScaling)
    bpy.types.Bone.NodeHasIndependentScaling = bpy.props.PointerProperty(type=NodeHasIndependentScaling)

    bpy.app.handlers.depsgraph_update_pre.append(depsgraph_update_handler)

@persistent
def depsgraph_update_handler(scene, graph):
    init_props_for_blender_objects()

def init_props_for_blender_objects():
    for blenderArmature in bpy.data.armatures:
        for blenderBone in blenderArmature.bones:
            if len(blenderBone.NodeHasIndependentScaling.links.items()) > 0:
                continue

            blenderBone.NodeHasIndependentScaling.from_dict(blenderBone)

    for blenderObject in bpy.data.objects:
        if blenderObject.type != "MESH" and blenderObject.type != "EMPTY":
            continue

        if len(blenderObject.NodeHasIndependentScaling.links.items()) > 0:
                continue
        
        blenderObject.NodeHasIndependentScaling.from_dict(blenderObject)
