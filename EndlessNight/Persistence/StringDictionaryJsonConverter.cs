using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EndlessNight.Persistence;

/// <summary>
/// Stores Dictionary&lt;string,string&gt; as JSON in SQLite.
/// </summary>
public sealed class StringDictionaryJsonConverter : ValueConverter<Dictionary<string, string>, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public StringDictionaryJsonConverter() : base(
        dict => JsonSerializer.Serialize(dict, Options),
        json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options) ?? new())
    {
    }
}

