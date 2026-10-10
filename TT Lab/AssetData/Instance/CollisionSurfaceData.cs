using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class CollisionSurfaceData : AbstractAssetData
{
    public const int MaxSurfaces = 128;

    private const string SoundKinds = "The game plays a surface's sounds by contact kind (0 impact, 1 and 2 steps, 3 land, 4 hard impact, 5 scrape): " +
                                      "objects landing on it play 0 (4 when hard) and 5 while scraping along, scripts play any with DoSound's surface sound kind. " +
                                      "The player's own footsteps come from the character's sound table by surface ID.";

    private const string ParticleKinds = "One of the default chunk's particle systems, kept by its index: the game's table of systems starts with the default chunk's. " +
                                         "Land and scrape have none.";

    public CollisionSurfaceData(IAsset asset) : base(asset)
    {
        SurfaceID = SurfaceType.SURF_DEFAULT;
        PhysicsParameters = new float[SurfacePhysics.Count];
        for (var i = 0; i < 5; i++)
        {
            PhysicsParameters[i] = -1;
        }

        PhysicsParameters[SurfacePhysics.Unread5] = 1000000;
        PhysicsParameters[SurfacePhysics.Friction] = 1;
        PhysicsParameters[SurfacePhysics.Restitution] = 1;
        CollisionMask = SurfaceCollisionFlags.SolidToPlayerProbes | SurfaceCollisionFlags.BlocksCamera | SurfaceCollisionFlags.SolidToObjects |
                        SurfaceCollisionFlags.BlocksLineOfSight | SurfaceCollisionFlags.SolidToPlayer | (SurfaceCollisionFlags)0xFF000;
        UnusedVector = new Vector4(0, 0, 0, 1);
        ContactMessage = new ContactMessage();
        StepSoundId1 = LabURI.Empty;
        StepSoundId2 = LabURI.Empty;
        ImpactSoundId = LabURI.Empty;
        HardImpactSoundId = LabURI.Empty;
        LandSoundId = LabURI.Empty;
        ScrapeSoundId = LabURI.Empty;
        ImpactParticleSystemId = DefaultParticleSystemFieldViewModel.None;
        HardImpactParticleSystemId = DefaultParticleSystemFieldViewModel.None;
        StepParticleSystemId = DefaultParticleSystemFieldViewModel.None;
    }

    public CollisionSurfaceData(IAsset asset, ITwinSurface collisionSurface) : this(asset)
    {
        SetTwinItem(collisionSurface);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "What the surface is: the characters play their footsteps from their own sound tables by it, and particle trails leave footprints on sand and snow")]
    public SurfaceType SurfaceID { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Which ray casts and bodies the surface stops: the characters' probes and collision (bit 4), the camera (5), objects and rigid bodies (6), lines of sight (7) and the playable characters' bodies (20). " +
                     "Bit 8 sends the contact message to rigid bodies' agents and 9 to the player (the deadly surfaces), 10 packs snow on the Rollerbrawl's ball rolling over it, 11 (Soft) is walls Nina clings to and slides down and the ground the Humiliskate's and the Rollerbrawl's skid trails are laid on. " +
                     "Bits 12-19 are set on every surface, the rest the game never reads.")]
    public SurfaceCollisionFlags CollisionMask { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Impact sound", Hint = "Contact kind 0: objects landing on the surface and touching water. " + SoundKinds)]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI ImpactSoundId { get; set; }

    [JsonIgnore]
    [Editable(Caption = "Impact sound volume", Hint = "Scales the impact sound's volume, -1 leaves it")]
    public Single ImpactSoundVolume { get => PhysicsParameters[SurfacePhysics.ImpactSoundVolume]; set => PhysicsParameters[SurfacePhysics.ImpactSoundVolume] = value; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Impact particles", Hint = "Played on impact (contact kind 0) and when objects touch water. " + ParticleKinds, EditorDescType = typeof(DefaultParticleSystemEditorDesc))]
    public UInt16 ImpactParticleSystemId { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Step sound 1", Hint = "Contact kind 1, played by scripts. " + SoundKinds)]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI StepSoundId1 { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Step sound 2", Hint = "Contact kind 2, played by scripts")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI StepSoundId2 { get; set; }

    [JsonIgnore]
    [Editable(Caption = "Step sound volume", Hint = "Scales both step sounds' volume, -1 leaves it")]
    public Single StepSoundVolume { get => PhysicsParameters[SurfacePhysics.StepSoundVolume]; set => PhysicsParameters[SurfacePhysics.StepSoundVolume] = value; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Step particles", Hint = "Played with the step sounds (contact kinds 1 and 2). " + ParticleKinds, EditorDescType = typeof(DefaultParticleSystemEditorDesc))]
    public UInt16 StepParticleSystemId { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Land sound", Hint = "Contact kind 3, played by scripts (on sand it throws sand up too)")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI LandSoundId { get; set; }

    [JsonIgnore]
    [Editable(Caption = "Land sound volume", Hint = "Scales the land sound's volume, -1 leaves it")]
    public Single LandSoundVolume { get => PhysicsParameters[SurfacePhysics.LandSoundVolume]; set => PhysicsParameters[SurfacePhysics.LandSoundVolume] = value; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Hard impact sound", Hint = "Contact kind 4: objects landing hard on the surface")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI HardImpactSoundId { get; set; }

    [JsonIgnore]
    [Editable(Caption = "Hard impact sound volume", Hint = "Scales the hard impact sound's volume, -1 leaves it")]
    public Single HardImpactSoundVolume { get => PhysicsParameters[SurfacePhysics.HardImpactSoundVolume]; set => PhysicsParameters[SurfacePhysics.HardImpactSoundVolume] = value; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Hard impact particles", Hint = "Played on a hard impact (contact kind 4). " + ParticleKinds, EditorDescType = typeof(DefaultParticleSystemEditorDesc))]
    public UInt16 HardImpactParticleSystemId { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Scrape sound", Hint = "Contact kind 5: objects scraping along the surface")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI ScrapeSoundId { get; set; }

    [JsonIgnore]
    [Editable(Caption = "Scrape sound volume", Hint = "Scales the scrape sound's volume, -1 leaves it")]
    public Single ScrapeSoundVolume { get => PhysicsParameters[SurfacePhysics.ScrapeSoundVolume]; set => PhysicsParameters[SurfacePhysics.ScrapeSoundVolume] = value; }

    [JsonIgnore]
    [Editable(Hint = "How hard rigid bodies grip the surface, with their own friction, and how fast a character who left it steers in the air (10 + 40 times it a second): 1 on most surfaces, 0.7 on metal and rock, 0.3 in water, 0.05 on ice, 0 on the walls only AI collides with")]
    public Single Friction { get => PhysicsParameters[SurfacePhysics.Friction]; set => PhysicsParameters[SurfacePhysics.Friction] = value; }

    [JsonIgnore]
    [Editable(Hint = "What rigid bodies' bounce is multiplied by on the surface: 1 on most surfaces, 0.5 on soft grounds, 0.1 on liquids and deadly surfaces")]
    public Single Restitution { get => PhysicsParameters[SurfacePhysics.Restitution]; set => PhysicsParameters[SurfacePhysics.Restitution] = value; }

    [JsonIgnore]
    [Editable(Caption = "Ground acceleration", Hint = "How fast a character standing on the surface gets to the velocity it's steered at, plus the flow, in units a second per second: 1000000 on most surfaces (at once), 5 and 2 on the slippy ones, 120 on liquids and deadly surfaces. " +
                                                     "A held crouch slide lasts longer the lower it is, while held at 3.5 or less")]
    public Single UnreadValue5 { get => PhysicsParameters[SurfacePhysics.Unread5]; set => PhysicsParameters[SurfacePhysics.Unread5] = value; }

    [JsonIgnore]
    [Editable(Caption = "Downhill pull", Hint = "How hard ground of the surface steeper than the downhill pull slope pulls a character down it, in units a second per second, all of it on ground of 30 degrees or more: 35 or 45 on the slippy surfaces, 0 for none")]
    public Single UnreadValue8 { get => PhysicsParameters[SurfacePhysics.Unread8]; set => PhysicsParameters[SurfacePhysics.Unread8] = value; }

    [JsonIgnore]
    [Editable(Caption = "Downhill pull slope", Hint = "The downhill pull works on ground whose normal's Y is below this: 0.98 (11 degrees of slope) or 0.99 (8 degrees) on the slippy surfaces")]
    [EditorLinkedField(typeof(DownhillSlopeRead), nameof(UnreadValue8))]
    public Single UnreadValue9 { get => PhysicsParameters[SurfacePhysics.Unread9]; set => PhysicsParameters[SurfacePhysics.Unread9] = value; }

    /// <summary>
    /// The 10 floats of the game's surface, edited through the properties above
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public Single[] PhysicsParameters { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Flow", Hint = "A velocity along the ground (X and Z) the surface carries the characters standing on it along at, added to where they're steered: (0, 0, 0, 1) on every retail surface")]
    public Vector4 UnusedVector { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Contact message", Hint = "What touching the surface does: handed to the player standing on it with bit 9 of the mask and to the agents of rigid bodies touching it with bit 8. The deadly surfaces deal 100 hit points of their kind of hit")]
    [EditorLinkedField(typeof(ContactMessageRead), nameof(CollisionMask))]
    public ContactMessage ContactMessage { get; set; }

    // Only handed on with one of the mask's message bits, the downhill pull's slope only matters with a pull
    private sealed class ContactMessageRead : ReadWhen<CollisionSurfaceData>
    {
        protected override Boolean IsRead(CollisionSurfaceData owner) =>
            (owner.CollisionMask & (SurfaceCollisionFlags.SendsContactMessageToObjects | SurfaceCollisionFlags.SendsContactMessageToPlayer)) != 0;
    }

    private sealed class DownhillSlopeRead : ReadWhen<CollisionSurfaceData>
    {
        protected override Boolean IsRead(CollisionSurfaceData owner) => owner.UnreadValue8 > 0;
    }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var collisionSurface = GetTwinItem<ITwinSurface>();
        SurfaceID = collisionSurface.SurfaceId;
        CollisionMask = collisionSurface.CollisionMask;
        StepSoundId1 = SoundOf(collisionSurface.StepSoundId1);
        StepSoundId2 = SoundOf(collisionSurface.StepSoundId2);
        ImpactSoundId = SoundOf(collisionSurface.ImpactSoundId);
        HardImpactSoundId = SoundOf(collisionSurface.HardImpactSoundId);
        LandSoundId = SoundOf(collisionSurface.LandSoundId);
        ScrapeSoundId = SoundOf(collisionSurface.ScrapeSoundId);
        ImpactParticleSystemId = collisionSurface.ImpactParticleSystemId;
        HardImpactParticleSystemId = collisionSurface.HardImpactParticleSystemId;
        StepParticleSystemId = collisionSurface.StepParticleSystemId;
        PhysicsParameters = CloneUtils.CloneArray(collisionSurface.PhysicsParameters);
        UnusedVector = CloneUtils.Clone(collisionSurface.UnusedVector);
        ContactMessage = new ContactMessage(collisionSurface.ContactMessage);
    }

    private LabURI SoundOf(UInt16 id)
    {
        return id == 0xFFFF ? LabURI.Empty : AssetManager.Get().GetUriByTwinId<SoundEffect>(Owner, id);
    }

    private static UInt16 IdOf(LabURI sound)
    {
        return sound == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)AssetManager.Get().GetAsset(sound).ExportTwinID;
    }

    // The game copies the default chunk's surfaces into a table of 128 in the order it reads them, which every chunk's collision
    // finds them in by the IDs it was written with (the decomp's SurfaceTable::Add doesn't check its count)
    private void CheckPlace()
    {
        if (LayoutIndexes.Current?.IndexOf(Owner) is not { } index)
        {
            return;
        }

        if (index >= MaxSurfaces)
        {
            throw new InvalidOperationException($"{Owner.Alias} is collision surface {index + 1} of its chunk, the game takes at most {MaxSurfaces}");
        }

        if (index != Owner.ID)
        {
            throw new InvalidOperationException($"{Owner.Alias} has ID {Owner.ID} but is the chunk's surface {index}: the collision finds surfaces by their ID, which has to be their place among the chunk's. Make a surface to fill the gap");
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckPlace();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((UInt32)CollisionMask);
        writer.Write((UInt16)SurfaceID);
        writer.Write(IdOf(StepSoundId1));
        writer.Write(IdOf(StepSoundId2));
        writer.Write(ImpactParticleSystemId);
        writer.Write(HardImpactParticleSystemId);
        writer.Write(IdOf(ImpactSoundId));
        writer.Write(IdOf(HardImpactSoundId));
        writer.Write(StepParticleSystemId);
        writer.Write(IdOf(LandSoundId));
        writer.Write(IdOf(ScrapeSoundId));
        writer.Write((UInt16)0xFFFF); // The game reads 9 IDs, the tools wrote one more
        foreach (var param in PhysicsParameters)
        {
            writer.Write(param);
        }

        UnusedVector.Write(writer);
        ContactMessage.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateSurface(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var soundSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_SOUND_EFFECTS_SECTION);

        if (StepSoundId1 != LabURI.Empty)
        {
            assetManager.GetAsset(StepSoundId1).ResolveChunkResources(factory, soundSection);
        }

        if (StepSoundId2 != LabURI.Empty)
        {
            assetManager.GetAsset(StepSoundId2).ResolveChunkResources(factory, soundSection);
        }

        if (ImpactSoundId != LabURI.Empty)
        {
            assetManager.GetAsset(ImpactSoundId).ResolveChunkResources(factory, soundSection);
        }

        if (LandSoundId != LabURI.Empty)
        {
            assetManager.GetAsset(LandSoundId).ResolveChunkResources(factory, soundSection);
        }

        if (ScrapeSoundId != LabURI.Empty)
        {
            assetManager.GetAsset(ScrapeSoundId).ResolveChunkResources(factory, soundSection);
        }

        if (HardImpactSoundId != LabURI.Empty)
        {
            assetManager.GetAsset(HardImpactSoundId).ResolveChunkResources(factory, soundSection);
        }

        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}