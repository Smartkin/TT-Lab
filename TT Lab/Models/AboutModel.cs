using System;

namespace TT_Lab.Models;

public class AboutModel
{
    public String Description { get; set; } = "TT Lab is an IDE created to allow anyone create modifications for Crash Twinsanity. Specifically PS2 version is the most supported with XBox having experimental support and not very tested. The project is open-source and can be found on GitHub.";
    public String Version { get; set; } = "1.1.0";
    public String Authors { get; set; } = "Smartkin, Neo_Kesha";
    public String SpecialThanks { get; set; } = "BetaM, SuperMoe, Marko, GPro";
    public String SourceCodeLink { get; set; } = "https://github.com/Smartkin/TT-Lab";
    public String Testers { get; set; } = ""; // TODO: When we gonna do actual testing add people here
    public String Artists { get; set; } = "Tralexium";
}