using Microsoft.AspNetCore.Http;

namespace Dackel.Log;

/// <summary>
/// Resolve as propriedades de enriquecimento a partir do <see cref="HttpContext"/>.
/// </summary>
internal static class LogEnrichmentResolver
{
    private const int MaxValueLength = 1024;
    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>();

    public static IReadOnlyDictionary<string, object?> Resolve(
        HttpContext context,
        IReadOnlyDictionary<string, string> enrichments)
    {
        if (enrichments.Count == 0)
        {
            return Empty;
        }

        var result = new Dictionary<string, object?>(enrichments.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (propertyName, expressionValue) in enrichments)
        {
            if (SensitiveLogNames.IsSensitiveProperty(propertyName))
            {
                continue;
            }

            if (!LogEnrichmentExpression.TryParse(expressionValue, out var expression))
            {
                continue;
            }

            if (expression.Source == LogEnrichmentSource.Header &&
                SensitiveLogNames.IsSensitiveHeader(expression.Key))
            {
                continue;
            }

            var value = Normalize(ResolveValue(context, expression));

            if (value is not null)
            {
                result[propertyName] = value;
            }
        }

        return result;
    }

    private static string? ResolveValue(HttpContext context, LogEnrichmentExpression expression)
    {
        return expression.Source switch
        {
            LogEnrichmentSource.Claim => context.User.FindFirst(expression.Key)?.Value,
            LogEnrichmentSource.Header => context.Request.Headers.TryGetValue(expression.Key, out var header)
                ? header.ToString()
                : null,
            LogEnrichmentSource.Item => context.Items.TryGetValue(expression.Key, out var item)
                ? item?.ToString()
                : null,
            LogEnrichmentSource.User => context.User.Identity?.Name,
            _ => null,
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = LogSanitizer.Sanitize(value) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        return sanitized.Length > MaxValueLength
            ? string.Concat(sanitized.AsSpan(0, MaxValueLength), "…")
            : sanitized;
    }
}
