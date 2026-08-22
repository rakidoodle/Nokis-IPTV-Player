using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IApplicationConfiguration
{
    ApplicationOptions Application { get; }

    NetworkOptions Network { get; }
}
