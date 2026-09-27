"""Imports a TT Lab model file with the add-on and exports it back, in Blender.

    blender --background --factory-startup --python tests/blender_roundtrip.py -- fixtures/ogi.tlm fixtures/ogi_from_blender.tlm

TT Lab's tests read the export of tests/fixtures/ogi.tlm (ogi_from_blender.tlm) and check it's the same model, run this again
after changing how the add-on imports or exports. With --edit the bone "Joint 1" is turned by 30 degrees on frame 2 of "Walk" and the
first vertex of the rigid body moved along X before exporting, which makes ogi_edited_in_blender.tlm. With --blend the scene is also
saved, to look at it.
"""

import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import twin_tech_tools  # noqa: E402
from twin_tech_tools import tlm_blender  # noqa: E402


def _edit(root):
    from mathutils import Quaternion

    action = bpy.data.actions["Walk"]
    bag = tlm_blender._existing_channelbag(action, next(slot for slot in action.slots if slot.target_id_type == "OBJECT"))
    path = 'pose.bones["Joint 1"].rotation_quaternion'
    curves = [next(fcurve for fcurve in bag.fcurves if fcurve.data_path == path and fcurve.array_index == index) for index in range(4)]
    rotation = Quaternion([curve.evaluate(2) for curve in curves])
    turned = Quaternion((0.0, 0.0, 1.0), math.radians(30)) @ rotation
    for index, curve in enumerate(curves):
        # A track of one key gets a key on every frame, the others keep their values
        if len(curve.keyframe_points) == 1:
            value = curve.keyframe_points[0].co[1]
            curve.keyframe_points.add(3)
            for frame in range(4):
                curve.keyframe_points[frame].co = (frame, value)
                curve.keyframe_points[frame].interpolation = "LINEAR"

        next(point for point in curve.keyframe_points if point.co[0] == 2).co[1] = turned[index]
        curve.update()

    body = next(child for child in tlm_blender._descendants(root) if tlm_blender.role_of(child) == "body")
    body.data.vertices[0].co.x += 0.1


# Blender adds shape keys at full weight, the face has to start at rest while no facial animation plays
def _check_face_at_rest():
    for blender_object in bpy.data.objects:
        key = blender_object.data.shape_keys if blender_object.type == "MESH" else None
        if key is None or (key.animation_data is not None and key.animation_data.action is not None):
            continue

        weights = [block.value for block in list(key.key_blocks)[1:]]
        if any(weight != 0.0 for weight in weights):
            raise AssertionError("%s's shape keys don't start at rest: %s" % (blender_object.name, weights))


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    blend = None
    if "--blend" in arguments:
        index = arguments.index("--blend")
        blend = arguments[index + 1]
        del arguments[index:index + 2]

    edit = "--edit" in arguments
    if edit:
        arguments.remove("--edit")

    source, target = arguments
    twin_tech_tools.register()
    for blender_object in list(bpy.data.objects):
        bpy.data.objects.remove(blender_object)

    root = tlm_blender.import_file(bpy.context, os.path.abspath(source))
    _check_face_at_rest()
    if edit:
        _edit(root)

    bpy.context.view_layer.update()
    tlm_blender.export_file(root, os.path.abspath(target))
    print("Exported %s to %s: %d objects, %d actions" % (source, target, len(bpy.data.objects), len(bpy.data.actions)))
    if blend is not None:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend))


main()
