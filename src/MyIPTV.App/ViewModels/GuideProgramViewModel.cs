using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed class GuideProgramViewModel(EpgProgram program)
{
    public string Title { get; } = program.Title;
    public string TimeLabel { get; } =
        $"{program.StartUtc.ToLocalTime():t} – {program.EndUtc.ToLocalTime():t}";
    public string Description { get; } = program.Description ?? "No description supplied";
}
