using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Caliburn.Micro;
using Splat;
using TT_Lab.Project;

namespace TT_Lab.Assets;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class LabURI : IEquatable<LabURI>, IComparable
{
    private String _uri;

    [JsonProperty(Required = Required.Always, PropertyName = "_uri")]
    private String JsonUri
    {
        get => _uri;
        set
        {
            _uri = value;
            if (_uri != null)
            {
                ParseAndVerify();
            }
        }
    }

    private static Dictionary<string, LabURI> _labIconUris = [];
    private static string _prefix = "res://";
    private static string _global = "__GLOBAL__";
    private bool _isBuiltIn;
    private string? _package = "";
    private string? _filePathInPackage;

    public static implicit operator String(LabURI labURI) => labURI._uri;
    public static explicit operator LabURI(String uri) => new(uri);

    public LabURI()
    {
        _uri = Empty;
    }

    [JsonConstructor]
    public LabURI(String uri, bool isBuiltIn = false)
    {
        _uri = uri;
        _isBuiltIn = isBuiltIn;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (uri != null)
        {
            ParseAndVerify();
        }
    }
    
    public bool IsBuiltIn() => _isBuiltIn;
    public string GetPackageName() => _package ?? "";
    public string GetFilePathInPackage() => _filePathInPackage ?? "";

    public override String ToString() => _uri;
    public Int32 CompareTo(object? obj)
    {
        var labURI = obj as LabURI;
        if (labURI == null) return 1;

        return _uri.ToString(CultureInfo.InvariantCulture).CompareTo(labURI._uri.ToString(CultureInfo.InvariantCulture));
    }

    public static void RegisterLabIcon(string iconName) => _labIconUris[iconName] = new LabURI($"{_prefix}{_global}/{iconName}", true);
    public static LabURI GetLabIcon(string iconName) => _labIconUris[iconName];
    public static LabURI Empty => new($"{_prefix}EMPTY");
    public static LabURI EmptyMaterial => new($"{_prefix}{_global}/NULL_MATERIAL", true);
    public static LabURI BoatGuy => new($"{_prefix}{_global}/BoatGuy", true);
    public static LabURI Plane => new($"{_prefix}{_global}/Plane", true);
    public static LabURI Box => new($"{_prefix}{_global}/Box", true);
    public static LabURI Sphere => new($"{_prefix}{_global}/Sphere", true);
    public static LabURI Circle => new($"{_prefix}{_global}/Circle", true);

    public static Boolean operator ==(LabURI? labURI, LabURI? other)
    {
        if (labURI is null && other is null) return true;
        if (labURI is null) return false;
        return labURI.Equals(other);
    }

    public static Boolean operator !=(LabURI? labURI, LabURI? other)
    {
        return !(labURI == other);
    }

    public override Boolean Equals(Object? obj)
    {
        return Equals(obj as LabURI);
    }

    public Boolean Equals(LabURI? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return other._uri == _uri;
    }

    public String GetUri() { return _uri; }

    public override Int32 GetHashCode()
    {
        return _uri.GetHashCode();
    }

    private void ParseAndVerify()
    {
        var uriStringCopy = _uri[..];
        Debug.Assert(uriStringCopy.StartsWith(_prefix), $"Created URI does not start with {_prefix}");
        uriStringCopy = uriStringCopy.Replace(_prefix, "");
        _package = uriStringCopy.Substring(0, uriStringCopy.Contains('/') ? uriStringCopy.IndexOf('/') : uriStringCopy.Length);
        if (string.IsNullOrEmpty(_package))
        {
            return;
        }
        uriStringCopy = uriStringCopy[_package.Length..];
        if (string.IsNullOrEmpty(uriStringCopy))
        {
            return;
        }
        uriStringCopy = uriStringCopy[1..(uriStringCopy.LastIndexOf('/') is -1 or 0 ? uriStringCopy.Length : uriStringCopy.LastIndexOf('/'))];
            
        _filePathInPackage = uriStringCopy;
    }

    private String DebuggerDisplay
    {
        get
        {
            if (this == Empty) return "Empty";
            if (Locator.Current.GetService<ProjectManager>()!.OpenedProject == null) return _uri;
            return Locator.Current.GetService<ProjectManager>()!.OpenedProject == null ? _uri : AssetManager.Get().GetAsset(this).Name;
        }
    }
}