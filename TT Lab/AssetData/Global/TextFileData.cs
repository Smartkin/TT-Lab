using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global;

public class TextFileData : AbstractAssetData
{
    public TextFileData(IAsset asset) : base(asset)
    {
        Text = "";
    }

    public TextFileData(IAsset asset, String text) : this(asset)
    {
        Text = $"{text}";
    }

    [Editable(EditorType = typeof(CodeEditorViewModel))]
    public String Text { get; set; }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        throw new NotSupportedException();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        return;
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new StreamWriter(dataPath, Encoding.UTF8, new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.Create });
        fs.Write(Text);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new StreamReader(dataPath, Encoding.UTF8);
        Text = fs.ReadToEnd();
    }

    protected override void Dispose(Boolean disposing)
    {
        Text = "";
    }
}