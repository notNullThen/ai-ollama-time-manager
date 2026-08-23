using System.Text.Json;
using AIOrchestrator.Core.AiAppFacade;
using AIOrchestrator.Core.AiAppFacade.Types;
using TimeCalculator.AiCore.Types;
using TimeCalculator.Core;
using TimeCalculator.Core.Types;

namespace TimeCalculator.AiCore;

public sealed class AiAppFacade(
    TimeCalculatorProgramm timeCalculator,
    bool multipleFunctionsAtOneResponse = false
) : AiAppFacadeBase(multipleFunctionsAtOneResponse)
{
    private const string TimeFormat = @"hh\:mm";

    private static readonly JsonSerializerOptions PrettyJsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    public void SetDuration(string hours)
    {
        try
        {
            timeCalculator.SetDurationHours(int.Parse(hours));
        }
        catch (Exception)
        {
            throw new ArgumentException($"Hours should have {TimeFormat} format. It was: {hours}");
        }
    }

    public Guid AddTimeEntry(string time, string type, string description)
    {
        SetTime(time);
        SetType(type);
        timeCalculator.SetDescription(description);

        return timeCalculator.AddTimeEntry();
    }

    public Guid ReplaceEntry(string guid, string time, string type, string description)
    {
        Guid parsedGuid;
        try
        {
            parsedGuid = Guid.Parse(guid);
        }
        catch (Exception)
        {
            throw new ArgumentException($"Invalid GUID format. It was: {guid}");
        }

        SetTime(time);
        SetType(type);
        timeCalculator.SetDescription(description);

        timeCalculator.ReplaceEntryWithCurrent(parsedGuid);
        return parsedGuid;
    }

    public void EndTheDay()
    {
        timeCalculator.SetType(TimeType.DayEnd);
        timeCalculator.SetDescription("Departure from work.");
        timeCalculator.SetRemainedTime();
        timeCalculator.AddTimeEntry();
    }

    public override string GetConstraints() =>
        @$"
You are filling the working day time report.
Understand the user request as a working day sequence of activities which have specific start times, durations, and descriptions.
Dont call same functions with same parameters multiple times in a row.

Current time entries table:
{GetTimeEntriesTable()}
";

    public override AppDescription GetDescription() =>
        [
            new()
            {
                Name = nameof(AddTimeEntry),
                Description =
                    "Adds new time entry with specified time and type. Returns the id of the created entry.",
                Parameters =
                [
                    new() { Name = "time", Description = $"Time in format {TimeFormat}" },
                    new()
                    {
                        Name = "type",
                        Description =
                            $"Type of the time entry. Should be only one of the options - {string.Join(", ", Enum.GetNames<TimeType>())}.",
                    },
                    new()
                    {
                        Name = "description",
                        Description =
                            "Short description for the current entry. Use all additional information except time. Make this description to look officialy-professional before using it as parameter.",
                    },
                ],
            },
            new()
            {
                Name = nameof(ReplaceEntry),
                Description =
                    "Replaces existing entry with the new time, type and description. Returns the id of the replaced entry. Call ONLY if you need to CHANGE SOME EXISTING ENTRY.",
                Parameters =
                [
                    new() { Name = "guid", Description = "Id of the entry to replace." },
                    new() { Name = "time", Description = $"Time in format {TimeFormat}" },
                    new()
                    {
                        Name = "type",
                        Description =
                            $"Type of the time entry. Should be only one of the options - {string.Join(", ", Enum.GetNames<TimeType>())}.",
                    },
                    new()
                    {
                        Name = "description",
                        Description =
                            "Short description for the current entry. Use all additional information except time. Make this description to look officialy-professional before using it as parameter.",
                    },
                ],
            },
            new()
            {
                Name = nameof(EndTheDay),
                Description =
                    "Sets remaining time to the end of the working day and adds the final entry with type DayEnd.",
                Parameters = [],
            },
        ];

    private void SetTime(string time)
    {
        try
        {
            timeCalculator.CurrentTimeEntry.Time = TimeSpan.Parse(input: time);
        }
        catch (Exception)
        {
            throw new ArgumentException($"Time should be in format {TimeFormat}. It was: {time}");
        }
    }

    private void SetType(string type)
    {
        try
        {
            timeCalculator.SetType(Enum.Parse<TimeType>(type));
        }
        catch (Exception)
        {
            var validTypes = string.Join(", ", Enum.GetNames<TimeType>());
            throw new ArgumentException($"Type should be one of: {validTypes}. It was: {type}");
        }
    }

    private string GetTimeEntriesTable()
    {
        var aiTimeEntries = timeCalculator
            .TimeEntries.Select(entry => new AiTimeEntry
            {
                Id = entry.Id.ToString(),
                Time = entry.Time.ToString(TimeFormat),
                Duration = entry.Duration.ToString(TimeFormat),
                Type = entry.Type.ToString(),
                Description = entry.Description,
            })
            .ToArray();

        return JsonSerializer.Serialize(aiTimeEntries, PrettyJsonSerializerOptions);
    }
}
