using HackerNewsApi.Models;
using HackerNewsApi.Services;

namespace HackerNewsApi.Tests.Fakes;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private readonly Dictionary<int, HackerNewsItem?> _items;
    private int _bestStoryIdCalls;
    private int _itemCalls;
    private int _inFlightItemCalls;
    private int _maxConcurrentItemCalls;

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

    public int MaxConcurrentItemCalls => Volatile.Read(ref _maxConcurrentItemCalls);

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _bestStoryIdCalls);
        await DelayIfNeeded(cancellationToken);
        return BestStoryIds.Count > 0 ? BestStoryIds : _items.Keys.ToArray();
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        var inFlight = Interlocked.Increment(ref _inFlightItemCalls);
        try
        {
            UpdateMaxConcurrent(inFlight);
            Interlocked.Increment(ref _itemCalls);
            await DelayIfNeeded(cancellationToken);
            return _items.TryGetValue(id, out var item) ? item : null;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightItemCalls);
        }
    }

    private void UpdateMaxConcurrent(int inFlight)
    {
        int currentMax;
        do
        {
            currentMax = Volatile.Read(ref _maxConcurrentItemCalls);
            if (inFlight <= currentMax)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _maxConcurrentItemCalls, inFlight, currentMax) != currentMax);
    }

    private async Task DelayIfNeeded(CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
    }
}
