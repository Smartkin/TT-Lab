using System;
using GlmSharp;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// What a scenery light shows next to its icon, in its color: a directional light's arrow the way its light goes, a point light's reach
/// (where its falloff halves its intensity) and a spot light's cones out to it
/// </summary>
public sealed class SceneryLightVisual : Renderable, IPrimitiveRenderable
{
    private const float ArrowLength = 3.0f;
    // Spot lights without a falloff light everything they shine at, their cones are drawn this long
    private const float ConeLength = 4.0f;
    private const int ConeLines = 8;

    private readonly EditableObject _light;
    private volatile Shape _shape;

    public SceneryLightVisual(RenderContext context, EditableObject light, Light data) : base(context, "SCENERY_LIGHT")
    {
        _light = light;
        _shape = Shape.Of(data);
        light.AddChild(this);
    }

    /// <summary>
    /// Takes the light's values again, from the UI thread
    /// </summary>
    public void Update(Light data)
    {
        _shape = Shape.Of(data);
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!_light.IsVisible)
        {
            return;
        }

        var shape = _shape;
        var position = WorldTransform.Column3.xyz;
        var selected = _light.IsSelected;
        var color = shape.Color with { w = selected ? 1.0f : 0.55f };
        var width = selected ? 2.5f : 1.5f;
        switch (shape.Type)
        {
            case LightType.Directional:
                renderer.DrawArrow(position, position - shape.Direction * ArrowLength, color, width, 0.5f, 0.2f, PrimitiveLayer.WorldXRay);
                break;
            case LightType.Point when shape.Reach > 0.0f:
                if (selected)
                {
                    renderer.DrawWireSphere(position, shape.Reach, color, width, PrimitiveLayer.WorldXRay);
                }
                else
                {
                    renderer.DrawCircle(position, vec3.UnitY, shape.Reach, color, width, PrimitiveLayer.WorldXRay);
                }

                break;
            case LightType.Spot:
                DrawCone(renderer, position, shape, shape.InnerHalfAngle, color, width);
                if (shape.OuterHalfAngle > shape.InnerHalfAngle)
                {
                    DrawCone(renderer, position, shape, shape.OuterHalfAngle, color with { w = color.w * 0.5f }, width * 0.75f);
                }

                break;
        }
    }

    private static void DrawCone(PrimitiveRenderer renderer, vec3 position, Shape shape, float halfAngle, vec4 color, float width)
    {
        var length = shape.Reach > 0.0f ? shape.Reach : ConeLength;
        var axis = shape.Direction;
        var side = vec3.Cross(axis, MathF.Abs(axis.y) < 0.9f ? vec3.UnitY : vec3.UnitX).NormalizedSafe;
        var up = vec3.Cross(side, axis).NormalizedSafe;
        // Past a half turn the cone opens backwards, its rim is drawn where it is
        var clamped = Math.Clamp(halfAngle, 0.0f, MathF.PI);
        var center = position + axis * (length * MathF.Cos(clamped));
        var radius = length * MathF.Sin(clamped);
        renderer.DrawCircle(center, axis, radius, color, width, PrimitiveLayer.WorldXRay);
        for (var i = 0; i < ConeLines; i++)
        {
            var angle = i * MathF.Tau / ConeLines;
            renderer.DrawLine(position, center + (side * MathF.Cos(angle) + up * MathF.Sin(angle)) * radius, color, width * 0.75f, PrimitiveLayer.WorldXRay);
        }
    }

    /// <summary>
    /// How far away a light's falloff 25 / (d² + 25) taken the attenuation power times halves its intensity, 0 when it doesn't fall off
    /// </summary>
    public static float ReachOf(int attenuationPower)
    {
        return attenuationPower <= 0 ? 0.0f : 5.0f * MathF.Sqrt(MathF.Pow(2.0f, 1.0f / attenuationPower) - 1.0f);
    }

    private sealed record Shape(LightType Type, vec4 Color, vec3 Direction, float Reach, float InnerHalfAngle, float OuterHalfAngle)
    {
        public static Shape Of(Light light)
        {
            // The game's colors add up to 1, shown as bright as they go
            var color = new vec3(light.Color.X, light.Color.Y, light.Color.Z);
            var brightest = MathF.Max(color.x, MathF.Max(color.y, color.z));
            color = brightest > 1e-6f ? color / brightest : new vec3(0.6f);
            return light switch
            {
                DirectionalLight directional => new Shape(LightType.Directional, new vec4(color, 1.0f), ToGlm(directional.Direction).NormalizedSafe, 0.0f, 0.0f, 0.0f),
                PointLight point => new Shape(LightType.Point, new vec4(color, 1.0f), vec3.Zero, ReachOf(point.AttenuationPower), 0.0f, 0.0f),
                // The cosines are what the game lights with
                SpotLight spot => new Shape(LightType.Spot, new vec4(color, 1.0f), ToGlm(spot.Direction).NormalizedSafe, ReachOf((short)spot.AttenuationPower),
                    MathF.Acos(Math.Clamp(spot.InnerConeCosine, -1.0f, 1.0f)), MathF.Acos(Math.Clamp(spot.OuterConeCosine, -1.0f, 1.0f))),
                _ => new Shape(LightType.Ambient, new vec4(color, 1.0f), vec3.Zero, 0.0f, 0.0f, 0.0f),
            };
        }
    }

    private static vec3 ToGlm(Vector4 vector) => new(vector.X, vector.Y, vector.Z);
}

/// <summary>
/// A light's direction turned by the gizmo: the light's rotation turns +Z to its direction, which keeps its length (the game lights by
/// it as it is) and its W
/// </summary>
public sealed class LightDirectionRotation(PropertyNode direction) : IRotationConverter
{
    public quat ToRotation(object? data)
    {
        return data is Vector4 vector ? FromZTo(new vec3(vector.X, vector.Y, vector.Z)) : quat.Identity;
    }

    public object ToData(quat rotation)
    {
        var current = direction.GetValue() as Vector4;
        var length = current == null ? 1.0f : new vec3(current.X, current.Y, current.Z).Length;
        if (length < 1e-6f || float.IsNaN(length))
        {
            length = 1.0f;
        }

        var turned = rotation * vec3.UnitZ * length;
        return new Vector4(turned.x, turned.y, turned.z, current?.W ?? 0.0f);
    }

    /// <summary>
    /// The shortest turn from +Z to the direction, none for a direction of no length
    /// </summary>
    public static quat FromZTo(vec3 direction)
    {
        var length = direction.Length;
        if (length < 1e-6f || float.IsNaN(length))
        {
            return quat.Identity;
        }

        var to = direction / length;
        var cosine = Math.Clamp(to.z, -1.0f, 1.0f);
        if (cosine > 0.999999f)
        {
            return quat.Identity;
        }

        if (cosine < -0.999999f)
        {
            return quat.FromAxisAngle(MathF.PI, vec3.UnitX);
        }

        return quat.FromAxisAngle(MathF.Acos(cosine), vec3.Cross(vec3.UnitZ, to).Normalized);
    }
}
