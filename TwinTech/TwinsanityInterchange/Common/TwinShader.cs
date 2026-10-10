using System;
using System.ComponentModel;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinShader : ITwinSerializable
    {
        public Type ShaderType { get; set; }
        public UInt32 IntParam { get; set; }
        public Single[] FloatParam { get; set; }
        public AlphaBlending ABlending;
        public AlphaBlendPresets AlphaRegSettingsIndex;
        public AlphaTest ATest;
        public AlphaTestMethod ATestMethod;
        public byte AlphaValueToBeComparedTo;
        public ProcessAfterAlphaTestFailed ProcessMethodWhenAlphaTestFailed;
        public DestinationAlphaTest DAlphaTest;
        public DestinationAlphaTestMode DAlphaTestMode;
        public DepthTestMethod DepthTest;
        /// <summary>
        /// Never read by the game: 4 in every retail material but the UI's, which have 6.
        /// </summary>
        public byte UnusedValue;
        public ShadingMethod ShdMethod;
        public TextureMapping TxtMapping;
        public TextureCoordinatesSpecification MethodOfSpecifyingTextureCoordinates;
        public Fogging Fog;
        public Context ContextNum;
        public XScrollFormula XScrollSettings;
        public YScrollFormula YScrollSettings;
        public bool UseCustomAlphaRegSettings;
        public ColorSpecMethod SpecOfColA;
        public ColorSpecMethod SpecOfColB;
        public AlphaSpecMethod SpecOfAlphaC;
        public ColorSpecMethod SpecOfColD;
        public byte FixedAlphaValue;
        public TextureFilter TextureFilterWhenTextureIsExpanded;
        /// <summary>
        /// The settings' "no FBA" bit (56): set, the GS's FBA is off and the pixels keep their alpha; clear, their alpha's top bit is set,
        /// and the shadows' pass (bucket 21) only darkens pixels whose alpha has it clear. Set on every retail scenery material, clear on
        /// the characters that cast shadows.
        /// </summary>
        public bool AlphaCorrectionValue;
        /// <summary>
        /// Never read by the game, false in every retail material.
        /// </summary>
        public bool UnusedFlag;
        /// <summary>
        /// The GS's antialiasing (PRMODE AA1), false in every retail material.
        /// </summary>
        public bool AntiAliasing;
        public ZValueDrawMask ZValueDrawingMask;
        /// <summary>
        /// With an animation, its color track drives <see cref="ShaderColor"/> every frame (RGB times 256, alpha times 127).
        /// </summary>
        public bool AnimationDrivesColor;
        public UInt16 LodParamK { get; set; }
        public UInt16 LodParamL { get; set; }
        /// <summary>
        /// Leftover memory of the tools (bits of strings), never read but to tell shaders apart when the game merges equal ones.
        /// </summary>
        public Vector4 LeftoverVector { get; set; }
        /// <summary>
        /// Only the integer part of X reaches the shader's VU1 program as a byte in its packet: 0 in the retail materials, 1 in
        /// the UI's (whose value is (1, 1, 1, 64), the rest have (0, 0, 0, 128)).
        /// </summary>
        public Vector4 ShaderColor { get; set; }
        /// <summary>
        /// Z component contains the U/X scroll speed and W component contains the V/Y scroll speed
        /// X and Y components are for internal code usage and are used as accumulators
        /// </summary>
        public Vector4 UvScrollSpeed { get; set; }
        public UInt32 TextureId { get; set; }
        public TwinShaderAnimation Animation { get; set; }
        public TwinShader()
        {
            FloatParam = new float[4];
            LeftoverVector = new Vector4();
            ShaderColor = new Vector4();
            UvScrollSpeed = new Vector4();
            Animation = null;
        }
        public int GetLength()
        {
            int blobLen = (Animation != null) ? Animation.GetLength() : 0;
            int paramLen = (ShaderType == Type.UnlitClothDeformation) ? 12 :
                            (ShaderType == Type.UnlitClothDeformation2) ? 20 :
                            (ShaderType == Type.LitReflectionSurface || ShaderType == Type.SHADER_17 || ShaderType == Type.ScreenCopy) ? 4 :
                            (ShaderType == Type.WaveDeformation) ? 8 :
                            0;
            return 4 + paramLen + 30 + 4 + Constants.SIZE_VECTOR4 * 3 + 8 + blobLen;
        }

        public void Read(BinaryReader reader, int length)
        {
            ShaderType = (Type)reader.ReadUInt32();
            switch (ShaderType)
            {
                case Type.UnlitClothDeformation:
                    IntParam = reader.ReadUInt32();
                    FloatParam[0] = reader.ReadSingle();
                    FloatParam[1] = reader.ReadSingle();
                    break;
                case Type.UnlitClothDeformation2:
                    IntParam = reader.ReadUInt32();
                    FloatParam[0] = reader.ReadSingle();
                    FloatParam[1] = reader.ReadSingle();
                    FloatParam[2] = reader.ReadSingle();
                    FloatParam[3] = reader.ReadSingle();
                    break;
                case Type.LitReflectionSurface:
                case Type.SHADER_17:
                case Type.ScreenCopy:
                    FloatParam[0] = reader.ReadSingle();
                    break;
                case Type.WaveDeformation:
                    FloatParam[0] = reader.ReadSingle();
                    FloatParam[1] = reader.ReadSingle();
                    break;
                default:
                    break;
            }
            ABlending = (AlphaBlending)reader.ReadByte();
            AlphaRegSettingsIndex = ((Int32)reader.ReadByte()).ToEnum();
            ATest = (AlphaTest)reader.ReadByte();
            ATestMethod = (AlphaTestMethod)reader.ReadByte();
            AlphaValueToBeComparedTo = reader.ReadByte();
            ProcessMethodWhenAlphaTestFailed = (ProcessAfterAlphaTestFailed)reader.ReadByte();
            DAlphaTest = (DestinationAlphaTest)reader.ReadByte();
            DAlphaTestMode = (DestinationAlphaTestMode)reader.ReadByte();
            DepthTest = (DepthTestMethod)reader.ReadByte();
            UnusedValue = reader.ReadByte();
            ShdMethod = (ShadingMethod)reader.ReadByte();
            TxtMapping = (TextureMapping)reader.ReadByte();
            MethodOfSpecifyingTextureCoordinates = (TextureCoordinatesSpecification)reader.ReadByte();
            Fog = (Fogging)reader.ReadByte();
            ContextNum = (Context)reader.ReadByte();
            XScrollSettings = (XScrollFormula)reader.ReadByte();
            YScrollSettings = (YScrollFormula)reader.ReadByte();
            UseCustomAlphaRegSettings = reader.ReadBoolean();
            SpecOfColA = (ColorSpecMethod)reader.ReadByte();
            SpecOfColB = (ColorSpecMethod)reader.ReadByte();
            SpecOfAlphaC = (AlphaSpecMethod)reader.ReadByte();
            SpecOfColD = (ColorSpecMethod)reader.ReadByte();
            FixedAlphaValue = reader.ReadByte();
            TextureFilterWhenTextureIsExpanded = (TextureFilter)reader.ReadByte();
            AlphaCorrectionValue = reader.ReadBoolean();
            UnusedFlag = reader.ReadBoolean();
            AntiAliasing = reader.ReadBoolean();
            ZValueDrawingMask = (ZValueDrawMask)reader.ReadByte();
            AnimationDrivesColor = reader.ReadBoolean();
            var hasAnimation = reader.ReadBoolean();
            LodParamK = reader.ReadUInt16();
            LodParamL = reader.ReadUInt16();
            LeftoverVector.Read(reader, Constants.SIZE_VECTOR4);
            ShaderColor.Read(reader, Constants.SIZE_VECTOR4);
            UvScrollSpeed.Read(reader, Constants.SIZE_VECTOR4);
            TextureId = reader.ReadUInt32();
            reader.ReadUInt32(); // ShaderType
            if (hasAnimation)
            {
                Animation = new TwinShaderAnimation();
                Animation.Read(reader, 0);
            }
        }

        public void Compile()
        {
            return;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)ShaderType);
            switch (ShaderType)
            {
                case Type.UnlitClothDeformation:
                    writer.Write(IntParam);
                    writer.Write(FloatParam[0]);
                    writer.Write(FloatParam[1]);
                    break;
                case Type.UnlitClothDeformation2:
                    writer.Write(IntParam);
                    writer.Write(FloatParam[0]);
                    writer.Write(FloatParam[1]);
                    writer.Write(FloatParam[2]);
                    writer.Write(FloatParam[3]);
                    break;
                case Type.LitReflectionSurface:
                case Type.SHADER_17:
                case Type.ScreenCopy:
                    writer.Write(FloatParam[0]);
                    break;
                case Type.WaveDeformation:
                    writer.Write(FloatParam[0]);
                    writer.Write(FloatParam[1]);
                    break;
                default:
                    break;
            }
            writer.Write((byte)ABlending);
            writer.Write((byte)AlphaRegSettingsIndex);
            writer.Write((byte)ATest);
            writer.Write((byte)ATestMethod);
            writer.Write(AlphaValueToBeComparedTo);
            writer.Write((byte)ProcessMethodWhenAlphaTestFailed);
            writer.Write((byte)DAlphaTest);
            writer.Write((byte)DAlphaTestMode);
            writer.Write((byte)DepthTest);
            writer.Write(UnusedValue);
            writer.Write((byte)ShdMethod);
            writer.Write((byte)TxtMapping);
            writer.Write((byte)MethodOfSpecifyingTextureCoordinates);
            writer.Write((byte)Fog);
            writer.Write((byte)ContextNum);
            writer.Write((byte)XScrollSettings);
            writer.Write((byte)YScrollSettings);
            writer.Write(UseCustomAlphaRegSettings);
            writer.Write((byte)SpecOfColA);
            writer.Write((byte)SpecOfColB);
            writer.Write((byte)SpecOfAlphaC);
            writer.Write((byte)SpecOfColD);
            writer.Write(FixedAlphaValue);
            writer.Write((byte)TextureFilterWhenTextureIsExpanded);
            writer.Write(AlphaCorrectionValue);
            writer.Write(UnusedFlag);
            writer.Write(AntiAliasing);
            writer.Write((byte)ZValueDrawingMask);
            writer.Write(AnimationDrivesColor);
            writer.Write(Animation != null);
            writer.Write(LodParamK);
            writer.Write(LodParamL);
            LeftoverVector.Write(writer);
            ShaderColor.Write(writer);
            UvScrollSpeed.Write(writer);
            writer.Write(TextureId);
            writer.Write((UInt32)ShaderType);
            Animation?.Write(writer);
        }

        // These are mostly helper enums to help keep stuff type safe
        #region Enums
        public enum Type
        {
            // The members' descriptions document them, the inspector shows them as the type dropdown's and the activated shaders' tooltips
#pragma warning disable CS1591
            [Description("Unlit: the texture times the vertexes' colors. Most of the game's scenery and objects are drawn with it")]
            StandardUnlit = 1,
            [Description("Lit: the texture times the vertexes' colors times the light at the instance (the scenery's three strongest lights and its " +
                         "ambient), for objects' rigid models")]
            StandardLit = 2,
            [Description("Lit like StandardLit, for skins and blend skins: the only type whose program takes a skin's packets, a skin drawn with " +
                         "another type hangs the game")]
            LitSkinnedModel = 4,
            [Description("The sky's: drawn around the camera into a buffer of half the screen's size before the level, without depth. On anything " +
                         "but a sky it draws with the last sky's matrix")]
            UnlitSkydome = 10,
            [Description("The characters' shadow volumes: positions in a constant color, no texture")]
            ColorOnly = 11,
            [Description("StandardLit's color with an environment map: the picture read by where the view and the normal point, turning with the " +
                         "view, not by the mesh's UVs. Always a second shader over a lit or unlit one")]
            LitEnvironmentMap = 12,
            [Description("The UI's: sprites of a font page's glyphs")]
            UiShader = 13,
            [Description("LitSkinnedModel's lighting with its picture read by the view reflected about the normal, only ordinary skins give it what " +
                         "it needs")]
            LitMetallic = 15,
            [Description("StandardLit's color drawing the frame, copied before the draw, moved by FloatParam[0] times the normal: never its own " +
                         "texture")]
            LitReflectionSurface = 16,
            [Description("A screen copy like LitReflectionSurface: the frame copied before the draw. No PS2 material of the game has it")]
            SHADER_17 = 17,
            [Description("The particle texture pages' (the default chunk's three): the particle code draws the pages with shaders of its own, the " +
                         "Page material blend mode with this one's settings")]
            Particle = 18,
            [Description("The decals' (footfalls, ripples)")]
            Decal = 19,
            [Description("No PS2 material of the game has it")]
            SHADER_20 = 20,
            [Description("Draws exactly like StandardUnlit: the gloss of the game's materials of it is the UnlitEnvironmentMap shader after it")]
            UnlitGlossy = 21,
            [Description("An environment map, unlit: the picture read by where the view and the normal point, turning with the view, not by the " +
                         "mesh's UVs. The game's gloss")]
            UnlitEnvironmentMap = 22,
            [Description("Unlit, every vertex moved by waves (IntParam the mode, FloatParam the speed and one amplitude), vertexes at the same " +
                         "place moving together")]
            UnlitClothDeformation = 23,
            [Description("The Distortion particles' copy of the screen (FloatParam[0] its corner value). A material can't have it: the game's " +
                         "material reader makes no shader of it and reading it crashes the game")]
            ScreenCopy = 24,
            [Description("No PS2 material of the game has it")]
            SHADER_25 = 25,
            [Description("UnlitClothDeformation with an amplitude per axis")]
            UnlitClothDeformation2 = 26,
            [Description("Drawn like StandardUnlit, the model turned about its up axis to face the camera")]
            UnlitBillboard = 27,
            [Description("Moves its vertexes by waves like the cloth deformations (FloatParam[0] their speed, [1] their amplitude). A material " +
                         "can't have it: the game's material reader makes no shader of it and reading it crashes the game")]
            WaveDeformation = 28,
            [Description("No PS2 material of the game has it")]
            SHADER_30 = 30,
            [Description("No PS2 material of the game has it")]
            SHADER_31 = 31,
            [Description("No PS2 material of the game has it")]
            SHADER_32 = 32,
#pragma warning restore CS1591
        }
        public enum AlphaBlending
        {
            OFF,
            ON
        }
        public enum AlphaTest
        {
            OFF,
            ON
        }
        public enum AlphaTestMethod
        {
            NEVER = 0b000,
            ALWAYS = 0b001,
            LESS = 0b010,
            LEQUAL = 0b011,
            EQUAL = 0b100,
            GEQUAL = 0b101,
            GREATER = 0b110,
            NOTEQUAL = 0b111
        }
        public enum ProcessAfterAlphaTestFailed
        {
            KEEP = 0b00,
            FB_ONLY = 0b01,
            ZB_ONLY = 0b10,
            RGB_ONLY = 0b11
        }
        public enum DestinationAlphaTest
        {
            OFF,
            ON
        }
        public enum DestinationAlphaTestMode
        {
            Alpha0Pass = 0,
            Alpha1Pass = 1
        }
        public enum DepthTestMethod
        {
            NEVER = 0b00,
            ALWAYS = 0b01,
            GEQUAL = 0b10,
            GREATER = 0b11
        }
        public enum ShadingMethod
        {
            FLAT = 0,
            GOURAND = 1
        }
        public enum TextureMapping
        {
            OFF,
            ON
        }
        public enum TextureCoordinatesSpecification
        {
            UV,
            STQ
        }
        public enum Fogging
        {
            OFF,
            ON
        }
        public enum Context
        {
            FIRST,
            SECOND
        }
        public enum ColorSpecMethod
        {
            SOURCE = 0b00,
            FB = 0b01,
            ZERO = 0b10,
            RESERVED = 0b11
        }
        public enum AlphaSpecMethod
        {
            SOURCE = 0b00,
            FB = 0b01,
            FIX = 0b10,
            RESERVED = 0b11
        }
        /// <summary>
        /// The game's GS alpha blending presets (the PAL executable's <c>G_AlphaRegPresets</c>), Cs and As the drawn color and alpha
        /// (128 is 1), Cd what's behind
        /// </summary>
        public enum AlphaBlendPresets
        {
            /// <summary>(Cs - Cd) * As + Cd, normal blending</summary>
            Mix,
            /// <summary>Cs * As + Cd</summary>
            Add,
            /// <summary>Cd - Cs * As</summary>
            Sub,
            /// <summary>Cd * As + Cd, brightens what's behind by the alpha</summary>
            Brighten,
            /// <summary>Cd - Cd * As, darkens what's behind by the alpha</summary>
            Darken,
            /// <summary>Cd * As, what's behind times the alpha</summary>
            Scale,
            /// <summary>Past the PS2 version's presets, where its table has zeros: (Cs - Cs) * As + Cs, drawn as it is</summary>
            Replace,
            // Only the Xbox version uses these
            Preset7,
            Preset8,
            Preset9,
            Preset10,
            Preset11,
            Preset12,
            Preset13,
            Preset14,
            Preset15
        }
        public enum TextureFilter
        {
            NEAREST,
            LINEAR
        }
        public enum ZValueDrawMask
        {
            UPDATE,
            NOT_UPDATE
        }

        /// <summary>
        /// How the U coordinate moves (UpdateShader, FUN_001cd208): 1 takes the shader animation's U track, 2 scrolls it at
        /// <see cref="UvScrollSpeed"/>.Z turns a second from the phase UvScrollSpeed.X, wrapped from 0 to 1, 3 sways it by the sine
        /// of that phase and 4 by its cosine, which the game puts into V's offset instead of U's
        /// </summary>
        public enum XScrollFormula
        {
            Disabled = 0,
            FromAnimation = 0x1,
            Linear = 0x2,
            LinearPlus_1 = 0x3,
            LinearPlus_2 = 0x4,
        }

        /// <summary>
        /// How the V coordinate moves, like <see cref="XScrollFormula"/> with the animation's V track, <see cref="UvScrollSpeed"/>.W
        /// turns a second and the phase UvScrollSpeed.Y
        /// </summary>
        public enum YScrollFormula
        {
            Disabled = 0,
            FromAnimation = 0x1,
            Linear = 0x2,
            LinearPlus_1 = 0x3,
            LinearPlus_2 = 0x4,
        }
        #endregion
    }

    public static class TwinShaderEnumConverter
    {
        // Presets the game doesn't have are kept as they are so the material writes back the same
        public static AlphaBlendPresets ToEnum(this Int32 value)
        {
            return (AlphaBlendPresets)value;
        }
    }
}
