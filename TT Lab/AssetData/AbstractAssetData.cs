using Caliburn.Micro;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Rendering;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public abstract class AbstractAssetData(IAsset owner) : IDocumentModel
{
    protected Boolean DisposedValue;
    protected IAsset Owner = owner;

    internal void SetOwner(IAsset owner)
    {
        Owner = owner;
    }

    public IAsset GetOwner()
    {
        return Owner;
    }

    // A list longer than the game takes fails the build, made before its editor stopped at the limit or edited by hand
    protected void CheckCount(string what, int count, int max)
    {
        if (count > max)
        {
            throw new InvalidOperationException($"{Owner.Alias} has {count} {what}, the game takes at most {max}");
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public virtual Boolean Disposed => DisposedValue;

    ITwinItem? twinRef = null;

    public void Load(String dataPath, JsonSerializerSettings? settings = null)
    {
        // Resolved up front and the working directory is left alone, assets load and save in parallel
        var projectPath = Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath;
        try
        {
            LoadInternal(System.IO.Path.Combine(projectPath, dataPath), settings);
        }
        catch (JsonException ex)
        {
            // Newtonsoft only says where in the file, which of thousands of files it was is what finds it
            throw new JsonSerializationException($"{dataPath} can't be read: {ex.Message}", ex);
        }
    }

    public virtual List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property) => [];

    protected virtual void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using System.IO.FileStream fs = new(dataPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        using System.IO.StreamReader reader = new(fs);
        // Read as it's parsed, OGIs keep hundreds of megabytes of animations in theirs and the whole text took several times that
        using var jsonReader = new JsonTextReader(reader);
        settings ??= new JsonSerializerSettings();
        settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
        JsonSerializer.Create(settings).Populate(jsonReader, this);
        DisposedValue = false;
    }

    public void Save(String dataPath, JsonSerializerSettings? settings = null)
    {
        if (DisposedValue)
        {
            return;
        }
        
        var assetsPath = System.IO.Path.Combine(Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath, "assets");
        SaveInternal(System.IO.Path.Combine(assetsPath, dataPath), settings);
    }

    public virtual string GetStringified()
    {
        return JsonConvert.SerializeObject(this);
    }

    /// <summary>
    /// A copy of data kept as JSON for another asset of the owner's type, made the way loading the data makes it
    /// </summary>
    public AbstractAssetData CopyFor(IAsset asset)
    {
        var copy = (AbstractAssetData)Activator.CreateInstance(GetType(), asset)!;
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(this), copy, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        return copy;
    }

    public void SaveInCurrentDirectory(String dataPath, JsonSerializerSettings? settings = null)
    {
        SaveInternal(dataPath, settings);
    }

    protected virtual void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using System.IO.FileStream fs = new(dataPath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
        using System.IO.BinaryWriter writer = new(fs);
        writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented, settings).ToCharArray());
        writer.Flush();
        writer.Close();
    }

    protected abstract void Dispose(Boolean disposing);

    public string DocumentName => Owner.Alias;

    public void Dispose()
    {
        // DO NOT change this code. Put cleanup code in 'Dispose(bool disposing)' method
        if (DisposedValue) return;

        DisposedValue = true;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected void SetTwinItem(ITwinItem item)
    {
        twinRef = item;
    }

    protected bool IsTwinItemValid()
    {
        return twinRef != null;
    }

    protected T GetTwinItem<T>() where T : ITwinItem
    {
        Debug.Assert(twinRef != null && twinRef.GetType().IsAssignableTo(typeof(T)), $"Twin ref must be set to a valid item that's assignable to {typeof(T).Name}");
        return (T)twinRef;
    }

    /// <summary>
    /// Casts the data to a specific data type
    /// </summary>
    /// <typeparam name="T">Type of desired data</typeparam>
    /// <returns>Asset data of a specific type</returns>
    public T To<T>() where T : AbstractAssetData
    {
        Debug.Assert(GetType().IsAssignableFrom(typeof(T)), $"Attempted to cast to an illegal type {typeof(T).Name}");
        return (T)this;
    }

    public abstract void Import(LabURI package, String? variant, Int32? layoutId);

    public abstract ITwinItem Export(ITwinItemFactory factory);

    public virtual ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id, int? layoutId = null)
    {
        if (section.ContainsItem(id))
        {
            return null;
        }

        var item = Export(factory);
        section.AddItem(item);
        return item;
    }

    public void NullifyReference()
    {
        twinRef = null;
    }
}