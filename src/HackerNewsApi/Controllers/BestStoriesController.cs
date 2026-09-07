using HackerNewsApi.Models;
using HackerNewsApi.Options;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Controllers;

[ApiController]
[Route("beststories")]
public sealed class BestStoriesController : ControllerBase
{
    private readonly IBestStoriesService _bestStoriesService;
    private readonly HackerNewsOptions _options;

    public BestStoriesController(IBestStoriesService bestStoriesService, IOptions<HackerNewsOptions> options)
    {
        _bestStoriesService = bestStoriesService;
        _options = options.Value;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StoryResponse>>> Get(
        [FromQuery] int? n,
        CancellationToken cancellationToken)
    {
        if (n is null or < 1 || n > _options.MaxStories)
        {
            ModelState.AddModelError(nameof(n), $"n must be an integer between 1 and {_options.MaxStories}.");
            return ValidationProblem(ModelState);
        }

        var stories = await _bestStoriesService.GetBestStoriesAsync(n.Value, cancellationToken);
        return Ok(stories);
    }
}
