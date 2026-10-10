using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.Views;

namespace TT_Lab.Controls;

// A row of the Replace links tree: a part of the asset, ticked when everything under it is, or one of its links
public sealed class LinkChoice : INotifyPropertyChanged
{
    private bool? _isChecked = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Ticking or unticking anywhere in the tree, for the pickers to follow
    internal event Action? Ticked;

    public required string Caption { get; init; }
    public string? Target { get; init; }
    public ReplaceableLink? Link { get; init; }
    public LinkChoice? Parent { get; init; }
    public bool IsExpanded { get; set; } = true;
    public List<LinkChoice> Children { get; } = [];

    // A click on a partly ticked branch unticks it, like any ticked one
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            SetDown(value == true);
            Parent?.FollowChildren();
            var root = this;
            while (root.Parent != null)
            {
                root = root.Parent;
            }

            root.Ticked?.Invoke();
        }
    }

    public IEnumerable<LinkChoice> Descendants => Children.SelectMany(child => child.Descendants.Prepend(child));

    private void SetDown(bool isChecked)
    {
        Set(isChecked);
        foreach (var child in Children)
        {
            child.SetDown(isChecked);
        }
    }

    private void FollowChildren()
    {
        var states = Children.Select(child => child._isChecked).Distinct().ToList();
        Set(states.Count == 1 ? states[0] : null);
        Parent?.FollowChildren();
    }

    private void Set(bool? isChecked)
    {
        if (_isChecked == isChecked)
        {
            return;
        }

        _isChecked = isChecked;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
    }
}

// What replaces the ticked links of one kind of asset, none leaves them as they are
public sealed class LinkKindChoice : INotifyPropertyChanged
{
    private int _ticked;
    private LabURI? _picked;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal event Action? PickChanged;

    public required Type Kind { get; init; }
    public required string Name { get; init; }

    public int Ticked
    {
        get => _ticked;
        set
        {
            _ticked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public LabURI? Picked
    {
        get => _picked;
        set
        {
            _picked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PickedText)));
            PickChanged?.Invoke();
        }
    }

    public string Title => $"{Name} links ({Ticked})";

    public string PickedText => Picked switch
    {
        null => "Pick what replaces them...",
        var picked when picked == LabURI.Empty => "Empty",
        var picked => AssetManager.Get().DoesAssetExist(picked) ? AssetManager.Get().GetAsset(picked).Alias : picked.ToString()
    };
}

// Asks which of an asset's links get replaced and with what: a tree of its links to tick, ticking a part ticks every link in it, and
// a picker for each kind of asset the ticked links take, offering what every ticked link of the kind takes, so one asset never goes
// into links of two kinds. None when cancelled
public partial class ReplaceLinksDialogue : Window
{
    private readonly LinkChoice? _root;
    private readonly Dictionary<Type, LinkKindChoice> _kinds = [];
    private readonly ObservableCollection<LinkKindChoice> _shownKinds = [];
    private readonly Button? _replace;
    private bool _isFollowing;

    public ReplaceLinksDialogue()
    {
        InitializeComponent();
    }

    public ReplaceLinksDialogue(LinkBranch tree) : this()
    {
        Title = $"Replace links in {tree.Caption}";
        Kinds.ItemsSource = _shownKinds;
        if (!tree.AllLinks.Any())
        {
            Message.Text = $"{tree.Caption} has no links that can be replaced.";
            AddButton("Close", true, true, Close);
            return;
        }

        Message.Text = "Tick the links to replace, then pick what replaces each kind of link. A link only gets what its own field takes.";
        _root = Choice(tree, null);
        _root.Ticked += Follow;
        Links.ItemsSource = new[] { _root };
        _replace = AddButton("Replace", true, false, Answer);
        AddButton("Cancel", false, true, Close);
        Follow();
    }

    // What got picked once Replace was pressed
    internal LinkReplacementChoice? Answered { get; private set; }

    public static async Task<LinkReplacementChoice?> Ask(LinkBranch tree)
    {
        var dialogue = new ReplaceLinksDialogue(tree);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue.Answered;
    }

    // The way TT Lab names kinds of assets elsewhere: OGI stays one word, AI paths and positions are AI's
    internal static string KindName(Type kind)
    {
        if (kind == typeof(IAsset))
        {
            return "Asset";
        }

        var words = Regex.Replace(kind.Name, "(?<!^)(?=[A-Z][a-z])|(?<=[a-z])(?=[A-Z])", " ");
        return words.StartsWith("Ai ", StringComparison.Ordinal) ? $"AI {words[3..]}" : words;
    }

    private static LinkChoice Choice(LinkBranch branch, LinkChoice? parent)
    {
        var choice = new LinkChoice { Caption = branch.Caption, Parent = parent };
        choice.Children.AddRange(branch.Branches.Select(inner => Choice(inner, choice)));
        choice.Children.AddRange(branch.Links.Select(link => new LinkChoice
        {
            Caption = link.Caption,
            Target = $"→ {TargetName(link.Original)}",
            Link = link,
            Parent = choice
        }));
        return choice;
    }

    private static string TargetName(LabURI link) => link == LabURI.Empty ? "Empty"
        : AssetManager.Get().DoesAssetExist(link) ? AssetManager.Get().GetAsset(link).Alias : link.ToString();

    private List<ReplaceableLink> TickedLinks() =>
        _root?.Descendants.Where(choice => choice is { Link: not null, IsChecked: true }).Select(choice => choice.Link!).ToList() ?? [];

    // The pickers of the kinds ticked, in the order the tree has them, and picks the ticked links no longer all take dropped
    private void Follow()
    {
        if (_isFollowing)
        {
            return;
        }

        _isFollowing = true;
        try
        {
            FollowTicks();
        }
        finally
        {
            _isFollowing = false;
        }
    }

    private void FollowTicks()
    {
        var ticked = TickedLinks();
        Problem.Text = string.Empty;
        foreach (var group in _root!.Descendants.Select(choice => choice.Link).OfType<ReplaceableLink>().GroupBy(link => link.Kind))
        {
            if (!_kinds.TryGetValue(group.Key, out var kind))
            {
                kind = new LinkKindChoice { Kind = group.Key, Name = KindName(group.Key) };
                kind.PickChanged += Follow;
                _kinds.Add(group.Key, kind);
            }

            var tickedOfKind = ticked.Where(link => link.Kind == group.Key).ToList();
            kind.Ticked = tickedOfKind.Count;
            if (kind.Picked is { } picked && tickedOfKind.Any(link => !link.Candidates.Contains(picked)))
            {
                kind.Picked = null;
                Problem.Text = $"Not every ticked {kind.Name} link takes what was picked for them, pick again.";
            }
        }

        var shown = _kinds.Values.Where(kind => kind.Ticked > 0).ToList();
        _shownKinds.Clear();
        foreach (var kind in shown)
        {
            _shownKinds.Add(kind);
        }

        if (_replace != null)
        {
            _replace.IsEnabled = ticked.Any(link => _kinds[link.Kind].Picked is { } picked && picked != link.Original);
        }
    }

    private async void Pick_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not LinkKindChoice kind)
        {
            return;
        }

        var candidates = LinkReplacement.CandidatesFor(TickedLinks().Where(link => link.Kind == kind.Kind));
        if (candidates.Count == 0)
        {
            Problem.Text = $"Nothing fits every ticked {kind.Name} link, untick some of them.";
            return;
        }

        var browser = new ResourceBrowserViewModel(kind.Kind, candidates, kind.Picked is { } picked && candidates.Contains(picked) ? picked : candidates[0]);
        var picker = new ResourceBrowserView { DataContext = browser };
        if (await picker.ShowDialog<bool?>(this) == true)
        {
            kind.Picked = browser.SelectedLink;
        }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is LinkKindChoice kind)
        {
            kind.Picked = null;
        }
    }

    private void Answer()
    {
        var replacements = _kinds.Values.Where(kind => kind.Picked != null).ToDictionary(kind => kind.Kind, kind => kind.Picked!);
        Answered = new LinkReplacementChoice(TickedLinks(), replacements);
        Close();
    }

    private Button AddButton(string text, bool isDefault, bool isCancel, Action click)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 90,
            Margin = new Avalonia.Thickness(5),
            Padding = new Avalonia.Thickness(8, 3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        button.Click += (_, _) => click();
        Answers.Children.Add(button);
        return button;
    }
}
