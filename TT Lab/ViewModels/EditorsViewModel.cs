using System;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using Splat;

namespace TT_Lab.ViewModels
{
    public class EditorsViewModel : Conductor<EditorsViewerViewModel>.Collection.OneActive
    {
        public override async Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var result = true;
            foreach (var item in Items)
            {
                result = await item.CanCloseAsync(cancellationToken);
                if (!result)
                {
                    break;
                }
            }
            
            return result;
        }

        public void Save()
        {
            foreach (var item in Items)
            {
                item.Save();
            }
        }

        protected override Task OnInitializedAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(Locator.Current.GetService<ScenesEditorsViewModel>()!, cancellationToken);
            ActivateItemAsync(Locator.Current.GetService<ResourcesEditorsViewModel>()!, cancellationToken);
            ActivateItemAsync(Items[0], cancellationToken);
            
            return base.OnInitializedAsync(cancellationToken);
        }
    }
}
