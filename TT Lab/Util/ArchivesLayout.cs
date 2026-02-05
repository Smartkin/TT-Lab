using System;
using System.Collections.Generic;

namespace TT_Lab.Util;

public static class ArchivesLayout
{
    public static List<String> ExtrasFolders =>
    [
        "Bosses",
        "Concept",
        "Enemies",
        "Storyboards",
        "Test",
        "Unseen"
    ];

    public static List<String> LanguageFolder =>
    [
        "AgentLab",
        "Code",
        "Credits",
        "Gameover",
        "Legal",
        "Loading",
        "Titles"
    ];

    public static List<String> StartupItems =>
    [
        "Fonts",
        "Decal",
        "Default",
        "Frontend",
        "Icons",
        "Crash",
        "LevelSelect"
    ];
}