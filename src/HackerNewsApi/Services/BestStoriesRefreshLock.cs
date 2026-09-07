namespace HackerNewsApi.Services;

/// <summary>
/// Process-wide gate so scoped <see cref="BestStoriesService"/> instances
/// still single-flight a cache refresh.
/// </summary>
public sealed class BestStoriesRefreshLock : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

    public void Release() => _gate.Release();

    public void Dispose() => _gate.Dispose();
}
