using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Factories;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Scene;

public class BillboardSet : Renderable
{
    private readonly MaterialData _renderMaterial;
    private readonly ModelBuffer _planeBuffer;
    private readonly List<Billboard> _billboards = [];
    private readonly List<mat4> _billboardMatrices = [];
    private readonly BufferObject<float> _modelMatricesView;
    
    public BillboardSet(RenderContext context, MeshFactory meshFactory, string labIconName, string name = "") : base(context, name)
    {
        _renderMaterial = new MaterialData();
        _renderMaterial.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        _renderMaterial.Shaders[0].TextureId = LabURI.GetLabIcon(labIconName);
        _renderMaterial.Shaders[0].DepthTest = TwinShader.DepthTestMethod.ALWAYS;
        _renderMaterial.Shaders[0].ShaderType = TwinShader.Type.UnlitBillboard;
        _renderMaterial.Shaders[0].ATest = TwinShader.AlphaTest.ON;
        _renderMaterial.Shaders[0].AlphaValueToBeComparedTo = 128;
        _renderMaterial.Shaders[0].ForcedShaderName = "CUSTOM_BILLBOARDS";

        _modelMatricesView = new BufferObject<float>(context, Span<Single>.Empty, BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
        
        var plane = meshFactory.CreateMesh(LabURI.Plane);
        _planeBuffer = plane!.GetModels()[0];
        var planeVao = _planeBuffer.GetVertexArrayObject();
        planeVao.Bind();
        _modelMatricesView.Bind();
        for (uint i = 0; i < 4; ++i)
        {
            planeVao.VertexAttributePointerInstanced(7 + i, 4, VertexAttribPointerType.Float, 16, (int)i * 4);
        }
        _planeBuffer.ReplaceMaterial(_renderMaterial);
    }

    public override (String, Int32)[] GetPriorityPasses()
    {
        return [("CUSTOM_BILLBOARDS", 0)];
    }

    public Billboard CreateBillboard(float x, float y, float z)
    {
        var billboard = new Billboard(Context);
        billboard.SetPosition(new vec3(x, y, z));
        _billboards.Add(billboard);
        _billboardMatrices.Add(mat4.Identity);
        return billboard;
    }

    public void RemoveBillboard(Billboard billboard)
    {
        var index = _billboards.IndexOf(billboard);
        _billboards.Remove(billboard);
        _billboardMatrices.RemoveAt(index);
    }

    public override void UpdateRenderTransform()
    {
        base.UpdateRenderTransform();

        foreach (var billboard in _billboards)
        {
            billboard.UpdateRenderTransform();
        }
    }

    protected override void RenderSelf(float delta)
    {
        base.RenderSelf(delta);

        if (_billboards.Count <= 0 || !_planeBuffer.Bind())
        {
            return;
        }

        var modelLoc = Context.CurrentPass.Program.GetUniformLocation("StartModel");
        Context.Gl.UniformMatrix4(modelLoc, false, RenderTransform.Values1D);
        
        var flipYLoc = Context.CurrentPass.Program.GetUniformLocation("FlipY");
        Context.Gl.Uniform1(flipYLoc, 1.0f);
        
        var diffuseOnlyLoc = Context.CurrentPass.Program.GetUniformLocation("DiffuseOnly");
        Context.Gl.Uniform1(diffuseOnlyLoc, 1.0f);
        
        for (uint i = 0; i < 4; ++i)
        {
            Context.Gl.EnableVertexAttribArray(7 + i);
        }
        for (var i = 0; i < _billboards.Count; ++i)
        {
            _billboardMatrices[i] = _billboards[i].RenderTransform;
        }
        _modelMatricesView.BufferData(_billboardMatrices.SelectMany(m => m.Values1D).ToArray());
        Context.Gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, _planeBuffer.IndexCount, (uint)_billboards.Count);
        for (uint i = 0; i < 4; ++i)
        {
            Context.Gl.DisableVertexAttribArray(7 + i);
        }
        _planeBuffer.Unbind();
    }
}