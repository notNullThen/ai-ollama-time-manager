using System.Text.Json.Serialization;

namespace TimeCalculator.Core.Types;

public class TimeEntry
{
    [JsonPropertyName("timeEntry")]
    public TimeData Time { get; set; } = new();
    public TimeType Type { get; set; } = TimeType.Work;

    public TimeEntry Clone()
    {
        return new() { Time = Time.Clone(), Type = Type };
    }
}
