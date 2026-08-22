using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed class ActiveProfileService : IActiveProfileService
{
    public IptvProfile? ActiveProfile { get; private set; }

    public event EventHandler? ActiveProfileChanged;

    public void SetActive(IptvProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ActiveProfile = profile;
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear(Guid profileId)
    {
        if (ActiveProfile?.Id != profileId)
        {
            return;
        }

        ActiveProfile = null;
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }
}
