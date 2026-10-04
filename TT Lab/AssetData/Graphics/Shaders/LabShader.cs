using TT_Lab.Attributes.EditorParamWrappers;
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
    
    [Editable(Hint = "The cloth deformations' mode (types 23 and 26)")]
    [EditorLinkedField(typeof(ClothRead), nameof(ShaderType))]
    public UInt32 IntParam { get; set; }
    
    [Editable(Hint = "The cloth deformations' speed and amplitudes (types 23 and 26), the screen copies' corner value (16, 17 and 24), the waves' speed and amplitude (28)")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    [EditorLinkedField(typeof(FloatParamRead), nameof(ShaderType))]
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
    [EditorLinkedField(typeof(DestinationAlphaRead), nameof(DAlphaTest))]
    public DestinationAlphaTestMode DAlphaTestMode { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<DepthTestMethod>))]
    [Editable]
    [EditorLinkedField(typeof(DepthTestRead), nameof(ShaderType))]
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
    [EditorLinkedField(typeof(CustomBlendRead), nameof(UseCustomAlphaRegSettings))]
    public ColorSpecMethod SpecOfColA { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    [Editable]
    [EditorLinkedField(typeof(CustomBlendRead), nameof(UseCustomAlphaRegSettings))]
    public ColorSpecMethod SpecOfColB { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<AlphaSpecMethod>))]
    [Editable]
    [EditorLinkedField(typeof(CustomBlendRead), nameof(UseCustomAlphaRegSettings))]
    public AlphaSpecMethod SpecOfAlphaC { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ColorSpecMethod>))]
    [Editable]
    [EditorLinkedField(typeof(CustomBlendRead), nameof(UseCustomAlphaRegSettings))]
    public ColorSpecMethod SpecOfColD { get; set; }
    
    [Editable]
    [EditorLinkedField(typeof(FixedAlphaRead), nameof(UseCustomAlphaRegSettings))]
    [EditorLinkedField(typeof(FixedAlphaRead), nameof(SpecOfAlphaC))]
    public Byte FixedAlphaValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<TextureFilter>))]
    [Editable]
    public TextureFilter TextureFilterWhenTextureIsExpanded { get; set; } = TextureFilter.LINEAR;
    
    // The game's "no FBA" bit of its shader settings (bit 56). Shadows only fall where the screen's alpha has its top bit clear, which
    // FBA sets: every retail scenery material has it on, the characters casting shadows off so theirs don't darken them
    [Editable(Caption = "Receives Shadows", Hint = "On, the GS's FBA is off and what the material draws keeps its alpha: the characters' shadows fall on it, like on every scenery material of the game. Off sets the top bit of its alpha, which keeps shadows off it: the characters that cast shadows have it off, so theirs don't darken them")]
    public Boolean AlphaCorrectionValue { get; set; } = true;
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<ZValueDrawMask>))]
    [Editable]
    public ZValueDrawMask ZValueDrawingMask { get; set; } = ZValueDrawMask.UPDATE;
    
    [Editable]
    [EditorHidden]
    public UInt16 LodParamK { get; set; }

    // The game reads the halfword signed (SetShaderSettings) and the GS's K is signed fixed point in sixteenths: 65467 is -69, -4.3125
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [Editable(Caption = "LOD K", Hint = "The GS's level of detail offset (TEX1 K), in mip levels: how much nearer or further than the distance says the texture's mips are picked. -4.3125 in most of the game's materials")]
    public Single LodK
    {
        get => (Int16)LodParamK / 16.0f;
        set => LodParamK = unchecked((UInt16)(Int16)Math.Clamp(Math.Round(value * 16.0), Int16.MinValue, Int16.MaxValue));
    }
    
    [Editable(Caption = "LOD L", Hint = "The GS's level of detail shift (TEX1 L): how many times the distance's log2 is doubled before K is added. Only its two low bits reach the GS, 0 in the game's textured materials")]
    public UInt16 LodParamL { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Texture))]
    public LabURI TextureId { get; set; } = LabURI.Empty;
    
    [Editable(Caption = "Unused value", Hint = "Never read by the game: 4 in every retail material but the UI's, which have 6")]
    [EditorHidden]
    public Byte UnusedValue { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<XScrollFormula>))]
    [Editable]
    public XScrollFormula XScrollSettings { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<YScrollFormula>))]
    [Editable]
    public YScrollFormula YScrollSettings { get; set; }
    
    [Editable(Caption = "Unused flag", Hint = "Never read by the game, off in every retail material")]
    [EditorHidden]
    public Boolean UnusedFlag { get; set; }
    
    [Editable(Caption = "Anti-aliasing", Hint = "The GS's antialiasing (PRMODE AA1), off in every retail material")]
    public Boolean AntiAliasing { get; set; }
    
    [Editable(Caption = "Animation drives color", Hint = "With an animation, its color track sets the shader color every frame (RGB times 256, alpha times 127)")]
    [EditorLinkedField(typeof(AnimationColorRead), nameof(Animation))]
    public Boolean AnimationDrivesColor { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable(Caption = "Leftover vector", Hint = "Leftover memory of the tools, never read")]
    [EditorHidden]
    public Vector4 LeftoverVector { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(ShaderBinaryVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable(Caption = "Shader color", Hint = "Only X's integer part reaches the shader's VU1 program as a byte: 0 in the retail materials, 1 in the UI's (1, 1, 1, 64); the rest have (0, 0, 0, 128)")]
    [EditorLinkedField(typeof(ShaderColorRead), nameof(AnimationDrivesColor))]
    [EditorLinkedField(typeof(ShaderColorRead), nameof(Animation))]
    public Vector4 ShaderColor { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    [Newtonsoft.Json.JsonConverter(typeof(BitsVector4Converter))]
    [Editable(Caption = "UV scroll", Hint = "X and Y the U and V scroll's phases in turns, Z and W their speeds in turns a second, for the Scroll and Sway settings. The U's cosine sway moves V instead, a mistake of the game's")]
    [EditorLinkedField(typeof(ScrollRead), nameof(XScrollSettings))]
    [EditorLinkedField(typeof(ScrollRead), nameof(YScrollSettings))]
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
            case TwinShader.Type.ScreenCopy:
                writer.Write(FloatParam[0]);
                break;
            case TwinShader.Type.WaveDeformation:
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

    // What the game only reads for some shader types or settings (the decomp's shaderclasses.cpp and shadersettings.cpp), grayed out while
    // it doesn't
    private sealed class ClothRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.ShaderType is TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2;
    }

    // The cloth's speed and amplitudes, the screen copies' corner value, the waves' speed and amplitude
    private sealed class FloatParamRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.ShaderType is TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2
            or TwinShader.Type.LitReflectionSurface or TwinShader.Type.SHADER_17 or TwinShader.Type.ScreenCopy or TwinShader.Type.WaveDeformation;
    }

    private sealed class CustomBlendRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.UseCustomAlphaRegSettings;
    }

    private sealed class FixedAlphaRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.UseCustomAlphaRegSettings && owner.SpecOfAlphaC == AlphaSpecMethod.FIX;
    }

    private sealed class DestinationAlphaRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.DAlphaTest == DestinationAlphaTest.ON;
    }

    // Skies always draw with ALWAYS
    private sealed class DepthTestRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.ShaderType != TwinShader.Type.UnlitSkydome;
    }

    private sealed class ScrollRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.XScrollSettings >= XScrollFormula.Linear || owner.YScrollSettings >= YScrollFormula.Linear;
    }

    private sealed class AnimationColorRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => owner.Animation != null;
    }

    // The animation's color track sets it every frame
    private sealed class ShaderColorRead : ReadWhen<LabShader>
    {
        protected override Boolean IsRead(LabShader owner) => !owner.AnimationDrivesColor || owner.Animation == null;
    }
}