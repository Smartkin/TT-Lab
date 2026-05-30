import bpy

def subscribe_to_blender():
    bpy.types.RenderSettings.use_lock_interface = True
    bpy.app.handlers.frame_change_pre.append(frame_change_pre)

def frame_change_pre(scene: bpy.types.Scene, graph: bpy.types.Depsgraph):
    pass