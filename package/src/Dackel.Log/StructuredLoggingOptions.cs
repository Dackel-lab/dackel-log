namespace Dackel.Log;

/// <summary>
/// Opções de enriquecimento do log estruturado.
/// </summary>
/// <remarks>
/// A seção <c>StructuredLogging:Enrichments</c> mapeia o nome da propriedade de log
/// para uma expressão de origem. Origens suportadas: <c>Claim:&lt;tipo&gt;</c>,
/// <c>Header:&lt;nome&gt;</c>, <c>Item:&lt;chave&gt;</c> e <c>User:Name</c>.
/// Exemplo: <c>"Matricula": "User:Name"</c>.
/// </remarks>
public sealed class StructuredLoggingOptions
{
    /// <summary>
    /// Nome da seção de configuração no <c>appsettings</c>.
    /// </summary>
    public const string SectionName = "StructuredLogging";

    /// <summary>
    /// Propriedades adicionadas ao log de cada requisição.
    /// </summary>
    public Dictionary<string, string> Enrichments { get; } = new(StringComparer.OrdinalIgnoreCase);
}
