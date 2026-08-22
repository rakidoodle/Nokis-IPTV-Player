using Microsoft.Extensions.Options;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Configuration;

public sealed class ApplicationConfiguration(
    IOptions<ApplicationOptions> applicationOptions,
    IOptions<NetworkOptions> networkOptions) : IApplicationConfiguration
{
    public ApplicationOptions Application { get; } = applicationOptions.Value;

    public NetworkOptions Network { get; } = networkOptions.Value;
}
