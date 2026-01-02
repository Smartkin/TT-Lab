using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia.Media.Imaging;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Util
{
    public static class MiscUtils
    {
        private static Bitmap? _boatguy;
        private static Dictionary<string, Bitmap> _labIconStorage = new();

        public static object? ConvertEnum(Type t, object? o)
        {
            if (o == null)
            {
                return null;
            }
            return Enum.Parse(t, o.ToString()!);
        }

        public static T? ConvertEnum<T>(object? o)
        {
            return (T?)ConvertEnum(typeof(T), o);
        }

        public static Bitmap GetBoatGuy()
        {
            _boatguy ??= new Bitmap(ManifestResourceLoader.GetPathInExe("Media\\boat_guy.png"));
            return _boatguy;
        }

        public static Bitmap GetLabIcon(string iconName)
        {
            if (_labIconStorage.TryGetValue(iconName, out Bitmap? value))
            {
                return value;
            }

            _labIconStorage.Add(iconName, new Bitmap(ManifestResourceLoader.GetPathInExe($"Media\\LabIcons\\{iconName}.png")));

            return _labIconStorage[iconName];
        }

        public static string GetFileFromDialogue(string filter, string initial_directory = "")
        {
            // using (System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog
            // {
            //     InitialDirectory = initial_directory,
            //     Filter = filter
            // })
            // {
            //     if (System.Windows.Forms.DialogResult.OK == ofd.ShowDialog())
            //     {
            //         return ofd.FileName;
            //     }
            // }
            return string.Empty;
        }

        public static Enums.InstanceState ChangeFlag(this Enums.InstanceState state, Enums.InstanceState flags, Boolean set)
        {
            if (!set)
            {
                return state.UnsetFlag(flags);
            }
            return state.SetFlag(flags);
        }
        public static Enums.InstanceState SetFlag(this Enums.InstanceState state, Enums.InstanceState flags)
        {
            state |= flags;
            return state;
        }
        public static Enums.InstanceState UnsetFlag(this Enums.InstanceState state, Enums.InstanceState flags)
        {
            state &= ~flags;
            return state;
        }

        public static Enums.TriggerActivatorObjects ChangeFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags, Boolean set)
        {
            if (!set)
            {
                return state.UnsetFlag(flags);
            }
            return state.SetFlag(flags);
        }
        public static Enums.TriggerActivatorObjects SetFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags)
        {
            state |= flags;
            return state;
        }
        public static Enums.TriggerActivatorObjects UnsetFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags)
        {
            state &= ~flags;
            return state;
        }
    }

    public static class GlobalConsts
    {
        public static string OgreGroup => "Project"; 
    }
}
