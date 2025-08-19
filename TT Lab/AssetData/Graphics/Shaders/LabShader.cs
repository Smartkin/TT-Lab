using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace TT_Lab.AssetData.Graphics.Shaders
{
    [ReferencesAssets]
    public class LabShader
    {
        public string ShaderName => ForcedShaderName ?? (ABlending == AlphaBlending.ON ? $"{ShaderType}Transparent" : ShaderType.ToString());
        public string? ForcedShaderName;
        public TwinShader.Type ShaderType { get; set; } = TwinShader.Type.StandardLit;
        public UInt32 IntParam { get; set; }
        public Single[] FloatParam { get; set; } = new Single[4];
        public AlphaBlending ABlending { get; set; } = AlphaBlending.OFF;
        public AlphaBlendPresets AlphaRegSettingsIndex { get; set; }
        public AlphaTest ATest { get; set; } = AlphaTest.OFF;
        public AlphaTestMethod ATestMethod { get; set; }
        public Byte AlphaValueToBeComparedTo { get; set; }
        public ProcessAfterAlphaTestFailed ProcessMethodWhenAlphaTestFailed { get; set; }
        public DestinationAlphaTest DAlphaTest { get; set; }
        public DestinationAlphaTestMode DAlphaTestMode { get; set; }
        public DepthTestMethod DepthTest { get; set; } = DepthTestMethod.GEQUAL;
        public ShadingMethod ShdMethod { get; set; } = ShadingMethod.GOURAND;
        public TextureMapping TxtMapping { get; set; } = TextureMapping.OFF;

        public TextureCoordinatesSpecification MethodOfSpecifyingTextureCoordinates { get; set; } = TextureCoordinatesSpecification.STQ;
        public Fogging Fog { get; set; } = Fogging.OFF;
        public Context ContextNum { get; set; }
        public Boolean UseCustomAlphaRegSettings { get; set; }
        public ColorSpecMethod SpecOfColA { get; set; }
        public ColorSpecMethod SpecOfColB { get; set; }
        public AlphaSpecMethod SpecOfAlphaC { get; set; }
        public ColorSpecMethod SpecOfColD { get; set; }
        public Byte FixedAlphaValue { get; set; }
        public TextureFilter TextureFilterWhenTextureIsExpanded { get; set; } = TextureFilter.LINEAR;
        public Boolean AlphaCorrectionValue { get; set; }
        public ZValueDrawMask ZValueDrawingMask { get; set; } = ZValueDrawMask.UPDATE;
        public UInt16 LodParamK { get; set; }
        public UInt16 LodParamL { get; set; }
        public LabURI TextureId { get; set; } = LabURI.Empty;
        public Byte UnkVal1 { get; set; }
        public XScrollFormula XScrollSettings { get; set; }
        public YScrollFormula YScrollSettings { get; set; }
        public Boolean UnkFlag1 { get; set; }
        public Boolean UnkFlag2 { get; set; }
        public Boolean UnkFlag3 { get; set; }
        public Vector4 UnkVector1 { get; set; } = new();
        public Vector4 UnkVector2 { get; set; } = new();
        public Vector4 UvScrollSpeed { get; set; } = new();
        public TwinShaderAnimation? Animation { get; set; }

        public LabShader() { }
        
        public LabShader(LabURI package, String? variant, TwinShader twinShader)
        {
            ShaderType = twinShader.ShaderType;
            IntParam = twinShader.IntParam;
            FloatParam = twinShader.FloatParam;
            ABlending = twinShader.ABlending;
            AlphaRegSettingsIndex = twinShader.AlphaRegSettingsIndex;
            ATest = twinShader.ATest;
            ATestMethod = twinShader.ATestMethod;
            AlphaValueToBeComparedTo = twinShader.AlphaValueToBeComparedTo;
            ProcessMethodWhenAlphaTestFailed = twinShader.ProcessMethodWhenAlphaTestFailed;
            DAlphaTest = twinShader.DAlphaTest;
            DAlphaTestMode = twinShader.DAlphaTestMode;
            DepthTest = twinShader.DepthTest;
            ShdMethod = twinShader.ShdMethod;
            TxtMapping = twinShader.TxtMapping;
            MethodOfSpecifyingTextureCoordinates = twinShader.MethodOfSpecifyingTextureCoordinates;
            Fog = twinShader.Fog;
            ContextNum = twinShader.ContextNum;
            UseCustomAlphaRegSettings = twinShader.UseCustomAlphaRegSettings;
            SpecOfColA = twinShader.SpecOfColA;
            SpecOfColB = twinShader.SpecOfColB;
            SpecOfAlphaC = twinShader.SpecOfAlphaC;
            SpecOfColD = twinShader.SpecOfColD;
            FixedAlphaValue = twinShader.FixedAlphaValue;
            TextureFilterWhenTextureIsExpanded = twinShader.TextureFilterWhenTextureIsExpanded;
            AlphaCorrectionValue = twinShader.AlphaCorrectionValue;
            ZValueDrawingMask = twinShader.ZValueDrawingMask;
            LodParamK = twinShader.LodParamK;
            LodParamL = twinShader.LodParamL;
            TextureId = (twinShader.TextureId == 0) ? LabURI.Empty : AssetManager.Get().GetUri(package, typeof(Texture).Name, variant, twinShader.TextureId);
            UnkVal1 = twinShader.UnkVal1;
            XScrollSettings = twinShader.XScrollSettings;
            YScrollSettings = twinShader.YScrollSettings;
            UnkFlag1 = twinShader.UnkFlag1;
            UnkFlag2 = twinShader.UnkFlag2;
            UnkFlag3 = twinShader.UnkFlag3;
            UnkVector1 = CloneUtils.Clone(twinShader.UnkVector1);
            UnkVector2 = CloneUtils.Clone(twinShader.UnkVector2);
            UvScrollSpeed = CloneUtils.Clone(twinShader.UvScrollSpeed);
            Animation = CloneUtils.DeepClone(twinShader.Animation);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)ShaderType);
            switch (ShaderType)
            {
                case TwinShader.Type.UnlitClothDeformation:
                    writer.Write(IntParam);
                    writer.Write(FloatParam[0]);
                    writer.Write(FloatParam[1]);
                    break;
                case TwinShader.Type.UnlitClothDeformation2:
                    writer.Write(IntParam);
                    writer.Write(FloatParam[0]);
                    writer.Write(FloatParam[1]);
                    writer.Write(FloatParam[2]);
                    writer.Write(FloatParam[3]);
                    break;
                case TwinShader.Type.LitReflectionSurface:
                case TwinShader.Type.SHADER_17:
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
            writer.Write(TextureId == LabURI.Empty ? 0U : AssetManager.Get().GetAsset(TextureId).ID);
            writer.Write((UInt32)ShaderType);
            Animation?.Write(writer);
        }
    }
}
