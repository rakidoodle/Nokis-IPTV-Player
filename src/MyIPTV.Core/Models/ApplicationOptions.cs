namespace MyIPTV.Core.Models;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; init; } = "MyIPTV";

    public string DataDirectoryName { get; init; } = "MyIPTV";
}
