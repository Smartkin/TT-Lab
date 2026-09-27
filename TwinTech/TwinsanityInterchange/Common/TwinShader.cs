using System;
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
        public byte UnkVal1;
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
        public bool AlphaCorrectionValue;
        public bool UnkFlag1;
        public bool UnkFlag2;
        public ZValueDrawMask ZValueDrawingMask;
        public bool UnkFlag3;
        public UInt16 LodParamK { get; set; }
        public UInt16 LodParamL { get; set; }
        public Vector4 UnkVector1 { get; set; }
        public Vector4 UnkVector2 { get; set; }
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
            UnkVector1 = new Vector4();
            UnkVector2 = new Vector4();
            UvScrollSpeed = new Vector4();
            Animation = null;
        }
        public int GetLength()
        {
            int blobLen = (Animation != null) ? Animation.GetLength() : 0;
            int paramLen = (ShaderType == Type.UnlitClothDeformation) ? 12 :
                            (ShaderType == Type.UnlitClothDeformation2) ? 20 :
                            (ShaderType == Type.LitReflectionSurface || ShaderType == Type.SHADER_17) ? 4 :
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
                    FloatParam[0] = reader.ReadSingle();
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
            UnkVal1 = reader.ReadByte();
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
            UnkFlag1 = reader.ReadBoolean();
            UnkFlag2 = reader.ReadBoolean();
            ZValueDrawingMask = (ZValueDrawMask)reader.ReadByte();
            UnkFlag3 = reader.ReadBoolean();
            var hasAnimation = reader.ReadBoolean();
            LodParamK = reader.ReadUInt16();
            LodParamL = reader.ReadUInt16();
            UnkVector1.Read(reader, Constants.SIZE_VECTOR4);
            UnkVector2.Read(reader, Constants.SIZE_VECTOR4);
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
                    writer.Write(FloatParam[0]);
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
            writer.Write(UnkVal1);
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
            writer.Write(UnkFlag1);
            writer.Write(UnkFlag2);
            writer.Write((byte)ZValueDrawingMask);
            writer.Write(UnkFlag3);
            writer.Write(Animation != null);
            writer.Write(LodParamK);
            writer.Write(LodParamL);
            UnkVector1.Write(writer);
            UnkVector2.Write(writer);
            UvScrollSpeed.Write(writer);
            writer.Write(TextureId);
            writer.Write((UInt32)ShaderType);
            Animation?.Write(writer);
        }

        // These are mostly helper enums to help keep stuff type safe
        #region Enums
        public enum Type
        {
            StandardUnlit = 1,
            StandardLit = 2,
            LitSkinnedModel = 4,
            UnlitSkydome = 10,
            ColorOnly = 11,
            LitEnvironmentMap = 12,
            UiShader = 13,
            LitMetallic = 15,
            LitReflectionSurface = 16,
            SHADER_17 = 17,
            Particle = 18,
            Decal = 19,
            SHADER_20 = 20,
            UnlitGlossy = 21,
            UnlitEnvironmentMap = 22,
            UnlitClothDeformation = 23,
            SHADER_25 = 25,
            UnlitClothDeformation2 = 26,
            UnlitBillboard = 27,
            SHADER_30 = 30,
            SHADER_31 = 31,
            SHADER_32 = 32,
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
        public enum AlphaBlendPresets
        {
            Mix,
            Add,
            Sub,
            Alpha,
            Zero,
            Destination,
            Source,
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

        public enum XScrollFormula
        {
            Disabled = 0,
            Linear = 0x2,
            LinearPlus_1 = 0x3,
            LinearPlus_2 = 0x4,
        }

        public enum YScrollFormula
        {
            Disabled = 0,
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
