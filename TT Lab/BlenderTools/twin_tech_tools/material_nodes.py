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

"""Draws a Twin Tech material in Blender the way the game draws it (see gs_material).

Every shader is a pass over the same triangles. A pass's color is the texture (its PNG's values, which the GS gets halved) times
the vertex colors (0x80 is 1, the lit types' lit by TT Lab's default lights) and its alpha the texture's times the vertex color's.
What the passes leave is kept as what they draw themselves (E) and how much of what's behind still shows through (T), in the
display's values the GS works in: a pass that doesn't blend draws over both where it passes its alpha test, a blending one gives
`source * Cs + frame * Cd` of the GS's formula, both clamped like the GS does. The material is then E emitted over T of the
background, both made Blender's linear values: exact where nothing behind shows through (opaque materials, passes over the
material's own) and where what's behind is only tinted, while colors added to what's behind are added in Blender's linear values.
Scene lights don't change it, the scene's view transform has to be Standard for the display's values to show as they are.
"""

import math
import typing

import bpy

from . import gs_material

OUTPUT_NODE = "TTT Output"
LINEAR_TO_DISPLAY = "TTT Linear To Display"
DISPLAY_TO_LINEAR = "TTT Display To Linear"
COLOR_ATTRIBUTE = "Color"
# TT Lab's default lights (SceneLights.Default) the way the game's lit programs take them: the ambient light's color times its
# intensity halved, the directional light's color times its intensity times 0.5, from above
AMBIENT_LIGHT = 1.0 / 3.0 * 4.5 * 0.5
DIRECTIONAL_LIGHT = 1.0 / 3.0 * 3.0 * 0.5


class Pass:
    """A shader to draw: its settings as Blender shows them (gs_material.shown_shader) with the picture it draws and where the
    animation's U and V tracks are, None when it has none."""

    def __init__(self, shown: typing.Dict[str, typing.Any], image: typing.Optional[bpy.types.Image] = None,
                 animation_paths: typing.Optional[typing.Tuple[str, str]] = None):
        self.shown = shown
        self.image = image
        self.animation_paths = animation_paths


def build(material: bpy.types.Material, passes: typing.Sequence[Pass]) -> None:
    """Makes the material's node tree draw the passes, replacing what it had."""
    if material.node_tree is None or not material.use_nodes:
        material.use_nodes = True

    tree = material.node_tree
    if tree.animation_data is not None:
        for driver in list(tree.animation_data.drivers):
            tree.animation_data.drivers.remove(driver)

    tree.nodes.clear()
    builder = _Builder(material, tree)
    emitted: typing.Any = (0.0, 0.0, 0.0)
    through: typing.Any = (1.0, 1.0, 1.0)
    blends = False
    for index, shader_pass in enumerate(passes):
        shown = shader_pass.shown
        source = gs_material.texture_source(shown.get("shader_type", ""))
        if shown.get("depth_test") == "NEVER" or source == "hidden":
            continue

        builder.start_pass(index)
        if source == "frame":
            # What's behind it, tinted: its own texture isn't read
            blends = True
            tint, alpha = builder.frame_tint(shader_pass)
            emitted, through = builder.tint(shown, tint, alpha, builder.alpha_test(shown, alpha), emitted, through)
            continue

        color, alpha = builder.source(shader_pass)
        drawn = builder.alpha_test(shown, alpha)
        if shown.get("blending"):
            blends = True
            emitted, through = builder.blend(shown, color, alpha, drawn, emitted, through)
        else:
            emitted, through = builder.replace(color, drawn, emitted, through)

    builder.finish(emitted, through)
    material.surface_render_method = "BLENDED" if blends else "DITHERED"
    material.use_backface_culling = False


class _Builder:
    def __init__(self, material: bpy.types.Material, tree: bpy.types.NodeTree):
        self.material = material
        self.tree = tree
        self.nodes = tree.nodes
        self.links = tree.links
        self.frame: typing.Optional[bpy.types.Node] = None
        self.column = 0
        self.row = 0
        self.pass_index = 0
        self._seconds: typing.Optional[bpy.types.NodeSocket] = None

    # Layout: a frame per pass, the nodes in columns as they're made

    def start_pass(self, index: int) -> None:
        self.pass_index = index
        self.frame = self.nodes.new("NodeFrame")
        self.frame.label = "Shader %d" % index
        self.column = 0
        self.row = 0

    def _node(self, kind: str, **settings: typing.Any) -> bpy.types.Node:
        node = self.nodes.new(kind)
        for name, value in settings.items():
            setattr(node, name, value)

        node.location = (-2600 + self.column * 180, -self.pass_index * 900 - self.row * 160)
        self.row += 1
        if self.row >= 5:
            self.row = 0
            self.column += 1

        if self.frame is not None:
            node.parent = self.frame

        return node

    def _connect(self, value: typing.Any, socket: bpy.types.NodeSocket) -> None:
        if isinstance(value, bpy.types.NodeSocket):
            self.links.new(value, socket)
        elif isinstance(value, (tuple, list)) and len(socket.default_value) != len(value):
            socket.default_value = tuple(value) + (1.0,) * (len(socket.default_value) - len(value))
        else:
            socket.default_value = value

    def math(self, operation: str, a: typing.Any, b: typing.Any = 0.0, c: typing.Any = 0.0, clamp: bool = False) -> bpy.types.NodeSocket:
        node = self._node("ShaderNodeMath", operation=operation, use_clamp=clamp)
        for socket, value in zip(node.inputs, (a, b, c)):
            self._connect(value, socket)

        return node.outputs[0]

    def vector(self, operation: str, a: typing.Any, b: typing.Any = (0.0, 0.0, 0.0), scale: typing.Any = 1.0) -> bpy.types.NodeSocket:
        node = self._node("ShaderNodeVectorMath", operation=operation)
        self._connect(a, node.inputs[0])
        self._connect(b, node.inputs[1])
        if operation == "SCALE":
            self._connect(scale, node.inputs["Scale"])

        return node.outputs["Vector"] if operation not in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else node.outputs["Value"]

    def combine(self, x: typing.Any, y: typing.Any, z: typing.Any) -> bpy.types.NodeSocket:
        node = self._node("ShaderNodeCombineXYZ")
        for socket, value in zip(node.inputs, (x, y, z)):
            self._connect(value, socket)

        return node.outputs[0]

    def separate(self, vector: typing.Any) -> typing.Tuple[bpy.types.NodeSocket, bpy.types.NodeSocket, bpy.types.NodeSocket]:
        node = self._node("ShaderNodeSeparateXYZ")
        self._connect(vector, node.inputs[0])
        return node.outputs[0], node.outputs[1], node.outputs[2]

    def lerp(self, a: typing.Any, b: typing.Any, factor: typing.Any) -> typing.Any:
        """a where the factor is 0, b where it's 1."""
        if factor is None:
            return b

        return self.vector("ADD", a, self.vector("SCALE", self.vector("SUBTRACT", b, a), scale=factor))

    def group(self, name: str, value: typing.Any) -> bpy.types.NodeSocket:
        node = self._node("ShaderNodeGroup")
        node.node_tree = _conversion_group(name)
        self._connect(value, node.inputs[0])
        return node.outputs[0]

    def seconds(self) -> bpy.types.NodeSocket:
        """The scene's time in seconds, which the game's scrolls go by."""
        if self._seconds is None:
            frame, self.frame = self.frame, None
            node = self._node("ShaderNodeValue", label="Seconds")
            node.location = (-3000, 300)
            self.frame = frame
            driver = node.outputs[0].driver_add("default_value").driver
            driver.type = "SCRIPTED"
            for name, path in (("fps", "render.fps"), ("fps_base", "render.fps_base")):
                variable = driver.variables.new()
                variable.name = name
                variable.type = "SINGLE_PROP"
                variable.targets[0].id_type = "SCENE"
                variable.targets[0].id = bpy.context.scene
                variable.targets[0].data_path = path

            driver.expression = "frame * fps_base / fps"
            self._seconds = node.outputs[0]

        return self._seconds

    def driven(self, path: str, label: str) -> bpy.types.NodeSocket:
        """A value of the material's own properties, which keyframes animate."""
        node = self._node("ShaderNodeValue", label=label)
        driver = node.outputs[0].driver_add("default_value").driver
        driver.type = "SCRIPTED"
        variable = driver.variables.new()
        variable.name = "value"
        variable.type = "SINGLE_PROP"
        variable.targets[0].id_type = "MATERIAL"
        variable.targets[0].id = self.material
        variable.targets[0].data_path = path
        driver.expression = "value"
        return node.outputs[0]

    # A pass

    def vertex_color(self, shown: typing.Dict[str, typing.Any]) -> typing.Tuple[typing.Any, typing.Any]:
        """The vertex colors in the display's values, lit by the default lights for the lit types and at most 255, and their alpha."""
        attribute = self._node("ShaderNodeVertexColor", layer_name=COLOR_ATTRIBUTE)
        vertex_color = self.group(LINEAR_TO_DISPLAY, attribute.outputs["Color"])
        if gs_material.is_lit(shown.get("shader_type", "")):
            vertex_color = self.vector("MINIMUM", self.vector("SCALE", vertex_color, scale=self.light()), (1.0, 1.0, 1.0))

        return vertex_color, attribute.outputs["Alpha"]

    def source(self, shader_pass: Pass) -> typing.Tuple[typing.Any, typing.Any]:
        """The pass's color in the display's values and its alpha (1 is 0x80): the texture times the vertex colors, the GS's MODULATE."""
        shown = shader_pass.shown
        vertex_color, vertex_alpha = self.vertex_color(shown)
        image = shader_pass.image
        if not shown.get("texture_mapping") or image is None:
            return vertex_color, vertex_alpha

        texture = self._node("ShaderNodeTexImage", image=image, extension="REPEAT",
                             interpolation="Closest" if shown.get("texture_filter") == "NEAREST" else "Linear")
        self._connect(self.texture_coordinates(shader_pass), texture.inputs["Vector"])
        texture_color = texture.outputs["Color"]
        if not image.colorspace_settings.is_data:
            texture_color = self.group(LINEAR_TO_DISPLAY, texture_color)

        return self.vector("MULTIPLY", texture_color, vertex_color), self.math("MULTIPLY", texture.outputs["Alpha"], vertex_alpha)

    def light(self) -> bpy.types.NodeSocket:
        """What the lit programs multiply the vertex colors by: the ambient light and the directional light by how much the normal
        faces it (their vectors not normalized, which a unit normal doesn't need)."""
        geometry = self._node("ShaderNodeNewGeometry")
        facing = self.math("MAXIMUM", self.separate(geometry.outputs["Normal"])[2], 0.0)
        return self.math("MULTIPLY_ADD", facing, DIRECTIONAL_LIGHT, AMBIENT_LIGHT)

    def frame_tint(self, shader_pass: Pass) -> typing.Tuple[typing.Any, typing.Any]:
        """What the reflection surface multiplies the frame by, its lit colors with 128 as 1, and its alpha."""
        vertex_color, vertex_alpha = self.vertex_color(shader_pass.shown)
        return self.vector("SCALE", vertex_color, scale=255.0 / 128.0), vertex_alpha

    def tint(self, shown: typing.Dict[str, typing.Any], tint: typing.Any, alpha: typing.Any, drawn: typing.Optional[typing.Any],
             emitted: typing.Any, through: typing.Any) -> typing.Tuple[typing.Any, typing.Any]:
        """A pass drawing the frame tinted: what's behind times the tint, blended by the pass's formula with what's behind."""
        scale: typing.Any = tint
        if shown.get("blending"):
            factors = gs_material.blend_factors(shown)
            c: typing.Any = alpha if factors.c == "SOURCE" else 1.0 if factors.c == "FB" else factors.fixed / 128.0
            frame = self._factor(c, factors.frame)
            scale = self.vector("MAXIMUM", self.vector("ADD", self.vector("SCALE", tint, scale=self._factor(c, factors.source)), self.combine(frame, frame, frame)),
                                (0.0, 0.0, 0.0))

        tinted = self.vector("MINIMUM", self.vector("MULTIPLY", emitted, scale), (1.0, 1.0, 1.0))
        return self.lerp(emitted, tinted, drawn), self.lerp(through, self.vector("MULTIPLY", through, scale), drawn)

    def texture_coordinates(self, shader_pass: Pass) -> bpy.types.NodeSocket:
        """The UVs, or what the environment and metallic programs read, moved by the scrolls. Blender's V goes the other way."""
        shown = shader_pass.shown
        source = gs_material.texture_source(shown.get("shader_type", ""))
        if source == "environment":
            coordinates = self.environment()
        elif source == "reflection":
            coordinates = self.reflection()
        else:
            coordinates = self._node("ShaderNodeUVMap").outputs["UV"]

        animated = shader_pass.animation_paths is not None
        sources = gs_material.scroll_sources(shown.get("scroll_u", ""), shown.get("scroll_v", ""), animated)
        offsets = [self.scroll(shader_pass, source) for source in sources]
        if offsets[0] is None and offsets[1] is None:
            return coordinates

        v = self.math("MULTIPLY", offsets[1], -1.0) if offsets[1] is not None else 0.0
        return self.vector("ADD", coordinates, self.combine(offsets[0] if offsets[0] is not None else 0.0, v, 0.0))

    def clip(self, direction: typing.Any) -> typing.Tuple[typing.Any, typing.Any, typing.Any]:
        """A direction of the world through the clip matrix's turn and scale: on the view's axes scaled by the game's lens. The game's
        camera looks along +Z, Blender's along -Z, and the game's x is Blender's -x: imported models are the game's turned Z up, which
        Blender shows mirrored from the console (TT Lab mirrors its frame before showing it), so every point reads the game's texel."""
        transform = self._node("ShaderNodeVectorTransform", vector_type="VECTOR", convert_from="WORLD", convert_to="CAMERA")
        self._connect(direction, transform.inputs[0])
        x, y, z = self.separate(transform.outputs[0])
        scale = gs_material.CLIP_SCALE
        return self.math("MULTIPLY", x, -scale[0]), self.math("MULTIPLY", y, scale[1]), self.math("MULTIPLY", z, -scale[2])

    def environment(self) -> bpy.types.NodeSocket:
        """VU1 programs 0x14 and 0x1D: the way to the eye plus the normal through the clip matrix, normalized to d, the picture read at
        (0.5 + 0.5 d.x, 0.5 - 0.5 d.y) clamped."""
        geometry = self._node("ShaderNodeNewGeometry")
        half = self.vector("ADD", self.vector("NORMALIZE", geometry.outputs["Incoming"]), geometry.outputs["Normal"])
        x, y, _ = self.separate(self.vector("NORMALIZE", self.combine(*self.clip(half))))
        return self.combine(self.math("MULTIPLY_ADD", x, 0.5, 0.5, clamp=True), self.math("MULTIPLY_ADD", y, 0.5, 0.5, clamp=True), 0.0)

    def reflection(self) -> bpy.types.NodeSocket:
        """VU1 program 0x17: the way to the eye reflected off the normal through the clip matrix as it is, r, the picture read at
        (0.5 + 0.5 r.x, 0.5 + 0.5 r.y) clamped."""
        geometry = self._node("ShaderNodeNewGeometry")
        eye = self.vector("NORMALIZE", geometry.outputs["Incoming"])
        normal = geometry.outputs["Normal"]
        along = self.vector("DOT_PRODUCT", eye, normal)
        reflected = self.vector("SUBTRACT", self.vector("SCALE", normal, scale=self.math("MULTIPLY", along, 2.0)), eye)
        x, y, _ = self.clip(reflected)
        return self.combine(self.math("MULTIPLY_ADD", x, 0.5, 0.5, clamp=True), self.math("MULTIPLY_ADD", y, -0.5, 0.5, clamp=True), 0.0)

    def scroll(self, shader_pass: Pass, source: typing.Optional[typing.Tuple[str, int]]) -> typing.Optional[bpy.types.NodeSocket]:
        if source is None:
            return None

        formula, axis = source
        if formula == "animation":
            return self.driven(shader_pass.animation_paths[axis], "Animation " + "UV"[axis])

        scroll = shader_pass.shown.get("uv_scroll", (0.0, 0.0, 0.0, 0.0))
        phase = self.math("MULTIPLY_ADD", self.seconds(), scroll[axis + 2], scroll[axis])
        if formula == "wrap":
            return self.math("FRACT", phase)

        return self.math("COSINE" if formula == "cosine" else "SINE", self.math("MULTIPLY", phase, 2.0 * math.pi))

    def alpha_test(self, shown: typing.Dict[str, typing.Any], alpha: typing.Any) -> typing.Optional[typing.Any]:
        """Where the pass draws its color, None for everywhere: where its alpha passes the test, or everywhere when failing pixels
        write their color anyway."""
        if not shown.get("alpha_test") or gs_material.fail_writes_color(shown.get("alpha_fail", "")):
            return None

        method = shown.get("alpha_test_method", "ALWAYS")
        if method == "ALWAYS":
            return None

        if method == "NEVER":
            return 0.0

        reference = shown.get("alpha_reference", 0) / 128.0
        epsilon = 0.5 / 128.0
        if method in ("EQUAL", "NOTEQUAL"):
            equal = self.math("COMPARE", alpha, reference, epsilon)
            return equal if method == "EQUAL" else self.math("SUBTRACT", 1.0, equal)

        if method in ("LESS", "GEQUAL"):
            below = self.math("LESS_THAN", alpha, reference)
            return below if method == "LESS" else self.math("SUBTRACT", 1.0, below)

        above = self.math("GREATER_THAN", alpha, reference)
        return above if method == "GREATER" else self.math("SUBTRACT", 1.0, above)

    def replace(self, color: typing.Any, drawn: typing.Optional[typing.Any], emitted: typing.Any, through: typing.Any) -> typing.Tuple[typing.Any, typing.Any]:
        return self.lerp(emitted, color, drawn), self.lerp(through, (0.0, 0.0, 0.0), drawn)

    def blend(self, shown: typing.Dict[str, typing.Any], color: typing.Any, alpha: typing.Any, drawn: typing.Optional[typing.Any],
              emitted: typing.Any, through: typing.Any) -> typing.Tuple[typing.Any, typing.Any]:
        factors = gs_material.blend_factors(shown)
        c: typing.Any = alpha if factors.c == "SOURCE" else 1.0 if factors.c == "FB" else factors.fixed / 128.0
        frame = self._factor(c, factors.frame)
        if factors.subtracts:
            # Taking the source off what's behind shows as darkening it by the source
            kept = self.vector("MAXIMUM", self.vector("SUBTRACT", self.combine(frame, frame, frame), self.vector("SCALE", color, scale=self._factor(c, (-factors.source[0], 0)))),
                               (0.0, 0.0, 0.0))
            return self.lerp(emitted, self.vector("MULTIPLY", emitted, kept), drawn), self.lerp(through, self.vector("MULTIPLY", through, kept), drawn)

        source = self._factor(c, factors.source)
        blended = self.vector("ADD", self.vector("SCALE", color, scale=source), self.vector("SCALE", emitted, scale=frame))
        blended = self.vector("MINIMUM", self.vector("MAXIMUM", blended, (0.0, 0.0, 0.0)), (1.0, 1.0, 1.0))
        kept = self.vector("MAXIMUM", self.vector("SCALE", through, scale=frame), (0.0, 0.0, 0.0))
        return self.lerp(emitted, blended, drawn), self.lerp(through, kept, drawn)

    def _factor(self, c: typing.Any, factor: typing.Tuple[int, int]) -> typing.Any:
        """c * a + b of a blend factor, as a constant where it is one."""
        multiplier, constant = factor
        if multiplier == 0:
            return float(constant)

        if not isinstance(c, bpy.types.NodeSocket):
            return c * multiplier + constant

        return self.math("MULTIPLY_ADD", c, float(multiplier), float(constant))

    def finish(self, emitted: typing.Any, through: typing.Any) -> None:
        self.frame = None
        self.pass_index = 0
        output = self.nodes.new("ShaderNodeOutputMaterial")
        output.name = OUTPUT_NODE
        output.location = (600, 0)
        emission = self.nodes.new("ShaderNodeEmission")
        emission.location = (200, 100)
        self._connect(self.group(DISPLAY_TO_LINEAR, emitted), emission.inputs["Color"])
        emission.inputs["Strength"].default_value = 1.0
        transparent = self.nodes.new("ShaderNodeBsdfTransparent")
        transparent.location = (200, -100)
        self._connect(self.group(DISPLAY_TO_LINEAR, through), transparent.inputs["Color"])
        add = self.nodes.new("ShaderNodeAddShader")
        add.location = (400, 0)
        self.links.new(emission.outputs[0], add.inputs[0])
        self.links.new(transparent.outputs[0], add.inputs[1])
        self.links.new(add.outputs[0], output.inputs["Surface"])


def _conversion_group(name: str) -> bpy.types.NodeTree:
    """sRGB's transfer function a channel at a time, Blender keeps colors linear and the GS works in what the display shows."""
    group = bpy.data.node_groups.get(name)
    if group is not None and group.bl_idname == "ShaderNodeTree" and len(group.nodes) > 2:
        return group

    if group is None:
        group = bpy.data.node_groups.new(name, "ShaderNodeTree")
        group.interface.new_socket(name="Color", in_out="INPUT", socket_type="NodeSocketVector")
        group.interface.new_socket(name="Color", in_out="OUTPUT", socket_type="NodeSocketVector")

    nodes = group.nodes
    links = group.links
    nodes.clear()
    inputs = nodes.new("NodeGroupInput")
    inputs.location = (-800, 0)
    outputs = nodes.new("NodeGroupOutput")
    outputs.location = (800, 0)
    separate = nodes.new("ShaderNodeSeparateXYZ")
    separate.location = (-600, 0)
    combine = nodes.new("ShaderNodeCombineXYZ")
    combine.location = (600, 0)
    links.new(inputs.outputs[0], separate.inputs[0])
    links.new(combine.outputs[0], outputs.inputs[0])

    def math(operation: str, a: typing.Any, b: typing.Any = 0.0, c: typing.Any = 0.0, x: float = 0.0, y: float = 0.0) -> bpy.types.NodeSocket:
        node = nodes.new("ShaderNodeMath")
        node.operation = operation
        node.location = (x, y)
        for socket, value in zip(node.inputs, (a, b, c)):
            if isinstance(value, bpy.types.NodeSocket):
                links.new(value, socket)
            else:
                socket.default_value = value

        return node.outputs[0]

    to_display = name == LINEAR_TO_DISPLAY
    for channel in range(3):
        value = separate.outputs[channel]
        y = 300 - channel * 300
        if to_display:
            straight = math("MULTIPLY", value, 12.92, x=-400, y=y)
            curved = math("MULTIPLY_ADD", math("POWER", value, 1.0 / 2.4, x=-400, y=y - 100), 1.055, -0.055, x=-200, y=y - 100)
            beyond = math("GREATER_THAN", value, 0.0031308, x=-200, y=y + 100)
        else:
            straight = math("MULTIPLY", value, 1.0 / 12.92, x=-400, y=y)
            curved = math("POWER", math("MULTIPLY_ADD", value, 1.0 / 1.055, 0.055 / 1.055, x=-400, y=y - 100), 2.4, x=-200, y=y - 100)
            beyond = math("GREATER_THAN", value, 0.04045, x=-200, y=y + 100)

        result = math("MULTIPLY_ADD", math("SUBTRACT", curved, straight, x=0, y=y), beyond, straight, x=200, y=y)
        links.new(result, combine.inputs[channel])

    return group
