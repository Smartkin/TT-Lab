import copy
import json
import math
import sys
import types
import unittest

from fakes import Container, Element, IdProperty, load_addon_module

schema = load_addon_module("schema")
twintech = load_addon_module("twintech")


def vector(*values):
    return [float(value) for value in values]


# Twin Tech values the way TT Lab writes them, one of every type
SAMPLES = {
    "Ogi": {"Type": "Ogi", "BoundingBoxMin": vector(-1, 0, -1, 1), "BoundingBoxMax": vector(1, 2, 1, 1)},
    "Scenery": {"Type": "Scenery", "FogColor": 2, "HasLighting": True, "UnusedByte": 18, "LightOrder": [0, 0, 0, 2, 1, 0]},
    "SceneryTreeNode": {"Type": "SceneryTreeNode", "LightsEnabler": [index % 3 == 0 for index in range(128)], "Kind": "Root", "Slot": 0,
                        "BoundsMin": vector(-100, -20, -100, 150), "BoundsMax": vector(100, 20, 100, 150), "BoundsCenter": vector(0, 0, 0, 150),
                        "BoundsHalfSize": vector(100, 20, 100, 150), "TreeDepth": 1, "SceneryTypes": [5632, 3, 5637, 3, 3, 3, 3, 3]},
    "SceneryMesh": {"Type": "SceneryMesh", "Order": 0, "Matrix": vector(2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 5, -3, 1, 1),
                    "BoundingBox": vector(-1, -2, -3, 4, 1, 2, 3, 0)},
    "SceneryLod": {"Type": "SceneryLod", "Order": 1, "Matrix": vector(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1), "LodType": "FULL", "MinDrawDistance": 10, "MaxDrawDistance": 300,
                   "ModelsDrawDistances": [50, 100, 200], "BoundingBox": []},
    "LodMesh": {"Type": "LodMesh", "Level": 2},
    "DirectionalLight": {"Type": "DirectionalLight", "Order": 0, "Color": vector(1, 1, 0.5, 1), "Intensity": 7.5, "Enabled": False, "PositionW": 1.0,
                         "BoundsMin": vector(0, 0, 0, 1), "BoundsMax": vector(0, 0, 0, 1), "Leftover": -3, "Direction": vector(0, 1, 0, 0)},
    "SpotLight": {"Type": "SpotLight", "Order": 1, "Color": vector(1, 0, 0, 1), "Intensity": 2.0, "Enabled": True, "PositionW": 1.0,
                      "BoundsMin": vector(0, 0, 0, 1), "BoundsMax": vector(0, 0, 0, 1), "ConeAngle": 18956, "FalloffAngle": 917, "AttenuationPower": 65535,
                      "SpotExponent": 3, "Direction": vector(1, 2, 3, 0), "InnerConeCosine": 0.5, "OuterConeCosine": 0.4},
    "ExitPoint": {"Type": "ExitPoint", "Id": 7},
    "Body": {"Type": "Body", "Order": 3},
    "BlendSkin": {"Type": "BlendSkin"},
    "DynamicSceneryModel": {"Type": "DynamicSceneryModel", "Order": 0, "UsesLod": True, "BoundingBoxMin": vector(-1, -1, -1, 1), "BoundingBoxMax": vector(1, 1, 1, 1)},
    "CollisionHull": {"Type": "CollisionHull"},
    "Collision": {"Type": "Collision", "UnusedVertexes": [4, 9], "UnusedPositions": vector(99, 99, 99, -1.5, 0, 2)},
    "Joint": {"Type": "Joint", "Index": 2, "Id": 255, "AdditionalAnimationRotation": vector(0, 0.25, 0, 0.97), "Detail": 1,
              "UnusedRotation": vector(0.1, 0.2, 0.3, 0.4), "LocalTranslation": vector(0, 0, -0.0, 1), "LocalRotation": vector(0, 0, 0, 0.5),
              "WorldTranslation": vector(0, 1.5, 0, 1), "InverseBindMatrix": vector(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -1.5, 0, 1)},
    "CollisionSurface": {"Type": "CollisionSurface", "Surface": "res://Global PS2/CollisionSurface/Floor"},
}

TYPES_OF = {**{t.name: schema.SCENE_TYPES for t in schema.SCENE_TYPES}, **{t.name: schema.OBJECT_TYPES for t in schema.OBJECT_TYPES},
            **{t.name: schema.BONE_TYPES for t in schema.BONE_TYPES}, **{t.name: schema.MATERIAL_TYPES for t in schema.MATERIAL_TYPES}}


def extras(sample_name, **other):
    return dict({schema.KEY: copy.deepcopy(SAMPLES[sample_name])}, **other)


class SchemaTests(unittest.TestCase):
    def test_every_sample_comes_back_the_same(self):
        for name, sample in SAMPLES.items():
            with self.subTest(name):
                types_ = TYPES_OF[name]
                type_name, values, unknown = schema.read_twin(types_, {schema.KEY: sample})
                self.assertEqual(type_name, name)
                self.assertEqual(unknown, "")
                self.assertEqual(schema.write_twin(types_, type_name, values, unknown), sample)

    # schema.json lists what TT Lab writes, its tests check their side against it
    def test_fields_are_what_tt_lab_writes(self):
        with open(schema.os.path.join(schema.os.path.dirname(schema.__file__), "schema.json"), encoding="utf-8") as file:
            listed = schema.json.load(file)["types"]

        for kind, twin_types in (("Scene", schema.SCENE_TYPES), ("Object", schema.OBJECT_TYPES), ("Bone", schema.BONE_TYPES),
                                 ("Material", schema.MATERIAL_TYPES), ("Items", schema.SUBTYPES)):
            self.assertEqual({twin_type.name: [field.key for field in twin_type.fields] for twin_type in twin_types}, listed[kind], kind)

    def test_every_field_is_in_a_sample(self):
        for name, sample in SAMPLES.items():
            twin_type = schema.find_type(TYPES_OF[name], name)
            self.assertEqual(set(sample) - {schema.TYPE_KEY}, set(twin_type.by_key), name)

    # Blender's custom properties turn lists into arrays, dictionaries into groups and booleans into integers
    def test_values_as_blender_keeps_them_are_read(self):
        sample = copy.deepcopy(SAMPLES["Scenery"])
        sample["HasLighting"] = 1
        _, values, _ = schema.read_twin(schema.OBJECT_TYPES, IdProperty({schema.KEY: IdProperty(sample)}))
        self.assertEqual(schema.write_twin(schema.OBJECT_TYPES, "Scenery", values, ""), SAMPLES["Scenery"])

        node = copy.deepcopy(SAMPLES["SceneryTreeNode"])
        node["LightsEnabler"] = [int(flag) for flag in node["LightsEnabler"]]
        _, values, _ = schema.read_twin(schema.OBJECT_TYPES, IdProperty({schema.KEY: IdProperty(node)}))
        self.assertEqual(schema.write_twin(schema.OBJECT_TYPES, "SceneryTreeNode", values, ""), SAMPLES["SceneryTreeNode"])


    def test_negative_zeros_stay(self):
        sample = dict(SAMPLES["SceneryMesh"], Matrix=vector(1, -0.0, 0, 0, -0.0, 1, 0, 0, 0, 0, 1, 0, 0, 0, -0.0, 1))
        _, values, _ = schema.read_twin(schema.OBJECT_TYPES, {schema.KEY: sample})

        written = schema.write_twin(schema.OBJECT_TYPES, "SceneryMesh", values, "")

        self.assertEqual([math.copysign(1, value) for value in written["Matrix"]], [math.copysign(1, value) for value in sample["Matrix"]])

    def test_what_the_add_on_doesnt_know_is_kept(self):
        sample = dict(SAMPLES["SceneryMesh"], Future=[1, 2])

        type_name, values, unknown = schema.read_twin(schema.OBJECT_TYPES, {schema.KEY: sample})

        self.assertEqual(json.loads(unknown), {"Future": [1, 2]})
        self.assertEqual(schema.write_twin(schema.OBJECT_TYPES, type_name, values, unknown), sample)

    def test_types_the_add_on_doesnt_know_are_kept(self):
        sample = {"Type": "Hologram", "Brightness": 3}

        type_name, values, unknown = schema.read_twin(schema.OBJECT_TYPES, {schema.KEY: sample})

        self.assertEqual(type_name, schema.NONE_TYPE)
        self.assertEqual(schema.write_twin(schema.OBJECT_TYPES, type_name, values, unknown), sample)

    def test_nothing_of_tt_labs_reads_as_nothing(self):
        self.assertEqual(schema.read_twin(schema.OBJECT_TYPES, {"Custom": 1}), (schema.NONE_TYPE, {}, ""))
        self.assertEqual(schema.read_twin(schema.OBJECT_TYPES, None), (schema.NONE_TYPE, {}, ""))
        self.assertIsNone(schema.write_twin(schema.OBJECT_TYPES, schema.NONE_TYPE, {}, ""))

    def test_missing_fields_get_the_games_defaults(self):
        _, values, _ = schema.read_twin(schema.OBJECT_TYPES, {schema.KEY: {"Type": "PointLight", "Intensity": 3.0}})

        written = schema.write_twin(schema.OBJECT_TYPES, "PointLight", values, "")

        self.assertEqual(written["Intensity"], 3.0)
        self.assertEqual(written["Color"], vector(1, 1, 1, 1))
        self.assertEqual(written["BoundsMin"], vector(0, 0, 0, 1))

    def test_numbers_of_enums_are_their_names(self):
        _, values, _ = schema.read_twin(schema.OBJECT_TYPES, {schema.KEY: {"Type": "SceneryLod", "LodType": 4097}})

        self.assertEqual(values["lod_type"], "FULL")

    def test_lights_are_ranges(self):
        self.assertEqual(schema.format_ranges([0, 1, 2, 3, 7, 9, 10]), "0-3, 7, 9-10")
        self.assertEqual(schema.parse_ranges("0-3, 7 9-10, 200"), [0, 1, 2, 3, 7, 9, 10])
        self.assertEqual(schema.format_ranges([]), "")


class ContainerTests(unittest.TestCase):
    def test_reading_fills_the_types_properties(self):
        element = Element(schema.OBJECT_TYPES)

        self.assertTrue(twintech.read_container(element.ttt, schema.OBJECT_TYPES, extras("SceneryLod")))

        self.assertEqual(element.ttt.type, "SceneryLod")
        lod = element.ttt.scenery_lod
        self.assertEqual((lod.order, lod.lod_type, lod.models_draw_distances), (1, "FULL", (50, 100, 200)))
        self.assertFalse(lod.bounding_box_stored)

    def test_edits_are_written(self):
        element = Element(schema.OBJECT_TYPES)
        twintech.read_container(element.ttt, schema.OBJECT_TYPES, extras("SceneryLod"))
        element.ttt.scenery_lod.lod_type = "COMPRESSED"
        element.ttt.scenery_lod.bounding_box = (-1.0, -1.0, -1.0, 2.0, 1.0, 1.0, 1.0, 0.0)
        element.ttt.scenery_lod.bounding_box_stored = True

        written = twintech.write_container(element.ttt, schema.OBJECT_TYPES)

        self.assertEqual(written["LodType"], "COMPRESSED")
        self.assertEqual(written["BoundingBox"], vector(-1, -1, -1, 2, 1, 1, 1, 0))

    def test_lights_are_edited_as_ranges(self):
        node = Element(schema.OBJECT_TYPES)
        twintech.read_container(node.ttt, schema.OBJECT_TYPES, extras("SceneryTreeNode"))
        self.assertEqual(node.ttt.scenery_tree_node.lights_enabler, schema.format_ranges(range(0, 128, 3)))

        node.ttt.scenery_tree_node.lights_enabler = "0-1"
        written = twintech.write_container(node.ttt, schema.OBJECT_TYPES)

        self.assertEqual(written["LightsEnabler"][:3], [True, True, False])

    def test_a_type_set_by_hand_writes_its_defaults(self):
        element = Element(schema.OBJECT_TYPES)
        element.ttt.type = "PointLight"

        written = twintech.write_container(element.ttt, schema.OBJECT_TYPES)

        self.assertEqual(written["Type"], "PointLight")
        self.assertEqual(written["Color"], vector(1, 1, 1, 1))
        self.assertEqual(written["BoundsMin"], vector(0, 0, 0, 1))

    def test_every_sample_comes_back_through_properties(self):
        for name in SAMPLES:
            with self.subTest(name):
                element = Element(TYPES_OF[name])
                twintech.read_container(element.ttt, TYPES_OF[name], extras(name))
                self.assertEqual(twintech.write_container(element.ttt, TYPES_OF[name]), SAMPLES[name])
