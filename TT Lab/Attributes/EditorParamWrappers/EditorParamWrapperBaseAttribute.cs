using System;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

[AttributeUsage(AttributeTargets.Property |  AttributeTargets.Field, AllowMultiple = true)]
public abstract class EditorParamWrapperBaseAttribute : Attribute
{
    public abstract void ApplyTo(DocumentPartViewModel viewModel);
}