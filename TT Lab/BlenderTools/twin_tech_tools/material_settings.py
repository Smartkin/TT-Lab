# Blender tools to work with TwinTech Lab(TT-Lab) files
# Copyright (C) 2025 Smyshliaev "Smartkin" Vladislav
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

"""The Twin Tech settings of a Blender material: every value TT Lab edits of the project's material (or of one made in Blender),
kept on the material and drawn the way the game draws them (material_nodes).

A material imported from a model file keeps the project's JSON it was made from as its base. Exporting writes what was changed
in Blender since then over what the project has by then (gs_material.merge_material), TT Lab takes it into the project's material.
A shader animation's tracks are the shaders' `anim_*` values, keyframed on the material: an animated track has keys, a static one
none. Frame `f` of the game's animation is at `f * scene fps / animation fps` of the scene, and the loop's end gets the first
frame's value again, so Blender plays it the way the game does.
"""

import contextlib
import json
import re
import typing

import bpy

from . import gs_material
from . import material_nodes

PROPERTY = "ttt_material"
# On an image loaded from a project texture, its URI
TEXTURE_URI_PROPERTY = "ttt_texture_uri"
# The add-on's ID of a picture made in Blender, TT Lab makes it a texture once and uses that one for it from then on
IMAGE_ID_PROPERTY = "ttt_blender_id"
TRACK_PROPS = ("anim_u", "anim_v", "anim_r", "anim_g", "anim_b", "anim_a")
_SHADER_PATH = re.compile(r"^%s\.shaders\[(\d+)\]\.(.+)$" % PROPERTY)

_suspended = 0


@contextlib.contextmanager
def suspended() -> typing.Iterator[None]:
    """Values set meanwhile don't draw the material again, the caller does once they're all set."""
    global _suspended
    _suspended += 1
    try:
        yield
    finally:
        _suspended -= 1


def _changed(self: typing.Any, context: typing.Any) -> None:
    if _suspended == 0 and isinstance(self.id_data, bpy.types.Material):
        refresh(self.id_data)


def _image_changed(self: typing.Any, context: typing.Any) -> None:
    if _suspended:
        return

    # A project's texture keeps being that texture, any other picture goes into the model file with the material
    with suspended():
        self.texture = self.image.get(TEXTURE_URI_PROPERTY, "") if self.image is not None else ""
        if self.image is not None:
            self.texture_mapping = True

    _changed(self, context)


def _animation_toggled(self: typing.Any, context: typing.Any) -> None:
    if _suspended:
        return

    if self.has_animation:
        with suspended():
            view = gs_material.new_animation()
            self.anim_fps = view.fps
            self.anim_frames = view.frames
            for prop, value in zip(TRACK_PROPS, view.tracks):
                setattr(self, prop, value)

    _changed(self, context)


def _field_property(field: gs_material.Field) -> typing.Any:
    settings = {"name": field.label, "description": field.description, "update": _changed}
    if field.kind == "enum":
        return bpy.props.EnumProperty(items=gs_material.enum_items(field.enum), **settings)

    if field.kind == "bucket":
        return bpy.props.EnumProperty(items=gs_material.bucket_items(), default="BUCKET_2", **settings)

    if field.kind in ("switch", "bool"):
        return bpy.props.BoolProperty(**settings)

    if field.kind == "int":
        return bpy.props.IntProperty(min=field.minimum, max=field.maximum, **settings)

    if field.kind == "uint32":
        return bpy.props.IntProperty(**settings)

    if field.kind == "lod_k":
        return bpy.props.FloatProperty(step=6, precision=4, **settings)

    if field.kind in ("floats", "bits"):
        return bpy.props.FloatVectorProperty(size=field.size, precision=4, **settings)

    if field.kind in ("uri", "text"):
        return bpy.props.StringProperty(**settings)

    raise ValueError(field.kind)


def _shader_annotations() -> typing.Dict[str, typing.Any]:
    annotations = {field.prop: _field_property(field) for field in gs_material.SHADER_FIELDS}
    annotations.update({
        "source": bpy.props.IntProperty(default=-1, options={"HIDDEN"}, description="The shader of the project's material it was read from"),
        "image": bpy.props.PointerProperty(type=bpy.types.Image, name="Picture", update=_image_changed,
                                           description="What the pass draws: a project texture's picture, any other one goes into the model file "
                                                       "with the material and becomes a texture of the project"),
        "has_animation": bpy.props.BoolProperty(name="Animation", update=_animation_toggled,
                                                description="Six tracks (U, V, red, green, blue, alpha), static or keyframed, looping at the "
                                                            "frames per second. The From Animation scrolls take U and V as the UV offset"),
        "anim_fps": bpy.props.IntProperty(name="Frames per Second", min=0, max=gs_material.MAX_ANIMATION_FPS, default=gs_material.DEFAULT_ANIMATION_FPS,
                                          update=_changed, description="How fast the animation's frames play, at most 31 (5 bits of its header)"),
        "anim_frames": bpy.props.IntProperty(name="Frames", min=1, max=0xFFFF, default=1, update=_changed,
                                             description="The frames the animation loops over, the last blending into the first"),
    })
    for prop, track in zip(TRACK_PROPS, gs_material.TRACKS):
        annotations[prop] = bpy.props.FloatProperty(name=track, precision=4, min=gs_material.to_raw(-8.0) / 4096.0, max=0x7FFF / 4096.0,
                                                    description="The %s track: keyframe it to animate it, without keys it keeps this value" % track)

    return annotations


class TTT_ShaderSettings(bpy.types.PropertyGroup):
    __annotations__ = _shader_annotations()


def _material_annotations() -> typing.Dict[str, typing.Any]:
    annotations = {field.prop: _field_property(field) for field in gs_material.MATERIAL_FIELDS}
    annotations.update({
        "managed": bpy.props.BoolProperty(options={"HIDDEN"}, description="The material is drawn and exported with its Twin Tech settings"),
        "shaders": bpy.props.CollectionProperty(type=TTT_ShaderSettings),
        "active_shader": bpy.props.IntProperty(options={"HIDDEN"}),
        "base": bpy.props.StringProperty(options={"HIDDEN"}, description="The project's material the settings were read from, as JSON"),
    })
    return annotations


class TTT_MaterialSettings(bpy.types.PropertyGroup):
    __annotations__ = _material_annotations()


def settings_of(material: bpy.types.Material) -> TTT_MaterialSettings:
    return getattr(material, PROPERTY)


def is_managed(material: typing.Optional[bpy.types.Material]) -> bool:
    return material is not None and settings_of(material).managed


def shader_path(index: int, prop: str) -> str:
    return "%s.shaders[%d].%s" % (PROPERTY, index, prop)


# Reading

def load(material: bpy.types.Material, data: typing.Dict[str, typing.Any], project: typing.Any = None,
         images: typing.Sequence[typing.Optional[bpy.types.Image]] = ()) -> None:
    """Gives the material the settings of a material's JSON, the base exporting compares with. Shaders link the project's textures,
    or the file's pictures by their index (`Image`)."""
    settings = settings_of(material)
    shown = gs_material.shown_material(data)
    shaders = gs_material._shaders(data)
    with suspended():
        settings.managed = True
        for field in gs_material.MATERIAL_FIELDS:
            setattr(settings, field.prop, shown[field.prop])

        _clear_animation(material)
        settings.shaders.clear()
        for index, values in enumerate(shown["shaders"]):
            item = settings.shaders.add()
            _set_shader(item, values)
            item.source = values["source"]
            picture = shaders[index].get("Image")
            if isinstance(picture, int) and 0 <= picture < len(images):
                item.image = images[picture]
            elif values["texture"] and project is not None:
                item.image = texture_image(project, values["texture"])

            _write_animation(material, index, values["animation"])

        settings.active_shader = 0
        settings.base = json.dumps(data)

    refresh(material)


def _set_shader(item: TTT_ShaderSettings, values: typing.Dict[str, typing.Any]) -> None:
    for field in gs_material.SHADER_FIELDS:
        setattr(item, field.prop, values[field.prop])

    view = values.get("animation")
    item.has_animation = view is not None
    if view is not None:
        item.anim_fps = view.fps
        item.anim_frames = view.frames
        for track, prop in enumerate(TRACK_PROPS):
            setattr(item, prop, view.value_at(track, 0))


def texture_image(project: typing.Any, uri: str) -> typing.Optional[bpy.types.Image]:
    """The picture of a project texture, loaded once."""
    for image in bpy.data.images:
        if image.get(TEXTURE_URI_PROPERTY) == uri:
            return image

    texture = project.textures.get(uri) if project is not None else None
    if texture is None:
        return None

    try:
        image = bpy.data.images.load(texture.png_path, check_existing=True)
    except RuntimeError:
        return None

    image[TEXTURE_URI_PROPERTY] = uri
    # The pixels' colors as the file has them whatever their alpha, the game's textures keep colors where they're see-through
    image.alpha_mode = "CHANNEL_PACKED"
    return image


def make(material: bpy.types.Material, skin: bool = False, global_package: bool = False) -> None:
    """Gives a material made in Blender the settings TT Lab gives such materials, drawing its picture."""
    image = next((node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image is not None), None) \
        if material.node_tree is not None else None
    blended = getattr(material, "surface_render_method", "") == "BLENDED"
    load(material, gs_material.new_material(material.name, skin=skin, textured=image is not None, blended=blended, global_package=global_package))
    if image is not None:
        with suspended():
            settings_of(material).shaders[0].image = image

        refresh(material)


# Drawing

def passes(material: bpy.types.Material) -> typing.List[material_nodes.Pass]:
    result = []
    for index, item in enumerate(settings_of(material).shaders):
        shown = {field.prop: _value(item, field) for field in gs_material.SHADER_FIELDS}
        paths = (shader_path(index, "anim_u"), shader_path(index, "anim_v")) if item.has_animation else None
        result.append(material_nodes.Pass(shown, item.image, paths))

    return result


def refresh(material: bpy.types.Material) -> None:
    material_nodes.build(material, passes(material))


def draw_default(material: bpy.types.Material, image: typing.Optional[bpy.types.Image], blended: bool = False) -> None:
    """Draws a material without settings of its own the way TT Lab makes such materials: unlit, the picture times the vertex colors."""
    shader = gs_material.shown_shader(gs_material.new_material(material.name, textured=image is not None, blended=blended)["Shaders"][0])
    material_nodes.build(material, [material_nodes.Pass(shader, image)])


def _value(item: TTT_ShaderSettings, field: gs_material.Field) -> typing.Any:
    value = getattr(item, field.prop)
    return tuple(value) if field.kind in ("floats", "bits") else value


# Writing

def shown(material: bpy.types.Material, images: typing.List[bpy.types.Image], project: typing.Any = None) -> typing.Dict[str, typing.Any]:
    """The material's settings the way gs_material compares them. A picture that isn't a project texture's (or was changed) gets an
    index into the images going into the file with it, one TT Lab made a texture of is that texture."""
    settings = settings_of(material)
    result: typing.Dict[str, typing.Any] = {field.prop: getattr(settings, field.prop) for field in gs_material.MATERIAL_FIELDS}
    result["shaders"] = []
    for index, item in enumerate(settings.shaders):
        values = {field.prop: _value(item, field) for field in gs_material.SHADER_FIELDS}
        values["source"] = item.source
        values["animation"] = shown_animation(material, index)
        values["image"] = None
        image = item.image
        if image is not None:
            uri = image.get(TEXTURE_URI_PROPERTY, "")
            if not uri and project is not None:
                texture = project.texture_of_image(image.get(IMAGE_ID_PROPERTY, ""))
                uri = texture.uri if texture is not None else ""

            if uri and not image.is_dirty:
                values["texture"] = uri
            else:
                values["texture"] = ""
                if image not in images:
                    images.append(image)

                values["image"] = images.index(image)

        result["shaders"].append(values)

    return result


def base_of(material: bpy.types.Material) -> typing.Optional[typing.Dict[str, typing.Any]]:
    try:
        base = json.loads(settings_of(material).base)
    except ValueError:
        return None

    return base if isinstance(base, dict) else None


def commit(material: bpy.types.Material, base: typing.Dict[str, typing.Any], applied: bool) -> None:
    """Keeps the base merging made once the model file is written. Blender's shaders are the base's in their order once the project
    has Blender's list of them."""
    settings = settings_of(material)
    with suspended():
        settings.base = json.dumps(base)
        if applied:
            for index, item in enumerate(settings.shaders):
                item.source = index


# Animations

def scene_fps() -> float:
    render = bpy.context.scene.render
    return render.fps / render.fps_base if render.fps_base > 0 else float(render.fps)


def _frame_step(fps: int) -> float:
    return scene_fps() / fps if fps > 0 else 1.0


def _action(material: bpy.types.Material, create: bool) -> typing.Tuple[typing.Optional[bpy.types.Action], typing.Any]:
    data = material.animation_data
    if data is None:
        if not create:
            return None, None

        data = material.animation_data_create()

    action = data.action
    if action is None:
        if not create:
            return None, None

        action = bpy.data.actions.new(material.name + " Shader Animation")
        data.action = action

    slot = data.action_slot
    if slot is None:
        slot = next((slot for slot in action.slots if slot.target_id_type == "MATERIAL"), None)
        if slot is None:
            if not create:
                return action, None

            slot = action.slots.new(id_type="MATERIAL", name=material.name)

        data.action_slot = slot

    return action, slot


def _channelbag(action: bpy.types.Action, slot: typing.Any, create: bool) -> typing.Any:
    for layer in action.layers:
        for strip in layer.strips:
            bag = strip.channelbag(slot)
            if bag is not None:
                return bag

    if not create:
        return None

    if len(action.layers) == 0:
        action.layers.new("Layer")

    layer = action.layers[0]
    if len(layer.strips) == 0:
        layer.strips.new(type="KEYFRAME")

    return layer.strips[0].channelbag(slot, ensure=True)


def _curves(material: bpy.types.Material) -> typing.List[typing.Any]:
    action, slot = _action(material, False)
    if action is None or slot is None:
        return []

    bag = _channelbag(action, slot, False)
    return list(bag.fcurves) if bag is not None else []


def track_curve(material: bpy.types.Material, index: int, track: int) -> typing.Any:
    path = shader_path(index, TRACK_PROPS[track])
    return next((curve for curve in _curves(material) if curve.data_path == path), None)


def shown_animation(material: bpy.types.Material, index: int) -> typing.Optional[gs_material.AnimationView]:
    """The shader's animation as the game would play it: a keyframed track sampled on every frame of the loop, the others static."""
    item = settings_of(material).shaders[index]
    if not item.has_animation:
        return None

    step = _frame_step(item.anim_fps)
    tracks: typing.List[typing.Union[float, typing.List[float]]] = []
    for track, prop in enumerate(TRACK_PROPS):
        curve = track_curve(material, index, track)
        if curve is not None and len(curve.keyframe_points) > 0:
            tracks.append([curve.evaluate(frame * step) for frame in range(item.anim_frames)])
        else:
            tracks.append(getattr(item, prop))

    return gs_material.AnimationView(item.anim_fps, item.anim_frames, tracks)


def _write_animation(material: bpy.types.Material, index: int, view: typing.Optional[gs_material.AnimationView]) -> None:
    """Keys of the animated tracks on the game's frames, the loop's end again at the first frame's value, played in a cycle."""
    if view is None or not any(view.is_animated(track) for track in range(len(TRACK_PROPS))):
        return

    action, slot = _action(material, True)
    bag = _channelbag(action, slot, True)
    step = _frame_step(view.fps)
    for track, prop in enumerate(TRACK_PROPS):
        if not view.is_animated(track):
            continue

        values = [view.value_at(track, frame) for frame in range(view.frames)]
        curve = bag.fcurves.new(shader_path(index, prop))
        curve.keyframe_points.add(len(values) + 1)
        for frame, value in enumerate(values + values[:1]):
            point = curve.keyframe_points[frame]
            point.co = (frame * step, value)
            point.interpolation = "LINEAR"

        curve.modifiers.new("CYCLES")
        curve.update()


def _clear_animation(material: bpy.types.Material) -> None:
    action, slot = _action(material, False)
    bag = _channelbag(action, slot, False) if action is not None and slot is not None else None
    if bag is None:
        return

    for curve in list(bag.fcurves):
        if _SHADER_PATH.match(curve.data_path):
            bag.fcurves.remove(curve)


def remap_animation(material: bpy.types.Material, mapping: typing.Dict[int, typing.Optional[int]]) -> None:
    """Moves the shaders' keys with them when shaders move or go, None for a shader taken out."""
    action, slot = _action(material, False)
    bag = _channelbag(action, slot, False) if action is not None and slot is not None else None
    if bag is None:
        return

    for curve in list(bag.fcurves):
        match = _SHADER_PATH.match(curve.data_path)
        if match is None or int(match.group(1)) not in mapping:
            continue

        target = mapping[int(match.group(1))]
        if target is None:
            bag.fcurves.remove(curve)
        else:
            curve.data_path = shader_path(target, match.group(2))


def add_shader(material: bpy.types.Material) -> None:
    settings = settings_of(material)
    if len(settings.shaders) >= gs_material.MAX_SHADERS:
        return

    with suspended():
        item = settings.shaders.add()
        _set_shader(item, gs_material.shown_shader(gs_material.new_shader()))
        item.source = -1
        settings.active_shader = len(settings.shaders) - 1

    refresh(material)


def remove_shader(material: bpy.types.Material, index: int) -> None:
    settings = settings_of(material)
    if not 0 <= index < len(settings.shaders):
        return

    count = len(settings.shaders)
    with suspended():
        remap_animation(material, {other: (None if other == index else other - 1 if other > index else other) for other in range(count)})
        settings.shaders.remove(index)
        settings.active_shader = max(0, min(index, len(settings.shaders) - 1))

    refresh(material)


def move_shader(material: bpy.types.Material, index: int, direction: int) -> None:
    settings = settings_of(material)
    target = index + direction
    if not 0 <= index < len(settings.shaders) or not 0 <= target < len(settings.shaders):
        return

    with suspended():
        remap_animation(material, {index: target, target: index})
        settings.shaders.move(index, target)
        settings.active_shader = target

    refresh(material)


def register() -> None:
    setattr(bpy.types.Material, PROPERTY, bpy.props.PointerProperty(type=TTT_MaterialSettings))


def unregister() -> None:
    if hasattr(bpy.types.Material, PROPERTY):
        delattr(bpy.types.Material, PROPERTY)
