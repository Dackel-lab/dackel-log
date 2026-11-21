using Microsoft.Extensions.Options;

namespace Dackel.Log;

/// <summary>
/// Opções do logging estruturado de exceções.
/// </summary>
public sealed class ExceptionLoggingOptions
{
    /// <summary>
    /// Nome da seção de configuração no <c>appsettings</c>.
    /// </summary>
    public const string SectionName = "ExceptionLogging";

    /// <summary>
    /// Profundidade máxima da cadeia de exceções (inner exceptions) registrada no log.
    /// Valor padrão: 10. Faixa válida: 1 a 50.
    /// </summary>
    public int MaxDepth { get; init; } = 10;
}

/// <summary>
/// Resultado da coleta recursiva da cadeia de exceções.
/// </summary>
/// <param name="Items">Exceções coletadas em ordem de profundidade (0 = exceção raiz).</param>
/// <param name="MaxDepth">Limite de profundidade efetivamente aplicado.</param>
/// <param name="Truncated">Indica que a cadeia foi cortada por limite de profundidade ou ciclo.</param>
public sealed record ExceptionChainResult(
    IReadOnlyList<ExceptionInfo> Items,
    int MaxDepth,
    bool Truncated);

/// <summary>
/// Representa uma exceção individual dentro da cadeia, já sanitizada para log.
/// </summary>
/// <param name="Depth">Profundidade na cadeia (0 = exceção raiz).</param>
/// <param name="Type">Nome completo do tipo da exceção.</param>
/// <param name="Message">Mensagem sanitizada (sem CPF/CNPJ, e-mail, token ou segredo).</param>
/// <param name="HResult">Código <see cref="Exception.HResult"/>.</param>
/// <param name="Source">Assembly/origem da exceção.</param>
/// <param name="TargetSite">Método que lançou a exceção.</param>
/// <param name="StackTrace">Stack trace original da exceção.</param>
public sealed record ExceptionInfo(
    int Depth,
    string Type,
    string Message,
    int HResult,
    string? Source,
    string? TargetSite,
    string? StackTrace);

/// <summary>
/// Coleta a cadeia completa de exceções (<see cref="Exception.InnerException"/> e
/// <see cref="AggregateException.InnerExceptions"/>) de forma recursiva, com limite
/// de profundidade configurável e proteção contra referências cíclicas.
/// </summary>
public sealed class ExceptionChainCollector(IOptions<ExceptionLoggingOptions> options)
{
    /// <summary>
    /// Limite absoluto de segurança, independente da configuração.
    /// </summary>
    public const int AbsoluteMaxDepth = 50;

    private readonly int _maxDepth = Math.Clamp(options.Value.MaxDepth, 1, AbsoluteMaxDepth);

    /// <summary>
    /// Coleta a cadeia de exceções a partir da exceção raiz.
    /// </summary>
    /// <param name="exception">Exceção raiz. Pode ser <c>null</c>.</param>
    /// <returns>A cadeia sanitizada e o indicador de truncamento.</returns>
    public ExceptionChainResult Collect(Exception? exception)
    {
        var items = new List<ExceptionInfo>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var truncated = false;

        Walk(exception, depth: 0, items, visited, ref truncated);

        return new ExceptionChainResult(items, _maxDepth, truncated);
    }

    private void Walk(
        Exception? exception,
        int depth,
        List<ExceptionInfo> items,
        HashSet<Exception> visited,
        ref bool truncated)
    {
        if (exception is null)
        {
            return;
        }

        if (depth >= _maxDepth)
        {
            truncated = true;
            return;
        }

        if (!visited.Add(exception))
        {
            // Referência cíclica: interrompe para não gerar log infinito.
            truncated = true;
            return;
        }

        items.Add(new ExceptionInfo(
            Depth: depth,
            Type: exception.GetType().FullName ?? exception.GetType().Name,
            Message: LogSanitizer.Sanitize(exception.Message) ?? string.Empty,
            HResult: exception.HResult,
            Source: exception.Source,
            TargetSite: exception.TargetSite?.Name,
            StackTrace: exception.StackTrace));

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                Walk(innerException, depth + 1, items, visited, ref truncated);
            }

            return;
        }

        Walk(exception.InnerException, depth + 1, items, visited, ref truncated);
    }
}
