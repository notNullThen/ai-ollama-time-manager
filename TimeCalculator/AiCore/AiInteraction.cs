using AIOrchestrator.Core;

using TimeCalculator.Core;

namespace TimeCalculator.AiCore;

public class AiInteraction
{
    public AiManager AiManager;

    public string UserInput { get; set; }

    private AiAppFacade _aiFacade;

    public AiInteraction(TimeCalculatorProgramm timeCalculator)
    {
        _aiFacade = new AiAppFacade(timeCalculator);
        AiManager = new(modelName: _modelName, appInstance: _aiFacade);
        UserInput = string.Empty;
    }


    private const string _modelName = "qwen2.5-coder:7b";


    public async Task AskAsync()
    {
        await AiManager.StartAsync(UserInput);
        Reset();
    }

    public string GetContext() => AiManager.ContextHandler.GetContextJson();

    public void Reset()
    {
        AiManager = new(modelName: _modelName, appInstance: _aiFacade);
        UserInput = string.Empty;
    }
}
