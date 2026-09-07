using HackerNewsApi.Models;

namespace HackerNewsApi.Services;

public interface IBestStoriesService
{
    Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken);
}
