namespace Dackel.Log;

/// <summary>
/// Opções de enriquecimento do log estruturado.
/// </summary>
/// <remarks>
/// A seção <c>StructuredLogging:Enrichments</c> mapeia o nome da propriedade de log
/// para uma expressão de origem declarativa. Origens suportadas: <c>Claim:&lt;tipo&gt;</c>,
/// <c>Header:&lt;nome&gt;</c>, <c>Item:&lt;chave&gt;</c> e <c>User:Name</c>.
/// Exemplo: <c>"Matricula": "User:Name"</c>.
/// <para>
/// Valores calculados em código (ex.: tenant obtido de um serviço da aplicação)
/// devem ser registrados com <see cref="AddEnrichment"/> ou com a extensão
/// <c>EnrichStructuredLogging()</c>. Enriquecimentos programáticos têm precedência
/// sobre os declarados na configuração.
/// </para>
/// </remarks>
public sealed class StructuredLoggingOptions
{
    /// <summary>
    /// Nome da seção de configuração no <c>appsettings</c>.
    /// </summary>
    public const string SectionName = "StructuredLogging";

    /// <summary>
    /// Propriedades adicionadas ao log de cada requisição, declaradas na configuração.
    /// </summary>
    public Dictionary<string, string> Enrichments { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Propriedades adicionadas ao log de cada requisição por código.
    /// </summary>
    /// <remarks>
    /// Prefira <see cref="AddEnrichment"/>, que valida o nome da propriedade.
    /// </remarks>
    public Dictionary<string, StructuredEnrichmentFactory> ProgrammaticEnrichments { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registra um enriquecimento cujo valor é obtido por código a cada requisição.
    /// </summary>
    /// <param name="propertyName">
    /// Nome da propriedade no log (ex.: <c>Tenant</c>). Deve começar com letra ou
    /// <c>_</c> e conter apenas letras, números, <c>_</c>, <c>-</c> ou <c>.</c>.
    /// </param>
    /// <param name="valueFactory">Fábrica do valor, executada por requisição.</param>
    /// <returns>As próprias opções, para encadeamento.</returns>
    /// <exception cref="ArgumentException">
    /// Quando o nome da propriedade é inválido ou representa um dado sensível.
    /// </exception>
    /// <exception cref="ArgumentNullException">Quando <paramref name="valueFactory"/> é <c>null</c>.</exception>
    public StructuredLoggingOptions AddEnrichment(string propertyName, StructuredEnrichmentFactory valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (!EnrichmentPropertyName.IsValid(propertyName))
        {
            throw new ArgumentException(
                $"Nome de propriedade inválido '{propertyName}'. Use letras, números, '_', '-' ou '.', " +
                $"começando por letra ou '_' (máx. {EnrichmentPropertyName.MaxLength}).",
                nameof(propertyName));
        }

        if (SensitiveLogNames.IsSensitiveProperty(propertyName))
        {
            throw new ArgumentException(
                $"A propriedade '{propertyName}' é sensível e não pode ser enriquecida.",
                nameof(propertyName));
        }

        ProgrammaticEnrichments[propertyName] = valueFactory;
        return this;
    }
}
