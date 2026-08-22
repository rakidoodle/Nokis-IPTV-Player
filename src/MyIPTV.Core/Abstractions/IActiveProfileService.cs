using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IActiveProfileService
{
    IptvProfile? ActiveProfile { get; }

    event EventHandler? ActiveProfileChanged;

    void SetActive(IptvProfile profile);

    void Clear(Guid profileId);
}
