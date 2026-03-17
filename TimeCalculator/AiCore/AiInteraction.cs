using AIOrchestrator.Core;
using AIOrchestrator.Core.Types;

using TimeCalculator.Core;

namespace TimeCalculator.AiCore;

public class AiInteraction
{
    public AiManager? AiManager { get; private set; }

    public string UserInput { get; set; }

    private readonly AiAppFacade _aiFacade;

    public event EventHandler<List<FunctionCallResponse>>? OnContextUpdated;

    public AiInteraction(TimeCalculatorProgramm timeCalculator)
    {
        _aiFacade = new AiAppFacade(timeCalculator);
        UserInput = string.Empty;
        Init();
    }


    private const string ModelName = "qwen2.5-coder:7b";


    public async Task AskAsync()
    {
        await AiManager!.StartAsync(UserInput);
    }

    public string GetContext() => AiManager!.ContextHandler.GetContextJson();

    public void Init()
    {
        if (AiManager?.ContextHandler != null)
        {
            AiManager.ContextHandler.OnContextUpdated -= InternalOnContextUpdated;
        }

        AiManager = new(modelName: ModelName, appInstance: _aiFacade);
        AiManager.ContextHandler.OnContextUpdated += InternalOnContextUpdated;
        UserInput = string.Empty;
    }

    private void InternalOnContextUpdated(object? sender, List<FunctionCallResponse> e)
    {
        OnContextUpdated?.Invoke(this, e);
    }
}

