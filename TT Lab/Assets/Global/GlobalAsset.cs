using System;

namespace TT_Lab.Assets.Global;

public abstract class GlobalAsset : SerializableAsset
{
    protected override string SavePathInPackage => GlobalPath;
    
    public string GlobalPath { get; set; }

    protected GlobalAsset() { }
    
    protected GlobalAsset(UInt32 id, String name, LabURI package, Boolean needVariant, String variant) : base(id,  name, package, needVariant, variant) {}
}