using HackerNewsApi.Models;
using HackerNewsApi.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Services;

public sealed class BestStoriesService : IBestStoriesService, IDisposable
{
    internal const string StoriesCacheKey = "hacker-news:best-stories";
    private const string ItemCacheKeyPrefix = "hacker-news:item:";

    private readonly IHackerNewsClient _client;
    private readonly IMemoryCache _cache;
    private readonly HackerNewsOptions _options;
    private readonly ILogger<BestStoriesService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public BestStoriesService(
        IHackerNewsClient client,
        IMemoryCache cache,
        IOptions<HackerNewsOptions> options,
        ILogger<BestStoriesService> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        var stories = await GetOrRefreshStoriesAsync(cancellationToken);
        return stories.Count <= count ? stories : stories.Take(count).ToArray();
    }

    private async Task<IReadOnlyList<StoryResponse>> GetOrRefreshStoriesAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedStories(out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedStories(out cached))
            {
                return cached;
            }

            _logger.LogInformation("Refreshing best stories from Hacker News.");

            var ids = await _client.GetBestStoryIdsAsync(cancellationToken);
            var items = await GetItemsAsync(ids, cancellationToken);

            var stories = items
                .Where(IsUsableStory)
                .Select(Map)
                .OrderByDescending(story => story.Score)
                .ThenBy(story => story.Title, StringComparer.Ordinal)
                .ToArray();

            _cache.Set(StoriesCacheKey, (IReadOnlyList<StoryResponse>)stories, _options.StoriesCacheDuration);
            return stories;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<HackerNewsItem>> GetItemsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var items = new HackerNewsItem?[ids.Count];
        using var throttle = new SemaphoreSlim(_options.MaxParallelItemRequests);

        var tasks = ids.Select(async (id, index) =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                items[index] = await GetItemAsync(id, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return items.OfType<HackerNewsItem>().ToArray();
    }

    private async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        var cacheKey = ItemCacheKey(id);
        if (_cache.TryGetValue(cacheKey, out HackerNewsItem? cached) && cached is not null)
        {
            return cached;
        }

        var item = await _client.GetItemAsync(id, cancellationToken);
        if (item is not null)
        {
            _cache.Set(cacheKey, item, _options.ItemCacheDuration);
        }

        return item;
    }

    private bool TryGetCachedStories(out IReadOnlyList<StoryResponse> stories)
    {
        if (_cache.TryGetValue(StoriesCacheKey, out IReadOnlyList<StoryResponse>? cached) && cached is not null)
        {
            stories = cached;
            return true;
        }

        stories = [];
        return false;
    }

    private static bool IsUsableStory(HackerNewsItem item) =>
        !item.Deleted
        && !item.Dead
        && !string.IsNullOrWhiteSpace(item.Title)
        && !string.IsNullOrWhiteSpace(item.By)
        && (item.Type is null || string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase));

    private static StoryResponse Map(HackerNewsItem item) => new()
    {
        Title = item.Title!,
        Uri = string.IsNullOrWhiteSpace(item.Url)
            ? $"https://news.ycombinator.com/item?id={item.Id}"
            : item.Url,
        PostedBy = item.By!,
        Time = DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score = item.Score,
        CommentCount = item.Descendants
    };

    private static string ItemCacheKey(int id) => $"{ItemCacheKeyPrefix}{id}";

    public void Dispose() => _refreshLock.Dispose();
}
