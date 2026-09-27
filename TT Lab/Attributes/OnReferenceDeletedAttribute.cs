using System;

namespace TT_Lab.Attributes;

public enum DeletedReferenceAction
{
    /// <summary>
    /// Reference gets replaced with a placeholder asset of the deleted asset's type
    /// </summary>
    ReplaceWithPlaceholder,
    /// <summary>
    /// Reference gets replaced with an empty reference
    /// </summary>
    Clear,
    /// <summary>
    /// Reference gets removed from the collection, for collections of objects the whole object referencing the deleted asset gets removed
    /// </summary>
    Remove
}

/// <summary>
/// What happens to the property's references to an asset when that asset gets deleted, by default they get replaced with a placeholder
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class OnReferenceDeletedAttribute(DeletedReferenceAction action) : Attribute
{
    public DeletedReferenceAction Action => action;
}
