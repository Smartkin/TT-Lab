using System;

namespace TT_Lab.Extensions;

public class StringExtensions
{
    public static string CapitalizeFirstChar(String input)
    {
        return string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1));
    }
}