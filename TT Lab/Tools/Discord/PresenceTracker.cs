using System;

namespace TT_Lab.Tools.Discord;

// Works out what Discord shows from TT Lab's state, tick by tick: the timer counts from when the project's tree showed up or the last
// break ended, a break is no TT Lab window having focus for 10 minutes while a project is open and the game Play started isn't running
internal sealed class PresenceTracker
{
    internal static readonly TimeSpan BreakAfter = TimeSpan.FromMinutes(10);

    private DateTime? _readyAt;
    private DateTime? _awaySince;
    private DateTime? _breakEndedAt;
    private bool _onBreak;

    public PresenceActivity Follow(PresenceInputs inputs, DateTime now)
    {
        if (inputs.Phase != ProjectPhase.Ready || inputs.ReadyAt == null)
        {
            Forget();
            return PresenceActivity.For(inputs.Phase == ProjectPhase.Ready ? ProjectPhase.None : inputs.Phase, false, false, null, null);
        }

        // Another project, or the same one opened again
        if (_readyAt != inputs.ReadyAt)
        {
            Forget();
            _readyAt = inputs.ReadyAt;
        }

        if (inputs.HasFocus || inputs.GameActive)
        {
            if (_onBreak)
            {
                _onBreak = false;
                _breakEndedAt = now;
            }

            _awaySince = null;
        }
        else
        {
            _awaySince ??= now;
            _onBreak |= now - _awaySince.Value >= BreakAfter;
        }

        var start = _breakEndedAt is { } ended && ended > _readyAt ? ended : _readyAt;
        return PresenceActivity.For(ProjectPhase.Ready, _onBreak, inputs.GameActive, inputs.ActiveAsset, start);
    }

    private void Forget()
    {
        _readyAt = null;
        _awaySince = null;
        _breakEndedAt = null;
        _onBreak = false;
    }
}
