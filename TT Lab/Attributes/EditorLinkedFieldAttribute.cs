using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TT_Lab.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class EditorLinkedFieldAttribute : Attribute
{
    public EditorLinkedFieldAttribute(Type linkDataChangeAction, string linkedField)
    {
        LinkedField = linkedField;
        ActionChange = linkDataChangeAction;
        Debug.Assert(ActionChange.GetInterfaces().FirstOrDefault(ifc => ifc.FullName == typeof(IFieldChange).FullName) != null, "Data change action type must implement IFieldChange");
    }
    
    public string LinkedField { get; }
    public Type ActionChange { get; }
}