using System;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Attributes.Viewport;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ViewportEditableAttribute(GizmoType gizmoType) : Attribute
{
    public GizmoType GizmoType { get; } = gizmoType;
    public Type? CustomGizmo { get; init; }
}