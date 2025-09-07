using Caliburn.Micro;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public abstract class AbstractAssetData(IAsset owner) : IDisposable
{
    protected Boolean DisposedValue;
    protected IAsset Owner = owner;

    public virtual Boolean Disposed => DisposedValue;

    ITwinItem? twinRef = null;

    public void Load(String dataPath, JsonSerializerSettings? settings = null)
    {
        var workingDirectory = System.IO.Directory.GetCurrentDirectory();
        System.IO.Directory.SetCurrentDirectory(IoC.Get<ProjectManager>().OpenedProject!.ProjectPath);
        LoadInternal(dataPath, settings);
        System.IO.Directory.SetCurrentDirectory(workingDirectory);
    }

    protected virtual void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using System.IO.FileStream fs = new(dataPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        using System.IO.StreamReader reader = new(fs);
        settings ??= new JsonSerializerSettings();
        settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            
        JsonConvert.PopulateObject(value: reader.ReadToEnd(), target: this, settings);
        DisposedValue = false;
    }

    public void Save(String dataPath, JsonSerializerSettings? settings = null)
    {
        var workingDirectory = System.IO.Directory.GetCurrentDirectory();
        System.IO.Directory.SetCurrentDirectory($"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets");
        SaveInternal(dataPath, settings);
        System.IO.Directory.SetCurrentDirectory(workingDirectory);
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

    public void Dispose()
    {
        // DO NOT change this code. Put cleanup code in 'Dispose(bool disposing)' method
        if (DisposedValue) return;

        Dispose(disposing: true);
        DisposedValue = true;
        GC.SuppressFinalize(this);
    }

    protected void SetTwinItem(ITwinItem item)
    {
        twinRef = item;
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

    public virtual ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
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