using Microsoft.Extensions.Options;

namespace Dackel.Log;

/// <summary>
/// Valida a configuração de enriquecimento no startup.
/// </summary>
internal sealed class StructuredLoggingOptionsValidator : IValidateOptions<StructuredLoggingOptions>
{
    public ValidateOptionsResult Validate(string? name, StructuredLoggingOptions options)
    {
        var failures = new List<string>();

        foreach (var (propertyName, expressionValue) in options.Enrichments)
        {
            if (!EnrichmentPropertyName.IsValid(propertyName))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments: nome de propriedade inválido '{propertyName}'. " +
                    $"Use letras, números, '_', '-' ou '.', começando por letra ou '_' (máx. {EnrichmentPropertyName.MaxLength}).");
                continue;
            }

            if (SensitiveLogNames.IsSensitiveProperty(propertyName))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments: a propriedade '{propertyName}' é sensível e não pode ser enriquecida.");
                continue;
            }

            if (!LogEnrichmentExpression.TryParse(expressionValue, out var expression))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments:{propertyName}: expressão inválida '{expressionValue}'. " +
                    "Use Claim:<tipo>, Header:<nome>, Item:<chave> ou User:Name.");
                continue;
            }

            if (expression.Source == LogEnrichmentSource.Header &&
                SensitiveLogNames.IsSensitiveHeader(expression.Key))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments:{propertyName}: o header '{expression.Key}' é sensível e não pode ser enriquecido.");
            }

            if (expression.Source == LogEnrichmentSource.User &&
                !string.Equals(expression.Key, "Name", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments:{propertyName}: a origem User suporta apenas User:Name.");
            }
        }

        foreach (var propertyName in options.ProgrammaticEnrichments.Keys)
        {
            if (!EnrichmentPropertyName.IsValid(propertyName))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments (programático): nome de propriedade inválido '{propertyName}'. " +
                    $"Use letras, números, '_', '-' ou '.', começando por letra ou '_' (máx. {EnrichmentPropertyName.MaxLength}).");
                continue;
            }

            if (SensitiveLogNames.IsSensitiveProperty(propertyName))
            {
                failures.Add(
                    $"StructuredLogging:Enrichments (programático): a propriedade '{propertyName}' é sensível e não pode ser enriquecida.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
