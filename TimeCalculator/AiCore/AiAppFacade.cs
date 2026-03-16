using AIOrchestrator.Core.AiAppFacade;
using AIOrchestrator.Core.AiAppFacade.Types;

using TimeCalculator.Core;
using TimeCalculator.Core.Types;

namespace TimeCalculator.AiCore;

public sealed class AiAppFacade(TimeCalculatorProgramm timeCalculator) : AiAppFacadeBase
{
    // For demonstration purposes I decided to make more functions calls
    // to see how AI can handle those.

    public void SetHours(int hours) => timeCalculator.SetHours(hours);
    public void SetMinutes(int minutes) => timeCalculator.SetMinutes(minutes);
    public void SetSeconds(int seconds) => timeCalculator.SetSeconds(seconds);
    public void SetType(TimeType type) => timeCalculator.SetType(type);
    public void AddTimeEntry() => timeCalculator.AddTimeEntry();
    public void SetRemainedTime() => timeCalculator.SetRemainedTime();

    public override AppDescription GetDescription() =>
    [
        new()
        {
            Name = "SetHours",
            Description = "Sets hours in current time entry.",
            Parameters =
            [
                new()
                {
                    Name = "hours",
                    Description = @"
Type: int.
Format: 0, 1, 2, ... 23.
"
                }
            ]
        },
        new()
        {
            Name = "SetMinutes",
            Description = "Set minutes in current time entry.",
            Parameters =
            [
                new()
                {
                    Name = "minutes",
                    Description = @"
Type: int.
Format: 0, 1, 2, ... 59.
"
                }
            ]
        },
        new()
        {
            Name = "SetSeconds",
            Description = "Set minutes in current time entry.",
            Parameters =
            [
                new()
                {
                    Name = "seconds",
                    Description = @"
Type: int.
Format: 0, 1, 2, ... 59.
"
                }
            ]
        },
        new()
        {
            Name = "SetType",
            Description = "Set type in current time entry.",
            Parameters =
            [
                new()
                {
                    Name = "type",
                    Description = @"
Type: string.
Format: ""Work"" or ""Break"".
"
                }
            ]
        },
        new()
        {
            Name = "AddTimeEntry",
            Description = @$"
Adds time entry to the list.
Needs to be called each time after the time entry ({nameof(SetHours)}, {nameof(SetMinutes)}, {nameof(SetSeconds)} or {nameof(SetType)}) is set.
",
            Parameters = []
        },
        new()
        {
            Name = "SetRemainedTime",
            Description = @"
Sets the remained time in current time entry, so the working time is fulfilled.
",
            Parameters = []
        }
    ];
}