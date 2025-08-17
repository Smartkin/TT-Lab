using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.ViewModels
{
    public abstract class ResourceEditorViewModel : Conductor<IScreen>.Collection.AllActive, IEditorViewModel, IDirtyMarker
    {
        protected readonly DirtyTracker DirtyTracker;
        
        public LabURI EditableResource { get; set; } = LabURI.Empty;

        public void ResetDirty()
        {
            DirtyTracker.ResetDirty();
            NotifyOfPropertyChange(nameof(IsDirty));
        }

        public Boolean IsDirty => DirtyTracker.IsDirty;

        private Boolean _startedEditing;
        private Boolean _usingConfirmClose = false;

        protected Boolean IsDataLoaded { get; private set; }
        protected Boolean IgnoreUnsavedPopup { get; set; } = false;
        
        private readonly ICommand _unsavedChangesCommand;
        private readonly OpenDialogueCommand.DialogueResult _dialogueResult = new();
        private string _tabDisplayName = string.Empty;

        public ResourceEditorViewModel()
        {
            _unsavedChangesCommand = new OpenDialogueCommand(() => new UnsavedChangesDialogue(_dialogueResult, AssetManager.Get().GetAsset(EditableResource).GetResourceTreeElement()));
            DirtyTracker = new DirtyTracker(this, () =>
            {
                EditorChangesHappened();
                NotifyOfPropertyChange(nameof(IsDirty));
            });
        }
        
        public override Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsDirty && !IgnoreUnsavedPopup)
            {
                _usingConfirmClose = true;
                _unsavedChangesCommand.Execute();
            }

            return Task.Factory.StartNew(() =>
            {
                if (!IsDirty || IgnoreUnsavedPopup)
                {
                    return true;
                }
            
                if (_dialogueResult.Result == null)
                {
                    return false;
                }
            
                var result = MiscUtils.ConvertEnum<UnsavedChangesDialogue.AnswerResult>(_dialogueResult.Result);
                switch (result)
                {
                    case UnsavedChangesDialogue.AnswerResult.YES:
                    case UnsavedChangesDialogue.AnswerResult.DISCARD:
                        return true;
                    case UnsavedChangesDialogue.AnswerResult.CANCEL:
                    default:
                        return false;
                }
            }, cancellationToken);
        }

        public void SaveChanges(bool force = false)
        {
            if (!force)
            {
                if (!IsDirty)
                {
                    return;
                }

                ResetDirty();

                if (_usingConfirmClose && (_dialogueResult.Result == null ||
                                           MiscUtils.ConvertEnum<UnsavedChangesDialogue.AnswerResult>(_dialogueResult
                                               .Result) == UnsavedChangesDialogue.AnswerResult.DISCARD))
                {
                    return;
                }
            }

            Save();
            var asset = AssetManager.Get().GetAsset(EditableResource);
            asset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
        }

        protected virtual void Save()
        {
            ResetDirty();
        }
        
        public abstract void LoadData();
        public override void NotifyOfPropertyChange([CallerMemberName] String propertyName = null)
        {
            if (_startedEditing && propertyName != nameof(IsDirty))
            {
                DirtyTracker.MarkDirtyByProperty(this, new PropertyChangedEventArgs(propertyName));
            }
        
            base.NotifyOfPropertyChange(propertyName);
        }

        protected override void OnViewAttached(object view, object context)
        {
            LoadData();
            
            IsDataLoaded = true;
            ResetDirty();
            
            base.OnViewAttached(view, context);
        }

        protected override void OnViewReady(Object view)
        {
            base.OnViewReady(view);

            ResetDirty();
            _startedEditing = true;
            if (Parent is TabbedEditorViewModel tabbedEditorViewModel)
            {
                _tabDisplayName = tabbedEditorViewModel.DisplayName;
            }
        }

        protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            if (!AssetManager.Get().DoesAssetExist(EditableResource))
            {
                return base.OnDeactivateAsync(true, cancellationToken);
            }

            if (close)
            {
                _startedEditing = false;
                // If we are ignoring unsaved popup then something else must be in charge of saving our viewmodel
                if (!IgnoreUnsavedPopup)
                {
                    SaveChanges();
                }
            }
            
            var asset = AssetManager.Get().GetAsset(EditableResource);
            if (asset.IsLoaded && close)
            {
                asset.GetData<AbstractAssetData>().Dispose();
            }

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        private void EditorChangesHappened()
        {
            if (Parent is not TabbedEditorViewModel parent || !_startedEditing)
            {
                return;
            }
            
            if (IsDirty)
            {
                parent.DisplayName = _tabDisplayName + "*";
            }
            else
            {
                parent.DisplayName = _tabDisplayName;
            }
        }
    }
}
