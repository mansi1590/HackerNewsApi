using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsApi.Models;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HackerNewsApi.Tests;

public sealed class BestStoriesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BestStoriesApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBestStoriesService>();
                services.AddSingleton<IBestStoriesService, StubBestStoriesService>();
            });
        });
    }

    [Fact]
    public async Task GetBestStories_ReturnsMappedJsonInSpecShape()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/beststories?n=1");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(payload);
        var story = document.RootElement[0];

        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", story.GetProperty("title").GetString());
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", story.GetProperty("uri").GetString());
        Assert.Equal("ismaildonmez", story.GetProperty("postedBy").GetString());
        Assert.Equal("2019-10-12T13:43:01+00:00", story.GetProperty("time").GetString());
        Assert.Equal(1716, story.GetProperty("score").GetInt32());
        Assert.Equal(572, story.GetProperty("commentCount").GetInt32());
    }

    [Fact]
    public async Task GetBestStories_ReturnsBadRequestWhenNIsMissingOrInvalid()
    {
        using var client = _factory.CreateClient();

        var missing = await client.GetAsync("/beststories");
        var zero = await client.GetAsync("/beststories?n=0");
        var tooLarge = await client.GetAsync("/beststories?n=501");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    private sealed class StubBestStoriesService : IBestStoriesService
    {
        public Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
        {
            IReadOnlyList<StoryResponse> stories =
            [
                new StoryResponse
                {
                    Title = "A uBlock Origin update was rejected from the Chrome Web Store",
                    Uri = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
                    PostedBy = "ismaildonmez",
                    Time = DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"),
                    Score = 1716,
                    CommentCount = 572
                }
            ];

            return Task.FromResult(stories.Take(count).ToArray() as IReadOnlyList<StoryResponse>);
        }
    }
}
