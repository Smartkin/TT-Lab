using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.AgentLab;

namespace TT_Lab.ViewModels.Editors;

public partial class CodeEditorViewModel(DocumentViewModel document, PropertyNode code, params DocumentNodeViewModel[] dependencies) : DocumentDataViewModel<string>(document, code, dependencies)
{
    // The Xbox version's scripts have commands with more arguments
    public string ActionDefinitionsFile => Project.Project.GetActionDefinitionsFile(Document.DocumentModel as IAsset);
    private static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(300);

    [Reactive]
    private String _code;

    [Reactive(SetModifier = AccessModifier.Private)]
    private AgentLabCompiler.CompilerStatus _codeStatus = new();

    private bool _canClose = true;

    public bool IsAgentLabCode => GetEditorParameter(ValidateAgentLabCode, false);

    // Code that can only be a list of commands, which can be empty as well
    public bool IsAgentLabCommandList => GetEditorParameter(AgentLabCommandsOnly, false);

    protected override void ApplyValidationRules(CompositeDisposable disposables)
    {
        base.ApplyValidationRules(disposables);

        if (!IsAgentLabCode)
        {
            return;
        }

        this.ValidationRule(x => x.Code, this.WhenAnyValue(x => x.CodeStatus), status => !status.IsError, FormatError)
            .DisposeWith(FullDeactivationDisposables);

        this.IsValid().Subscribe(x =>
        {
            _canClose = x;
        }).DisposeWith(FullDeactivationDisposables);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        RxSchedulers.MainThreadScheduler.Schedule(this, (_, viewModel) =>
        {
            Code = new String(CurrentValue);
            return viewModel.WhenAnyValue(x => x.Code).ObserveOn(RxSchedulers.MainThreadScheduler)
                .Skip(1)
                .Subscribe(code =>
                {
                    SetValueCommand.Execute(code);
                });
        }).DisposeWith(disposables);

        if (!IsAgentLabCode)
        {
            return;
        }

        // Checking reads the action definitions every time so it's only done once the typing stops
        var actionDefinitions = ActionDefinitionsFile;
        Func<string, string, AgentLabCompiler.CompilerStatus> check = IsAgentLabCommandList ? AgentLabCompiler.CheckCommands : AgentLabCompiler.Check;
        this.WhenAnyValue(x => x.Code)
            .Where(code => code != null)
            .Throttle(CheckDelay, RxSchedulers.TaskpoolScheduler)
            .Select(code => Observable.Start(() => check(code, actionDefinitions), RxSchedulers.TaskpoolScheduler))
            .Switch()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(status => CodeStatus = status)
            .DisposeWith(disposables);
    }

    public override Boolean CanClose()
    {
        return _canClose;
    }

    private static string FormatError(AgentLabCompiler.CompilerStatus status)
    {
        if (!status.IsError)
        {
            return string.Empty;
        }

        return status.Line > 0 ? $"Line {status.Line}, column {status.Column}: {GetErrorText(status)}" : status.Message;
    }

    // Positioned errors carry the position in their message as well
    private static string GetErrorText(AgentLabCompiler.CompilerStatus status)
    {
        var prefix = $"({status.Line}:{status.Column}) ";
        return status.Message.StartsWith(prefix) ? status.Message[prefix.Length..] : status.Message;
    }

    public const string ValidateAgentLabCode = "CODE_EDITOR_VALIDATE_AGENT_LAB_CODE";
    public const string AgentLabCommandsOnly = "CODE_EDITOR_AGENT_LAB_COMMANDS_ONLY";
}
