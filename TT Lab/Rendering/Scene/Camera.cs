using System;
using GlmSharp;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.Shaders;

namespace TT_Lab.Rendering.Scene;

public class Camera(RenderContext context) : Renderable(context, "Scene Camera")
{
    private mat4 _projectionMatrix = mat4.Perspective(glm.Radians(60.0f), 1.0f, 1.0f, 100000.0f);
    private readonly RenderContext _context = context;
    private readonly float _fov = 60.0f;
    private readonly float _zNear = 0.05f;
    private readonly float _zFar = 100000.0f;
    private vec2 _viewportResolution = vec2.Zero;

    public void SetResolution(vec2 resolution)
    {
        _viewportResolution = resolution;
        _projectionMatrix = mat4.Perspective(glm.Radians(_fov), resolution.x / resolution.y, _zNear, _zFar);
    }

    public vec3 GetRayFromViewport(float x, float y)
    {
        return GetFrameCamera().ScreenRay(new vec2(x, y)).Direction;
    }

    /// <summary>
    /// Camera as it's getting rendered on the render thread or as it currently is everywhere else
    /// </summary>
    public FrameCamera GetFrameCamera(bool asRendered = false)
    {
        return new FrameCamera(asRendered ? RenderTransform : WorldTransform, _projectionMatrix, _viewportResolution, glm.Radians(_fov));
    }

    public override (String, int)[] GetPriorityPasses()
    {
        return [(PassService.EVERY_PASS, int.MinValue)];
    }

    protected override void RenderSelf(float delta)
    {
        var program = _context.CurrentPass.Program;
        var camera = _context.FrameCamera;
        program.SetUniform(KnownUniform.StartProjection, camera.Projection);
        program.SetUniform(KnownUniform.StartView, camera.View);
        program.SetUniform(KnownUniform.InverseView, camera.World);
        // 1 the PS2's colour filter, 2 the Xbox's fog stage (GameFog)
        var fog = _context.Fog;
        program.SetUniform(KnownUniform.FogMode, fog == null ? 0 : fog.Xbox ? 2 : 1);
        program.SetUniform(KnownUniform.EyePosition, camera.Position);
        program.SetUniform(KnownUniform.EyeDirection, camera.World.Column2.xyz);
        program.SetUniform(KnownUniform.GameClipScale, EnvironmentMapping.ClipScale);
        program.SetUniform(KnownUniform.Fov, glm.Radians(_fov));
        program.SetUniform(KnownUniform.Aspect, _viewportResolution.x / _viewportResolution.y);
    }
}
