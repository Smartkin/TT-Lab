"""Linking a material again: Blender's changes since its snapshot over TT Lab's current values, without Blender."""

import copy
import unittest

from fakes import load_addon_module

gs_material = load_addon_module("gs_material")
link_state = load_addon_module("link_state")


def material():
    lit = gs_material.new_shader(2)
    lit.update(TxtMapping=1, TextureId={"_uri": "res://PS2/Texture/Wood"}, UvScrollSpeed=[0, 0, 0, 0])
    gloss = gs_material.new_shader(22)
    gloss.update(ABlending=1, TextureId={"_uri": "res://PS2/Texture/Sky"})
    return {"ActivatedShaders": 0, "Name": "WOOD", "DmaChainIndex": 2, "Shaders": [lit, gloss]}


class RelinkTests(unittest.TestCase):
    def test_what_each_side_changed_since_is_kept(self):
        base = material()
        theirs = copy.deepcopy(base)
        theirs["DmaChainIndex"] = 6
        shown = gs_material.shown_material(base)
        shown["shaders"][1]["alpha_reference"] = 70

        merged, sent = link_state.relink(base, True, shown, theirs)

        self.assertEqual((6, 70), (merged["DmaChainIndex"], merged["Shaders"][1]["AlphaValueToBeComparedTo"]))
        self.assertEqual([["Shaders", 1, "AlphaValueToBeComparedTo"]], sent["changed"])

    def test_nothing_changed_in_blender_takes_tt_labs(self):
        base = material()
        theirs = copy.deepcopy(base)
        theirs["Shaders"][0]["ABlending"] = 1

        merged, sent = link_state.relink(base, True, gs_material.shown_material(base), theirs)

        self.assertIsNone(sent)
        self.assertEqual(theirs, merged)

    def test_shaders_blender_moved_keep_tt_labs_values(self):
        base = material()
        theirs = copy.deepcopy(base)
        theirs["Shaders"][1]["FixedAlphaValue"] = 99
        shown = gs_material.shown_material(base)
        shown["shaders"] = [shown["shaders"][1], shown["shaders"][0]]

        merged, sent = link_state.relink(base, True, shown, theirs)

        self.assertEqual([1, 0], sent["shader_sources"])
        self.assertEqual([22, 2], [shader["ShaderType"] for shader in merged["Shaders"]])
        self.assertEqual(99, merged["Shaders"][0]["FixedAlphaValue"])

    def test_a_material_tt_lab_never_had_is_blenders(self):
        base = material()
        shown = gs_material.shown_material(base)

        merged, sent = link_state.relink(base, False, shown, {})

        self.assertIsNone(sent["changed"])
        self.assertEqual("WOOD", merged["Name"])


if __name__ == "__main__":
    unittest.main()
