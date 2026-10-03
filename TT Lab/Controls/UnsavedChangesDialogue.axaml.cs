using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TT_Lab.Command;
using TT_Lab.Util;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Controls;

public partial class UnsavedChangesDialogue : Window
{
    OpenDialogueCommand.DialogueResult result;

    public enum AnswerResult
    {
        YES,
        DISCARD,
        CANCEL
    }

    public UnsavedChangesDialogue()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialogue(OpenDialogueCommand.DialogueResult result, string unsavedDocumentName) : this()
    {
        this.result = result;
        DataContext = unsavedDocumentName;
    }

    // Asked over the main window, closing the dialogue's own window cancels
    public static async Task<AnswerResult> Ask(string unsavedDocumentName)
    {
        var result = new OpenDialogueCommand.DialogueResult();
        await new UnsavedChangesDialogue(result, unsavedDocumentName).ShowDialog(MiscUtils.GetMainWindow());
        return result.Result is AnswerResult answer ? answer : AnswerResult.CANCEL;
    }

    private void YesButton_Click(Object sender, RoutedEventArgs e)
    {
        result.Result = AnswerResult.YES;
        Close();
    }

    private void DiscardButton_Click(Object sender, RoutedEventArgs e)
    {
        result.Result = AnswerResult.DISCARD;
        Close();
    }

    private void CancelButton_Click(Object sender, RoutedEventArgs e)
    {
        result.Result = AnswerResult.CANCEL;
        Close();
    }
}