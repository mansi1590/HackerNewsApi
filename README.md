# Hacker News Best Stories API

ASP.NET Core REST API that returns the best `n` stories from the [Hacker News API](https://github.com/HackerNews/API), ranked by score.

## Run

Requires the .NET 9 SDK.

```bash
dotnet test
dotnet run --project src/HackerNewsApi --launch-profile http
```

Then:

```http
GET http://localhost:5063/beststories?n=5
```

HTTPS is available with `--launch-profile https` (`https://localhost:7105`).

OpenAPI is served in Development at `/openapi/v1.json`.

## Response

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

`n` must be an integer between 1 and 500. If fewer stories are available than requested, the API returns all available stories.

## Design

Hacker News exposes a list of best-story IDs and a separate item endpoint per ID. Fetching items on every incoming request would overload their API and add avoidable latency.

This service therefore:

1. Fetches `beststories.json` once per cache window, then keeps at most `MaxStories` IDs (default 500) before any item fetch.
2. Loads item details in parallel, with a concurrency cap.
3. Caches individual items for longer than the ranked list, so a list refresh rarely needs a full refetch.
4. Caches the mapped, score-sorted result.
5. Coalesces concurrent cache misses behind a single refresh (single-flight), so a burst of traffic does not stampede Hacker News.

`BestStoriesService` is scoped so it does not capture the typed `HttpClient` (those are transient). A singleton refresh lock still coordinates one upstream refresh across concurrent requests.

Default cache windows are 2 minutes for the assembled list and 10 minutes for items. Both are configurable in `appsettings.json`.

```
GET /beststories?n=10
        │
        ▼
 BestStoriesController
        │
        ▼
 BestStoriesService  ── memory cache (list + items)
        │
        ▼
 HackerNewsClient    ── HttpClient  ── Hacker News
```

## Assumptions

- The Hacker News `beststories` list is the source of "best" stories; this API only applies the caller's `n` and a descending score sort.
- Items without a `url` (for example Ask HN) use the Hacker News discussion page as `uri`.
- Deleted, dead, untitled, or non-story items are omitted rather than returned as incomplete records.
- `time` is the Hacker News Unix timestamp converted to UTC ISO-8601.
- `commentCount` maps from Hacker News `descendants`.
- In-memory cache is sufficient for a single instance, which is the expected deployment for this exercise.
- Scores and comment counts can be a few minutes stale because the assembled list and item details are cached; that is a deliberate trade-off against overloading Hacker News.
- Hacker News currently returns a few hundred best-story IDs; the API still caps that list at `MaxStories` so a larger upstream payload cannot fan out unbounded item requests.

## Further work

Given more time I would:

- Add Redis (or another distributed cache) so multiple instances share one cached view of Hacker News.
- Add retries, timeouts per request, and a circuit breaker around the Hacker News client (for example Polly).
- Refresh the cache in the background before expiry so callers never wait on a cold fetch.
- Add a Dockerfile and a GitHub Actions workflow for build, test, and publish.
- Emit cache-hit / upstream-call metrics and a health endpoint that reports cache freshness.
- Support `ETag` / `Cache-Control` on the API response itself.
