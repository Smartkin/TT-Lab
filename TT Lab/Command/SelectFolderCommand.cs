using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace TT_Lab.Command
{
    public class SelectFolderCommand(object target, string textStoragePropName, string startPath = "")
        : ICommand
    {
        private TopLevel? owner;

        public SelectFolderCommand(TopLevel? owner, object target, string textStoragePropName, string startPath = "")
            : this(target, textStoragePropName, startPath)
        {
            this.owner = owner;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public async void Execute(object? parameter = null)
        {
            if (owner == null)
            {
                if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }
                else if (Application.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
                {
                    owner = singleView.MainView as TopLevel;
                }
            }

            if (owner == null)
            {
                throw new Exception("Unsupported platform!");
            }

            var folderPickerOptions = new FolderPickerOpenOptions
            {
                AllowMultiple = false
            };
            var folder = await owner.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);
            if (folder.Count > 0)
            {
                var prop = target.GetType().GetProperty(textStoragePropName);
                Debug.Assert(prop != null, $"Invalid property {textStoragePropName}!");
                prop.SetValue(target, folder[0].Path.LocalPath);
            }
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
