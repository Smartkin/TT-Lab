using System;
using GlmSharp;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;

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
        var win = new vec3(_viewportResolution.x - x, _viewportResolution.y - y, 0.0f);
        var view = new vec4(0, 0, _viewportResolution.x, _viewportResolution.y);
        var worldPos = mat4.UnProject(win, WorldTransform.Inverse, _projectionMatrix, view);
        return glm.Normalized(worldPos - GetPosition());
    }

    public override (String, int)[] GetPriorityPasses()
    {
        return [(PassService.EVERY_PASS, int.MinValue)];
    }

    protected override void RenderSelf(float delta)
    {
        var projectionLoc = _context.CurrentPass.Program.GetUniformLocation("StartProjection");
        var viewMatrixLoc = _context.CurrentPass.Program.GetUniformLocation("StartView");
        var cameraPositionLoc = _context.CurrentPass.Program.GetUniformLocation("EyePosition");
        var cameraDirectionLoc = _context.CurrentPass.Program.GetUniformLocation("EyeDirection");
        var fovLoc = _context.CurrentPass.Program.GetUniformLocation("Fov");
        var aspectLoc = _context.CurrentPass.Program.GetUniformLocation("Aspect");
        var resultView = RenderTransform.Inverse;
        var position = GetRenderPosition();
        var forward = GetRenderForward();
        _context.Gl.UniformMatrix4(projectionLoc, false, _projectionMatrix.Values1D);
        _context.Gl.UniformMatrix4(viewMatrixLoc, false, resultView.Values1D);
        _context.Gl.Uniform3(cameraPositionLoc, position.x, position.y, position.z);
        _context.Gl.Uniform3(cameraDirectionLoc, forward.x, forward.y, forward.z);
        _context.Gl.Uniform1(fovLoc, glm.Radians(_fov));
        _context.Gl.Uniform1(aspectLoc, _viewportResolution.x / _viewportResolution.y);
    }
}