using System;

namespace TT_Lab.Models
{
    public class AboutModel
    {
        public String Description { get; set; } = "TT Lab is an IDE created to allow anyone create modifications for Crash Twinsanity. Currently the IDE is in active development and is not recommended for long term projects. The project is open-source and can be found on GitHub.";
        public String Version { get; set; } = "0.0.9";
        public String Authors { get; set; } = "Smartkin, Neo_Kesha";
        public String SpecialThanks { get; set; } = "BetaM, SuperMoe, Marko, GPro";
        public String SourceCodeLink { get; set; } = "https://github.com/NeoKesha/TT-Lab";
        public String Testers { get; set; } = ""; // TODO: When we gonna do actual testing add people here
        public String Artists { get; set; } = ""; // TODO: Add Tralexium to credits
    }
}
