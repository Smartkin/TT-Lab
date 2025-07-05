using System;
using System.IO;
using System.Text;

namespace Twinsanity.Libraries
{
    public static class StringUtils
    {
        public static string GetStringInBetween(String src, String str1, String str2)
        {
            int pos1 = src.IndexOf(str1) + str1.Length - 1;
            int pos2 = src.IndexOf(str2, pos1 + 1);
            if (pos2 == -1)
            {
                return String.Empty;
            }
            return src.Substring(pos1 + 1, pos2 - pos1 - 1);
        }
        public static string GetStringAfter(String src, String str)
        {
            int pos = src.IndexOf(str) + str.Length - 1;
            return src[(pos + 1)..];
        }
        public static string GetStringBefore(String src, String str)
        {
            int pos = src.IndexOf(str) + str.Length - 1;
            return src[..pos];
        }
        public static int GetIndexAfter(String src, String str)
        {
            return GetIndexAfter(src, str, 0);
        }
        public static int GetIndexAfter(String src, String str, int startIndex)
        {
            return src.IndexOf(str, startIndex) + str.Length;
        }
        public static int GetIndexBefore(String src, String str)
        {
            return GetIndexBefore(src, str, 0);
        }
        public static int GetIndexBefore(String src, String str, int startIndex)
        {
            return src.IndexOf(str, startIndex);
        }
        public static string GetTabulatedString(String src, Int32 tabs)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Clear();
            for (int i = 0; i < tabs; ++i)
            {
                stringBuilder.Append("   ");
            }
            stringBuilder.Append(src);
            return stringBuilder.ToString();
        }
        public static void WriteLineTabulated(StreamWriter writer, String src, Int32 tabs)
        {
            writer.WriteLine(GetTabulatedString(src, tabs));
        }
        public static void WriteTabulated(StreamWriter writer, String src, Int32 tabs)
        {
            writer.Write(GetTabulatedString(src, tabs));
        }
    }
}
