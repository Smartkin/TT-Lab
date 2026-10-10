using System.Collections.Generic;
using System.Text;

namespace TT_Lab.Util;

// Arguments typed on one line: spaces between them, double or single quotes keep spaces in one. Backslashes stay as they are (Windows'
// paths have them), but for \" inside double quotes, which is a quote
internal static class CommandLineArguments
{
    public static List<string> Split(string? line)
    {
        var arguments = new List<string>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return arguments;
        }

        var current = new StringBuilder();
        var hasArgument = false;
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (quote != null)
            {
                if (quote == '"' && character == '\\' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (character == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
                hasArgument = true;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (hasArgument)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    hasArgument = false;
                }

                continue;
            }

            current.Append(character);
            hasArgument = true;
        }

        if (hasArgument)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }
}
