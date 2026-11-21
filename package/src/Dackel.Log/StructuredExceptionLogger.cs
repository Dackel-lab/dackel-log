using Microsoft.Extensions.Logging;

namespace Dackel.Log;

/// <summary>
/// Encapsula a geração do log estruturado de exceções, incluindo a cadeia
/// recursiva de inner exceptions e o contexto de correlação.
/// </summary>
/// <remarks>
/// A exceção não é anexada ao evento como <c>@x</c> do Serilog: a mensagem crua
/// poderia conter dados sensíveis. Toda a informação necessária (tipo, mensagem
/// sanitizada e stack trace) está em <c>ExceptionChain</c>.
/// </remarks>
public sealed class StructuredExceptionLogger(
    ILogger<StructuredExceptionLogger> logger,
    ExceptionChainCollector exceptionChainCollector)
{
    /// <summary>
    /// Registra uma exceção com a cadeia completa de inner exceptions.
    /// </summary>
    /// <param name="exception">Exceção a ser logada.</param>
    /// <param name="level">Nível do log (Error para 5xx, Warning para 4xx de negócio).</param>
    /// <param name="messageTemplate">Template estruturado, ex.: <c>"Falha ao processar {RequestPath}"</c>.</param>
    /// <param name="args">Argumentos do template, na mesma ordem dos placeholders.</param>
    /// <param name="traceId">Trace id W3C da requisição, quando disponível.</param>
    public void Log(
        Exception exception,
        LogLevel level,
        string messageTemplate,
        object?[]? args = null,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            throw new ArgumentException("O template da mensagem é obrigatório.", nameof(messageTemplate));
        }

        var chain = exceptionChainCollector.Collect(exception);

        var scope = new Dictionary<string, object?>
        {
            ["ExceptionTruncated"] = chain.Truncated,
            ["ExceptionDepthLimit"] = chain.MaxDepth,
        };

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            scope["TraceId"] = traceId;
        }

        // O ExceptionChainEnricher lê esta cadeia e adiciona os objetos estruturados ao evento.
        using var chainContext = ExceptionChainContext.Push(chain);
        using (logger.BeginScope(scope))
        {
            // Sem exceção anexada: evita o @x com mensagem/stack cru, que pode conter PII/segredos.
            logger.Log(level, exception: null, messageTemplate, SanitizeArgs(args));
        }
    }

    private static object?[] SanitizeArgs(object?[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return [];
        }

        var sanitized = new object?[args.Length];

        for (var index = 0; index < args.Length; index++)
        {
            // Apenas strings são sanitizadas: números, datas e objetos de domínio
            // mantêm o formato estruturado esperado pela plataforma de logs.
            sanitized[index] = args[index] is string text
                ? LogSanitizer.Sanitize(text)
                : args[index];
        }

        return sanitized;
    }

    /// <summary>
    /// Registra uma exceção como <see cref="LogLevel.Error"/>.
    /// </summary>
    public void LogError(
        Exception exception,
        string messageTemplate,
        object?[]? args = null,
        string? traceId = null)
        => Log(exception, LogLevel.Error, messageTemplate, args, traceId);

    /// <summary>
    /// Registra uma exceção como <see cref="LogLevel.Warning"/>.
    /// </summary>
    public void LogWarning(
        Exception exception,
        string messageTemplate,
        object?[]? args = null,
        string? traceId = null)
        => Log(exception, LogLevel.Warning, messageTemplate, args, traceId);

    /// <summary>
    /// Registra uma exceção como <see cref="LogLevel.Information"/>.
    /// </summary>
    public void LogInformation(
        Exception exception,
        string messageTemplate,
        object?[]? args = null,
        string? traceId = null)
        => Log(exception, LogLevel.Information, messageTemplate, args, traceId);
}
