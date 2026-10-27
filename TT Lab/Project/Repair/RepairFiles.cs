using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;

namespace TT_Lab.Project.Repair;

/// <summary>
/// A project's asset files the repair reads and changes as JSON: every asset's metadata file found by the URI it has (a project made on
/// Windows can have its folders in another case than its URIs), what's read kept to be changed, and the changes written once the repair
/// worked them all out
/// </summary>
internal sealed class RepairFiles
{
    public const string EmptyUri = "res://EMPTY";
    private const string BuiltInUris = "res://__GLOBAL__/";

    private readonly Dictionary<string, Entry> _assets;
    // The URIs TT Lab knows a command sequence's packs by, which objects' slots link and no file has
    private readonly HashSet<string> _packs;
    private readonly Dictionary<string, JObject> _json = new(StringComparer.Ordinal);
    private readonly HashSet<string> _changedJson = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
    // Another folder's files put into this one, read as they're copied: the shadow goes before anything is written
    private readonly List<(string Path, byte[] Content)> _copies = [];

    // An asset by its metadata file, its type the class without its namespace, and the URIs of a command sequence's packs
    public sealed record Entry(string Uri, string Path, string Type, UInt32 Id, string Variation, string Package, string Alias, IReadOnlyList<string> Packs)
    {
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        public bool IsVariant => Variation.Length > 0;

        public string DataFile(string extension) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, Name + extension);

        public override string ToString() => Alias.Length > 0 ? Alias : Name;
    }

    public RepairFiles(string assetsFolder)
    {
        AssetsFolder = assetsFolder;
        var files = Directory.Exists(assetsFolder) ? Directory.EnumerateFiles(assetsFolder, "*.json", SearchOption.AllDirectories).ToList() : [];
        var read = new ConcurrentBag<Entry>();
        Parallel.ForEach(files, file =>
        {
            if (ReadEntry(file) is { } entry)
            {
                read.Add(entry);
            }
        });

        // A copy of a file (a file manager's) has its URI too: the first by path is the asset, like opening the project takes it
        _assets = read.OrderBy(entry => entry.Path, StringComparer.Ordinal).GroupBy(entry => entry.Uri, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _packs = _assets.Values.SelectMany(entry => entry.Packs).ToHashSet(StringComparer.Ordinal);
    }

    public string AssetsFolder { get; }

    public Entry? Find(string? uri) => uri != null && _assets.TryGetValue(uri, out var entry) ? entry : null;

    // Whether a link leads somewhere: an asset of the project, a command sequence's pack, none, or one of TT Lab's own (its placeholder textures
    // and shapes)
    public bool Has(string uri) => uri == EmptyUri || uri.StartsWith(BuiltInUris, StringComparison.Ordinal) || _assets.ContainsKey(uri) || _packs.Contains(uri);

    public IEnumerable<Entry> OfType(string type, IReadOnlySet<string> packages)
    {
        return _assets.Values.Where(entry => entry.Type == type && packages.Contains(entry.Package)).OrderBy(entry => entry.Uri, StringComparer.Ordinal);
    }

    private static Entry? ReadEntry(string file)
    {
        try
        {
            var json = Load(File.ReadAllText(file));
            if (json["URI"]?["_uri"] is not JValue { Type: JTokenType.String } uri || json["Type"] is not JValue { Type: JTokenType.String } type)
            {
                return null;
            }

            var typeName = ((string)type!).Split(',')[0];
            var packs = json["BehaviourGraphLinks"] is JObject links ? links.Properties().Select(link => UriOf(link.Value)).Where(link => link.Length > 0).ToList() : [];
            return new Entry((string)uri!, file, typeName[(typeName.LastIndexOf('.') + 1)..], json["ID"]?.Type == JTokenType.Integer ? (UInt32)json["ID"]! : 0,
                (string?)json["Variation"] ?? string.Empty, (string?)json["Package"]?["_uri"] ?? string.Empty, (string?)json["Alias"] ?? string.Empty, packs);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidCastException or OverflowException)
        {
            Log.WriteLine($"The repair leaves {file} out, it can't be read: {exception.Message}", Log.LogType.Warning);
            return null;
        }
    }

    // Read the way TT Lab wrote them: dates and numbers stay as they are written
    private static JObject Load(string text)
    {
        using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double };
        return JObject.Load(reader);
    }

    public JObject Metadata(Entry entry) => Json(entry.Path)!;

    // The asset's data kept as JSON (its .data file), none when it has none
    public JObject? Data(Entry entry) => Json(entry.DataFile(".data"));

    // A JSON file as the repair has it now, changes included
    public JObject? Json(string path)
    {
        if (_json.TryGetValue(path, out var known))
        {
            return known;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = Load(File.ReadAllText(path));
            _json[path] = json;
            return json;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Changed(string path) => _changedJson.Add(path);

    public string? Text(string path) => _texts.TryGetValue(path, out var text) ? text : File.Exists(path) ? File.ReadAllText(path) : null;

    public void SetText(string path, string text) => _texts[path] = text;

    // The document chunks' own values of the asset are paths in: its metadata without what makes it an asset of the project, its data under
    // AssetData (AssetOverrides.GetDocument)
    public JObject Document(Entry entry)
    {
        var document = (JObject)Metadata(entry).DeepClone();
        foreach (var property in AssetOverrides.IdentityProperties)
        {
            document.Remove(property);
        }

        if (Data(entry) is { } data)
        {
            document[AssetOverrides.DataProperty] = data.DeepClone();
        }

        return document;
    }

    // Copies another folder's asset into this project at the same place, its metadata and its data file
    public Entry Copy(RepairFiles source, Entry entry, string dataExtension)
    {
        var destination = System.IO.Path.Combine(AssetsFolder, System.IO.Path.GetRelativePath(source.AssetsFolder, entry.Path));
        _copies.Add((destination, File.ReadAllBytes(entry.Path)));
        var data = entry.DataFile(dataExtension);
        if (File.Exists(data))
        {
            _copies.Add((System.IO.Path.Combine(AssetsFolder, System.IO.Path.GetRelativePath(source.AssetsFolder, data)), File.ReadAllBytes(data)));
        }

        var copied = entry with { Path = destination };
        _assets[entry.Uri] = copied;
        return copied;
    }

    // The files whose content changes and their content, every file the way TT Lab writes it
    public List<(string Path, byte[] Content)> Changes()
    {
        var changes = _copies.Where(copy => !File.Exists(copy.Path) || !File.ReadAllBytes(copy.Path).AsSpan().SequenceEqual(copy.Content)).ToList();
        var texts = _changedJson.Order(StringComparer.Ordinal).Select(path => (Path: path, Text: _json[path].ToString(Formatting.Indented)))
            .Concat(_texts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (Path: pair.Key, Text: pair.Value)));
        foreach (var (path, text) in texts)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != text)
            {
                changes.Add((path, Encoding.UTF8.GetBytes(text)));
            }
        }

        return changes;
    }

    // Sets a property of the JSON where the template has it among the properties both have, the way TT Lab would write it
    public static void SetLike(JObject target, string name, JToken value, JObject template)
    {
        if (target.Property(name) is { } existing)
        {
            existing.Value = value;
            return;
        }

        var property = new JProperty(name, value);
        var previous = template.Properties().TakeWhile(candidate => candidate.Name != name).Select(candidate => target.Property(candidate.Name)).LastOrDefault(candidate => candidate != null);
        if (previous != null)
        {
            previous.AddAfterSelf(property);
        }
        else
        {
            target.AddFirst(property);
        }
    }

    // The URIs a value links to, wherever they are in it
    public static IEnumerable<JObject> Links(JToken token)
    {
        var tokens = token is JContainer container ? container.DescendantsAndSelf() : [token];
        return tokens.OfType<JObject>().Where(link => link.Count == 1 && link["_uri"] is JValue { Type: JTokenType.String });
    }

    public static string UriOf(JToken? link) => link is JObject && link["_uri"] is JValue { Type: JTokenType.String } uri ? (string)uri! : string.Empty;
}
