using Caliburn.Micro;
using System;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace TT_Lab.Util;

public static class ViewModelUtil
{
    // Instances enums
    public static readonly BindableCollection<object> Layers;
    
    // Object enums
    public static readonly BindableCollection<ITwinObject.ObjectType> ObjectTypes;
    
    // Camera enums
    public static readonly BindableCollection<object> CameraTypes;
    
    // Shader/Material enums
    public static readonly BindableCollection<object> AlphaTestMethods;
    public static readonly BindableCollection<object> ShaderTypes;
    public static readonly BindableCollection<object> ProcessesAfterTestFailed;
    public static readonly BindableCollection<object> DestinationAlphaTestMode;
    public static readonly BindableCollection<object> DepthTestMethods;
    public static readonly BindableCollection<object> ShadingMethods;
    public static readonly BindableCollection<object> TextureCoordinates;
    public static readonly BindableCollection<object> TextureFilters;
    public static readonly BindableCollection<object> ZValueDrawMask;
    public static readonly BindableCollection<object> ColorSpecs;
    public static readonly BindableCollection<object> AlphaSpecs;
    public static readonly BindableCollection<object> AlphaBlendingPresets;
    public static readonly BindableCollection<object> XScrollSettings;
    public static readonly BindableCollection<object> YScrollSettings;

    static ViewModelUtil()
    {
        Layers = new BindableCollection<object>(Enum.GetValues(typeof(Enums.Layouts)).Cast<object>());
        
        CameraTypes = new BindableCollection<object>(Enum.GetValues(typeof(ITwinCamera.CameraType)).Cast<object>());
        
        ObjectTypes = new BindableCollection<ITwinObject.ObjectType>(Enum.GetValues<ITwinObject.ObjectType>());
        
        AlphaTestMethods = new BindableCollection<object>(Enum.GetValues(typeof(AlphaTestMethod)).Cast<object>());
        ShaderTypes = new BindableCollection<object>(Enum.GetValues(typeof(TwinShader.Type)).Cast<object>());
        ProcessesAfterTestFailed = new BindableCollection<object>(Enum.GetValues(typeof(ProcessAfterAlphaTestFailed)).Cast<object>());
        DestinationAlphaTestMode = new BindableCollection<object>(Enum.GetValues(typeof(DestinationAlphaTestMode)).Cast<object>());
        DepthTestMethods = new BindableCollection<object>(Enum.GetValues(typeof(DepthTestMethod)).Cast<object>());
        ShadingMethods = new BindableCollection<object>(Enum.GetValues(typeof(ShadingMethod)).Cast<object>());
        TextureCoordinates = new BindableCollection<object>(Enum.GetValues(typeof(TextureCoordinatesSpecification)).Cast<object>());
        TextureFilters = new BindableCollection<object>(Enum.GetValues(typeof(TextureFilter)).Cast<object>());
        ZValueDrawMask = new BindableCollection<object>(Enum.GetValues(typeof(ZValueDrawMask)).Cast<object>());
        ColorSpecs = new BindableCollection<object>(Enum.GetValues(typeof(ColorSpecMethod)).Cast<object>());
        AlphaSpecs = new BindableCollection<object>(Enum.GetValues(typeof(AlphaSpecMethod)).Cast<object>());
        AlphaBlendingPresets = new BindableCollection<object>(Enum.GetValues(typeof(AlphaBlendPresets)).Cast<object>());
        XScrollSettings = new BindableCollection<object>(Enum.GetValues(typeof(XScrollFormula)).Cast<object>());
        YScrollSettings = new BindableCollection<object>(Enum.GetValues(typeof(YScrollFormula)).Cast<object>());
    }
}