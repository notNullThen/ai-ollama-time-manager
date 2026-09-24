using AIOrchestrator.Core;
using AIOrchestrator.Core.Types;
using TimeCalculator.Core;
using TimeCalculator.Services;

namespace TimeCalculator.AiCore;

public class AiInteraction
{
    public AiManager? AiManager { get; private set; }

    public string UserInput { get; set; }

    private AiAppFacade _aiFacade;
    private readonly IConsoleLogger _logger;
    private readonly TimeCalculatorProgramm _timeCalculator;
    private readonly string _contentRootPath;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _healingReviewCompletion;

    public event EventHandler<List<FunctionCallResponse>>? OnContextUpdated;
    public event EventHandler? OnBusyChanged;
    public event EventHandler? OnHealingStateChanged;

    public bool IsAnalyzingError { get; private set; }
    public string? SavedHealingConstraints { get; private set; }
    public string? HealingPrompt { get; private set; }
    public string? HealingConstraint { get; private set; }
    public bool IsAwaitingHealingReview => _healingReviewCompletion is not null;
    public bool IsHealingVisible => IsAnalyzingError || IsAwaitingHealingReview;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnBusyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public AiInteraction(
        TimeCalculatorProgramm timeCalculator,
        IConsoleLogger logger,
        string contentRootPath
    )
    {
        _aiFacade = new AiAppFacade(timeCalculator);
        _logger = logger;
        _timeCalculator = timeCalculator;
        _contentRootPath = contentRootPath;
        UserInput = string.Empty;
        Init();
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void ContinueHealing() => _healingReviewCompletion?.TrySetResult();

    public void SetMultipleFunctionsAtOneResponse(bool enabled)
    {
        if (_aiFacade.MultipleFunctionsAtOneResponse == enabled)
        {
            return;
        }

        _aiFacade = new AiAppFacade(_timeCalculator, enabled);
        Init();
    }

    public async Task AskAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            await AiManager!.StartAsync(UserInput, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            await _logger.LogInfoAsync("AI processing was stopped by the user.");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"AI Error: {ex.Message}", ex);
            throw;
        }
        finally
        {
            ClearHealingState();
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public string GetContext() => AiManager!.ContextHandler.GetContextJson();

    public string GetManagementPrompt() => AiManager!.GetManagementPrompt();

    public void Init()
    {
        if (AiManager?.ContextHandler != null)
        {
            AiManager.ContextHandler.OnContextUpdated -= InternalOnContextUpdated;
        }

        AiManager = new(
            modelName: _timeCalculator.AiSettings.ModelName,
            appInstance: _aiFacade,
            options: new() { Temperature = 0.0f },
            ollamaBaseUrl: _timeCalculator.AiSettings.BaseUrl,
            ollamaHttpTimeout: TimeSpan.FromMinutes(3),
            healingConstraintsFilePath: string.IsNullOrWhiteSpace(
                _timeCalculator.AiSettings.HealingConstraintsFilePath
            )
                ? null
                : Path.GetFullPath(
                    _timeCalculator.AiSettings.HealingConstraintsFilePath,
                    _contentRootPath
                )
        );
        if (_timeCalculator.AiSettings.PauseForHealingReview)
        {
            AiManager.HealingStarted += OnHealingStarted;
            AiManager.OnConstraintGeneratedAsync = OnConstraintGeneratedAsync;
        }
        _aiFacade.OnExit = () => IsBusy = false;
        AiManager.ContextHandler.OnContextUpdated += InternalOnContextUpdated;
    }

    private void OnHealingStarted(object? sender, string prompt)
    {
        IsAnalyzingError = true;
        SavedHealingConstraints = (sender as AiManager)?.LearnedConstraints;
        HealingPrompt = prompt;
        HealingConstraint = null;
        OnHealingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task OnConstraintGeneratedAsync(
        string constraint,
        CancellationToken cancellationToken
    )
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _healingReviewCompletion = completion;
        HealingConstraint = constraint;
        IsAnalyzingError = false;
        OnHealingStateChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await completion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            ClearHealingState();
        }
    }

    private void ClearHealingState()
    {
        IsAnalyzingError = false;
        SavedHealingConstraints = null;
        HealingPrompt = null;
        HealingConstraint = null;
        _healingReviewCompletion = null;
        OnHealingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InternalOnContextUpdated(object? sender, List<FunctionCallResponse> e)
    {
        OnContextUpdated?.Invoke(this, e);
    }
}
