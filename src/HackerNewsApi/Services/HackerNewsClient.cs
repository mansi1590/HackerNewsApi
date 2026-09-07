using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsApi.Models;

namespace HackerNewsApi.Services;

public sealed class HackerNewsClient : IHackerNewsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HackerNewsClient> _logger;

    public HackerNewsClient(HttpClient httpClient, ILogger<HackerNewsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await _httpClient.GetFromJsonAsync<int[]>("beststories.json", JsonOptions, cancellationToken);

        if (ids is null)
        {
            throw new HttpRequestException("Hacker News returned an empty best-stories payload.");
        }

        return ids;
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Hacker News item {ItemId}", id);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Hacker News item {ItemId}", id);
            return null;
        }
    }
}
