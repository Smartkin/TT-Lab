using System;
using Caliburn.Micro;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Composite;

public class InstanceStateFlagsViewModel : Screen, IDirtyMarker
{
    private Enums.InstanceState _stateFlags;
    private DirtyTracker _dirtyTracker;
    

    public InstanceStateFlagsViewModel(Enums.InstanceState stateFlags)
    {
        _stateFlags = stateFlags;
        _dirtyTracker = new DirtyTracker(this);
    }
    
    [MarkDirty]
    public UInt32 StateFlags
    {
        get
        {
            return (UInt32)_stateFlags;
        }
        set
        {
            var flags = MiscUtils.ConvertEnum<Enums.InstanceState>(value);
            if (flags != _stateFlags)
            {
                _stateFlags = flags;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsDeactivated));
                NotifyOfPropertyChange(nameof(CollisionActive));
                NotifyOfPropertyChange(nameof(Visible));
                NotifyOfPropertyChange(nameof(ReceiveOnTriggerSignals));
                NotifyOfPropertyChange(nameof(CanDamageCharacter));
                NotifyOfPropertyChange(nameof(CanAlwaysDamageCharacter));
                NotifyOfPropertyChange(nameof(ShadowActive));
                NotifyOfPropertyChange(nameof(PlayableCharacterCanMoveAlong));
                NotifyOfPropertyChange(nameof(Unknown3));
                NotifyOfPropertyChange(nameof(SyncCrossChunkState));
                NotifyOfPropertyChange(nameof(Unknown5));
                NotifyOfPropertyChange(nameof(SolidToBodySlam));
                NotifyOfPropertyChange(nameof(SolidToSlide));
                NotifyOfPropertyChange(nameof(SolidToSpin));
                NotifyOfPropertyChange(nameof(SolidToTwinSlam));
                NotifyOfPropertyChange(nameof(SolidToThrownCortex));
                NotifyOfPropertyChange(nameof(Targettable));
                NotifyOfPropertyChange(nameof(BulletsWillBounceBack));
                NotifyOfPropertyChange(nameof(Unknown13));
                NotifyOfPropertyChange(nameof(Unknown14));
                NotifyOfPropertyChange(nameof(Unknown15));
                NotifyOfPropertyChange(nameof(Unknown16));
                NotifyOfPropertyChange(nameof(Unknown17));
                NotifyOfPropertyChange(nameof(Unknown18));
                NotifyOfPropertyChange(nameof(Unknown19));
                NotifyOfPropertyChange(nameof(Unknown20));
                NotifyOfPropertyChange(nameof(Unknown21));
                NotifyOfPropertyChange(nameof(Unknown22));
                NotifyOfPropertyChange(nameof(Unknown23));
                NotifyOfPropertyChange(nameof(Unknown24));
                NotifyOfPropertyChange(nameof(Unknown25));
                NotifyOfPropertyChange(nameof(Unknown26));
            }
        }
    }
    [MarkDirty]
    public Boolean IsDeactivated
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Deactivated);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Deactivated, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean CollisionActive
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.CollisionActive);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.CollisionActive, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Visible
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Visible);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Visible, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean ReceiveOnTriggerSignals
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.ReceiveOnTriggerSignals);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.ReceiveOnTriggerSignals, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean CanDamageCharacter
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.CanDamageCharacter);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.CanDamageCharacter, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean CanAlwaysDamageCharacter
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.CanAlwaysDamageCharacter);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.CanAlwaysDamageCharacter, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean ShadowActive
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.ShadowActive);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.ShadowActive, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean PlayableCharacterCanMoveAlong
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.PlayableCharacterCanMoveAlong);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.PlayableCharacterCanMoveAlong, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown3
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown1);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown1, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SyncCrossChunkState
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SyncCrossChunkState);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SyncCrossChunkState, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown5
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown2);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown2, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SolidToBodySlam
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SolidToBodySlam);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SolidToBodySlam, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SolidToSlide
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SolidToSlide);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SolidToSlide, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SolidToSpin
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SolidToSpin);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SolidToSpin, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SolidToTwinSlam
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SolidToTwinSlam);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SolidToTwinSlam, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean SolidToThrownCortex
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.SolidToThrownCortex);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.SolidToThrownCortex, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Targettable
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Targettable);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Targettable, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean BulletsWillBounceBack
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.BulletsWillBounceBack);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.BulletsWillBounceBack, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown13
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown3);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown3, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown14
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown4);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown4, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown15
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown5);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown5, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown16
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown6);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown6, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown17
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown7);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown7, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown18
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown8);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown8, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown19
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown9);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown9, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown20
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown10);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown10, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown21
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown11);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown11, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown22
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown12);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown12, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown23
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown13);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown13, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown24
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown14);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown14, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown25
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown15);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown15, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }
    [MarkDirty]
    public Boolean Unknown26
    {
        get => _stateFlags.HasFlag(Enums.InstanceState.Unknown16);
        set
        {
            _stateFlags = _stateFlags.ChangeFlag(Enums.InstanceState.Unknown16, value);
                
            NotifyOfPropertyChange(nameof(StateFlags));
        }
    }

    public void ResetDirty()
    {
        _dirtyTracker.ResetDirty();
    }

    public bool IsDirty => _dirtyTracker.IsDirty;
}