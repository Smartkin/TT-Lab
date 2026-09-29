using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Splat;
using TT_Lab.AssetData.Global;
using TT_Lab.Project;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.ViewModels;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Assets.Global;

// The startup's LevelSelect.txt: a line per level with its name, a note and its chunk's path in quotes ("nsanity isle and jungle
// bungle"<tabs>""<tabs>"levels\earth\hub\beach"), blank lines between the worlds. The level select Play puts in the PAL release's
// pause menu reads it (Ghidra Stuff/Engine_RE/pnach): a world is named after the folder after levels\ and goes on in another page after
// 7 levels. Chunks made in TT Lab are added at its end, deleted ones taken out
public static class LevelSelect
{
    public const string FileName = "LevelSelect";
    // The patch keeps 127 characters of a path
    public const int MaxPathLength = 127;

    public static TextFile? Find(TT_Lab.Project.Project project, GamePlatform platform)
    {
        var package = platform == GamePlatform.Xbox ? project.GlobalPackageXbox : project.GlobalPackagePS2;
        return project.AssetManager.GetAllAssetsOf<TextFile>()
            .FirstOrDefault(file => file.Package == package.URI && string.Equals(file.InvariantName, FileName, StringComparison.OrdinalIgnoreCase));
    }

    public static List<(string Name, string Path)> Entries(string text)
    {
        return text.Split('\n').Select(Fields).Where(IsEntry).Select(fields => (fields[0], fields[^1])).ToList();
    }

    // The folder after levels\ (levels\earth\hub\beach is earth), the first folder of other paths
    public static string WorldOf(string path)
    {
        var folders = path.Split('\\', '/');
        return folders.Length > 1 && folders[0].Equals(Folder.LevelsFolderName, StringComparison.OrdinalIgnoreCase) ? folders[1] : folders[0];
    }

    // At the end, in the last world when it's the level's, after a blank line starting its world otherwise
    public static string WithLevel(string text, string name, string path)
    {
        if (Entries(text).Any(entry => IsSamePath(entry.Path, path)))
        {
            return text;
        }

        var newLine = text.Contains("\r\n") || !text.Contains('\n') ? "\r\n" : "\n";
        var builder = new StringBuilder(text);
        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            builder.Append(newLine);
        }

        var lines = Lines(text);
        var last = lines.LastOrDefault(line => line.Text.Trim().Length > 0).Text;
        var lastFields = last == null ? [] : Fields(last);
        var endsWithBlankLine = lines.Count > 0 && lines[^1].Text.Trim().Length == 0 && lines[^1].Ending.Length > 0;
        if (IsEntry(lastFields) && !endsWithBlankLine && !WorldOf(lastFields[^1]).Equals(WorldOf(path), StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(newLine);
        }

        // The patch reads a field up to the next quote, a line up to its end
        var shownName = name.Replace('"', '\'').Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        builder.Append($"\"{shownName}\"\t\"\"\t\"{path}\"{newLine}");
        return builder.ToString();
    }

    // Takes the level's lines out, and a blank line with them that no longer separates two worlds
    public static string WithoutLevel(string text, string path)
    {
        var lines = Lines(text);
        var kept = new List<(string Text, string Ending)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var fields = Fields(lines[i].Text);
            if (!IsEntry(fields) || !IsSamePath(fields[^1], path))
            {
                kept.Add(lines[i]);
                continue;
            }

            var next = i + 1 < lines.Count ? lines[i + 1] : default;
            var nextIsBlankOrEnd = next.Text == null || next.Text.Trim().Length == 0 && next.Ending.Length > 0;
            if (kept.Count > 0 && kept[^1].Text.Trim().Length == 0 && nextIsBlankOrEnd)
            {
                kept.RemoveAt(kept.Count - 1);
            }
            else if (kept.Count == 0 && next.Text != null && next.Text.Trim().Length == 0 && next.Ending.Length > 0)
            {
                i++;
            }
        }

        return string.Concat(kept.Select(line => line.Text + line.Ending));
    }

    public static void AddChunk(LevelChunk chunk)
    {
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is not TT_Lab.Project.Project project || string.IsNullOrEmpty(chunk.AdditionalPath)
            || Find(project, project.GetPlatform(chunk.Package)) is not { } file)
        {
            return;
        }

        var path = GameExecutable.ChunkPath(chunk.AdditionalPath);
        if (path.Length > MaxPathLength)
        {
            Log.WriteLine($"{chunk.Alias} wasn't added to {file.Alias}, the level select keeps paths of up to {MaxPathLength} characters", Log.LogType.Warning);
            return;
        }

        if (Update(file, text => WithLevel(text, chunk.Alias, path)))
        {
            Log.WriteLine($"Added {chunk.Alias} to {file.Alias}");
        }
    }

    public static void RemoveChunks(IEnumerable<LevelChunk> chunks)
    {
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is not TT_Lab.Project.Project project)
        {
            return;
        }

        foreach (var chunk in chunks.Where(chunk => !string.IsNullOrEmpty(chunk.AdditionalPath)))
        {
            if (Find(project, project.GetPlatform(chunk.Package)) is { } file
                && Update(file, text => WithoutLevel(text, GameExecutable.ChunkPath(chunk.AdditionalPath!))))
            {
                Log.WriteLine($"Took {chunk.Alias} out of {file.Alias}");
            }
        }
    }

    // An editor that has the file open gets the change as an edit of its own, shown, undone and saved with it. The file's written
    // otherwise
    private static bool Update(TextFile file, Func<string, string> change)
    {
        var document = Locator.Current.GetService<EditorsViewModel>()?.ResourcesEditorsViewModel.Tabs
            .FirstOrDefault(tab => tab.EditableResource == file.URI)?.Document;
        if (document?.PropertyGraph.Find($"Root.{nameof(SerializableAsset.AssetData)}.{nameof(TextFileData.Text)}") is { } node)
        {
            var current = node.GetValue<string>() ?? "";
            var changed = change(current);
            if (changed == current)
            {
                return false;
            }

            node.SetValue(changed);
            return true;
        }

        var wasLoaded = file.IsLoaded;
        var data = ((IAsset)file).GetData<TextFileData>();
        var text = change(data.Text);
        if (text == data.Text)
        {
            return false;
        }

        data.Text = text;
        file.Serialize(SerializationFlags.SaveData | (wasLoaded ? SerializationFlags.PreserveData : SerializationFlags.None));
        return true;
    }

    private static bool IsSamePath(string a, string b) => string.Equals(a.Replace('/', '\\'), b.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);

    // A level has a name and a path, the fields between them are notes
    private static bool IsEntry(List<string> fields) => fields.Count >= 2 && fields[0].Length > 0 && fields[^1].Length > 0;

    // The quoted fields of a line the way the patch reads them
    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var position = 0;
        while (true)
        {
            var start = line.IndexOf('"', position);
            if (start == -1)
            {
                return fields;
            }

            var end = line.IndexOf('"', start + 1);
            if (end == -1)
            {
                fields.Add(line[(start + 1)..].TrimEnd('\r'));
                return fields;
            }

            fields.Add(line[(start + 1)..end]);
            position = end + 1;
        }
    }

    // The lines with what ends them, the last one without an ending when the text doesn't end on one
    private static List<(string Text, string Ending)> Lines(string text)
    {
        var lines = new List<(string, string)>();
        var position = 0;
        while (position < text.Length)
        {
            var end = text.IndexOf('\n', position);
            if (end == -1)
            {
                lines.Add((text[position..], ""));
                break;
            }

            var lineEnd = end > position && text[end - 1] == '\r' ? end - 1 : end;
            lines.Add((text[position..lineEnd], text[lineEnd..(end + 1)]));
            position = end + 1;
        }

        return lines;
    }
}
