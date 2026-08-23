namespace MyIPTV.Core.Models;

public sealed record EpgChannelSchedule(EpgChannel Channel, IReadOnlyList<EpgProgram> Programs);
