using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Story;

/// <summary>
/// Run-scoped progress through the main story chapters.
/// </summary>
public sealed class RunStoryState : IRunScoped
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    /// <summary>
    /// Chapter key currently in progress.
    /// </summary>
    public string? ActiveChapterKey { get; set; }

    /// <summary>
    /// Dialogue node key within the active chapter.
    /// </summary>
    public string? CurrentNodeKey { get; set; }

    /// <summary>
    /// JSON-encoded list of completed chapter keys.
    /// </summary>
    public List<string> CompletedChapterKeys { get; set; } = new();

    public DateTime UpdatedUtc { get; set; }
}

