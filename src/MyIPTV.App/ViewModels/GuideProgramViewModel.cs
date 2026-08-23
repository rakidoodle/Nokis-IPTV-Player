using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed class GuideProgramViewModel(EpgProgram program, bool displayLocalTime = true)
{
    public string Title { get; } = program.Title;
    public string TimeLabel { get; } = FormatTime(program, displayLocalTime);
    public string Description { get; } = program.Description ?? "No description supplied";

    private static string FormatTime(EpgProgram program, bool displayLocalTime)
    {
        DateTimeOffset start = displayLocalTime ? program.StartUtc.ToLocalTime() : program.StartUtc;
        DateTimeOffset end = displayLocalTime ? program.EndUtc.ToLocalTime() : program.EndUtc;
        return $"{start:t} – {end:t}" + (displayLocalTime ? string.Empty : " UTC");
    }
}
