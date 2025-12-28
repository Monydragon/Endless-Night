using System.Collections.Generic;

namespace EndlessNight.Domain;

/// <summary>
/// Snapshot of meta-memory state used to influence future runs.
/// </summary>
public sealed class MetaMemoryState
{
    public MetaMemoryState(MetaMemoryMode mode, IReadOnlyDictionary<string, string> flags)
    {
        Mode = mode;
        Flags = flags;
    }

    public MetaMemoryMode Mode { get; }

    public IReadOnlyDictionary<string, string> Flags { get; }

    public bool TryGetFlag(string key, out string value) => Flags.TryGetValue(key, out value);
}

