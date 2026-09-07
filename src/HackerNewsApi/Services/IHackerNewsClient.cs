using HackerNewsApi.Models;

namespace HackerNewsApi.Services;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken);
}
