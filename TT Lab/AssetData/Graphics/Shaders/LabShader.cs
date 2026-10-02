using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Graphics;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;
using Type = System.Type;

namespace TT_Lab.AssetData.Graphics.Shaders;

[ReferencesAssets]
public class LabShader : IDocumentModel
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ShaderName => ForcedShaderName ?? (ABlending == AlphaBlending.ON ? $"{ShaderType}Transparent" : ShaderType.ToString());
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ForcedShaderName;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TwinShader.Type>))]
    [Editable]
    public TwinShader.Type ShaderType { get; set; } = TwinShader.Type.StandardLit;
    
    [Editable]
    public UInt32 IntParam { get; set; }
    
    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public Single[] FloatParam { get; set; } = new Single[4];
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaBlending>))]
    [Editable]
    public AlphaBlending ABlending { get; set; } = AlphaBlending.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaBlendPresets>))]
    [Editable]
    public AlphaBlendPresets AlphaRegSettingsIndex { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaTest>))]
    [Editable]
    public AlphaTest ATest { get; set; } = AlphaTest.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaTestMethod>))]
    [Editable]
    public AlphaTestMethod ATestMethod { get; set; }
    
    [Editable]
    public Byte AlphaValueToBeComparedTo { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ProcessAfterAlphaTestFailed>))]
    [Editable]
    public ProcessAfterAlphaTestFailed ProcessMethodWhenAlphaTestFailed { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DestinationAlphaTest>))]
    [Editable]
    public DestinationAlphaTest DAlphaTest { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DestinationAlphaTestMode>))]
    [Editable]
    public DestinationAlphaTestMode DAlphaTestMode { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DepthTestMethod>))]
    [Editable]
    public DepthTestMethod DepthTest { get; set; } = DepthTestMethod.GEQUAL;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ShadingMethod>))]
    [Editable]
    public ShadingMethod ShdMethod { get; set; } = ShadingMethod.GOURAND;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureMapping>))]
    [Editable]
    public TextureMapping TxtMapping { get; set; } = TextureMapping.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureCoordinatesSpecification>))]
    [Editable]
    public TextureCoordinatesSpecification MethodOfSpecifyingTextureCoordinates { get; set; } = TextureCoordinatesSpecification.STQ;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<Fogging>))]
    [Editable]
    public Fogging Fog { get; set; } = Fogging.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<Context>))]
    [Editable]
    public Context ContextNum { get; set; }
    
    [Editable]
    public Boolean UseCustomAlphaRegSettings { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    [Editable]
    public ColorSpecMethod SpecOfColA { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    [Editable]
    public ColorSpecMethod SpecOfColB { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaSpecMethod>))]
    [Editable]
    public AlphaSpecMethod SpecOfAlphaC { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    [Editable]
    public ColorSpecMethod SpecOfColD { get; set; }
    
    [Editable]
    public Byte FixedAlphaValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureFilter>))]
    [Editable]
    public TextureFilter TextureFilterWhenTextureIsExpanded { get; set; } = TextureFilter.LINEAR;
    
    [Editable]
    public Boolean AlphaCorrectionValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ZValueDrawMask>))]
    [Editable]
    public ZValueDrawMask ZValueDrawingMask { get; set; } = ZValueDrawMask.UPDATE;
    
    [Editable]
    public UInt16 LodParamK { get; set; }
    
    [Editable]
    public UInt16 LodParamL { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Texture))]
    public LabURI TextureId { get; set; } = LabURI.Empty;
    
    [Editable(Caption = "Unused value", Hint = "Never read by the game: 4 in every retail material but the UI's, which have 6")]
    public Byte UnusedValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<XScrollFormula>))]
    [Editable]
    public XScrollFormula XScrollSettings { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<YScrollFormula>))]
    [Editable]
    public YScrollFormula YScrollSettings { get; set; }
    
    [Editable(Caption = "Unused flag", Hint = "Never read by the game, off in every retail material")]
    public Boolean UnusedFlag { get; set; }
    
    [Editable(Caption = "Anti-aliasing", Hint = "The GS's antialiasing (PRMODE AA1), off in every retail material")]
    public Boolean AntiAliasing { get; set; }
    
    [Editable(Caption = "Animation drives color", Hint = "With an animation, its color track sets the shader color every frame (RGB times 256, alpha times 127)")]
    public Boolean AnimationDrivesColor { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable(Caption = "Leftover vector", Hint = "Leftover memory of the tools, never read")]
    public Vector4 LeftoverVector { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable(Caption = "Shader color", Hint = "Only X's integer part reaches the shader's VU1 program as a byte: 0 in the retail materials, 1 in the UI's (1, 1, 1, 64); the rest have (0, 0, 0, 128)")]
    public Vector4 ShaderColor { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable]
    public Vector4 UvScrollSpeed { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonIgnore]
    [Editable(IsConstructible = true, EditorDescType = typeof(ShaderAnimationEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top,
              Hint = "Six tracks (U, V, red, green, blue, alpha) in 1/4096ths, static or a value per frame, looping at the frames per second. The FromAnimation scroll modes take U and V as the UV offset, 'Animation drives color' the color. The viewport plays it.")]
    public TwinShaderAnimation? Animation { get; set; }

    public LabShader() { }
        
    public LabShader(IAsset owner, TwinShader twinShader)
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
        TextureId = (twinShader.TextureId == 0) ? LabURI.Empty : AssetManager.Get().GetUriByTwinId<Texture>(owner, twinShader.TextureId);
        UnusedValue = twinShader.UnusedValue;
        XScrollSettings = twinShader.XScrollSettings;
        YScrollSettings = twinShader.YScrollSettings;
        UnusedFlag = twinShader.UnusedFlag;
        AntiAliasing = twinShader.AntiAliasing;
        AnimationDrivesColor = twinShader.AnimationDrivesColor;
        LeftoverVector = CloneUtils.Clone(twinShader.LeftoverVector);
        ShaderColor = CloneUtils.Clone(twinShader.ShaderColor);
        UvScrollSpeed = CloneUtils.Clone(twinShader.UvScrollSpeed);
        Animation = CloneUtils.DeepClone(twinShader.Animation);
    }

    public string GetStringified()
    {
        var result = new StringBuilder();
        result.AppendLine(ShaderType.ToString());
        result.AppendLine(IntParam.ToString());
        result.AppendLine(FloatParam.ToString());
        result.AppendLine(ABlending.ToString());
        result.AppendLine(AlphaRegSettingsIndex.ToString());
        result.AppendLine(ATest.ToString());
        result.AppendLine(ATestMethod.ToString());
        result.AppendLine(AlphaValueToBeComparedTo.ToString());
        result.AppendLine(ProcessMethodWhenAlphaTestFailed.ToString());
        result.AppendLine(DAlphaTest.ToString());
        result.AppendLine(DAlphaTestMode.ToString());
        result.AppendLine(DepthTest.ToString());
        result.AppendLine(ShdMethod.ToString());
        result.AppendLine(TxtMapping.ToString());
        result.AppendLine(MethodOfSpecifyingTextureCoordinates.ToString());
        result.AppendLine(Fog.ToString());
        result.AppendLine(ContextNum.ToString());
        // TODO: Check if these lines are actually needed because these settings seem to be utterly unused in Twinsanity and only use presets
        // result.AppendLine(UseCustomAlphaRegSettings.ToString());
        // result.AppendLine(SpecOfColA.ToString());
        // result.AppendLine(SpecOfColB.ToString());
        // result.AppendLine(SpecOfAlphaC.ToString());
        // result.AppendLine(SpecOfColD.ToString());
        result.AppendLine(FixedAlphaValue.ToString());
        result.AppendLine(TextureFilterWhenTextureIsExpanded.ToString());
        result.AppendLine(AlphaCorrectionValue.ToString());
        result.AppendLine(ZValueDrawingMask.ToString());
        result.AppendLine(LodParamK.ToString());
        result.AppendLine(LodParamL.ToString());
        result.AppendLine(TextureId == LabURI.Empty ? "EMPTY" : AssetManager.Get().GetAsset(TextureId).GetDataHash().ToString());
        result.AppendLine(UnusedValue.ToString());
        result.AppendLine(XScrollSettings.ToString());
        result.AppendLine(YScrollSettings.ToString());
        result.AppendLine(UnusedFlag.ToString());
        result.AppendLine(AntiAliasing.ToString());
        result.AppendLine(AnimationDrivesColor.ToString());
        result.AppendLine(LeftoverVector.ToString());
        result.AppendLine(ShaderColor.ToString());
        result.AppendLine(UvScrollSpeed.ToString());

        return result.ToString();
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
        writer.Write(TextureId == LabURI.Empty ? 0U : AssetManager.Get().GetAsset(TextureId).ExportTwinID);
        writer.Write((UInt32)ShaderType);
        Animation?.Write(writer);
    }

    private class ShaderBinaryVector4Converter : System.Text.Json.Serialization.JsonConverter<Vector4>
    {
        private bool _isConvertingFromString = false;
        
        public override Vector4? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var stringified = string.Empty;
            if (reader.TokenType == JsonTokenType.String)
            {
                stringified = reader.GetString();
            }

            if (!string.IsNullOrEmpty(stringified))
            {
                _isConvertingFromString = true;
                var stringOptions = new JsonSerializerOptions();
                stringOptions.Converters.Add(this);
                var convertedToObjectString = "{ \"internalList\" : " + stringified + "}";
                var result = JsonSerializer.Deserialize<Vector4>(convertedToObjectString, stringOptions);
                _isConvertingFromString = false;
                return result;
            }
            
            reader.Read();
            if (_isConvertingFromString)
            {
                reader.Read();
                reader.Read();
            }
            
            var newUnkVec = new Vector4();
            newUnkVec.SetBinaryX(reader.GetUInt32()); reader.Read();
            newUnkVec.SetBinaryY(reader.GetUInt32()); reader.Read();
            newUnkVec.SetBinaryZ(reader.GetUInt32()); reader.Read();
            newUnkVec.SetBinaryW(reader.GetUInt32()); reader.Read();

            if (_isConvertingFromString)
            {
                reader.Read();
            }
            
            return newUnkVec;
        }

        public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.GetBinaryX());
            writer.WriteNumberValue(value.GetBinaryY());
            writer.WriteNumberValue(value.GetBinaryZ());
            writer.WriteNumberValue(value.GetBinaryW());
            writer.WriteEndArray();
        }
    }

    // The UI's shaders keep bytes in their vectors, many of them NaNs JSON can't keep, so the asset data stores their bits. The HUD's clock
    // came out upside down once its material lost them
    internal class BitsVector4Converter : Newtonsoft.Json.JsonConverter<Vector4>
    {
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, Vector4? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            value ??= new Vector4();
            writer.WriteStartArray();
            writer.WriteValue(value.GetBinaryX());
            writer.WriteValue(value.GetBinaryY());
            writer.WriteValue(value.GetBinaryZ());
            writer.WriteValue(value.GetBinaryW());
            writer.WriteEndArray();
        }

        public override Vector4? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, Vector4? existingValue, Boolean hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var bits = serializer.Deserialize<UInt32[]>(reader)!;
            var vector = new Vector4();
            vector.SetBinaryX(bits[0]);
            vector.SetBinaryY(bits[1]);
            vector.SetBinaryZ(bits[2]);
            vector.SetBinaryW(bits[3]);
            return vector;
        }
    }

    public string DocumentName => ShaderName;
}