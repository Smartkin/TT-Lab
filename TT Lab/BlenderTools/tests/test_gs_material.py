import copy
import json
import math
import unittest

from fakes import load_addon_module

schema = load_addon_module("schema")
gs_material = load_addon_module("gs_material")
png = load_addon_module("png")


def shader(**values):
    result = gs_material.new_shader(1)
    result.update(values)
    return result


# A retail-like material: a textured lit pass with a blended pass over it, the UI's leftovers in its vectors (NaN bits among them)
RETAIL = {
    "ActivatedShaders": 6,
    "Name": "lambert2",
    "DmaChainIndex": 2,
    "Shaders": [
        shader(ShaderType=2, TxtMapping=1, TextureId={"_uri": "res://PS2/Texture/Wood"}, LodParamK=65467, UnusedValue=4,
               ShaderColor=[0, 0, 0, 0x43000000], UvScrollSpeed=[0x3E800000, 0, 0x3F000000, 0], LeftoverVector=[0x7FC00001, 1, 2, 3]),
        shader(ShaderType=22, TxtMapping=1, ABlending=1, AlphaRegSettingsIndex=1, ZValueDrawingMask=1, ShaderColor=[0x7FC00000, 0xFFFFFFFF, 0, 0],
               TextureId={"_uri": "res://PS2/Texture/Sky"}),
    ],
}


def animation(header_frames=2, stored=2, rate=10):
    """Like some of the game's: the values start past leftovers and a second settings block nothing reads follows."""
    return {
        "Header": rate << 16 | header_frames,
        "TotalFrames": stored,
        "AnimationSettings": [
            {"TranslateX": 0, "TranslateY": 1, "ColorR": 1, "ColorG": 1, "ColorB": 1, "ColorA": 1, "StaticTransformationIndex": 1, "AnimationTransformationIndex": 1},
            {"TranslateX": 1, "TranslateY": 1, "ColorR": 1, "ColorG": 1, "ColorB": 1, "ColorA": 1, "StaticTransformationIndex": 7, "AnimationTransformationIndex": 3},
        ],
        "StaticTransformations": [{"PureValue": raw} for raw in (9 * 4096, 2048, 4096, 4096, 4096, 4096)],
        "AnimatedTransformations": [{"Count": 2, "Transforms": [{"PureValue": 7 * 4096}, {"PureValue": frame * 1024}]} for frame in range(stored)],
    }


class EnumTests(unittest.TestCase):
    def test_every_value_of_the_games_enums_has_a_label(self):
        for field in gs_material.SHADER_FIELDS:
            if field.kind != "enum":
                continue

            items = gs_material.enum_items(field.enum)
            self.assertEqual([(name, value) for name, value in schema.ENUMS[field.enum]], [(item[0], item[3]) for item in items])
            for name, label, description, _ in items:
                self.assertIn(name, gs_material.ENUM_LABELS[field.enum], "%s.%s has no label" % (field.enum, name))
                self.assertTrue(label and description)

    def test_buckets_are_tt_labs(self):
        items = gs_material.bucket_items()
        self.assertEqual(28, len(items))
        self.assertEqual(("BUCKET_2", "2 Opaque"), items[2][:2])


class FieldTests(unittest.TestCase):
    def test_what_blender_shows_goes_back_as_it_was(self):
        for original in RETAIL["Shaders"]:
            shown = gs_material.shown_shader(original)
            for field in gs_material.SHADER_FIELDS:
                self.assertEqual(original[field.key], field.raw(shown[field.prop], original[field.key]), field.key)

    def test_values_the_game_keeps_in_bits_show_as_floats(self):
        shown = gs_material.shown_shader(RETAIL["Shaders"][0])
        self.assertEqual((0.25, 0.0, 0.5, 0.0), shown["uv_scroll"])
        self.assertEqual(-4.3125, shown["lod_k"])
        # NaNs show as 0 and stay NaN while they're left alone
        shown = gs_material.shown_shader(RETAIL["Shaders"][1])
        self.assertEqual(0.0, shown["shader_color"][0])
        field = gs_material.SHADER_FIELD["shader_color"]
        self.assertEqual([0x7FC00000, 0xFFFFFFFF, 0, 0], field.raw(shown["shader_color"], RETAIL["Shaders"][1]["ShaderColor"]))
        self.assertEqual([gs_material.bits_of_float(2.0), 0xFFFFFFFF, 0, 0], field.raw((2.0, 0.0, 0.0, 0.0), RETAIL["Shaders"][1]["ShaderColor"]))

    def test_lod_k_is_signed_sixteenths(self):
        field = gs_material.SHADER_FIELD["lod_k"]
        self.assertEqual(65467, field.raw(-4.3125, 0))
        self.assertEqual(16, field.raw(1.0, 0))
        self.assertEqual(0x7FFF, field.raw(5000.0, 0))

    def test_unsigned_words_fit_blenders_signed_ints(self):
        field = gs_material.SHADER_FIELD["int_param"]
        self.assertEqual(-1, field.shown(0xFFFFFFFF))
        self.assertEqual(0xFFFFFFFF, field.raw(-1, 0))

    def test_names_are_ascii(self):
        self.assertEqual("Caf? 2", gs_material.MATERIAL_FIELD["game_name"].raw("Café 2", ""))

    def test_an_empty_texture_is_tt_labs_empty_uri(self):
        field = gs_material.SHADER_FIELD["texture"]
        self.assertEqual("", field.shown({"_uri": "res://EMPTY"}))
        self.assertEqual({"_uri": "res://EMPTY"}, field.raw("", None))


class MergeTests(unittest.TestCase):
    def shown(self, material=RETAIL):
        return gs_material.shown_material(material)

    def test_nothing_changed_sends_nothing(self):
        result, base, _ = gs_material.merge_material(RETAIL, RETAIL, self.shown())
        self.assertFalse(gs_material.is_changed(result, RETAIL))
        self.assertEqual(RETAIL, base)

    def test_a_change_in_blender_goes_over_what_tt_lab_changed_meanwhile(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["DmaChainIndex"] = 3
        theirs["Shaders"][0]["FixedAlphaValue"] = 40
        shown = self.shown()
        shown["shaders"][0]["blending"] = True
        shown["shaders"][1]["alpha_reference"] = 50

        result, base, _ = gs_material.merge_material(RETAIL, theirs, shown)

        self.assertTrue(gs_material.is_changed(result, theirs))
        self.assertEqual(3, result["DmaChainIndex"])
        self.assertEqual(40, result["Shaders"][0]["FixedAlphaValue"])
        self.assertEqual(1, result["Shaders"][0]["ABlending"])
        self.assertEqual(50, result["Shaders"][1]["AlphaValueToBeComparedTo"])
        # Leftovers the add-on doesn't show stay as the project has them
        self.assertEqual([0x7FC00001, 1, 2, 3], result["Shaders"][0]["LeftoverVector"])
        self.assertEqual(RETAIL, base)

    def test_a_change_the_project_already_has_becomes_the_base(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["Shaders"][0]["ABlending"] = 1
        shown = self.shown()
        shown["shaders"][0]["blending"] = True
        shown["shaders"][0]["alpha_test"] = True

        result, base, _ = gs_material.merge_material(RETAIL, theirs, shown)

        self.assertEqual(1, base["Shaders"][0]["ABlending"])
        self.assertEqual(0, base["Shaders"][0]["ATest"])
        self.assertEqual(1, result["Shaders"][0]["ATest"])
        # Once TT Lab has it all, the project's is the base and later changes in TT Lab come through
        theirs["Shaders"][0]["ATest"] = 1
        result, base, _ = gs_material.merge_material(base, theirs, shown)
        self.assertFalse(gs_material.is_changed(result, theirs))
        self.assertEqual(theirs, base)
        theirs["Shaders"][0]["ABlending"] = 0
        result, _, _ = gs_material.merge_material(base, theirs, shown)
        self.assertEqual(0, result["Shaders"][0]["ABlending"])

    def test_vectors_merge_by_component(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["Shaders"][0]["UvScrollSpeed"][2] = gs_material.bits_of_float(0.75)
        shown = self.shown()
        scroll = list(shown["shaders"][0]["uv_scroll"])
        scroll[3] = 2.0
        shown["shaders"][0]["uv_scroll"] = tuple(scroll)

        result, _, _ = gs_material.merge_material(RETAIL, theirs, shown)

        self.assertEqual([0x3E800000, 0, gs_material.bits_of_float(0.75), gs_material.bits_of_float(2.0)], result["Shaders"][0]["UvScrollSpeed"])

    def test_shaders_added_or_taken_out_in_blender_make_blenders_list(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["Shaders"][1]["FixedAlphaValue"] = 9
        shown = self.shown()
        added = gs_material.shown_shader(gs_material.new_shader(4))
        added.update(source=-1, animation=None)
        shown["shaders"] = [shown["shaders"][1], added]

        result, _, _ = gs_material.merge_material(RETAIL, theirs, shown)

        self.assertEqual([22, 4], [item["ShaderType"] for item in result["Shaders"]])
        self.assertEqual(9, result["Shaders"][0]["FixedAlphaValue"])

    def test_a_value_changed_in_tt_lab_alone_stays_the_projects(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["Shaders"][1]["FixedAlphaValue"] = 77
        shown = self.shown()
        shown["shaders"][0]["blending"] = True

        result, base, _ = gs_material.merge_material(RETAIL, theirs, shown)
        # TT Lab took Blender's change, and 77 stays TT Lab's to change
        result, base, _ = gs_material.merge_material(base, result, shown)
        self.assertEqual(0, base["Shaders"][1]["FixedAlphaValue"])
        self.assertEqual(1, base["Shaders"][0]["ABlending"])
        self.assertFalse(gs_material.is_changed(gs_material.merge_material(base, base, shown)[0], base))
        theirs = copy.deepcopy(result)
        theirs["Shaders"][1]["FixedAlphaValue"] = 90
        self.assertFalse(gs_material.is_changed(gs_material.merge_material(base, theirs, shown)[0], theirs))

    def test_blenders_list_of_shaders_becomes_the_base_once_the_project_has_it(self):
        shown = self.shown()
        added = gs_material.shown_shader(gs_material.new_shader(4))
        added.update(source=-1, animation=None)
        shown["shaders"] = [shown["shaders"][1], added]
        result, base, applied = gs_material.merge_material(RETAIL, RETAIL, shown)
        self.assertFalse(applied)
        self.assertEqual(RETAIL["Shaders"], base["Shaders"])

        result, base, applied = gs_material.merge_material(base, result, shown)

        self.assertTrue(applied)
        self.assertFalse(gs_material.is_changed(result, base))
        self.assertEqual([22, 4], [item["ShaderType"] for item in base["Shaders"]])

    def test_a_shader_tt_lab_added_stays(self):
        theirs = copy.deepcopy(RETAIL)
        theirs["Shaders"].append(shader(ShaderType=12))

        result, _, _ = gs_material.merge_material(RETAIL, theirs, self.shown())

        self.assertFalse(gs_material.is_changed(result, theirs))

    def test_a_picture_made_in_blender_replaces_the_texture(self):
        shown = self.shown()
        shown["shaders"][0]["texture"] = ""
        shown["shaders"][0]["image"] = 0

        result, _, _ = gs_material.merge_material(RETAIL, RETAIL, shown)

        self.assertEqual({"_uri": "res://EMPTY"}, result["Shaders"][0]["TextureId"])
        self.assertEqual(0, result["Shaders"][0]["Image"])
        self.assertNotIn("Image", result["Shaders"][1])

    def test_the_merge_is_json(self):
        shown = self.shown()
        shown["shaders"][0]["float_param"] = (0.1, 0.0, 0.0, 0.0)
        result, _, _ = gs_material.merge_material(RETAIL, RETAIL, shown)
        self.assertEqual(result, json.loads(json.dumps(result)))
        self.assertEqual(gs_material.f32(0.1), result["Shaders"][0]["FloatParam"][0])


class AnimationTests(unittest.TestCase):
    def test_tracks_are_read_where_the_game_reads_them(self):
        view = gs_material.animation_view(animation())

        self.assertEqual((10, 2), (view.fps, view.frames))
        self.assertEqual([0.0, 0.25], view.tracks[0])
        self.assertEqual([0.5, 1.0, 1.0, 1.0, 1.0], view.tracks[1:])

    def test_the_loop_plays_the_headers_frames_while_they_are_stored(self):
        self.assertEqual(2, gs_material.animation_view(animation(header_frames=2, stored=3)).frames)
        self.assertEqual(3, gs_material.animation_view(animation(header_frames=5, stored=3)).frames)

    def test_an_unchanged_animation_keeps_its_leftovers(self):
        material = copy.deepcopy(RETAIL)
        material["Shaders"][0]["Animation"] = animation()

        result, _, _ = gs_material.merge_material(material, material, gs_material.shown_material(material))

        self.assertEqual(animation(), result["Shaders"][0]["Animation"])

    def test_a_changed_animation_is_written_as_tt_lab_reads_it(self):
        material = copy.deepcopy(RETAIL)
        material["Shaders"][0]["Animation"] = animation()
        shown = gs_material.shown_material(material)
        view = shown["shaders"][0]["animation"]
        shown["shaders"][0]["animation"] = gs_material.AnimationView(view.fps, 3, [[0.0, 0.5, 1.0], 0.5, [1.0, 0.0, 1.0], 1.0, 1.0, 1.0])

        result, _, _ = gs_material.merge_material(material, material, shown)
        written = result["Shaders"][0]["Animation"]

        self.assertEqual(10 << 16 | 3, written["Header"])
        self.assertEqual(3, written["TotalFrames"])
        self.assertEqual(shown["shaders"][0]["animation"], gs_material.animation_view(written))
        self.assertEqual([{"PureValue": 2048}, {"PureValue": 4096}, {"PureValue": 4096}, {"PureValue": 4096}], written["StaticTransformations"])
        self.assertEqual([2048, 0], [value["PureValue"] for value in written["AnimatedTransformations"][1]["Transforms"]])
        self.assertEqual([4096, 4096], [value["PureValue"] for value in written["AnimatedTransformations"][2]["Transforms"]])

    def test_an_animation_taken_away(self):
        material = copy.deepcopy(RETAIL)
        material["Shaders"][0]["Animation"] = animation()
        shown = gs_material.shown_material(material)
        shown["shaders"][0]["animation"] = None

        result, _, _ = gs_material.merge_material(material, material, shown)

        self.assertIsNone(result["Shaders"][0]["Animation"])

    def test_values_round_to_the_games_fixed_point(self):
        self.assertEqual(0x7FFF, gs_material.to_raw(100.0))
        self.assertEqual(-0x8000, gs_material.to_raw(-100.0))
        self.assertEqual(gs_material.AnimationView(30, 1, [0.0, 0.0, 1.0, 1.0, 1.0, 1.0]),
                         gs_material.AnimationView(30, 1, [0.00001, 0.0, 1.0, 1.0, 1.0, 1.0]))


class GameDrawingTests(unittest.TestCase):
    def factors(self, preset, custom=None):
        values = {"blend_preset": preset, "custom_blend": custom is not None}
        if custom is not None:
            values.update(blend_a=custom[0], blend_b=custom[1], blend_c=custom[2], blend_d=custom[3], fixed_alpha=custom[4])

        return gs_material.blend_factors(values)

    def test_presets_blend_like_the_gs(self):
        source, frame, alpha = 0.8, 0.4, 0.5
        self.assertAlmostEqual((source - frame) * alpha + frame, self.factors("Mix").apply(source, frame, alpha))
        self.assertAlmostEqual(source * alpha + frame, self.factors("Add").apply(source, frame, alpha))
        self.assertAlmostEqual(frame - source * alpha, self.factors("Sub").apply(source, frame, alpha))
        self.assertAlmostEqual(frame * alpha + frame, self.factors("Brighten").apply(source, frame, alpha))
        self.assertAlmostEqual(frame - frame * alpha, self.factors("Darken").apply(source, frame, alpha))
        self.assertAlmostEqual(frame * alpha, self.factors("Scale").apply(source, frame, alpha))
        self.assertAlmostEqual(source, self.factors("Replace").apply(source, frame, alpha))
        self.assertAlmostEqual(source, self.factors("Preset9").apply(source, frame, alpha))
        self.assertTrue(self.factors("Sub").subtracts)
        self.assertFalse(self.factors("Darken").subtracts)

    def test_an_own_formula(self):
        factors = self.factors("Mix", ("FB", "ZERO", "FIX", "SOURCE", 64))
        self.assertAlmostEqual(0.4 * 0.5 + 0.8, factors.apply(0.8, 0.4, 2.0))

    def test_the_alpha_test_compares_bytes(self):
        self.assertTrue(gs_material.alpha_passes("GEQUAL", 50 / 128.0, 50))
        self.assertFalse(gs_material.alpha_passes("GEQUAL", 49 / 128.0, 50))
        self.assertTrue(gs_material.alpha_passes("EQUAL", 50.2 / 128.0, 50))
        self.assertFalse(gs_material.alpha_passes("NEVER", 1.0, 0))
        self.assertTrue(gs_material.fail_writes_color("RGB_ONLY"))
        self.assertFalse(gs_material.fail_writes_color("ZB_ONLY"))

    def test_the_cosine_sway_of_u_moves_v(self):
        self.assertEqual((None, ("cosine", 0)), gs_material.scroll_sources("LinearPlus_2", "Disabled", False))
        self.assertEqual((None, ("wrap", 1)), gs_material.scroll_sources("LinearPlus_2", "Linear", False))
        self.assertEqual((("animation", 0), ("animation", 1)), gs_material.scroll_sources("FromAnimation", "FromAnimation", True))
        self.assertEqual((None, None), gs_material.scroll_sources("FromAnimation", "FromAnimation", False))

    def test_scrolls_move_by_turns_a_second(self):
        scroll = (0.25, 0.0, 0.5, 2.0)
        self.assertAlmostEqual(0.75, gs_material.scroll_value(("wrap", 0), scroll, 1.0))
        self.assertAlmostEqual(0.25, gs_material.scroll_value(("wrap", 0), scroll, 2.0))
        self.assertAlmostEqual(math.sin(0.5 * math.pi), gs_material.scroll_value(("sine", 0), scroll, 0.0))
        self.assertAlmostEqual(1.0, gs_material.scroll_value(("cosine", 1), scroll, 1.0))

    def test_the_lens_is_the_follow_cameras_on_four_by_three(self):
        self.assertAlmostEqual(1.0 + math.sqrt(2.0), gs_material.CLIP_SCALE[1], places=5)
        self.assertAlmostEqual(gs_material.CLIP_SCALE[1] * 0.75, gs_material.CLIP_SCALE[0], places=5)

    def test_where_types_read_their_texture(self):
        self.assertEqual("uv", gs_material.texture_source("StandardUnlit"))
        self.assertEqual("uv", gs_material.texture_source("UnlitGlossy"))
        self.assertEqual("environment", gs_material.texture_source("LitEnvironmentMap"))
        self.assertEqual("environment", gs_material.texture_source("UnlitEnvironmentMap"))
        self.assertEqual("reflection", gs_material.texture_source("LitMetallic"))
        self.assertEqual("frame", gs_material.texture_source("LitReflectionSurface"))
        self.assertEqual("hidden", gs_material.texture_source("ColorOnly"))
        self.assertTrue(gs_material.is_lit("LitSkinnedModel"))
        self.assertFalse(gs_material.is_lit("UnlitEnvironmentMap"))

    def test_settings_are_grayed_like_tt_labs(self):
        self.assertFalse(gs_material.read_when({"shader_type": "StandardLit"}, "int_param"))
        self.assertTrue(gs_material.read_when({"shader_type": "UnlitClothDeformation2"}, "int_param"))
        self.assertTrue(gs_material.read_when({"shader_type": "LitReflectionSurface"}, "float_param"))
        self.assertFalse(gs_material.read_when({"shader_type": "UnlitSkydome"}, "depth_test"))
        self.assertFalse(gs_material.read_when({"custom_blend": True, "blend_c": "SOURCE"}, "fixed_alpha"))
        self.assertTrue(gs_material.read_when({"custom_blend": True, "blend_c": "FIX"}, "fixed_alpha"))
        self.assertFalse(gs_material.read_when({"scroll_u": "FromAnimation", "scroll_v": "Disabled"}, "uv_scroll"))
        self.assertFalse(gs_material.read_when({"has_animation": True, "animation_drives_color": True}, "shader_color"))

    def test_new_materials_are_drawn_like_tt_labs(self):
        rigid = gs_material.new_material("Wood")
        skin = gs_material.new_material("Skin", skin=True, blended=True)
        self.assertEqual((1, 1, True, 2), (rigid["Shaders"][0]["ShaderType"], rigid["Shaders"][0]["TxtMapping"], rigid["Shaders"][0]["AlphaCorrectionValue"], rigid["DmaChainIndex"]))
        self.assertEqual((4, False, 1, 1, 14), (skin["Shaders"][0]["ShaderType"], skin["Shaders"][0]["AlphaCorrectionValue"], skin["Shaders"][0]["ABlending"],
                                                skin["Shaders"][0]["ZValueDrawingMask"], skin["DmaChainIndex"]))
        self.assertEqual(3, gs_material.new_material("Crate", global_package=True)["DmaChainIndex"])


class PngTests(unittest.TestCase):
    def test_pixels_come_back(self):
        pixels = bytes(range(256)) * 3
        data = png.encode(8, 24, pixels)
        self.assertTrue(data.startswith(b"\x89PNG"))
        self.assertEqual((8, 24, pixels), png.decode(data))

    def test_a_size_the_pixels_arent(self):
        with self.assertRaises(ValueError):
            png.encode(2, 2, b"\x00" * 15)


if __name__ == "__main__":
    unittest.main()
