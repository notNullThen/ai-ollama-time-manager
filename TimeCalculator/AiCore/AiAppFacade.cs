using System.Text.Json;
using System.Text.RegularExpressions;
using AIOrchestrator.Core.AiAppFacade;
using AIOrchestrator.Core.AiAppFacade.Types;
using TimeCalculator.Core;
using TimeCalculator.Core.Types;

namespace TimeCalculator.AiCore;

public sealed class AiAppFacade(TimeCalculatorProgramm timeCalculator) : AiAppFacadeBase
{
    // For demonstration purposes I decided to test the AI's ability to handle
    // complex logic with multiple function calls.
    // Otherwise we would have only one function call to set the time entry.

    public void SetHours(int hours) => timeCalculator.SetHours(hours);

    public void SetMinutes(int minutes) => timeCalculator.SetMinutes(minutes);

    public void SetSeconds(int seconds) => timeCalculator.SetSeconds(seconds);

    public void SetType(TimeType type) => timeCalculator.SetType(type);

    public object WriteTimeEntryToTable()
    {
        timeCalculator.AddTimeEntry();
        return GetTimeEntriesTable();
    }

    public void SetRemainedTime() => timeCalculator.SetRemainedTime();

    public override string GetConstraints() =>
        @$"
# TASK: CHRONOLOGICAL LOGGING
Process the user's timeline step-by-step. 

# THE 'ATOMIC ENTRY' RULE
Every time entry is an ATOMIC UNIT. You must complete one unit before starting the next.
One Unit = (SetType) -> (SetHours/Minutes/Seconds) -> (WriteTimeEntryToTable).

# CRITICAL LOGIC GATES
- FORBIDDEN: Calling {nameof(SetType)} twice in a row without {nameof(WriteTimeEntryToTable)} in between.
- FORBIDDEN: Calculating a new time segment while a previous segment is still 'Open' (Unwritten).
- REQUIRED: You must call {nameof(WriteTimeEntryToTable)} after setting the time entry.
- REQUIREMENT: If the user provides a 'Total' time, perform the subtraction for the 'Remaining' period and treat that result as its own ATOMIC UNIT.

# MATH SCRATCHPAD
If calculation is needed, use {nameof(SetRemainedTime)} function.
";

    public override AppDescription GetDescription() =>
        [
            new()
            {
                Name = nameof(SetHours),
                Description = "Sets the hour value for the PENDING entry. Range 0-23.",
                Parameters = [new() { Name = "hours", Description = "int" }],
            },
            new()
            {
                Name = nameof(SetMinutes),
                Description = "Sets the minute value for the PENDING entry. Range 0-59.",
                Parameters = [new() { Name = "minutes", Description = "int" }],
            },
            new()
            {
                Name = nameof(SetSeconds),
                Description = "Sets the second value for the PENDING entry. Range 0-59.",
                Parameters = [new() { Name = "seconds", Description = "int" }],
            },
            new()
            {
                Name = nameof(SetType),
                Description = "Sets the category ('Work' or 'Break') for the PENDING entry.",
                Parameters = [new() { Name = "type", Description = "string" }],
            },
            new()
            {
                Name = nameof(WriteTimeEntryToTable),
                Description =
                    "Saves the PENDING entry to the permanent table. You MUST call this after setting Type/H/M/S and BEFORE starting the next entry.",
                Parameters = [],
            },
            new()
            {
                Name = nameof(SetRemainedTime),
                Description = "Sets the remaining Work time for the PENDING entry.",
                Parameters = [],
            },
            new()
            {
                Name = "Exit",
                Description =
                    "Finalize the session. Call ONLY after every entry has been Written to the table.",
                Parameters = [],
            },
        ];

    private string GetCurrentTimeEntry()
    {
        return $"Current Time Entry: {timeCalculator.CurrentTimeEntry.Time.Hours} hours, {timeCalculator.CurrentTimeEntry.Time.Minutes} minutes, {timeCalculator.CurrentTimeEntry.Time.Seconds} seconds. Type: {timeCalculator.CurrentTimeEntry.Type}";
    }

    private object GetTimeEntriesTable()
    {
        return timeCalculator.TimeEntries.Values.ToList();
    }
}
