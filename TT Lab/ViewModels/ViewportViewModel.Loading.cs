using System;
using System.Threading;
using Avalonia.Threading;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Controls;

namespace TT_Lab.ViewModels;

// What the scene is loading, shown over the viewport in the game's font until it renders: the stage, what's being read or built and how
// far along the building is
public partial class ViewportViewModel
{
    public const string ReadingStage = "Reading assets";
    public const string StartingStage = "Starting the renderer";
    public const string BuildingStage = "Building the scene";

    private readonly object _loadingLock = new();
    private (string Stage, string Detail, int Done, int Total) _loadingReport = (ReadingStage, string.Empty, 0, 0);
    private bool _isLoadingReportPosted;
    private int _assetsRead;

    [Reactive(SetModifier = AccessModifier.Private)]
    private PsfGlyphs? _loadingGlyphs;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _loadingStage = ReadingStage;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _loadingDetail = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private double _loadingProgress;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isLoadingProgressKnown;

    /// <summary>
    /// Shows the scene of the asset loading, in the font of its version of the game
    /// </summary>
    public void BeginLoading(IAsset asset)
    {
        var package = asset.Package;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                LoadingGlyphs = PsfGlyphs.OfDefaultFont(package);
            }
            catch (Exception ex)
            {
                Log.WriteLine($"The game's font couldn't be read for the viewport: {ex.Message}", Log.LogType.Warning);
            }
        });
    }

    /// <summary>
    /// An asset the scene's document reads, from the thread building it
    /// </summary>
    internal void ReportRead(IAsset asset)
    {
        var count = Interlocked.Increment(ref _assetsRead);
        ReportLoading(ReadingStage, $"{asset.Alias} ({count})");
    }

    /// <summary>
    /// What the scene's loading is at, from any thread. The UI shows the newest report whenever it gets to it
    /// </summary>
    internal void ReportLoading(string stage, string detail = "", int done = 0, int total = 0)
    {
        lock (_loadingLock)
        {
            _loadingReport = (stage, detail, done, total);
            if (_isLoadingReportPosted)
            {
                return;
            }

            _isLoadingReportPosted = true;
        }

        Dispatcher.UIThread.Post(ShowLoadingReport, DispatcherPriority.Background);
    }

    private void ShowLoadingReport()
    {
        (string Stage, string Detail, int Done, int Total) report;
        lock (_loadingLock)
        {
            report = _loadingReport;
            _isLoadingReportPosted = false;
        }

        LoadingStage = report.Stage;
        LoadingDetail = report.Detail;
        IsLoadingProgressKnown = report.Total > 0;
        LoadingProgress = report.Total > 0 ? (double)report.Done / report.Total : 0.0;
    }
}
