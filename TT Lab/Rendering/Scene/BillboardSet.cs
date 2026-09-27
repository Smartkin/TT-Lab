using System;
using System.Collections.Generic;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Scene;

public class BillboardSet : Renderable, IInstancedRenderable
{
    private static readonly vec4 HighlightColor = new(1.0f, 0.85f, 0.1f, 1.0f);

    private readonly bool _useDiffuseOnly;
    private readonly MaterialData _renderMaterial;
    private readonly ModelBuffer _planeBuffer;
    private readonly List<Billboard> _billboards = [];
    private int _baseInstance;
    private int _instanceCount;

    public BillboardSet(RenderContext context, MeshFactory meshFactory, string labIconName, string name = "", bool useDiffuseOnly = true) : base(context, name)
    {
        _useDiffuseOnly = useDiffuseOnly;
        _renderMaterial = new MaterialData(null);
        _renderMaterial.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        _renderMaterial.Shaders[0].TextureId = LabURI.GetLabIcon(labIconName);
        _renderMaterial.Shaders[0].DepthTest = TwinShader.DepthTestMethod.ALWAYS;
        _renderMaterial.Shaders[0].ShaderType = TwinShader.Type.UnlitBillboard;
        _renderMaterial.Shaders[0].ATest = TwinShader.AlphaTest.ON;
        _renderMaterial.Shaders[0].AlphaValueToBeComparedTo = 128;
        _renderMaterial.Shaders[0].ForcedShaderName = "CUSTOM_BILLBOARDS";

        var plane = meshFactory.CreateMesh(LabURI.Plane);
        _planeBuffer = plane!.GetModels()[0];
        _planeBuffer.ReplaceMaterial(_renderMaterial);
    }

    public override (String, Int32)[] GetPriorityPasses()
    {
        return [("CUSTOM_BILLBOARDS", 0)];
    }

    public Billboard CreateBillboard(float x, float y, float z)
    {
        var billboard = new Billboard(Context)
        {
            Set = this
        };
        billboard.SetPosition(new vec3(x, y, z));
        _billboards.Add(billboard);
        return billboard;
    }

    public void RemoveBillboard(Billboard billboard)
    {
        _billboards.Remove(billboard);
    }

    public override void UpdateRenderTransform()
    {
        base.UpdateRenderTransform();

        foreach (var billboard in _billboards)
        {
            billboard.UpdateRenderTransform();
        }
    }

    public void PrepareInstances(InstanceBuffer instances)
    {
        _baseInstance = instances.Count;
        _instanceCount = 0;
        if (!IsVisible)
        {
            return;
        }

        foreach (var billboard in _billboards)
        {
            // Billboards are owned by what they represent, which hides them along with itself
            if (!billboard.IsVisible)
            {
                continue;
            }

            instances.Add(billboard.RenderTransform, billboard.IsHighlighted ? HighlightColor : Diffuse);
            _instanceCount++;
        }
    }

    protected override void RenderSelf(float delta)
    {
        if (_instanceCount == 0 || !_planeBuffer.Bind())
        {
            return;
        }

        var program = Context.CurrentPass.Program;
        program.SetUniform(KnownUniform.FlipY, 1.0f);
        program.SetUniform(KnownUniform.DiffuseOnly, _useDiffuseOnly ? 1.0f : 0.0f);
        Context.Gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, _planeBuffer.IndexCount, (uint)_instanceCount, (uint)_baseInstance);
        program.SetUniform(KnownUniform.FlipY, 0.0f);
        program.SetUniform(KnownUniform.DiffuseOnly, 0.0f);
        _planeBuffer.Unbind();
    }
}
