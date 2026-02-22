using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels
{
    public abstract class EditorsViewerViewModel : Conductor<TabbedEditorViewModel>.Collection.OneActive
    {
        public virtual async Task CloseEditorTab(TabbedEditorViewModel editor)
        {
            await DeactivateItemAsync(editor, true);
        }

        public virtual void SaveEditorTab(TabbedEditorViewModel editor)
        {
            editor.Document.Save(string.Empty);
        }

        public override async Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var result = true;
            // foreach (var editor in Items)
            // {
            //     result = await editor.ActiveItem.CanCloseAsync(cancellationToken);
            //     if (!result)
            //     {
            //         break;
            //     }
            // }
            
            return result;
        }

        public virtual void Save()
        {
            foreach (var item in Items)
            {
                item.Document.Save(string.Empty);
            }
        }


        protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            DeactivateItemAsync(ActiveItem, close, cancellationToken);

            GC.Collect();
            return base.OnDeactivateAsync(close, cancellationToken);
        }
    }
}
