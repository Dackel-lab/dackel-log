using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dackel.Log;

/// <summary>
/// Middleware que registra de forma estruturada qualquer exceção não tratada,
/// incluindo a cadeia recursiva de inner exceptions, e repropaga a exceção para
/// que o pipeline de resposta (ex.: <c>IExceptionHandler</c>) produza o
/// <c>ProblemDetails</c>.
/// </summary>
/// <remarks>
/// Deve ser registrado <b>depois</b> de <c>UseExceptionHandler()</c> (mais interno),
/// para que a exceção passe por ele antes de ser convertida em resposta HTTP.
/// Use <see cref="WasLogged(HttpContext)"/> no handler de exceção para evitar
/// logging duplicado.
/// </remarks>
public sealed class ExceptionLoggingMiddleware(
    RequestDelegate next,
    StructuredExceptionLogger exceptionLogger,
    ILogger<ExceptionLoggingMiddleware> logger)
{
    /// <summary>
    /// Chave em <see cref="HttpContext.Items"/> que indica que a exceção já foi logada.
    /// </summary>
    public const string LoggedItemKey = "__S303ExceptionLogged";

    /// <summary>
    /// Template padrão do evento de exceção não tratada.
    /// </summary>
    public const string MessageTemplate =
        "Falha não tratada ao processar {HttpMethod} {RequestPath}";

    /// <summary>
    /// Executa o middleware.
    /// </summary>
    /// <param name="context">Contexto HTTP da requisição.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                // Cancelamento do cliente não é falha da API: sem stack trace e em Information.
                logger.LogInformation(
                    "Requisição cancelada pelo cliente em {RequestPath}",
                    context.Request.Path.Value);
            }
            else
            {
                exceptionLogger.LogError(
                    exception,
                    MessageTemplate,
                    [context.Request.Method, context.Request.Path.Value],
                    traceId: TraceIdMiddleware.GetTraceId(context));
            }

            context.Items[LoggedItemKey] = true;
            throw;
        }
    }

    /// <summary>
    /// Verifica se a exceção da requisição já foi registrada por este middleware.
    /// </summary>
    /// <param name="context">Contexto HTTP da requisição.</param>
    /// <returns><c>true</c> quando o log já foi emitido.</returns>
    public static bool WasLogged(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(LoggedItemKey, out var value) && value is true;
    }
}
