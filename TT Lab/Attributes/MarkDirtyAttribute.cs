using System;
using System.Runtime.CompilerServices;

namespace TT_Lab.Attributes;

/// <summary>
/// When property marked with this attribute is changed then IsDirty will be triggered in the ViewModel containing this property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MarkDirtyAttribute : Attribute
{
    public string MarkTarget { get; private set; }
    
    public MarkDirtyAttribute([CallerMemberName] string propertyName = "")
    {
        MarkTarget = propertyName;
    }
}