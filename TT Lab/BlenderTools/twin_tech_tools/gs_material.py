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

"""A Twin Tech material's settings as TT Lab keeps them, and how the game draws it. Nothing here needs Blender.

A material is what TT Lab saves in its data file: Newtonsoft's JSON of its MaterialData, enums as their numbers and the shaders'
vectors as the bits of their floats. The add-on shows every setting TT Lab edits, and writes back what was changed in Blender over
what the project has when the model is exported (`merge_material`), so what was changed in TT Lab meanwhile stays.

How the game draws a material comes from the decompilation (twinsanity-reversed, src/platform/ps2/renderer): every shader is a
pass over the same triangles, the GS modulating the texture (stored at half its PNG's values) with the vertex colors (0x80 is 1),
testing the alpha, blending with the frame ((A - B) * C + D) and showing the frame as it is.
"""

import copy
import math
import struct
import typing

from . import schema

EMPTY_URI = "res://EMPTY"
MAX_SHADERS = 4

ANIMATED = 0
STATIC = 1
TRACKS = ("U", "V", "Red", "Green", "Blue", "Alpha")
_TRACK_KEYS = ("TranslateX", "TranslateY", "ColorR", "ColorG", "ColorB", "ColorA")
MAX_ANIMATION_FPS = 31
DEFAULT_ANIMATION_FPS = 30

# Shader types the VU1 programs light with the chunk's lights (MaterialFactory.IsLit, 0x2dbeb0, 0x2e3b40)
LIT_TYPES = frozenset((2, 4, 12, 15, 16))
# Where a type's VU1 program reads its texture, the UVs for the rest (checked by emulating the programs). The environment maps
# (programs 0x14 and 0x1D) take the half vector of the way to the eye and the normal through the object's clip matrix, normalized:
# (0.5 + 0.5 d.x, 0.5 - 0.5 d.y). The metallic one (0x17) takes the way to the eye reflected off the normal through it, not
# normalized: (0.5 + 0.5 r.x, 0.5 + 0.5 r.y). The reflection surface (0x18) draws the frame, copied before its draw, instead of its
# texture: what's behind it, moved by its normal on the screen times the first float parameter, times its lit colors (128 is 1).
# Color only (0x13) draws the shadows' volumes into their own buffer
ENVIRONMENT_TYPES = frozenset((12, 22))
REFLECTION_TYPES = frozenset((15,))
FRAME_TYPES = frozenset((16,))
HIDDEN_TYPES = frozenset((11,))
# The follow camera's lens (g_DefaultFov, 45 degrees: the field of view blender starts there and most of the game's cameras keep it) on
# the PAL screen's 4:3 (NarrowAspect, a pixel aspect of 1), which the clip matrix scales the camera's axes by: x by cot(fov / 2) /
# aspect, y by cot(fov / 2), z by about 1 (RenderView::SetProjection). TT Lab's renderer takes the same (EnvironmentMapping)
GAME_FOV_DEGREES = 45.0
GAME_ASPECT = 4.0 / 3.0
CLIP_SCALE = (1.0 / math.tan(math.radians(GAME_FOV_DEGREES) / 2.0) / GAME_ASPECT, 1.0 / math.tan(math.radians(GAME_FOV_DEGREES) / 2.0), 1.0)
SKIN_TYPE = 4
SKY_TYPE = 10
CLOTH_TYPES = frozenset((23, 26))
FLOAT_PARAM_TYPES = frozenset((16, 17, 23, 24, 26, 28))

# Labels and descriptions of the enums' values, by the names TT Lab gives them
ENUM_LABELS: typing.Dict[str, typing.Dict[str, typing.Tuple[str, str]]] = {
    "ShaderType": {
        "StandardUnlit": ("Standard Unlit", "The texture times the vertex colors"),
        "StandardLit": ("Standard Lit", "The texture times the vertex colors lit by the chunk's lights"),
        "LitSkinnedModel": ("Lit Skinned Model", "Lit like Standard Lit, the only shader the game draws a skin's parts with right"),
        "UnlitSkydome": ("Unlit Skydome", "The sky's: drawn around the camera before the level, into a buffer of half the screen's size"),
        "ColorOnly": ("Color Only", "The shadows' volumes: positions and a color of its own drawn into the shadows' buffer (bucket 21), not the screen. The preview leaves it out"),
        "LitEnvironmentMap": ("Lit Environment Map", "Lit like Standard Lit, the texture read by the half vector of the way to the eye and the "
                              "normal on the screen's axes (it turns with the view), not by the UVs. The game's go over a lit or unlit pass, added"),
        "UiShader": ("UI", "The menus' and HUD's: sprites of the glyphs of a font page on the screen, the texture times a color per draw"),
        "LitMetallic": ("Lit Metallic", "Lit like a skin, the texture read by the way to the eye reflected off the surface on the screen's "
                        "axes, not by the UVs. Only skins give it what it reads. The game's go over a skinned pass, added"),
        "LitReflectionSurface": ("Lit Reflection Surface", "Lit, draws the frame behind it instead of its texture (copied before the draw), "
                                 "moved by its normal on the screen times the first parameter, times its lit colors (128 is 1). The preview shows "
                                 "what's behind it tinted without moving it"),
        "SHADER_17": ("Type 17", "No retail material has it"),
        "Particle": ("Particle", "Type 18"),
        "Decal": ("Decal", "Type 19"),
        "SHADER_20": ("Type 20", "No retail material has it"),
        "UnlitGlossy": ("Unlit Glossy", "Drawn like Standard Unlit, the game's glossy ones get their gloss from an Unlit Environment Map pass "
                        "after it"),
        "UnlitEnvironmentMap": ("Unlit Environment Map", "The texture read by the half vector of the way to the eye and the normal on the "
                                "screen's axes (it turns with the view), not by the UVs, times the vertex colors"),
        "UnlitClothDeformation": ("Unlit Cloth Deformation", "Unlit, the vertexes moved by waves (the type parameters' mode, speed and "
                                  "amplitude), which the preview doesn't show"),
        "ScreenCopy": ("Screen Copy", "Draws a copy of the frame, the Distortion particles' shader"),
        "SHADER_25": ("Type 25", "No retail material has it"),
        "UnlitClothDeformation2": ("Unlit Cloth Deformation 2", "Unlit, the vertexes moved by waves (the type parameters' mode, speed and an "
                                   "amplitude per axis), which the preview doesn't show"),
        "UnlitBillboard": ("Unlit Billboard", "Standard Unlit, the model turned about its up axis to face the camera"),
        "WaveDeformation": ("Wave Deformation", "The vertexes moved by a wave (speed and amplitude), no retail material has it"),
        "SHADER_30": ("Type 30", "No retail material has it"),
        "SHADER_31": ("Type 31", "No retail material has it"),
        "SHADER_32": ("Type 32", "No retail material has it"),
    },
    "AlphaBlendPresets": {
        "Mix": ("Mix", "(Cs - Cd) * As + Cd: the source over the frame by its alpha"),
        "Add": ("Add", "Cs * As + Cd: the source added to the frame by its alpha"),
        "Sub": ("Subtract", "Cd - Cs * As: the source taken off the frame by its alpha"),
        "Brighten": ("Brighten", "Cd * As + Cd: the frame brightened by the source's alpha"),
        "Darken": ("Darken", "Cd - Cd * As: the frame darkened by the source's alpha"),
        "Scale": ("Scale", "Cd * As: the frame scaled by the source's alpha"),
        "Replace": ("Replace", "Zeros in the game's table: the source as it is"),
        **{"Preset%d" % number: ("Preset %d" % number, "Zeros in the PS2 version's table: the source as it is") for number in range(7, 16)},
    },
    "AlphaTestMethod": {
        "NEVER": ("Never", "Every pixel fails"), "ALWAYS": ("Always", "Every pixel passes"),
        "LESS": ("Less", "Passes below the reference"), "LEQUAL": ("Less or Equal", "Passes at the reference or below"),
        "EQUAL": ("Equal", "Passes at the reference"), "GEQUAL": ("Greater or Equal", "Passes at the reference or above, the game's cut-outs"),
        "GREATER": ("Greater", "Passes above the reference"), "NOTEQUAL": ("Not Equal", "Passes away from the reference"),
    },
    "ProcessAfterAlphaTestFailed": {
        "KEEP": ("Keep", "A failing pixel writes nothing"), "FB_ONLY": ("Color Only", "A failing pixel writes its color but not its depth"),
        "ZB_ONLY": ("Depth Only", "A failing pixel writes its depth but not its color"),
        "RGB_ONLY": ("RGB Only", "A failing pixel writes its color but not its alpha or depth"),
    },
    "DestinationAlphaTestMode": {
        "Alpha0Pass": ("Alpha Bit 0 Passes", "Draws where the frame's alpha has its top bit clear"),
        "Alpha1Pass": ("Alpha Bit 1 Passes", "Draws where the frame's alpha has its top bit set"),
    },
    "DepthTestMethod": {
        "NEVER": ("Never", "Draws nothing"), "ALWAYS": ("Always", "Draws over everything, the preview can't show it"),
        "GEQUAL": ("Nearer or Equal", "Draws what's as near as what's drawn there or nearer, the game's materials"),
        "GREATER": ("Nearer", "Draws what's nearer than what's drawn there"),
    },
    "ShadingMethod": {
        "FLAT": ("Flat", "A triangle gets the color of its last vertex"), "GOURAND": ("Gouraud", "The vertexes' colors go across the triangle"),
    },
    "TextureCoordinatesSpecification": {
        "UV": ("UV", "Texel coordinates without perspective"), "STQ": ("STQ", "Perspective corrected coordinates, the game's materials"),
    },
    "Context": {
        "FIRST": ("First", "The GS's first set of drawing settings"), "SECOND": ("Second", "The GS's second set of drawing settings"),
    },
    "ColorSpecMethod": {
        "SOURCE": ("Source (Cs)", "The color drawn"), "FB": ("Frame (Cd)", "The frame's color"),
        "ZERO": ("Zero", "Nothing"), "RESERVED": ("Reserved", "Not a setting of the GS"),
    },
    "AlphaSpecMethod": {
        "SOURCE": ("Source Alpha (As)", "The alpha drawn"), "FB": ("Frame Alpha (Ad)", "The frame's alpha, which the preview takes for 1"),
        "FIX": ("Fixed", "The fixed alpha"), "RESERVED": ("Reserved", "Not a setting of the GS"),
    },
    "TextureFilter": {
        "NEAREST": ("Nearest", "Texels drawn as squares"), "LINEAR": ("Linear", "Texels blended into each other"),
    },
    "ZValueDrawMask": {
        "UPDATE": ("Written", "The depth gets written, what's drawn later behind it is hidden"),
        "NOT_UPDATE": ("Not Written", "The depth stays as it was, like most of the game's blended materials"),
    },
}
_SCROLL_LABELS = {
    "Disabled": ("None", "Doesn't move"),
    "FromAnimation": ("From Animation", "The animation's track sets the offset"),
    "Linear": ("Scroll", "Moves by the speed (turns a second), wrapped into one turn"),
    "LinearPlus_1": ("Sine Sway", "Sways by the sine of the phase moved by the speed"),
    "LinearPlus_2": ("Cosine Sway", "Sways by the cosine of the phase moved by the speed. The game moves V with U's"),
}
ENUM_LABELS["XScrollFormula"] = _SCROLL_LABELS
ENUM_LABELS["YScrollFormula"] = _SCROLL_LABELS


def enum_items(enum: str) -> typing.List[typing.Tuple[str, str, str, int]]:
    """Items of a Blender enum of the game's enum: its names, labels, descriptions and numbers."""
    labels = ENUM_LABELS.get(enum, {})
    return [(name, labels.get(name, (name, ""))[0], labels.get(name, ("", name))[1], value) for name, value in schema.ENUMS[enum]]


def bucket_items() -> typing.List[typing.Tuple[str, str, str, int]]:
    return [("BUCKET_%d" % bucket, "%d %s" % (bucket, name), "Render bucket %d" % bucket, bucket) for bucket, name in schema.RENDER_BUCKETS]


# Floats

def f32(value: float) -> float:
    """The value as a 32 bit float holds it."""
    try:
        return struct.unpack("<f", struct.pack("<f", value))[0]
    except OverflowError:
        return math.copysign(math.inf, value)


def float_of_bits(bits: int) -> float:
    return struct.unpack("<f", struct.pack("<I", int(bits) & 0xFFFFFFFF))[0]


def bits_of_float(value: float) -> int:
    return struct.unpack("<I", struct.pack("<f", f32(value)))[0]


def _same_float(a: float, b: float) -> bool:
    a, b = f32(a), f32(b)
    if math.isnan(a) or math.isnan(b):
        return math.isnan(a) and math.isnan(b)

    # Blender may flush denormal leftovers to 0
    return a == b or abs(a - b) <= 1e-30


def to_raw(value: float) -> int:
    """An animation track's value in the game's 4096ths."""
    return max(-0x8000, min(0x7FFF, int(round(float(value) * 4096.0))))


# Fields

class Field:
    """A setting TT Lab edits: its key in the JSON, its property in Blender and how one becomes the other."""

    def __init__(self, key: str, prop: str, kind: str, label: str, description: str = "", enum: str = "", minimum: typing.Optional[int] = None,
                 maximum: typing.Optional[int] = None, size: int = 1):
        self.key = key
        self.prop = prop
        self.kind = kind
        self.label = label
        self.description = description
        self.enum = enum
        self.minimum = minimum
        self.maximum = maximum
        self.size = size

    def shown(self, raw: typing.Any) -> typing.Any:
        """What Blender shows of the stored value."""
        kind = self.kind
        if kind == "enum":
            values = dict((value, name) for name, value in schema.ENUMS[self.enum])
            return values.get(_int(raw), schema.ENUMS[self.enum][0][0])

        if kind == "bucket":
            return "BUCKET_%d" % _int(raw) if 0 <= _int(raw) < len(schema.RENDER_BUCKETS) else "BUCKET_2"

        if kind == "switch":
            return _int(raw) != 0

        if kind == "bool":
            return bool(raw)

        if kind == "int":
            return max(self.minimum, min(self.maximum, _int(raw))) if self.minimum is not None else _int(raw)

        if kind == "uint32":
            value = _int(raw) & 0xFFFFFFFF
            return value - 0x100000000 if value >= 0x80000000 else value

        if kind == "lod_k":
            value = _int(raw) & 0xFFFF
            return (value - 0x10000 if value >= 0x8000 else value) / 16.0

        if kind == "floats":
            values = list(raw) if isinstance(raw, list) else []
            values += [0.0] * (self.size - len(values))
            return tuple(_finite(_float(value)) for value in values[:self.size])

        if kind == "bits":
            values = list(raw) if isinstance(raw, list) else []
            values += [0] * (self.size - len(values))
            return tuple(_finite(float_of_bits(_int(value))) for value in values[:self.size])

        if kind == "text":
            return str(raw) if raw is not None else ""

        if kind == "uri":
            uri = uri_of(raw)
            return "" if uri == EMPTY_URI else uri

        raise ValueError(self.kind)

    def same(self, a: typing.Any, b: typing.Any) -> bool:
        """Whether two shown values are the same, floats as the game keeps them."""
        if self.kind in ("floats", "bits"):
            return all(_same_float(x, y) for x, y in zip(a, b))

        if self.kind == "lod_k":
            return _lod_k_raw(a) == _lod_k_raw(b)

        return a == b

    def raw(self, shown: typing.Any, original: typing.Any) -> typing.Any:
        """The stored value of what Blender shows, the original's bits where it still shows them."""
        kind = self.kind
        if kind == "enum":
            names = dict(schema.ENUMS[self.enum])
            return names[shown] if shown in names else _int(original)

        if kind == "bucket":
            return int(str(shown).replace("BUCKET_", "")) if str(shown).startswith("BUCKET_") else _int(original)

        if kind == "switch":
            return 1 if shown else 0

        if kind == "bool":
            return bool(shown)

        if kind == "int":
            value = int(shown)
            return max(self.minimum, min(self.maximum, value)) if self.minimum is not None else value

        if kind == "uint32":
            return int(shown) & 0xFFFFFFFF

        if kind == "lod_k":
            return _lod_k_raw(shown)

        if kind == "floats":
            return [f32(float(value)) for value in shown]

        if kind == "bits":
            originals = list(original) if isinstance(original, list) else []
            originals += [0] * (self.size - len(originals))
            return [_int(originals[index]) if _same_float(value, _finite(float_of_bits(_int(originals[index])))) else bits_of_float(value)
                    for index, value in enumerate(shown)]

        if kind == "text":
            return ascii_name(str(shown))

        if kind == "uri":
            return {"_uri": shown or EMPTY_URI}

        raise ValueError(self.kind)


def _int(value: typing.Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def _float(value: typing.Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def _finite(value: float) -> float:
    return value if math.isfinite(value) else 0.0


def _lod_k_raw(value: float) -> int:
    return int(max(-0x8000, min(0x7FFF, round(float(value) * 16.0)))) & 0xFFFF


def ascii_name(name: str) -> str:
    """The name as the game's files keep names, a byte per character, TT Lab takes ASCII only."""
    return "".join(character if 0x20 <= ord(character) < 0x7F else "?" for character in name)


def uri_of(value: typing.Any) -> str:
    if isinstance(value, dict):
        value = value.get("_uri")

    return str(value) if value else ""


MATERIAL_FIELDS = [
    Field("Name", "game_name", "text", "Game Name", "The material's name in the game's files (ASCII)"),
    Field("DmaChainIndex", "render_bucket", "bucket", "Render Bucket",
          "Which of the game's 28 render buckets the material's draws go into, drawn in bucket order: 0 skydomes, 2 opaque scenery and "
          "objects, 3 opaque global objects, 6-19 alpha-blended (later ones draw over earlier ones), 22 particles, 24 the UI, 26 fonts. "
          "A bucket only changes when the material is drawn"),
]

SHADER_FIELDS = [
    Field("ShaderType", "shader_type", "enum", "Type", "The VU1 program drawing the pass", enum="ShaderType"),
    Field("TxtMapping", "texture_mapping", "switch", "Textured", "Whether the pass draws its texture (the GS's TME), the vertex colors alone otherwise"),
    Field("TextureId", "texture", "uri", "Texture", "The project texture the pass draws"),
    Field("TextureFilterWhenTextureIsExpanded", "texture_filter", "enum", "Filter", "How the texture is drawn larger than it is (TEX1 MMAG)",
          enum="TextureFilter"),
    Field("MethodOfSpecifyingTextureCoordinates", "texture_coordinates", "enum", "Coordinates", "How the GS takes the texture's coordinates (PRMODE FST)",
          enum="TextureCoordinatesSpecification"),
    Field("ShdMethod", "shading", "enum", "Shading", "How the vertexes' colors fill a triangle (PRMODE IIP)", enum="ShadingMethod"),
    Field("ABlending", "blending", "switch", "Blending", "Whether the pass blends with what's drawn (PRMODE ABE)"),
    Field("AlphaRegSettingsIndex", "blend_preset", "enum", "Preset", "The game's table of blend formulas, (A - B) * C + D", enum="AlphaBlendPresets"),
    Field("UseCustomAlphaRegSettings", "custom_blend", "bool", "Own Formula", "Blends by the formula below instead of a preset"),
    Field("SpecOfColA", "blend_a", "enum", "A", "(A - B) * C + D", enum="ColorSpecMethod"),
    Field("SpecOfColB", "blend_b", "enum", "B", "(A - B) * C + D", enum="ColorSpecMethod"),
    Field("SpecOfAlphaC", "blend_c", "enum", "C", "(A - B) * C + D", enum="AlphaSpecMethod"),
    Field("SpecOfColD", "blend_d", "enum", "D", "(A - B) * C + D", enum="ColorSpecMethod"),
    Field("FixedAlphaValue", "fixed_alpha", "int", "Fixed Alpha", "C when it's the fixed alpha, 128 is 1", minimum=0, maximum=255),
    Field("ATest", "alpha_test", "switch", "Alpha Test", "Whether the pass tests its pixels' alpha against the reference"),
    Field("ATestMethod", "alpha_test_method", "enum", "Passes When", "How the alpha is compared with the reference", enum="AlphaTestMethod"),
    Field("AlphaValueToBeComparedTo", "alpha_reference", "int", "Reference", "The alpha the pixels' is compared with, 128 is 1 (the game's "
          "cut-outs take 50 or more)", minimum=0, maximum=255),
    Field("ProcessMethodWhenAlphaTestFailed", "alpha_fail", "enum", "Failing Pixels", "What a pixel failing the test writes", enum="ProcessAfterAlphaTestFailed"),
    Field("DAlphaTest", "destination_alpha_test", "switch", "Frame Alpha Test", "Draws only where the frame's alpha has the top bit the mode "
          "asks for (DATE), which the preview doesn't show"),
    Field("DAlphaTestMode", "destination_alpha_mode", "enum", "Mode", "Which top bit of the frame's alpha passes", enum="DestinationAlphaTestMode"),
    Field("DepthTest", "depth_test", "enum", "Depth Test", "What the pass draws over, by depth. Skies always draw with Always", enum="DepthTestMethod"),
    Field("ZValueDrawingMask", "depth_write", "enum", "Depth", "Whether the pass writes its depth", enum="ZValueDrawMask"),
    Field("AlphaCorrectionValue", "receives_shadows", "bool", "Receives Shadows",
          "On, the GS's FBA is off and what the material draws keeps its alpha: the characters' shadows fall on it, like on every scenery "
          "material of the game. Off sets the top bit of its alpha, which keeps shadows off it: the characters that cast shadows have it off, "
          "so theirs don't darken them"),
    Field("Fog", "fog", "switch", "Fog", "The GS's fogging (PRMODE FGE)"),
    Field("ContextNum", "context", "enum", "Context", "Which of the GS's two sets of drawing settings the pass uses", enum="Context"),
    Field("AntiAliasing", "anti_aliasing", "bool", "Anti-aliasing", "The GS's antialiasing (PRMODE AA1), off in every retail material"),
    Field("LodParamK", "lod_k", "lod_k", "LOD K", "The GS's level of detail offset (TEX1 K), in mip levels: how much nearer or further than "
          "the distance says the texture's mips are picked. -4.3125 in most of the game's materials"),
    Field("LodParamL", "lod_l", "int", "LOD L", "The GS's level of detail shift (TEX1 L): how many times the distance's log2 is doubled "
          "before K is added. Only its two low bits reach the GS, 0 in the game's textured materials", minimum=0, maximum=0xFFFF),
    Field("XScrollSettings", "scroll_u", "enum", "U Scroll", "How the texture moves along U", enum="XScrollFormula"),
    Field("YScrollSettings", "scroll_v", "enum", "V Scroll", "How the texture moves along V", enum="YScrollFormula"),
    Field("UvScrollSpeed", "uv_scroll", "bits", "Scroll Phases and Speeds", "The U and V scroll's phases in turns, then their speeds in turns "
          "a second, for the Scroll and Sway settings", size=4),
    Field("AnimationDrivesColor", "animation_drives_color", "bool", "Animation Drives Color", "With an animation, its color tracks set the "
          "shader color every frame (RGB times 256, alpha times 127). Only the red byte reaches the VU1 program"),
    Field("ShaderColor", "shader_color", "bits", "Shader Color", "Only X's integer part reaches the shader's VU1 program as a byte: 0 in the "
          "retail materials, 1 in the UI's (1, 1, 1, 64); the rest have (0, 0, 0, 128)", size=4),
    Field("IntParam", "int_param", "uint32", "Mode", "The cloth deformations' mode (types 23 and 26)"),
    Field("FloatParam", "float_param", "floats", "Parameters", "The cloth deformations' speed and amplitudes (types 23 and 26), how far the "
          "reflection surface's frame moves by its normal (16), the screen copies' value (17 and 24), the waves' speed and amplitude (28)", size=4),
]

SHADER_FIELD = {field.prop: field for field in SHADER_FIELDS}
MATERIAL_FIELD = {field.prop: field for field in MATERIAL_FIELDS}

# What TT Lab's new LabShader() is, a shader added to a material
DEFAULT_SHADER: typing.Dict[str, typing.Any] = {
    "ForcedShaderName": None, "ShaderName": "StandardLit", "ShaderType": 2, "IntParam": 0, "FloatParam": [0.0, 0.0, 0.0, 0.0], "ABlending": 0,
    "AlphaRegSettingsIndex": 0, "ATest": 0, "ATestMethod": 0, "AlphaValueToBeComparedTo": 0, "ProcessMethodWhenAlphaTestFailed": 0,
    "DAlphaTest": 0, "DAlphaTestMode": 0, "DepthTest": 2, "ShdMethod": 1, "TxtMapping": 0, "MethodOfSpecifyingTextureCoordinates": 1, "Fog": 0,
    "ContextNum": 0, "UseCustomAlphaRegSettings": False, "SpecOfColA": 0, "SpecOfColB": 0, "SpecOfAlphaC": 0, "SpecOfColD": 0,
    "FixedAlphaValue": 0, "TextureFilterWhenTextureIsExpanded": 1, "AlphaCorrectionValue": True, "ZValueDrawingMask": 0, "LodParamK": 0,
    "LodParamL": 0, "TextureId": {"_uri": EMPTY_URI}, "UnusedValue": 0, "XScrollSettings": 0, "YScrollSettings": 0, "UnusedFlag": False,
    "AntiAliasing": False, "AnimationDrivesColor": False, "LeftoverVector": [0, 0, 0, 0], "ShaderColor": [0, 0, 0, 0],
    "UvScrollSpeed": [0, 0, 0, 0], "Animation": None,
}


def new_shader(shader_type: int = 2) -> typing.Dict[str, typing.Any]:
    shader = copy.deepcopy(DEFAULT_SHADER)
    shader["ShaderType"] = shader_type
    return shader


def new_material(name: str, skin: bool = False, textured: bool = True, blended: bool = False, global_package: bool = False) -> typing.Dict[str, typing.Any]:
    """A material drawn the way the game draws most of its textured models: unlit, a skin's skinned and lit and keeping shadows off it
    like the characters casting them, blended ones without writing their depth like most of the game's."""
    shader = new_shader(SKIN_TYPE if skin else 1)
    shader["TxtMapping"] = 1 if textured else 0
    shader["AlphaCorrectionValue"] = not skin
    if blended:
        shader["ABlending"] = 1
        shader["ZValueDrawingMask"] = 1

    bucket = (16 if not skin else 14) if blended else (3 if global_package else 2)
    return {"ActivatedShaders": 0, "Name": ascii_name(name), "DmaChainIndex": bucket, "Shaders": [shader]}


# Animations

class AnimationView:
    """A shader animation as Blender shows it: the frames a second, the frames the loop plays and the six tracks, each a static value
    or a value per frame."""

    def __init__(self, fps: int, frames: int, tracks: typing.Sequence[typing.Union[float, typing.Sequence[float]]]):
        self.fps = int(fps)
        self.frames = max(1, int(frames))
        self.tracks = [list(track) if isinstance(track, (list, tuple)) else float(track) for track in tracks]

    def is_animated(self, track: int) -> bool:
        return isinstance(self.tracks[track], list)

    def value_at(self, track: int, frame: int) -> float:
        values = self.tracks[track]
        if not isinstance(values, list):
            return values

        return values[min(frame, len(values) - 1)] if values else 0.0

    def key(self) -> typing.Tuple[typing.Any, ...]:
        tracks = tuple(tuple(to_raw(self.value_at(track, frame)) for frame in range(self.frames)) if self.is_animated(track) else to_raw(self.tracks[track])
                       for track in range(len(TRACKS)))
        return self.fps, self.frames, tracks

    def __eq__(self, other: object) -> bool:
        return isinstance(other, AnimationView) and self.key() == other.key()

    def __repr__(self) -> str:
        return "AnimationView(%d, %d, %r)" % (self.fps, self.frames, self.tracks)


def new_animation() -> AnimationView:
    """TT Lab's new animation: one frame without UV offset and white, every track static."""
    return AnimationView(DEFAULT_ANIMATION_FPS, 1, [0.0, 0.0, 1.0, 1.0, 1.0, 1.0])


def _track_value(transformation: typing.Any) -> float:
    if not isinstance(transformation, dict):
        return 0.0

    if "PureValue" in transformation:
        return _int(transformation["PureValue"]) / 4096.0

    return to_raw(_float(transformation.get("Value", 0.0))) / 4096.0


def animation_view(animation: typing.Any) -> typing.Optional[AnimationView]:
    """The animation of a shader's JSON the way the game plays it (AnimateShader): the first settings say which tracks are static, a
    static track takes the next static value from the settings' index, an animated one the next value of every frame. The loop plays
    the header's frame count, the frames stored when that's none of them."""
    if not isinstance(animation, dict):
        return None

    header = _int(animation.get("Header", 0))
    fps = header >> 16 & 0x1F
    stored = [frame for frame in animation.get("AnimatedTransformations") or [] if isinstance(frame, dict)]
    timed = header & 0xFFFF
    frames = timed if 0 < timed <= len(stored) else max(1, len(stored))
    settings = (animation.get("AnimationSettings") or [None])[0]
    if not isinstance(settings, dict):
        return AnimationView(fps, frames, [0.0] * len(TRACKS))

    kinds = [_int(settings.get(key, STATIC)) for key in _TRACK_KEYS]
    statics = animation.get("StaticTransformations") or []
    static_index = _int(settings.get("StaticTransformationIndex", 0))
    frame_index = _int(settings.get("AnimationTransformationIndex", 0))
    tracks: typing.List[typing.Union[float, typing.List[float]]] = []
    for track, kind in enumerate(kinds):
        before = sum(1 for other in kinds[:track] if other == kind)
        if kind == STATIC:
            index = static_index + before
            tracks.append(_track_value(statics[index]) if index < len(statics) else 0.0)
            continue

        index = frame_index + before
        values = []
        for frame in stored[:frames]:
            transforms = frame.get("Transforms") or []
            values.append(_track_value(transforms[index]) if index < len(transforms) else 0.0)

        tracks.append(values or [0.0])

    return AnimationView(fps, frames, tracks)


def animation_json(view: AnimationView, original: typing.Any = None) -> typing.Dict[str, typing.Any]:
    """The animation as TT Lab keeps it: one settings block, the static tracks' values, then a value of every animated track for each
    frame. The header's bits past the frame count and rate stay as the original had them."""
    header = _int(original.get("Header", 0)) if isinstance(original, dict) else 0
    header = header & ~0x1FFFFF | (view.fps & 0x1F) << 16 | view.frames & 0xFFFF
    kinds = [ANIMATED if view.is_animated(track) else STATIC for track in range(len(TRACKS))]
    animated = [track for track in range(len(TRACKS)) if kinds[track] == ANIMATED]
    settings = {key: kinds[track] for track, key in enumerate(_TRACK_KEYS)}
    settings.update({"StaticTransformationIndex": 0, "AnimationTransformationIndex": 0})
    return {
        "Header": header,
        "TotalFrames": view.frames,
        "TimedFrames": view.frames,
        "FramesPerSecond": view.fps & 0x1F,
        "AnimationSettings": [settings],
        "StaticTransformations": [{"PureValue": to_raw(view.tracks[track])} for track in range(len(TRACKS)) if kinds[track] == STATIC],
        "AnimatedTransformations": [{"Count": len(animated), "Transforms": [{"PureValue": to_raw(view.value_at(track, frame))} for track in animated]}
                                    for frame in range(view.frames)],
    }


# What Blender shows and what goes back

def shown_shader(shader: typing.Dict[str, typing.Any]) -> typing.Dict[str, typing.Any]:
    shown = {field.prop: field.shown(shader.get(field.key, DEFAULT_SHADER.get(field.key))) for field in SHADER_FIELDS}
    shown["animation"] = animation_view(shader.get("Animation"))
    return shown


def shown_material(material: typing.Dict[str, typing.Any]) -> typing.Dict[str, typing.Any]:
    shown = {field.prop: field.shown(material.get(field.key)) for field in MATERIAL_FIELDS}
    shown["shaders"] = [dict(shown_shader(shader), source=index) for index, shader in enumerate(_shaders(material))]
    return shown


def _shaders(material: typing.Any) -> typing.List[typing.Dict[str, typing.Any]]:
    shaders = material.get("Shaders") if isinstance(material, dict) else None
    return [shader for shader in shaders if isinstance(shader, dict)] if isinstance(shaders, list) else []


def _merge_values(fields: typing.Sequence[Field], base: typing.Dict[str, typing.Any], theirs: typing.Dict[str, typing.Any], shown: typing.Dict[str, typing.Any],
                  result: typing.Dict[str, typing.Any], rebased: typing.Dict[str, typing.Any]) -> None:
    """The values changed in Blender over the project's, a vector's components one by one. A change the project already has becomes
    part of the base, so a later change of it in TT Lab isn't taken back by Blender's."""
    for field in fields:
        if field.prop not in shown:
            continue

        original = base.get(field.key)
        current = theirs.get(field.key)
        value = shown[field.prop]
        if field.kind in ("floats", "bits"):
            base_shown = field.shown(original)
            their_shown = field.shown(current)
            base_raw = _components(field, original)
            their_raw = _components(field, current)
            new_raw = field.raw(value, original)
            changed = [not _same_float(value[index], base_shown[index]) for index in range(field.size)]
            if not any(changed):
                continue

            result[field.key] = [new_raw[index] if changed[index] else their_raw[index] for index in range(field.size)]
            applied = [changed[index] and _same_float(their_shown[index], value[index]) for index in range(field.size)]
            if any(applied):
                rebased[field.key] = [their_raw[index] if applied[index] else base_raw[index] for index in range(field.size)]

            continue

        if field.same(value, field.shown(original)):
            if field.key in theirs:
                result[field.key] = copy.deepcopy(current)

            continue

        new_raw = field.raw(value, original)
        result[field.key] = new_raw
        if field.same(field.shown(current), value):
            rebased[field.key] = copy.deepcopy(current)


def _components(field: Field, raw: typing.Any) -> typing.List[typing.Any]:
    """A vector's stored components, missing ones made of what Blender would show for them."""
    values = list(raw) if isinstance(raw, list) else []
    filler = field.raw(field.shown(None), None)
    return values[:field.size] + filler[len(values):field.size]


def merge_shader(base: typing.Dict[str, typing.Any], theirs: typing.Dict[str, typing.Any], shown: typing.Dict[str, typing.Any]
                 ) -> typing.Tuple[typing.Dict[str, typing.Any], typing.Dict[str, typing.Any]]:
    """The project's shader with what was changed in Blender, and the base with what the project already has of it."""
    result = copy.deepcopy(theirs)
    rebased: typing.Dict[str, typing.Any] = {}
    _merge_values(SHADER_FIELDS, base, theirs, shown, result, rebased)
    image = shown.get("image")
    result.pop("Image", None)
    if image is not None:
        result["TextureId"] = {"_uri": EMPTY_URI}
        result["Image"] = int(image)

    if "animation" in shown:
        view = shown["animation"]
        if view != animation_view(base.get("Animation")):
            result["Animation"] = animation_json(view, base.get("Animation")) if view is not None else None
            if view == animation_view(theirs.get("Animation")):
                rebased["Animation"] = copy.deepcopy(theirs.get("Animation"))
        else:
            result["Animation"] = copy.deepcopy(theirs.get("Animation"))

    new_base = copy.deepcopy(base)
    new_base.update(rebased)
    return result, new_base


def merge_material(base: typing.Optional[typing.Dict[str, typing.Any]], theirs: typing.Optional[typing.Dict[str, typing.Any]], shown: typing.Dict[str, typing.Any]
                   ) -> typing.Tuple[typing.Dict[str, typing.Any], typing.Dict[str, typing.Any], bool]:
    """The project's material (theirs, the base when the project doesn't have it) with what was changed in Blender since the base,
    which Blender shows; the base to compare with from now on; and whether the project has Blender's list of shaders, which is then
    the base's, Blender's shaders coming from its shaders in their order.

    A value goes into the base once the project has what Blender shows of it, the others stay: a value changed in TT Lab but not in
    Blender keeps being the project's. The shaders are matched by the base's index they came from (`source`, -1 for one added in
    Blender). While Blender has the base's shaders in their order, the project's list stays as it is with Blender's changes; shaders
    added, taken out or moved in Blender make Blender's list the material's.
    """
    base = copy.deepcopy(base) if isinstance(base, dict) else {}
    theirs = copy.deepcopy(theirs) if isinstance(theirs, dict) else copy.deepcopy(base)
    result = copy.deepcopy(theirs)
    rebased: typing.Dict[str, typing.Any] = {}
    _merge_values(MATERIAL_FIELDS, base, theirs, shown, result, rebased)
    base_shaders = _shaders(base)
    their_shaders = _shaders(theirs)
    shown_shaders = shown.get("shaders", [])
    new_base = copy.deepcopy(base)
    new_base.update(rebased)
    sources = [shader.get("source", -1) for shader in shown_shaders]
    applied = False
    if sources == list(range(len(base_shaders))):
        shaders = []
        new_base_shaders = copy.deepcopy(base_shaders)
        for index, shader in enumerate(shown_shaders):
            if index >= len(their_shaders):
                # Taken out in TT Lab
                continue

            merged, new_base_shaders[index] = merge_shader(base_shaders[index], their_shaders[index], shader)
            shaders.append(merged)

        shaders.extend(copy.deepcopy(their_shaders[len(base_shaders):]))
        new_base["Shaders"] = new_base_shaders
    elif len(their_shaders) == len(shown_shaders) and all(shows(their, shader) for their, shader in zip(their_shaders, shown_shaders)):
        shaders = copy.deepcopy(their_shaders)
        new_base["Shaders"] = copy.deepcopy(their_shaders)
        applied = True
    else:
        shaders = []
        for shader in shown_shaders:
            source = shader.get("source", -1)
            if 0 <= source < len(base_shaders):
                original = base_shaders[source]
                current = their_shaders[source] if source < len(their_shaders) else original
            else:
                original = current = new_shader()

            shaders.append(merge_shader(original, current, shader)[0])

    result["Shaders"] = shaders
    return result, new_base, applied


def shows(shader: typing.Dict[str, typing.Any], shown: typing.Dict[str, typing.Any]) -> bool:
    """Whether Blender shows the shader's values."""
    if shown.get("image") is not None:
        return False

    if any(field.prop in shown and not field.same(shown[field.prop], field.shown(shader.get(field.key))) for field in SHADER_FIELDS):
        return False

    return "animation" not in shown or shown["animation"] == animation_view(shader.get("Animation"))


def _comparable(material: typing.Dict[str, typing.Any]) -> typing.Any:
    """The material without what TT Lab works out itself."""
    material = copy.deepcopy(material)
    for shader in _shaders(material):
        for key in ("ShaderName", "DocumentName"):
            shader.pop(key, None)

    return material


def is_changed(result: typing.Dict[str, typing.Any], theirs: typing.Optional[typing.Dict[str, typing.Any]]) -> bool:
    return not isinstance(theirs, dict) or _comparable(result) != _comparable(theirs)


# How the game draws it

def blend_formula(shader: typing.Dict[str, typing.Any]) -> typing.Tuple[str, str, str, str, int]:
    """The GS's (A - B) * C + D of a shown shader: A, B and D SOURCE, FB or ZERO, C SOURCE, FB or FIX, and the fixed alpha. The game's
    presets (G_AlphaRegPresets, FUN_001daa68) are zeros past Scale: (Cs - Cs) * As + Cs, the source as it is."""
    if shader.get("custom_blend"):
        return (_specified(shader.get("blend_a")), _specified(shader.get("blend_b")), _specified(shader.get("blend_c"), True),
                _specified(shader.get("blend_d")), int(shader.get("fixed_alpha", 0)))

    return {
        "Mix": ("SOURCE", "FB", "SOURCE", "FB", 0),
        "Add": ("SOURCE", "ZERO", "SOURCE", "FB", 0),
        "Sub": ("ZERO", "SOURCE", "SOURCE", "FB", 0),
        "Brighten": ("FB", "ZERO", "SOURCE", "FB", 0),
        "Darken": ("ZERO", "FB", "SOURCE", "FB", 0),
        "Scale": ("FB", "ZERO", "SOURCE", "ZERO", 0),
    }.get(str(shader.get("blend_preset")), ("SOURCE", "SOURCE", "SOURCE", "SOURCE", 0))


def _specified(value: typing.Any, alpha: bool = False) -> str:
    # The GS's reserved settings act as nothing here
    value = str(value)
    if value == "RESERVED":
        return "FIX" if alpha else "ZERO"

    return value


class BlendFactors:
    """Cout = source * Cs + frame * Cd, each factor `alpha * c + constant` where alpha is C: the source's alpha (1 is 0x80), the
    frame's (taken for 1) or the fixed one."""

    def __init__(self, a: str, b: str, c: str, d: str, fixed: int):
        self.c = c
        self.fixed = fixed if c == "FIX" else 0
        self.source = ((a == "SOURCE") - (b == "SOURCE"), int(d == "SOURCE"))
        self.frame = ((a == "FB") - (b == "FB"), int(d == "FB"))

    def alpha(self, source_alpha: float) -> float:
        return source_alpha if self.c == "SOURCE" else 1.0 if self.c == "FB" else self.fixed / 128.0

    def apply(self, source: float, frame: float, source_alpha: float) -> float:
        alpha = self.alpha(source_alpha)
        return (alpha * self.source[0] + self.source[1]) * source + (alpha * self.frame[0] + self.frame[1]) * frame

    @property
    def subtracts(self) -> bool:
        """Whether the source is taken away from the frame, which Blender only shows as darkening what's behind."""
        return self.source[0] < 0 and self.source[1] == 0


def blend_factors(shader: typing.Dict[str, typing.Any]) -> BlendFactors:
    return BlendFactors(*blend_formula(shader))


def alpha_passes(method: str, alpha: float, reference: int) -> bool:
    """The GS's alpha test of an alpha (1 is 0x80) against the reference byte."""
    value = alpha * 128.0
    return {
        "NEVER": False, "ALWAYS": True, "LESS": value < reference, "LEQUAL": value <= reference, "EQUAL": abs(value - reference) < 0.5,
        "GEQUAL": value >= reference, "GREATER": value > reference, "NOTEQUAL": abs(value - reference) >= 0.5,
    }.get(method, True)


def fail_writes_color(fail: str) -> bool:
    return fail in ("FB_ONLY", "RGB_ONLY")


def scroll_sources(u_mode: str, v_mode: str, animated: bool) -> typing.Tuple[typing.Optional[typing.Tuple[str, int]], typing.Optional[typing.Tuple[str, int]]]:
    """What moves the texture along U and along V (UpdateShader), each a formula and the axis whose phase and speed it takes: the
    U's cosine sway moves V (retail's mistake, the V's own setting goes over it), the animated ones only with an animation."""
    formulas = {"Linear": "wrap", "LinearPlus_1": "sine", "LinearPlus_2": "cosine"}
    u = v = None
    if u_mode in ("Linear", "LinearPlus_1"):
        u = (formulas[u_mode], 0)
    elif u_mode == "LinearPlus_2":
        v = ("cosine", 0)

    if v_mode in formulas:
        v = (formulas[v_mode], 1)

    if animated and u_mode == "FromAnimation":
        u = ("animation", 0)

    if animated and v_mode == "FromAnimation":
        v = ("animation", 1)

    return u, v


def scroll_value(source: typing.Optional[typing.Tuple[str, int]], scroll: typing.Sequence[float], seconds: float,
                 animation: typing.Sequence[float] = (0.0, 0.0)) -> float:
    """The offset a source gives after that many seconds: the phase moved on by the speed (turns a second), wrapped into a turn or
    its sine or cosine."""
    if source is None:
        return 0.0

    formula, axis = source
    if formula == "animation":
        return animation[axis]

    phase = scroll[axis] + scroll[axis + 2] * seconds
    if formula == "wrap":
        return phase - math.floor(phase)

    return math.cos(phase * 2.0 * math.pi) if formula == "cosine" else math.sin(phase * 2.0 * math.pi)


def is_lit(shader_type: str) -> bool:
    return dict(schema.ENUMS["ShaderType"]).get(shader_type, 0) in LIT_TYPES


def texture_source(shader_type: str) -> str:
    """Where the type's program reads its texture: "uv", "environment", "reflection", "frame", or "hidden" when it doesn't draw on the
    screen."""
    number = type_number(shader_type)
    if number in HIDDEN_TYPES:
        return "hidden"

    if number in ENVIRONMENT_TYPES:
        return "environment"

    if number in REFLECTION_TYPES:
        return "reflection"

    return "frame" if number in FRAME_TYPES else "uv"


def type_number(shader_type: str) -> int:
    return dict(schema.ENUMS["ShaderType"]).get(shader_type, 0)


def read_when(shader: typing.Dict[str, typing.Any], prop: str) -> bool:
    """Whether the game reads the setting with the shader's others, TT Lab grays it out otherwise (LabShader's ReadWhen)."""
    shader_type = type_number(shader.get("shader_type", ""))
    if prop == "int_param":
        return shader_type in CLOTH_TYPES

    if prop == "float_param":
        return shader_type in FLOAT_PARAM_TYPES

    if prop in ("blend_a", "blend_b", "blend_c", "blend_d"):
        return bool(shader.get("custom_blend"))

    if prop == "blend_preset":
        return not shader.get("custom_blend")

    if prop == "fixed_alpha":
        return bool(shader.get("custom_blend")) and shader.get("blend_c") == "FIX"

    if prop == "destination_alpha_mode":
        return bool(shader.get("destination_alpha_test"))

    if prop == "depth_test":
        return shader_type != SKY_TYPE

    if prop == "uv_scroll":
        return shader.get("scroll_u") in ("Linear", "LinearPlus_1", "LinearPlus_2") or shader.get("scroll_v") in ("Linear", "LinearPlus_1", "LinearPlus_2")

    if prop == "animation_drives_color":
        return bool(shader.get("has_animation"))

    if prop == "shader_color":
        return not shader.get("animation_drives_color") or not shader.get("has_animation")

    return True
