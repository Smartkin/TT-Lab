using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Project.Repair;

/// <summary>
/// What's known of the repair of each version's game packages (<see cref="RetailRepair"/>), kept in repairs.json in the project's
/// folder and never in the project's file: TT Lab 1.1.0 writes the project's file again and drops what it doesn't know
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public sealed class RepairMarker
{
    public const string FileName = "repairs.json";

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public RepairEntry? PS2 { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public RepairEntry? Xbox { get; set; }

    public RepairEntry? Of(GamePlatform platform) => platform == GamePlatform.Xbox ? Xbox : PS2;

    // The entry of the version, made when it has none
    public RepairEntry For(GamePlatform platform)
    {
        if (platform == GamePlatform.Xbox)
        {
            return Xbox ??= new RepairEntry();
        }

        return PS2 ??= new RepairEntry();
    }

    // Whether the version needs nothing more asked: repaired, declined for good or made without 1.1.0's faults
    public bool IsSettled(GamePlatform platform) => Of(platform)?.Repair != null;

    // Whether the project's Xbox packages were unpacked reading the disc's startup first (A8). 1.1.0 read the levels first, and the assets the
    // default chunk shares with levels got URIs of the Xbox package then, which a project's own packages link: an unpack of the Xbox version
    // for the project (made again, or the repair's) has to read the disc the way its packages were made
    public bool HasNewXboxFileOrder => Xbox?.NewFileOrder == true;

    // The project's marker, an empty one when it has none or it can't be read (the repair is offered again, which changes nothing it did)
    public static RepairMarker Read(string projectFolder)
    {
        var path = Path.Combine(projectFolder, FileName);
        if (!File.Exists(path))
        {
            return new RepairMarker();
        }

        try
        {
            return JsonConvert.DeserializeObject<RepairMarker>(File.ReadAllText(path)) ?? new RepairMarker();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"{path} can't be read, the repair of the game's packages is offered again: {exception.Message}", Log.LogType.Warning);
            return new RepairMarker();
        }
    }

    public void Write(string projectFolder)
    {
        // Made before the file is opened, like the project's own files
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(Path.Combine(projectFolder, FileName), json);
    }

    // A project this TT Lab makes: neither version has 1.1.0's faults, the Xbox one is read startup first
    public static void WriteForNewProject(string projectFolder)
    {
        var marker = new RepairMarker();
        marker.For(GamePlatform.PS2).Repair = RepairState.NotNeeded;
        var xbox = marker.For(GamePlatform.Xbox);
        xbox.Repair = RepairState.NotNeeded;
        xbox.NewFileOrder = true;
        marker.Write(projectFolder);
    }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class RepairEntry
{
    // What became of the repair, none while it's still to be offered
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public RepairState? Repair { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? RepairedAt { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Result { get; set; }

    // The Xbox version's alone, see RepairMarker.HasNewXboxFileOrder
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? NewFileOrder { get; set; }
}

public enum RepairState
{
    Repaired,
    Declined,
    NotNeeded
}
