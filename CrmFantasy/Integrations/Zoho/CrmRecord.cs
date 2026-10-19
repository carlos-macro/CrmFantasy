using System.Globalization;

namespace CrmFantasy.Integrations.Zoho;

/// <summary>
/// Registro do CRM sem o JSON do Zoho. Lookups e usuários viram o ID.
/// </summary>
public sealed record CrmRecord(long Id, IReadOnlyDictionary<string, object?> Fields)
{
    public string? GetString(string field)
    {
        if (!Fields.TryGetValue(field, out var value) || value is null) return null;

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public long? GetLong(string field)
    {
        var parsed = long.TryParse(GetString(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);

        return parsed ? value : null;
    }

    public int? GetInt(string field)
    {
        var parsed = decimal.TryParse(GetString(field), NumberStyles.Number, CultureInfo.InvariantCulture, out var value);

        return parsed ? (int)value : null;
    }
}
