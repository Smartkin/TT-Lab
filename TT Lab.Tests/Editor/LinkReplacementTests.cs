using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Tests.Editor;

// The asset header's Replace links: the links the inspector lets be edited, in its tree, ticked and replaced a kind at a time with what
// every ticked link of the kind takes, in one step of the history
[Collection(ProjectCollection.Name)]
public sealed class LinkReplacementTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private BehaviourGraph Graph(string name, uint id)
    {
        var graph = _project.Add(new BehaviourGraph(), name, id, _project.Project.Ps2Package);
        graph.SetData(new BehaviourGraphData(graph) { Graph = $"graph {name} {{ state Start {{ }} }}" });
        return graph;
    }

    private GameObject Object(string name, uint id)
    {
        var gameObject = _project.Add(new GameObject(), name, id, _project.Project.Ps2Package);
        gameObject.SetData(new GameObjectData(gameObject) { Name = name });
        return gameObject;
    }

    private sealed record Crate(DocumentViewModel Document, GameObjectData Data, BehaviourGraph Walk, BehaviourGraph Run, BehaviourGraph Jump, GameObject Fruit);

    // Two behaviour slots and an object slot
    private Crate MakeCrate()
    {
        var walk = Graph("COM_WALK", 0x101);
        var run = Graph("COM_RUN", 0x103);
        var jump = Graph("COM_JUMP", 0x105);
        var fruit = Object("FRUIT", 0x20);
        var crate = _project.Add(new GameObject(), "CRATE", 0x10, _project.Project.Ps2Package);
        var data = new GameObjectData(crate) { Name = "CRATE", BehaviourSlots = [walk.URI, run.URI], ObjectSlots = [fruit.URI] };
        crate.SetData(data);
        var document = new DocumentViewModel(crate);
        document.Initialize();
        return new Crate(document, data, walk, run, jump, fruit);
    }

    private static LinkBranch Branch(LinkBranch tree, string caption) => tree.Branches.Single(branch => branch.Caption == caption);

    [AvaloniaFact]
    public void TheTreeHasTheLinksTheInspectorEditsWithTheKindTheyTake()
    {
        var crate = MakeCrate();

        var tree = LinkReplacement.Find(crate.Document, crate.Document.PropertyGraph.Root);

        Assert.Equal("CRATE", tree.Caption);
        var behaviours = Branch(tree, "Behaviour Slots");
        // Captioned like the inspector shows them, by what the game starts the slots with
        Assert.Equal(["Behaviour Slot 0 · Spawned", "Behaviour Slot 1 · Triggered"], behaviours.Links.Select(link => link.Caption));
        Assert.All(behaviours.Links, link => Assert.Equal(typeof(BehaviourGraph), link.Kind));
        Assert.Equal([crate.Walk.URI, crate.Run.URI], behaviours.Links.Select(link => link.Original));
        Assert.Equal(typeof(GameObject), Assert.Single(Branch(tree, "Object Slots").Links).Kind);
        Assert.Equal(3, tree.AllLinks.Count());

        var candidates = LinkReplacement.CandidatesFor(behaviours.Links);
        Assert.Contains(crate.Jump.URI, candidates);
        Assert.DoesNotContain(crate.Fruit.URI, candidates);

        // A link the inspector shows read only isn't one to replace
        crate.Document.PropertyGraph.Find("Root.AssetData.BehaviourSlots[1]")!.IsReadOnly = true;
        Assert.Equal(["Behaviour Slot 0 · Spawned"], Branch(LinkReplacement.Find(crate.Document, crate.Document.PropertyGraph.Root), "Behaviour Slots").Links.Select(link => link.Caption));
    }

    [AvaloniaFact]
    public void TheTickedLinksOfAKindGetItsReplacementInOneStep()
    {
        var crate = MakeCrate();
        var root = crate.Document.PropertyGraph.Root;
        var tree = LinkReplacement.Find(crate.Document, root);

        var replaced = LinkReplacement.Apply(crate.Document, root,
            new LinkReplacementChoice(tree.AllLinks.ToList(), new Dictionary<Type, LabURI> { [typeof(BehaviourGraph)] = crate.Jump.URI }));

        Assert.Equal(2, replaced);
        Assert.Equal([crate.Jump.URI, crate.Jump.URI], crate.Data.BehaviourSlots);
        // A kind without a replacement keeps its links
        Assert.Equal([crate.Fruit.URI], crate.Data.ObjectSlots);
        Assert.Equal("Replaced links in 'CRATE'", crate.Document.History.Current.Description);
        crate.Document.Undo();
        Assert.Equal([crate.Walk.URI, crate.Run.URI], crate.Data.BehaviourSlots);
        crate.Document.Redo();
        Assert.Equal([crate.Jump.URI, crate.Jump.URI], crate.Data.BehaviourSlots);
    }

    // One asset never goes into links of another kind, nor where a link's own rules don't take it
    [AvaloniaFact]
    public void ALinkOnlyGetsWhatItsOwnFieldTakes()
    {
        var crate = MakeCrate();
        var root = crate.Document.PropertyGraph.Root;
        var tree = LinkReplacement.Find(crate.Document, root);
        var steps = crate.Document.History.Current;

        var replaced = LinkReplacement.Apply(crate.Document, root, new LinkReplacementChoice(tree.AllLinks.ToList(),
            new Dictionary<Type, LabURI> { [typeof(BehaviourGraph)] = crate.Fruit.URI, [typeof(GameObject)] = crate.Walk.URI }));

        Assert.Equal(0, replaced);
        Assert.Equal([crate.Walk.URI, crate.Run.URI], crate.Data.BehaviourSlots);
        Assert.Equal([crate.Fruit.URI], crate.Data.ObjectSlots);
        Assert.Same(steps, crate.Document.History.Current);

        // An instance's links to instances only take its own chunk's, links of two chunks take nothing in common
        var own = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Own");
        own.SetData(new ObjectInstanceData(own));
        var elsewhere = _project.Add(new ObjectInstance { Chunk = "levels/other", LayoutID = 0 }, "Elsewhere");
        elsewhere.SetData(new ObjectInstanceData(elsewhere));
        var links = new[] { own, elsewhere }.Select(instance =>
        {
            var document = new DocumentViewModel(instance);
            document.Initialize();
            document.PropertyGraph.Find("Root.AssetData.Instances")!.AddElement();
            return Branch(LinkReplacement.Find(document, document.PropertyGraph.Root), "Instances").Links.Single();
        }).ToList();
        Assert.Contains(own.URI, LinkReplacement.CandidatesFor(links.Take(1)));
        Assert.DoesNotContain(elsewhere.URI, LinkReplacement.CandidatesFor(links.Take(1)));
        Assert.DoesNotContain(LinkReplacement.CandidatesFor(links), link => link != LabURI.Empty);
    }

    [AvaloniaFact]
    public void TickingAPartTicksItsLinksAndEachKindTickedGetsAPicker()
    {
        var crate = MakeCrate();
        var dialogue = new ReplaceLinksDialogue(LinkReplacement.Find(crate.Document, crate.Document.PropertyGraph.Root));
        dialogue.Show();
        Pump();
        var root = Assert.Single(Assert.IsAssignableFrom<IEnumerable<LinkChoice>>(dialogue.Links.ItemsSource));
        var kinds = Assert.IsAssignableFrom<IEnumerable<LinkKindChoice>>(dialogue.Kinds.ItemsSource);
        var replace = dialogue.Answers.Children.OfType<Button>().Single(button => (string?)button.Content == "Replace");
        Assert.False(root.IsChecked);
        Assert.Empty(kinds);
        Assert.False(replace.IsEnabled);

        root.IsChecked = true;
        Assert.All(root.Descendants, choice => Assert.True(choice.IsChecked));
        Assert.Equal(["Behaviour Graph links (2)", "Game Object links (1)"], kinds.Select(kind => kind.Title));

        // Unticking a part leaves the parts around it partly ticked, its kind's picker goes
        root.Children.Single(choice => choice.Caption == "Object Slots").IsChecked = false;
        Assert.Null(root.IsChecked);
        var behaviours = Assert.Single(kinds);
        Assert.Equal("Behaviour Graph links (2)", behaviours.Title);
        Assert.False(replace.IsEnabled);

        behaviours.Picked = crate.Jump.URI;
        Assert.Equal("COM_JUMP", behaviours.PickedText);
        Assert.True(replace.IsEnabled);
        replace.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var answer = dialogue.Answered!;
        Assert.Equal([crate.Walk.URI, crate.Run.URI], answer.Links.Select(link => link.Original));
        Assert.Equal(crate.Jump.URI, Assert.Single(answer.Replacements).Value);
    }

    // Shift and a click on a link ticks it and every other link to the same asset, again unticks them all; a plain click ticks only it
    [AvaloniaFact]
    public void ShiftAndAClickOnALinkTicksEveryLinkToTheSameAsset()
    {
        var walk = Graph("COM_WALK", 0x101);
        var run = Graph("COM_RUN", 0x103);
        var crate = _project.Add(new GameObject(), "CRATE", 0x10, _project.Project.Ps2Package);
        crate.SetData(new GameObjectData(crate) { Name = "CRATE", BehaviourSlots = [walk.URI, run.URI, walk.URI] });
        var document = new DocumentViewModel(crate);
        document.Initialize();
        var dialogue = new ReplaceLinksDialogue(LinkReplacement.Find(document, document.PropertyGraph.Root));
        dialogue.Show();
        var root = Assert.Single(Assert.IsAssignableFrom<IEnumerable<LinkChoice>>(dialogue.Links.ItemsSource));
        var walks = root.Descendants.Where(choice => choice.Link?.Original == walk.URI).ToList();
        var running = Assert.Single(root.Descendants, choice => choice.Link?.Original == run.URI);
        Assert.Equal(2, walks.Count);

        // On the link's caption
        Click(dialogue, walks[1], RawInputModifiers.Shift, onCaption: true);
        Assert.All(walks, choice => Assert.True(choice.IsChecked));
        Assert.False(running.IsChecked);
        Assert.Equal(["Behaviour Graph links (2)"], Assert.IsAssignableFrom<IEnumerable<LinkKindChoice>>(dialogue.Kinds.ItemsSource).Select(kind => kind.Title));

        // On its check box
        Click(dialogue, walks[0], RawInputModifiers.Shift, onCaption: false);
        Assert.All(walks, choice => Assert.False(choice.IsChecked));

        Click(dialogue, walks[0], RawInputModifiers.None, onCaption: false);
        Assert.True(walks[0].IsChecked);
        Assert.False(walks[1].IsChecked);
        dialogue.Close();
    }

    // Clicks go by what the compositor got last
    private static void Click(Window window, LinkChoice choice, RawInputModifiers modifiers, bool onCaption)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Control target = onCaption
            ? window.GetVisualDescendants().OfType<TextBlock>().First(text => text.DataContext == choice && text.Text == choice.Caption)
            : window.GetVisualDescendants().OfType<CheckBox>().First(box => box.DataContext == choice);
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AnAssetWithoutLinksSaysSo()
    {
        var gameObject = Object("EMPTY", 0x30);
        var document = new DocumentViewModel(gameObject);
        document.Initialize();
        var tree = LinkReplacement.Find(document, document.PropertyGraph.Root);
        Assert.Empty(tree.AllLinks);

        var dialogue = new ReplaceLinksDialogue(tree);
        dialogue.Show();
        Pump();

        Assert.Equal("EMPTY has no links that can be replaced.", dialogue.Message.Text);
        Assert.Equal(["Close"], dialogue.Answers.Children.OfType<Button>().Select(button => (string?)button.Content));
    }
}
