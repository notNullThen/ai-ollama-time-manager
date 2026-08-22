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
    private CancellationTokenSource? _cts;

    public event EventHandler<List<FunctionCallResponse>>? OnContextUpdated;
    public event EventHandler? OnBusyChanged;

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

    public AiInteraction(TimeCalculatorProgramm timeCalculator, IConsoleLogger logger)
    {
        _aiFacade = new AiAppFacade(timeCalculator);
        _logger = logger;
        _timeCalculator = timeCalculator;
        UserInput = string.Empty;
        Init();
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

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
            ollamaHttpTimeout: TimeSpan.FromMinutes(3)
        );
        _aiFacade.OnExit = () => IsBusy = false;
        AiManager.ContextHandler.OnContextUpdated += InternalOnContextUpdated;
    }

    private void InternalOnContextUpdated(object? sender, List<FunctionCallResponse> e)
    {
        OnContextUpdated?.Invoke(this, e);
    }
}
