using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Tests.Assets;

// A project made from the game names its assets after twinsanity-editor's names of their IDs (TwinTech's DefaultHashes), the IDs without
// one keep TT Lab's names
public class RetailNamesTests
{
    private static readonly IReadOnlyDictionary<uint, string>[] Tables =
    [
        DefaultHashes.Ogis, DefaultHashes.Textures, DefaultHashes.Sounds, DefaultHashes.Skydomes, DefaultHashes.BehaviourCommandsSequences,
        DefaultHashes.InstanceTemplates, DefaultHashes.Animations
    ];

    [Fact]
    public void TheGamesAssetsAreNamedByTheirIds()
    {
        Assert.Equal("Crash", RetailNames.Of(DefaultHashes.Ogis, 0, "OGI 0"));
        Assert.Equal("Earth", RetailNames.Of(DefaultHashes.Skydomes, 0xCA9A682F, "Skydome CA9A682F"));
        Assert.Equal("Crate_Basic", RetailNames.Of(DefaultHashes.InstanceTemplates, 1, "BASICCRATE"));
        Assert.Equal("Crash_Walk", RetailNames.Of(DefaultHashes.Animations, 1, "Animation 1"));
        Assert.Equal("Pickup_Wumpa", RetailNames.Of(DefaultHashes.BehaviourCommandsSequences, 0, "Behaviour Commands Sequence 0"));
        Assert.Equal("OGI 99999", RetailNames.Of(DefaultHashes.Ogis, 99999, "OGI 99999"));
    }

    // TT Lab keeps an asset's name in its file name, so a name loses the folders that group it, but where two of a kind would be the same
    [Fact]
    public void NamesLoseTheirFoldersButWhereTheyWouldBeTheSame()
    {
        Assert.Equal("Crate/Crate_TNT_BEEP", DefaultHashes.Sounds[0xE]);
        Assert.Equal("Crate_TNT_BEEP", RetailNames.Of(DefaultHashes.Sounds, 0xE, "Sound E"));

        var names = new Dictionary<uint, string> { [1] = "Earth/Hub/Sand", [2] = "Ice/Hub/Sand", [3] = "Earth/Rock", [4] = "Odd: name?", [5] = "Twice", [6] = "twice" };
        Assert.Equal("Earth_Hub_Sand", RetailNames.Of(names, 1, ""));
        Assert.Equal("Ice_Hub_Sand", RetailNames.Of(names, 2, ""));
        Assert.Equal("Rock", RetailNames.Of(names, 3, ""));
        Assert.Equal("Odd_ name_", RetailNames.Of(names, 4, ""));
        // File systems that ignore case would take them for one
        Assert.Equal(("Twice_5", "twice_6"), (RetailNames.Of(names, 5, ""), RetailNames.Of(names, 6, "")));
    }

    [Fact]
    public void EveryNameIsAFileNameOfItsOwn()
    {
        foreach (var table in Tables)
        {
            var names = table.Keys.Select(id => RetailNames.Of(table, id, "")).ToList();
            Assert.All(names, name => Assert.True(name.Length > 0 && name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0, name));
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
