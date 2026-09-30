using System.IO;

namespace Twinsanity.Libraries;

// The definition and table files are embedded in TwinTech, programs using it need nothing next to them
internal static class EmbeddedFiles
{
    public static string ReadText(string name)
    {
        using var stream = typeof(EmbeddedFiles).Assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException($"TwinTech has no embedded {name}", name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
