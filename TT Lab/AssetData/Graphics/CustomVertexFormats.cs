using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;

namespace TT_Lab.AssetData.Graphics;

public struct VertexColor2Texture1WithAlpha : SharpGLTF.Geometry.VertexTypes.IVertexCustom
{
    private SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1 _base;
        
    public VertexColor2Texture1WithAlpha(System.Numerics.Vector4 color0, System.Numerics.Vector4 color1, System.Numerics.Vector2 texcoord, bool alphaBlendingBit)
    {
        _base = new SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1(color0, color1, texcoord);
        AlphaBlendingBit = alphaBlendingBit;
    }
    
    public VertexColor2Texture1WithAlpha(SharpGLTF.Geometry.VertexTypes.IVertexMaterial src, bool alphaBlendingBit = true)
    {
        _base = new SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1(src);
        AlphaBlendingBit = alphaBlendingBit;
    }
        
    public IEnumerable<KeyValuePair<String, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("COLOR_0", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
        yield return new KeyValuePair<string, AttributeFormat>("COLOR_1", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0", new AttributeFormat(DimensionType.VEC2));
        yield return new KeyValuePair<string, AttributeFormat>(ALPHA_BLENDING_ATTRIBUTE, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
    }

    public System.Numerics.Vector4 GetColor(int index)
    {
        return _base.GetColor(index);
    }

    public System.Numerics.Vector2 GetTexCoord(int index)
    {
        return _base.GetTexCoord(index);
    }

    public void SetColor(int setIndex, System.Numerics.Vector4 color)
    {
        switch (setIndex)
        {
            case 0:
                _base.Color0 = color;
                break;
            case 1:
                _base.Color1 = color;
                break;
        }
    }

    public void SetTexCoord(int setIndex, System.Numerics.Vector2 coord)
    {
        if (setIndex == 0) _base.TexCoord = coord;
    }

    public SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta Subtract(SharpGLTF.Geometry.VertexTypes.IVertexMaterial baseValue)
    {
        return _base.Subtract(baseValue);
    }

    public void Add(in SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta delta)
    {
        _base.Add(delta);
    }
        
    public bool AlphaBlendingBit = true;

    public int MaxColors => _base.MaxColors;
    public int MaxTextCoords => _base.MaxTextCoords;
    public void Validate() {}

    public const string ALPHA_BLENDING_ATTRIBUTE = "COLOR_2";
    public Boolean TryGetCustomAttribute(string attributeName, [UnscopedRef] out object value)
    {
        if (attributeName != ALPHA_BLENDING_ATTRIBUTE) { value = null; return false; }

        value = AlphaBlendingBit ? System.Numerics.Vector4.One : System.Numerics.Vector4.Zero;
        return true;
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == ALPHA_BLENDING_ATTRIBUTE && value is System.Numerics.Vector4 blendingBit) AlphaBlendingBit = Math.Abs(blendingBit.W - 1.0f) < 0.00001f;
    }

    public override Int32 GetHashCode()
    {
        return HashCode.Combine(_base.Color0, _base.Color1, _base.TexCoord, AlphaBlendingBit);
    }

    public IEnumerable<String> CustomAttributes
    {
        get { yield return ALPHA_BLENDING_ATTRIBUTE; }
    }
}
    
public struct VertexColor1Texture1WithAlpha : SharpGLTF.Geometry.VertexTypes.IVertexCustom
{
    private SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1 _base;
        
    public VertexColor1Texture1WithAlpha(System.Numerics.Vector4 color0, System.Numerics.Vector2 texcoord, bool alphaBlendingBit)
    {
        _base = new SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1(color0, texcoord);
        AlphaBlendingBit = alphaBlendingBit;
    }
        
    public VertexColor1Texture1WithAlpha(SharpGLTF.Geometry.VertexTypes.IVertexMaterial src, bool alphaBlendingBit = true)
    {
        _base = new SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1(src);
        AlphaBlendingBit = alphaBlendingBit;
    }
        
    public IEnumerable<KeyValuePair<String, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("COLOR_0", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0", new AttributeFormat(DimensionType.VEC2));
        yield return new KeyValuePair<string, AttributeFormat>(ALPHA_BLENDING_ATTRIBUTE, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
    }

    public System.Numerics.Vector4 GetColor(int index)
    {
        return _base.GetColor(index);
    }

    public System.Numerics.Vector2 GetTexCoord(int index)
    {
        return _base.GetTexCoord(index);
    }

    public void SetColor(int setIndex, System.Numerics.Vector4 color)
    {
        switch (setIndex)
        {
            case 0:
                _base.Color = color;
                break;
        }
    }

    public void SetTexCoord(int setIndex, System.Numerics.Vector2 coord)
    {
        if (setIndex == 0) _base.TexCoord = coord;
    }

    public SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta Subtract(SharpGLTF.Geometry.VertexTypes.IVertexMaterial baseValue)
    {
        return _base.Subtract(baseValue);
    }

    public void Add(in SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta delta)
    {
        _base.Add(delta);
    }
        
    public bool AlphaBlendingBit = true;

    public int MaxColors => _base.MaxColors;
    public int MaxTextCoords => _base.MaxTextCoords;
    public void Validate() {}

    public const string ALPHA_BLENDING_ATTRIBUTE = "COLOR_1";
    public Boolean TryGetCustomAttribute(string attributeName, [UnscopedRef] out object value)
    {
        if (attributeName != ALPHA_BLENDING_ATTRIBUTE) { value = null; return false; }

        value = AlphaBlendingBit ? System.Numerics.Vector4.One : new System.Numerics.Vector4(0, 0, 0, 1.0f);
        return true;
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == ALPHA_BLENDING_ATTRIBUTE && value is System.Numerics.Vector4 blendingBit) AlphaBlendingBit = Math.Abs(blendingBit.X - 1.0f) < 0.00001f;
    }

    public override Int32 GetHashCode()
    {
        return HashCode.Combine(_base.Color, _base.TexCoord, AlphaBlendingBit);
    }

    public IEnumerable<String> CustomAttributes
    {
        get { yield return ALPHA_BLENDING_ATTRIBUTE; }
    }
}

public struct VertexColor3Texture1 : SharpGLTF.Geometry.VertexTypes.IVertexCustom
    {
        private SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1 _base;
        
        public VertexColor3Texture1(System.Numerics.Vector4 color0, System.Numerics.Vector4 color1, System.Numerics.Vector4 color2, System.Numerics.Vector2 texcoord)
        {
            _base = new SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1(color0, color1, texcoord);
            Color2 = color2;
        }
        
        public VertexColor3Texture1(SharpGLTF.Geometry.VertexTypes.IVertexMaterial src)
        {
            _base = new SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1(src);
            Color2 = 2 < src.MaxColors ? src.GetColor(2) : System.Numerics.Vector4.UnitW;
        }

        public static implicit operator SharpGLTF.Geometry.VertexTypes.VertexColor2Texture1(VertexColor3Texture1 src) =>
            src._base;
        
        public IEnumerable<KeyValuePair<String, AttributeFormat>> GetEncodingAttributes()
        {
            yield return new KeyValuePair<string, AttributeFormat>("COLOR_0", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
            yield return new KeyValuePair<string, AttributeFormat>("COLOR_1", new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
            yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0", new AttributeFormat(DimensionType.VEC2));
            yield return new KeyValuePair<string, AttributeFormat>(COLOR_2_ATTRIBUTE, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_BYTE, true));
        }

        public System.Numerics.Vector4 GetColor(int index)
        {
            return _base.GetColor(index);
        }

        public System.Numerics.Vector2 GetTexCoord(int index)
        {
            return _base.GetTexCoord(index);
        }

        public void SetColor(int setIndex, System.Numerics.Vector4 color)
        {
            switch (setIndex)
            {
                case 0:
                    _base.Color0 = color;
                    break;
                case 1:
                    _base.Color1 = color;
                    break;
            }
        }

        public void SetTexCoord(int setIndex, System.Numerics.Vector2 coord)
        {
            if (setIndex == 0) _base.TexCoord = coord;
        }

        public SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta Subtract(SharpGLTF.Geometry.VertexTypes.IVertexMaterial baseValue)
        {
            return _base.Subtract(baseValue);
        }

        public void Add(in SharpGLTF.Geometry.VertexTypes.VertexMaterialDelta delta)
        {
            _base.Add(delta);
        }
        
        public System.Numerics.Vector4 Color2 = new(0, 0, 0, 1);

        public int MaxColors => _base.MaxColors;
        public int MaxTextCoords => _base.MaxTextCoords;
        public void Validate() {}

        public const string COLOR_2_ATTRIBUTE = "COLOR_2";
        public Boolean TryGetCustomAttribute(string attributeName, [UnscopedRef] out object value)
        {
            if (attributeName != COLOR_2_ATTRIBUTE) { value = null; return false; }

            value = Color2;
            return true;
        }

        public void SetCustomAttribute(string attributeName, object value)
        {
            if (attributeName == COLOR_2_ATTRIBUTE && value is System.Numerics.Vector4 color2) Color2 = color2;
        }

        public override Int32 GetHashCode()
        {
            return HashCode.Combine(_base.Color0, _base.Color1, _base.TexCoord, Color2);
        }

        public IEnumerable<String> CustomAttributes
        {
            get { yield return COLOR_2_ATTRIBUTE; }
        }
    }