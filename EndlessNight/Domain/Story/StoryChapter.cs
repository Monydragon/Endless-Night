using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Story;

/// <summary>
/// Main story chapter/segment. Stored in SQLite so it can be iterated on without code changes.
/// A chapter points at a DialogueNode key that represents the start of the chapter.
/// </summary>
public sealed class StoryChapter : IKeyedEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key, e.g. "chapter.01.awakening".
    /// </summary>
    public required string Key { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// The dialogue node key where this chapter begins.
    /// </summary>
    public required string StartNodeKey { get; set; }

    /// <summary>
    /// Order (0..N). Lowest not-yet-completed chapter is next.
    /// </summary>
    public int Order { get; set; }
}