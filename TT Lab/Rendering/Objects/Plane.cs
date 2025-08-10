using System;
using GlmSharp;
using System.Drawing;
using TT_Lab.AssetData.Graphics;

namespace TT_Lab.Rendering.Objects;

[Obsolete("Use Ogre's entity with SceneManager.PT_PLANE as a mesh attached")]
public class Plane
{
    //IndexedBufferArray planeBuffer;
    //TextureBuffer? texture;

    public Plane() : this(new vec3())
    {
    }

    public Plane(vec3 position)
    {
        //planeBuffer = BufferGeneration.GetModelBuffer(GL,
        //    new List<vec3>
        //    {
        //        new vec3(1 + position.x, 1 + position.y, -1 + position.z),
        //        new vec3(-1 + position.x, 1 + position.y, -1 + position.z),
        //        new vec3(-1 + position.x, -1 + position.y, -1 + position.z),
        //        new vec3(1 + position.x, -1 + position.y, -1 + position.z)
        //    },
        //    new List<AssetData.Graphics.SubModels.IndexedFace>
        //    {
        //        new AssetData.Graphics.SubModels.IndexedFace(new int[] { 0, 1, 2 }),
        //        new AssetData.Graphics.SubModels.IndexedFace(new int[] { 2, 3, 0 })
        //    },
        //    new List<System.Drawing.Color> { System.Drawing.Color.White },
        //    new List<vec3>
        //    {
        //        new vec3(0, 0, 0),
        //        new vec3(1, 0, 0),
        //        new vec3(1, 1, 0),
        //        new vec3(0, 1, 0)
        //    });
    }

    public Plane(MaterialData material)
    {
        //if (material.Shaders[0].TxtMapping == Twinsanity.TwinsanityInterchange.Common.TwinShader.TextureMapping.ON)
        //{
        //    var tex = AssetManager.Get().GetAssetData<TextureData>(material.Shaders[0].TextureId);
        //    texture = new TextureBuffer(GL, tex.Bitmap.Width, tex.Bitmap.Height, tex.Bitmap);
        //}
    }

    public Plane(TextureData tex) : this(tex.Bitmap)
    {
    }

    public Plane(Bitmap texImage)
    {
        //texture = new TextureBuffer(GL, texImage.Width, texImage.Height, texImage);
    }

    public void Bind()
    {
        //if (texture != null)
        //{
        //    Window.Renderer?.RenderProgram.SetTextureUniform("Texture[0]", TbTextureTarget.Texture2D, texture.Buffer, 0);
        //}
        //Window.Renderer?.RenderProgram.SetUniform1("Opacity", Opacity);
        //planeBuffer.Bind();
    }

    public void Delete()
    {
        //texture?.Delete();
        //planeBuffer.Delete();
    }

    public void Unbind()
    {
        //texture?.Unbind();
        //planeBuffer.Unbind();
    }
}