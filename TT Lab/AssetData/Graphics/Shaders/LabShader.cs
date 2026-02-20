using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;
using Type = System.Type;

namespace TT_Lab.AssetData.Graphics.Shaders;

[ReferencesAssets]
public class LabShader
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ShaderName => ForcedShaderName ?? (ABlending == AlphaBlending.ON ? $"{ShaderType}Transparent" : ShaderType.ToString());
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ForcedShaderName;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TwinShader.Type>))]
    public TwinShader.Type ShaderType { get; set; } = TwinShader.Type.StandardLit;
    
    public UInt32 IntParam { get; set; }
    
    public Single[] FloatParam { get; set; } = new Single[4];
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaBlending>))]
    public AlphaBlending ABlending { get; set; } = AlphaBlending.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaBlendPresets>))]
    public AlphaBlendPresets AlphaRegSettingsIndex { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaTest>))]
    public AlphaTest ATest { get; set; } = AlphaTest.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaTestMethod>))]
    public AlphaTestMethod ATestMethod { get; set; }
    
    public Byte AlphaValueToBeComparedTo { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ProcessAfterAlphaTestFailed>))]
    public ProcessAfterAlphaTestFailed ProcessMethodWhenAlphaTestFailed { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DestinationAlphaTest>))]
    public DestinationAlphaTest DAlphaTest { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DestinationAlphaTestMode>))]
    public DestinationAlphaTestMode DAlphaTestMode { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DepthTestMethod>))]
    public DepthTestMethod DepthTest { get; set; } = DepthTestMethod.GEQUAL;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ShadingMethod>))]
    public ShadingMethod ShdMethod { get; set; } = ShadingMethod.GOURAND;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureMapping>))]
    public TextureMapping TxtMapping { get; set; } = TextureMapping.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureCoordinatesSpecification>))]
    public TextureCoordinatesSpecification MethodOfSpecifyingTextureCoordinates { get; set; } = TextureCoordinatesSpecification.STQ;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<Fogging>))]
    public Fogging Fog { get; set; } = Fogging.OFF;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<Context>))]
    public Context ContextNum { get; set; }
    
    public Boolean UseCustomAlphaRegSettings { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    public ColorSpecMethod SpecOfColA { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    public ColorSpecMethod SpecOfColB { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaSpecMethod>))]
    public AlphaSpecMethod SpecOfAlphaC { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    public ColorSpecMethod SpecOfColD { get; set; }
    
    public Byte FixedAlphaValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureFilter>))]
    public TextureFilter TextureFilterWhenTextureIsExpanded { get; set; } = TextureFilter.LINEAR;
    
    public Boolean AlphaCorrectionValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ZValueDrawMask>))]
    public ZValueDrawMask ZValueDrawingMask { get; set; } = ZValueDrawMask.UPDATE;
    
    public UInt16 LodParamK { get; set; }
    
    public UInt16 LodParamL { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public LabURI TextureId { get; set; } = LabURI.Empty;
    
    public Byte UnkVal1 { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<XScrollFormula>))]
    public XScrollFormula XScrollSettings { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<YScrollFormula>))]
    public YScrollFormula YScrollSettings { get; set; }
    
    public Boolean UnkFlag1 { get; set; }
    
    public Boolean UnkFlag2 { get; set; }
    
    public Boolean UnkFlag3 { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    public Vector4 UnkVector1 { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    public Vector4 UnkVector2 { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 UvScrollSpeed { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonIgnore]
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
        result.AppendLine(UnkVal1.ToString());
        result.AppendLine(XScrollSettings.ToString());
        result.AppendLine(YScrollSettings.ToString());
        result.AppendLine(UnkFlag1.ToString());
        result.AppendLine(UnkFlag2.ToString());
        result.AppendLine(UnkFlag3.ToString());
        result.AppendLine(UnkVector1.ToString());
        result.AppendLine(UnkVector2.ToString());
        result.AppendLine(UvScrollSpeed.ToString());

        return result.ToString();
    }

    public JsonNode GetJsonFormat()  
    {
        return JsonSerializer.SerializeToNode(this)!;
    }

    public static LabShader? GetShaderFromGltf(SharpGLTF.Schema2.Material gltfMaterial)
    {
        return gltfMaterial.Extras.Deserialize<LabShader>();
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
}