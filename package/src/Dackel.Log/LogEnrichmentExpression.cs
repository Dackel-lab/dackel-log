namespace Dackel.Log;

/// <summary>
/// Origem de um valor de enriquecimento.
/// </summary>
internal enum LogEnrichmentSource
{
    Claim,
    Header,
    Item,
    User,
}

/// <summary>
/// Expressão de origem de um enriquecimento, no formato <c>fonte:chave</c>.
/// </summary>
internal readonly record struct LogEnrichmentExpression(LogEnrichmentSource Source, string Key)
{
    private const int MaxKeyLength = 128;

    public static bool TryParse(string? value, out LogEnrichmentExpression expression)
    {
        expression = default;

        if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
        {
            return false;
        }

        var separator = value.IndexOf(':');

        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        var sourceText = value[..separator].Trim();
        var key = value[(separator + 1)..].Trim();

        if (key.Length is 0 or > MaxKeyLength || key.Any(char.IsControl))
        {
            return false;
        }

        var source = sourceText.ToLowerInvariant() switch
        {
            "claim" => LogEnrichmentSource.Claim,
            "header" => LogEnrichmentSource.Header,
            "item" => LogEnrichmentSource.Item,
            "user" => LogEnrichmentSource.User,
            _ => (LogEnrichmentSource?)null,
        };

        if (source is null)
        {
            return false;
        }

        expression = new LogEnrichmentExpression(source.Value, key);
        return true;
    }
}
