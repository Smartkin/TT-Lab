using Caliburn.Micro;
using System;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;

namespace TT_Lab.Util;

public static class ViewModelUtil
{
    public static readonly BindableCollection<object> Layers;

    static ViewModelUtil()
    {
        Layers = new BindableCollection<object>(Enum.GetValues(typeof(Enums.Layouts)).Cast<object>());
    }
}