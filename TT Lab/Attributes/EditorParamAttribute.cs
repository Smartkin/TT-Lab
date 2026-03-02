using System;

namespace TT_Lab.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
public class EditorParamAttribute(string param, object value) : Attribute
{
    public string Param { get; } = param;
    public object Value { get; } = value;
}