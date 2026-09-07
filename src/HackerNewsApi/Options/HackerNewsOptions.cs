using System.ComponentModel.DataAnnotations;

namespace HackerNewsApi.Options;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan StoriesCacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan ItemCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    [Range(1, 50)]
    public int MaxParallelItemRequests { get; set; } = 16;

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    [Range(1, 500)]
    public int MaxStories { get; set; } = 500;
}
