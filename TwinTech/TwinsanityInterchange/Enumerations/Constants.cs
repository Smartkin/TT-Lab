using System;

namespace Twinsanity.TwinsanityInterchange.Enumerations
{
    public static class Constants
    {
        //Root level constants
        public const Int32 LEVEL_GRAPHICS_SECTION = 11;
        public const Int32 LEVEL_CODE_SECTION = 10;
        public const Int32 LEVEL_LAYOUT_1_SECTION = 0;
        public const Int32 LEVEL_LAYOUT_2_SECTION = 1;
        public const Int32 LEVEL_LAYOUT_3_SECTION = 2;
        public const Int32 LEVEL_LAYOUT_4_SECTION = 3;
        public const Int32 LEVEL_LAYOUT_5_SECTION = 4;
        public const Int32 LEVEL_LAYOUT_6_SECTION = 5;
        public const Int32 LEVEL_LAYOUT_7_SECTION = 6;
        public const Int32 LEVEL_LAYOUT_8_SECTION = 7;
        public const Int32 LEVEL_PARTICLES_ITEM = 8;
        public const Int32 LEVEL_COLLISION_ITEM = 9;
        //Scenery root level constants
        public const Int32 SCENERY_GRAPHICS_SECTION = 6;
        public const Int32 SCENERY_SECENERY_ITEM = 0;
        public const Int32 SCENERY_UNK_1_ITEM = 1; // Unused by the game
        public const Int32 SCENERY_UNK_2_ITEM = 2; // Unused by the game
        public const Int32 SCENERY_UNK_3_ITEM = 3; // Unused by the game
        public const Int32 SCENERY_DYNAMIC_SECENERY_ITEM = 4;
        public const Int32 SCENERY_LINK_ITEM = 5;
        //Graphics section level constants
        public const Int32 GRAPHICS_TEXTURES_SECTION = 0;
        public const Int32 GRAPHICS_MATERIALS_SECTION = 1;
        public const Int32 GRAPHICS_MODELS_SECTION = 2;
        public const Int32 GRAPHICS_RIGID_MODELS_SECTION = 3;
        public const Int32 GRAPHICS_SKINS_SECTION = 4;
        public const Int32 GRAPHICS_BLEND_SKINS_SECTION = 5;
        public const Int32 GRAPHICS_MESHES_SECTION = 6;
        public const Int32 GRAPHICS_LODS_SECTION = 7;
        public const Int32 GRAPHICS_SKYDOMES_SECTION = 8;
        //Code section level constants
        public const Int32 CODE_GAME_OBJECTS_SECTION = 0;
        public const Int32 CODE_BEHAVIOURS_SECTION = 1;
        public const Int32 CODE_ANIMATIONS_SECTION = 2;
        public const Int32 CODE_OGIS_SECTION = 3;
        public const Int32 CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION = 4;
        public const Int32 CODE_UNUSED_SECTION = 5; // The resource kind the game never loads or lets go of
        public const Int32 CODE_SOUND_EFFECTS_SECTION = 6;
        public const Int32 CODE_LANG_ENG_SECTION = 7;
        public const Int32 CODE_LANG_FRE_SECTION = 8;
        public const Int32 CODE_LANG_GER_SECTION = 9;
        public const Int32 CODE_LANG_SPA_SECTION = 10;
        public const Int32 CODE_LANG_ITA_SECTION = 11;
        public const Int32 CODE_LANG_JPN_SECTION = 12;
        //Layout section level constants
        public const Int32 LAYOUT_TEMPLATES_SECTION = 0;
        public const Int32 LAYOUT_AI_POSITIONS_SECTION = 1;
        public const Int32 LAYOUT_AI_PATHS_SECTION = 2;
        public const Int32 LAYOUT_POSITIONS_SECTION = 3;
        public const Int32 LAYOUT_PATHS_SECTION = 4;
        public const Int32 LAYOUT_SURFACES_SECTION = 5;
        public const Int32 LAYOUT_INSTANCES_SECTION = 6;
        public const Int32 LAYOUT_TRIGGERS_SECTION = 7;
        public const Int32 LAYOUT_CAMERAS_SECTION = 8;
        //Sizes
        public const Int32 SIZE_RECORD = 12;
        public const Int32 SIZE_VECTOR2 = 8;
        public const Int32 SIZE_VECTOR3 = 12;
        public const Int32 SIZE_VECTOR4 = 16;
        public const Int32 SIZE_MATRIX4 = 64;
        public const Int32 SIZE_UINT16 = 2;
        public const Int32 SIZE_UINT32 = 4;
        public const Int32 SIZE_JOINT = 100;
        public const Int32 SIZE_EXIT_POINT = 72;
    }
}
