using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed class ProceduralDialogueComposer
{
    public sealed record ComposeRequest(
        Guid RunId,
        int Seed,
        int Turn,
        string PlayerName,
        string RoomName,
        IReadOnlyList<string> EnabledLorePacks,
        IReadOnlyList<string> ContextTags,
        int Sanity,
        int Morality,
        ActorDisposition Disposition,
        int MaxLines = 2,
        int? SeedOffset = null,
        string? Phase = null);

    public sealed record ComposeResult(string Text, IReadOnlyList<string> SnippetKeys);

    private readonly SqliteDbContext _db;

    public ProceduralDialogueComposer(SqliteDbContext db)
    {
        _db = db;
    }

    public async Task<ComposeResult> ComposeAsync(ComposeRequest req, CancellationToken cancellationToken = default)
    {
        if (req.MaxLines <= 0)
            return new ComposeResult(string.Empty, Array.Empty<string>());

        var rng = new Random(HashCode.Combine(req.Seed, req.SeedOffset ?? 0, req.Turn, req.RunId.GetHashCode(), (int)req.Disposition));

        var candidates = await _db.DialogueSnippets
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Filter by constraints.
        var eligible = candidates
            .Where(s => IsEligible(s, req))
            .ToList();

        if (eligible.Count == 0)
            return new ComposeResult(string.Empty, Array.Empty<string>());

        // Prefer an opening/middle/closing progression if possible.
        var phases = req.Phase is null
            ? new[] { "opening", "middle", "closing" }
            : new[] { req.Phase };
        var picked = new List<DialogueSnippet>();

        foreach (var role in phases)
        {
            if (picked.Count >= req.MaxLines)
                break;

            var pool = eligible.Where(s => string.Equals(s.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();
            if (pool.Count == 0)
                continue;

            var next = PickWeighted(rng, pool);
            picked.Add(next);

            // Remove the exact snippet so we don't repeat within a single compose.
            eligible.RemoveAll(s => s.Key == next.Key);
        }

        // If a specific phase was requested, don't backfill with other roles.
        if (req.Phase is null)
        {
            // If we still need lines, fill from anything eligible.
            while (picked.Count < req.MaxLines && eligible.Count > 0)
            {
                var next = PickWeighted(rng, eligible);
                picked.Add(next);
                eligible.RemoveAll(s => s.Key == next.Key);
            }
        }

        var snippetKeys = picked.Select(p => p.Key).ToList();
        var text = string.Join("\n", picked.Select(p => ApplyTemplate(p.Text, req, rng)));

        return new ComposeResult(text, snippetKeys);
    }

    private static bool IsEligible(DialogueSnippet s, ComposeRequest req)
    {
        if (!string.IsNullOrWhiteSpace(s.PackKey))
        {
            if (!req.EnabledLorePacks.Any(p => p.Equals(s.PackKey, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (s.MinSanity is not null && req.Sanity < s.MinSanity.Value)
            return false;
        if (s.MaxSanity is not null && req.Sanity > s.MaxSanity.Value)
            return false;

        if (s.MinMorality is not null && req.Morality < s.MinMorality.Value)
            return false;
        if (s.MaxMorality is not null && req.Morality > s.MaxMorality.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(s.RequiredDisposition))
        {
            if (!s.RequiredDisposition.Equals(req.Disposition.ToString(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Tag matching: if snippet has tags, at least one must match request context tags.
        // If snippet has no tags, it's always eligible.
        var sTags = ParseTags(s.Tags);
        if (sTags.Count > 0)
        {
            var reqTags = new HashSet<string>(req.ContextTags, StringComparer.OrdinalIgnoreCase);
            if (!sTags.Any(t => reqTags.Contains(t)))
                return false;
        }

        return true;
    }

    private static DialogueSnippet PickWeighted(Random rng, List<DialogueSnippet> pool)
    {
        // Weight <= 0 means "never".
        var filtered = pool.Where(p => p.Weight > 0).ToList();
        if (filtered.Count == 0)
            return pool[0];

        var total = filtered.Sum(p => p.Weight);
        var roll = rng.Next(total);
        foreach (var s in filtered)
        {
            roll -= s.Weight;
            if (roll < 0)
                return s;
        }

        return filtered[^1];
    }

    private string ApplyTemplate(string input, ComposeRequest req, Random rng)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var composed = input
            .Replace("{player}", req.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{room}", req.RoomName, StringComparison.OrdinalIgnoreCase);

        composed = ReplaceFearWord(composed, req, rng);
        return composed;
    }

    private static List<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return new List<string>();

        return tags
            .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Select(t => t.Trim())
            .ToList();
    }

    private string ReplaceFearWord(string input, ComposeRequest req, Random rng)
    {
        if (!input.Contains("{fearWord}", StringComparison.OrdinalIgnoreCase))
            return input;

        var fearWord = PickFearWordAsync(req, rng).GetAwaiter().GetResult();
        return input.Replace("{fearWord}", fearWord, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> PickFearWordAsync(ComposeRequest req, Random rng)
    {
        // Pull from DB so modding/expansion is purely data-driven.
        var words = await _db.LoreWords
            .AsNoTracking()
            .Where(w => w.Category == "fear")
            .ToListAsync();

        // Sanity + pack gating
        var eligible = words
            .Where(w => w.MinSanity is null || req.Sanity >= w.MinSanity.Value)
            .Where(w => w.MaxSanity is null || req.Sanity <= w.MaxSanity.Value)
            .Where(w => string.IsNullOrWhiteSpace(w.PackKey) || req.EnabledLorePacks.Any(p => p.Equals(w.PackKey, StringComparison.OrdinalIgnoreCase)))
            .Where(w => w.Weight > 0)
            .ToList();

        if (eligible.Count == 0)
            return "whisper";

        // Sanity-driven bias: at low sanity, favor pack-specific words more.
        // This keeps the style sharper and makes "lovecraft"/"zork"/"undertale" feel more present.
        var sanity01 = Math.Clamp(req.Sanity / 100.0, 0.0, 1.0);
        var lowSanityBias = 1.0 - sanity01; // 0 when sane, 1 when broken

        // When bias is high, pack-specific words get a multiplier.
        // Deterministic and cheap.
        const int packBoostMax = 4; // up to 4x at 0 sanity

        var weightedPool = eligible
            .Select(w => (w, effectiveWeight: w.Weight * (string.IsNullOrWhiteSpace(w.PackKey) ? 1 : 1 + (int)Math.Round(lowSanityBias * packBoostMax))))
            .Where(x => x.effectiveWeight > 0)
            .ToList();

        var total = weightedPool.Sum(x => x.effectiveWeight);
        var roll = rng.Next(total);
        foreach (var entry in weightedPool)
        {
            roll -= entry.effectiveWeight;
            if (roll < 0)
                return entry.w.Text;
        }

        return weightedPool[^1].w.Text;
    }
}
