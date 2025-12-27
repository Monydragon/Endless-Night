using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EndlessNight.Persistence;

/// <summary>
/// Stores List&lt;string&gt; as a JSON string in SQLite.
/// </summary>
public sealed class StringListJsonConverter : ValueConverter<List<string>, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public StringListJsonConverter() : base(
        list => JsonSerializer.Serialize(list, Options),
        json => JsonSerializer.Deserialize<List<string>>(json, Options) ?? new())
    {
    }
}

