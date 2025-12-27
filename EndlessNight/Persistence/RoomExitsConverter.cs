using System.Text.Json;
using EndlessNight.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EndlessNight.Persistence;

/// <summary>
/// Stores RoomInstance.Exits as a JSON string in SQLite.
/// </summary>
public sealed class RoomExitsConverter : ValueConverter<Dictionary<Direction, Guid>, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public RoomExitsConverter() : base(
        exits => JsonSerializer.Serialize(exits, Options),
        json => JsonSerializer.Deserialize<Dictionary<Direction, Guid>>(json, Options) ?? new())
    {
    }
}

