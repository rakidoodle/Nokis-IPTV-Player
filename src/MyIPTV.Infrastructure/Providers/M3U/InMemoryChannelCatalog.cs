using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Providers.M3U;

public sealed class InMemoryChannelCatalog : IChannelCatalog
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, IptvChannel[]> _channelsByProfile = [];

    public event EventHandler? ChannelsChanged;

    public IReadOnlyList<IptvChannel> GetAll()
    {
        lock (_syncRoot)
        {
            return _channelsByProfile.Values.SelectMany(channels => channels).ToArray();
        }
    }

    public IReadOnlyList<IptvChannel> GetForProfile(Guid profileId)
    {
        lock (_syncRoot)
        {
            return _channelsByProfile.TryGetValue(profileId, out IptvChannel[]? channels)
                ? channels.ToArray()
                : [];
        }
    }

    public void ReplaceForProfile(Guid profileId, IReadOnlyList<IptvChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Any(channel => channel.ProfileId != profileId))
        {
            throw new ArgumentException("Every channel must belong to the supplied profile.", nameof(channels));
        }

        lock (_syncRoot)
        {
            _channelsByProfile[profileId] = channels.ToArray();
        }

        ChannelsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveProfile(Guid profileId)
    {
        bool removed;
        lock (_syncRoot)
        {
            removed = _channelsByProfile.Remove(profileId);
        }

        if (removed)
        {
            ChannelsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
