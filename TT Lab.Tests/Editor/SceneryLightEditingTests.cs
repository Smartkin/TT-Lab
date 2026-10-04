using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets.Instance;
using LightDirectionRotation = TT_Lab.Rendering.Objects.LightDirectionRotation;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Tests.Editor;

// A scenery's lights are edited in the inspector: a spot light's cone in degrees with the cosines the game lights with following it,
// new lights starting like a new chunk's, the values the game never reads left out
[Collection(ProjectCollection.Name)]
public sealed class SceneryLightEditingTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (SceneryData Data, DocumentViewModel Document) Open(params Light[] lights)
    {
        var scenery = _project.Add(new Scenery { Chunk = "default" }, "Scenery");
        var data = new SceneryData(scenery)
        {
            AmbientLights = lights.OfType<AmbientLight>().ToList(),
            DirectionalLights = lights.OfType<DirectionalLight>().ToList(),
            PointLights = lights.OfType<PointLight>().ToList(),
            SpotLights = lights.OfType<SpotLight>().ToList(),
        };
        scenery.SetData(data);
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        return (data, document);
    }

    [AvaloniaFact]
    public void TheSpotConeIsEditedInDegreesAndTheCosinesFollow()
    {
        var spot = DefaultLights.Spot();
        var (_, document) = Open(spot);
        var cone = document.PropertyGraph.Find("Root.AssetData.SpotLights[0].ConeAngle")!;
        var falloff = document.PropertyGraph.Find("Root.AssetData.SpotLights[0].FalloffAngle")!;
        var inner = document.PropertyGraph.Find("Root.AssetData.SpotLights[0].InnerConeCosine")!;
        var outer = document.PropertyGraph.Find("Root.AssetData.SpotLights[0].OuterConeCosine")!;

        Assert.IsType<AngleFieldViewModel>(EditorDescRegistry.GetDesc(document, cone).Construct());
        Assert.True(EditorDescRegistry.GetDesc(document, inner).Construct().IsReadOnly);
        Assert.True(EditorDescRegistry.GetDesc(document, outer).Construct().IsReadOnly);
        var degrees = MathF.PI / 180;

        // Angles are kept in 65536ths of a turn, 15 degrees is a little more
        cone.SetValue(AngleFieldViewModel.ToUnits(90, typeof(UInt32)));
        Assert.Equal(MathF.Cos(45 * degrees), spot.InnerConeCosine, 1e-4f);
        Assert.Equal(MathF.Cos(60 * degrees), spot.OuterConeCosine, 1e-4f);
        falloff.SetValue(AngleFieldViewModel.ToUnits(30, typeof(UInt32)));
        Assert.Equal(MathF.Cos(75 * degrees), spot.OuterConeCosine, 1e-4f);

        // A step takes the cosines back with the angle
        document.Undo();
        document.Undo();
        Assert.Equal(MathF.Cos(30 * degrees), spot.InnerConeCosine, 1e-4f);
        Assert.Equal(MathF.Cos(45 * degrees), spot.OuterConeCosine, 1e-4f);
    }

    // The game's lights keep colors adding up to 1 with the brightness in the intensity: the picker shows the hue at full brightness and a
    // picked color keeps the total
    [AvaloniaFact]
    public void LightColorsArePickedAndKeepTheirTotal()
    {
        var ambient = DefaultLights.Ambient();
        ambient.Color = new Vector4(1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f, 0.25f);
        var (_, document) = Open(ambient);
        var field = Assert.IsType<LightColorFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.AmbientLights[0].Color")!).Construct());
        var window = new Avalonia.Controls.Window { Content = new Avalonia.Controls.ContentControl { Content = field }, Width = 400, Height = 100 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var picker = window.GetVisualDescendants().OfType<Avalonia.Controls.ColorPicker>().Single();

        Assert.Equal(Avalonia.Media.Colors.White, field.ShownColor);
        Assert.Equal(Avalonia.Media.Colors.White, picker.Color);

        field.ShownColor = Avalonia.Media.Color.FromRgb(255, 51, 0);
        Assert.Equal((5.0f / 6.0f, 1.0f / 6.0f, 0.0f, 0.25f), (ambient.Color.X, ambient.Color.Y, ambient.Color.Z, ambient.Color.W));
        Assert.Equal(Avalonia.Media.Color.FromRgb(255, 51, 0), field.ShownColor);

        document.Undo();
        Assert.Equal(1.0f / 3.0f, ambient.Color.X);
        Assert.Equal(Avalonia.Media.Colors.White, field.ShownColor);

        // Picked in the picker itself
        picker.Color = Avalonia.Media.Color.FromRgb(0, 0, 255);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal((0.0f, 0.0f, 1.0f), (ambient.Color.X, ambient.Color.Y, ambient.Color.Z));
        window.Close();
    }

    // Bit 8 of the header and the bounds the game works out at load are never read
    [AvaloniaFact]
    public void WhatTheGameNeverReadsIsLeftOut()
    {
        var (_, document) = Open(DefaultLights.Directional());

        foreach (var hidden in new[] { "BoundsMin", "BoundsMax", "Enabled", "Leftover", "Type" })
        {
            var node = document.PropertyGraph.Find($"Root.AssetData.DirectionalLights[0].{hidden}");
            Assert.NotNull(node);
            Assert.False(EditorDescRegistry.GetDesc(document, node!).Construct().IsVisible, hidden);
        }

        Assert.True(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.DirectionalLights[0].Intensity")!).Construct().IsVisible);
    }

    [AvaloniaFact]
    public void NewLightsStartLikeANewChunks()
    {
        var (data, document) = Open();

        var added = document.PropertyGraph.Find("Root.AssetData.PointLights")!.AddElement();

        Assert.NotNull(added);
        var point = Assert.Single(data.PointLights);
        Assert.Equal(DefaultLights.Intensity, point.Intensity);
        Assert.Equal(DefaultLights.ThirdGrey, point.Color.X);
        Assert.Equal(1, point.AttenuationPower);
        Assert.Equal(1.0f, point.Position.W);
    }

    // The game lights by the direction as it is, turning it keeps its length
    [AvaloniaFact]
    public void TurningALightKeepsTheLengthOfItsDirection()
    {
        var directional = DefaultLights.Directional();
        directional.Direction = new Vector4(0, 0, 2, 0);
        var (_, document) = Open(directional);
        var rotation = new LightDirectionRotation(document.PropertyGraph.Find("Root.AssetData.DirectionalLights[0].Direction")!);

        var turned = (Vector4)rotation.ToData(quat.FromAxisAngle(MathF.PI / 2, vec3.UnitX));

        Assert.Equal((0.0f, -2.0f, 0.0f, 0.0f), (MathF.Round(turned.X, 5), MathF.Round(turned.Y, 5), MathF.Round(turned.Z, 5), turned.W));
        var shown = rotation.ToRotation(new Vector4(0, -2, 0, 0)) * vec3.UnitZ;
        Assert.Equal(-1.0f, shown.y, 1e-5f);
    }
}
