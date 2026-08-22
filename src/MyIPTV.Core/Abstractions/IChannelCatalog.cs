using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IChannelCatalog
{
    event EventHandler? ChannelsChanged;

    IReadOnlyList<IptvChannel> GetAll();

    IReadOnlyList<IptvChannel> GetForProfile(Guid profileId);

    void ReplaceForProfile(Guid profileId, IReadOnlyList<IptvChannel> channels);

    void RemoveProfile(Guid profileId);
}
