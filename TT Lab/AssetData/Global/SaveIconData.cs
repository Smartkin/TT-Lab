using System;
using System.Collections.Generic;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Newtonsoft.Json;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global;

/// <summary>
/// The PS2 memory card icon saves get (Startup\Crash.ico), kept as a TT Lab model file (<see cref="SaveIconTlm"/>) the Blender add-on edits
/// </summary>
public class SaveIconData : AbstractAssetData
{
    public SaveIconData(IAsset asset) : base(asset)
    {
        Icon = new PS2SaveIcon();
    }

    public SaveIconData(IAsset asset, Byte[] iconData) : this(asset)
    {
        Icon = SaveIconTlm.FromBytes(iconData);
    }

    /// <summary>
    /// The icon as the console's browser has it
    /// </summary>
    [Editable(Caption = "Animation", EditorDescType = typeof(SaveIconAnimationEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top,
              Hint = "The icon's shapes blended by their weights over its frames, which the console's browser plays 60 a second times the icon's speed, over and over. Made in Blender: shape keys with their values keyed")]
    public PS2SaveIcon Icon { get; set; }

    public Byte[] ToIco() => SaveIconTlm.ToBytes(Icon);

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        return Icon;
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var context = viewportContext.RenderContext;
        var preview = new SaveIconPreview(context, Owner.URI, Icon);
        var (min, max) = SaveIconPreview.GetBounds(Icon);
        var editableObject = new EditableObject(context, preview, "SAVE_ICON_PREVIEW_EDITABLE", min, max - min)
        {
            IsSelectable = false
        };
        return [new ViewportObject(editableObject, $"SAVE_ICON_PREVIEW_{property.Path}", property, preview)];
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        SaveIconTlm.Write(Owner.Name, Icon).Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        Icon = SaveIconTlm.Read(TlmFile.Load(dataPath));
        DisposedValue = false;
    }

    protected override void Dispose(Boolean disposing)
    {
        Icon = new PS2SaveIcon();
    }
}
