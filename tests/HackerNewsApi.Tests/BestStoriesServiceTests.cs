using HackerNewsApi.Models;
using HackerNewsApi.Options;
using HackerNewsApi.Services;
using HackerNewsApi.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerNewsApi.Tests;

public sealed class BestStoriesServiceTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesInDescendingScoreOrder()
    {
        var client = new FakeHackerNewsClient(
        [
            Story(1, "Low", score: 10),
            Story(2, "High", score: 50),
            Story(3, "Mid", score: 25)
        ]);
        var service = CreateService(client);

        var stories = await service.GetBestStoriesAsync(3, CancellationToken.None);

        Assert.Equal(["High", "Mid", "Low"], stories.Select(story => story.Title));
        Assert.Equal([50, 25, 10], stories.Select(story => story.Score));
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsOnlyRequestedCount()
    {
        var client = new FakeHackerNewsClient(
        [
            Story(1, "A", score: 3),
            Story(2, "B", score: 2),
            Story(3, "C", score: 1)
        ]);
        var service = CreateService(client);

        var stories = await service.GetBestStoriesAsync(2, CancellationToken.None);

        Assert.Equal(2, stories.Count);
        Assert.Equal("A", stories[0].Title);
    }

    [Fact]
    public async Task GetBestStoriesAsync_MapsHackerNewsFields()
    {
        var client = new FakeHackerNewsClient(
        [
            new HackerNewsItem
            {
                Id = 21233041,
                Title = "A uBlock Origin update was rejected from the Chrome Web Store",
                Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
                By = "ismaildonmez",
                Time = 1570887781,
                Score = 1716,
                Descendants = 572,
                Type = "story"
            }
        ]);
        var service = CreateService(client);

        var story = Assert.Single(await service.GetBestStoriesAsync(1, CancellationToken.None));

        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", story.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", story.Uri);
        Assert.Equal("ismaildonmez", story.PostedBy);
        Assert.Equal(DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"), story.Time);
        Assert.Equal(1716, story.Score);
        Assert.Equal(572, story.CommentCount);
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesHnDiscussionUrlWhenStoryHasNoUrl()
    {
        var client = new FakeHackerNewsClient(
        [
            new HackerNewsItem
            {
                Id = 99,
                Title = "Ask HN: Favorite tools?",
                By = "alice",
                Time = 1570887781,
                Score = 20,
                Type = "story"
            }
        ]);
        var service = CreateService(client);

        var story = Assert.Single(await service.GetBestStoriesAsync(1, CancellationToken.None));

        Assert.Equal("https://news.ycombinator.com/item?id=99", story.Uri);
    }

    [Fact]
    public async Task GetBestStoriesAsync_SkipsDeletedDeadAndUntitledItems()
    {
        var client = new FakeHackerNewsClient(
        [
            Story(1, "Keep", score: 10),
            new HackerNewsItem { Id = 2, Title = "Gone", By = "x", Deleted = true, Score = 100, Type = "story" },
            new HackerNewsItem { Id = 3, Title = "Dead", By = "x", Dead = true, Score = 100, Type = "story" },
            new HackerNewsItem { Id = 4, Title = "", By = "x", Score = 100, Type = "story" },
            new HackerNewsItem { Id = 5, Title = "Comment", By = "x", Score = 100, Type = "comment" }
        ]);
        var service = CreateService(client);

        var stories = await service.GetBestStoriesAsync(10, CancellationToken.None);

        var story = Assert.Single(stories);
        Assert.Equal("Keep", story.Title);
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCachedStoriesOnSubsequentCalls()
    {
        var client = new FakeHackerNewsClient([Story(1, "Cached", score: 10)]);
        var service = CreateService(client);

        await service.GetBestStoriesAsync(1, CancellationToken.None);
        await service.GetBestStoriesAsync(1, CancellationToken.None);

        Assert.Equal(1, client.BestStoryIdCalls);
        Assert.Equal(1, client.ItemCalls);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReusesCachedItemsWhenStoryListRefreshes()
    {
        var client = new FakeHackerNewsClient([Story(1, "Cached item", score: 10)]);
        var service = CreateService(client);

        await service.GetBestStoriesAsync(1, CancellationToken.None);
        _cache.Remove(BestStoriesService.StoriesCacheKey);
        await service.GetBestStoriesAsync(1, CancellationToken.None);

        Assert.Equal(2, client.BestStoryIdCalls);
        Assert.Equal(1, client.ItemCalls);
    }

    [Fact]
    public async Task GetBestStoriesAsync_SingleFlightsConcurrentCacheMisses()
    {
        var client = new FakeHackerNewsClient([Story(1, "Once", score: 10)], TimeSpan.FromMilliseconds(150));
        var service = CreateService(client);

        await Task.WhenAll(
            service.GetBestStoriesAsync(1, CancellationToken.None),
            service.GetBestStoriesAsync(1, CancellationToken.None),
            service.GetBestStoriesAsync(1, CancellationToken.None));

        Assert.Equal(1, client.BestStoryIdCalls);
        Assert.Equal(1, client.ItemCalls);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsAllStoriesWhenCountExceedsAvailable()
    {
        var client = new FakeHackerNewsClient([Story(1, "Only", score: 10)]);
        var service = CreateService(client);

        var stories = await service.GetBestStoriesAsync(20, CancellationToken.None);

        Assert.Single(stories);
    }

    private BestStoriesService CreateService(IHackerNewsClient client)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new HackerNewsOptions
        {
            StoriesCacheDuration = TimeSpan.FromMinutes(2),
            ItemCacheDuration = TimeSpan.FromMinutes(10),
            MaxParallelItemRequests = 4
        });

        return new BestStoriesService(client, _cache, options, NullLogger<BestStoriesService>.Instance);
    }

    private static HackerNewsItem Story(int id, string title, int score) => new()
    {
        Id = id,
        Title = title,
        Url = $"https://example.com/{id}",
        By = "tester",
        Time = 1570887781,
        Score = score,
        Descendants = 3,
        Type = "story"
    };

    public void Dispose()
    {
        _cache.Dispose();
    }
}
