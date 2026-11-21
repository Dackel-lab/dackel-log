using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Dackel.Log;

/// <summary>
/// Middleware que garante um <c>traceId</c> único por requisição, devolve o valor
/// no header <c>X-Trace-Id</c> em <b>todas</b> as respostas (sucesso e erro),
/// disponibiliza o valor em <see cref="HttpContext.Items"/> e o injeta no
/// <c>LogContext</c> do Serilog.
/// </summary>
public sealed class TraceIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Header de resposta que expõe o trace id para o chamador.
    /// </summary>
    public const string HeaderName = "X-Trace-Id";

    /// <summary>
    /// Chave utilizada em <see cref="HttpContext.Items"/> para o trace id.
    /// </summary>
    public const string ItemKey = "TraceId";

    private const string EmptyTraceId = "00000000000000000000000000000000";

    /// <summary>
    /// Executa o middleware.
    /// </summary>
    /// <param name="context">Contexto HTTP da requisição.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var traceId = Activity.Current?.TraceId.ToString();

        if (string.IsNullOrWhiteSpace(traceId) || traceId == EmptyTraceId)
        {
            traceId = context.TraceIdentifier;
        }

        context.Items[ItemKey] = traceId;

        // OnStarting garante o header mesmo quando o corpo é escrito pelo exception handler.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemKey, traceId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Obtém o trace id da requisição, com fallbacks seguros.
    /// </summary>
    /// <param name="context">Contexto HTTP da requisição.</param>
    /// <returns>Trace id no formato W3C (32 caracteres hexadecimais) ou o identificador da requisição.</returns>
    public static string GetTraceId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(ItemKey, out var value) && value is string traceId && traceId.Length > 0)
        {
            return traceId;
        }

        var activityTraceId = Activity.Current?.TraceId.ToString();

        return string.IsNullOrWhiteSpace(activityTraceId) || activityTraceId == EmptyTraceId
            ? context.TraceIdentifier
            : activityTraceId;
    }
}
