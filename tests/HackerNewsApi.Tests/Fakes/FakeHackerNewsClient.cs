using HackerNewsApi.Models;
using HackerNewsApi.Services;

namespace HackerNewsApi.Tests.Fakes;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private readonly Dictionary<int, HackerNewsItem?> _items;
    private int _bestStoryIdCalls;
    private int _itemCalls;

    public FakeHackerNewsClient(IEnumerable<HackerNewsItem> items, TimeSpan? delay = null)
        : this(items.ToDictionary(item => item.Id, item => (HackerNewsItem?)item), delay)
    {
    }

    public FakeHackerNewsClient(Dictionary<int, HackerNewsItem?> items, TimeSpan? delay = null)
    {
        _items = items;
        Delay = delay ?? TimeSpan.Zero;
    }

    public TimeSpan Delay { get; set; }

    public IReadOnlyList<int> BestStoryIds { get; init; } = [];

    public int BestStoryIdCalls => Volatile.Read(ref _bestStoryIdCalls);

    public int ItemCalls => Volatile.Read(ref _itemCalls);

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _bestStoryIdCalls);
        await DelayIfNeeded(cancellationToken);
        return BestStoryIds.Count > 0 ? BestStoryIds : _items.Keys.ToArray();
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _itemCalls);
        await DelayIfNeeded(cancellationToken);
        return _items.TryGetValue(id, out var item) ? item : null;
    }

    private async Task DelayIfNeeded(CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
    }
}
