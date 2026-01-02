#if WINDOWS
using Microsoft.WindowsAPICodePack.Dialogs;
#endif
using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace TT_Lab.Command
{
    public class SelectFolderCommand : ICommand
    {
        private readonly object target;
        private readonly string propName;
        private readonly string startPath;
        private TopLevel? owner;

        public SelectFolderCommand(object target, string textStoragePropName, string startPath = "")
        {
            this.target = target;
            this.startPath = startPath;
            propName = textStoragePropName;
        }

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

        public void Execute(object? parameter = null)
        {
#if WINDOWS
            using CommonOpenFileDialog ofd = new()
            {
                IsFolderPicker = true,
                InitialDirectory = startPath
            };
            var dialRes = owner == null ? ofd.ShowDialog() : ofd.ShowDialog(owner);
            if (dialRes == CommonFileDialogResult.Ok)
            {
                var prop = target.GetType().GetProperty(propName)!;
                prop.SetValue(target, ofd.FileName);
            }
#else
            if (owner == null)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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
            
            var folder = owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startPath)).ConfigureAwait(false).GetAwaiter().GetResult();
            if (folder != null)
            {
                var prop = target.GetType().GetProperty(propName);
                prop.SetValue(target, folder.Name);
            }
#endif
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
