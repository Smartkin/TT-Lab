using System;
using GlmSharp;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Scene;

public class Camera(RenderContext context) : Renderable(context, "Scene Camera")
{
    private mat4 projectionMatrix = mat4.Perspective(glm.Radians(60.0f), 1.0f, 1.0f, 100000.0f);
    private readonly RenderContext _context = context;
    private float fov = 60.0f;
    private float zNear = 0.05f;
    private float zFar = 100000.0f;
    private vec2 viewportResolution = vec2.Zero;

    public void SetResolution(vec2 resolution)
    {
        viewportResolution = resolution;
        projectionMatrix = mat4.Perspective(glm.Radians(fov), resolution.x / resolution.y, zNear, zFar);
    }

    public vec3 GetRayFromViewport(float x, float y)
    {
        var win = new vec3(x, viewportResolution.y - y, 0.0f);
        var view = new vec4(0, 0, viewportResolution.x, viewportResolution.y);
        var worldPos = mat4.UnProject(win, WorldTransform.Inverse, projectionMatrix, view);
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
        var fovLoc = _context.CurrentPass.Program.GetUniformLocation("uFovY");
        var aspectLoc = _context.CurrentPass.Program.GetUniformLocation("uAspect");
        var resultView = RenderTransform.Inverse;
        var position = GetRenderPosition();
        var forward = GetRenderForward();
        _context.Gl.UniformMatrix4(projectionLoc, false, projectionMatrix.Values1D);
        _context.Gl.UniformMatrix4(viewMatrixLoc, false, resultView.Values1D);
        _context.Gl.Uniform3(cameraPositionLoc, position.x, position.y, position.z);
        _context.Gl.Uniform3(cameraDirectionLoc, forward.x, forward.y, forward.z);
        _context.Gl.Uniform1(fovLoc, glm.Radians(fov));
        _context.Gl.Uniform1(aspectLoc, viewportResolution.x / viewportResolution.y);
    }
}