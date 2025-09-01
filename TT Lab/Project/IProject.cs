using Newtonsoft.Json;
using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;

namespace TT_Lab.Project
{
    /// <summary>
    /// Base interface for the PS2 and XBox project types
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public interface IProject
    {
        /// <summary>
        /// Project's collection of assets
        /// </summary>
        AssetManager AssetManager { get; }

        /// <summary>
        /// Base package required for all projects
        /// </summary>
        Package BasePackage { get; }

        /// <summary>
        /// Global package where all the PS2 resources that can be accessed globally are put
        /// </summary>
        Package GlobalPackagePS2 { get; }

        /// <summary>
        /// Global package where all the XBOX resources that can be accessed globally are put
        /// </summary>
        Package GlobalPackageXbox { get; }

        /// <summary>
        /// PS2 assets package [optional]
        /// </summary>
        Package Ps2Package { get; }

        /// <summary>
        /// XBOX assets package [optional]
        /// </summary>
        Package XboxPackage { get; }

        /// <summary>
        /// Project's UUID
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        Guid UUID { get; }

        /// <summary>
        /// Project's Name
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        string Name { get; set; }

        /// <summary>
        /// Project's folder location
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        string Path { get; set; }

        /// <summary>
        /// PS2's disc content path
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        string? DiscContentPathPS2 { get; set; }

        /// <summary>
        /// XBox's disc content path
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        string? DiscContentPathXbox { get; set; }

        /// <summary>
        /// Date and time when project was last modified
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        DateTime LastModified { get; set; }

        /// <summary>
        /// Project's version
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        string Version { get; }

        /// <summary>
        /// Project's root file path
        /// </summary>
        string ProjectPath { get; }

        void CreateBasePackages();

        void CopyDiscContents();

        /// <summary>
        /// Unpacks PS2 assets data into project readable format
        /// </summary>
        void UnpackAssetsPS2();

        /// <summary>
        /// Unpacks XBox assets data into project readable format
        /// </summary>
        void UnpackAssetsXbox();

        /// <summary>
        /// Generates a specifically given chunk
        /// </summary>
        /// <param name="chunkUri">Chunk's URI</param>
        /// <param name="itemFactory">Factory to generate its data from (optional)</param>
        void PackChunk(LabURI chunkUri, ITwinItemFactory? itemFactory = null);

        /// <summary>
        /// Packs all the assets back into Twinsanity's PS2 format
        /// </summary>
        void PackAssetsPS2();

        /// <summary>
        /// Packs all the assets back into Twinsanity's XBox format
        /// </summary>
        void PackAssetsXbox();

        /// <summary>
        /// Creates the BD/BH archives and the PS2 compatible ISO
        /// </summary>
        void CreatePs2ArchivesAndIso();

        /// <summary>
        /// Dump on disk in JSON format
        /// </summary>
        void Serialize();

        /// <summary>
        /// Initializes project directories
        /// </summary>
        void CreateProjectStructure();
    }
}
