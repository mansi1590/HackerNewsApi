using System.Net;
using System.Text;
using HackerNewsApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerNewsApi.Tests;

public sealed class HackerNewsClientTests
{
    [Fact]
    public async Task GetBestStoryIdsAsync_DeserializesIdArray()
    {
        using var httpClient = CreateHttpClient("""[21233041, 21232987]""", "https://hacker-news.firebaseio.com/v0/beststories.json");
        var client = new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);

        var ids = await client.GetBestStoryIdsAsync(CancellationToken.None);

        Assert.Equal([21233041, 21232987], ids);
    }

    [Fact]
    public async Task GetItemAsync_DeserializesStory()
    {
        const string json = """
            {
              "id": 21233041,
              "title": "Example",
              "url": "https://example.com",
              "by": "alice",
              "time": 1570887781,
              "score": 12,
              "descendants": 4,
              "type": "story"
            }
            """;
        using var httpClient = CreateHttpClient(json, "https://hacker-news.firebaseio.com/v0/item/21233041.json");
        var client = new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);

        var item = await client.GetItemAsync(21233041, CancellationToken.None);

        Assert.NotNull(item);
        Assert.Equal("Example", item.Title);
        Assert.Equal("alice", item.By);
        Assert.Equal(12, item.Score);
        Assert.Equal(4, item.Descendants);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsNullWhenRequestFails()
    {
        using var httpClient = CreateHttpClient("{}", "https://hacker-news.firebaseio.com/v0/item/1.json", HttpStatusCode.InternalServerError);
        var client = new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);

        var item = await client.GetItemAsync(1, CancellationToken.None);

        Assert.Null(item);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsNullWhenIndividualRequestTimesOut()
    {
        using var httpClient = new HttpClient(new TimeoutHandler())
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        };
        var client = new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);

        var item = await client.GetItemAsync(1, CancellationToken.None);

        Assert.Null(item);
    }

    private static HttpClient CreateHttpClient(string content, string expectedUri, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpClient(new StubHandler(content, expectedUri, statusCode))
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly string _expectedUri;
        private readonly HttpStatusCode _statusCode;

        public StubHandler(string content, string expectedUri, HttpStatusCode statusCode)
        {
            _content = content;
            _expectedUri = expectedUri;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(_expectedUri, request.RequestUri?.ToString());

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        }
    }
}
