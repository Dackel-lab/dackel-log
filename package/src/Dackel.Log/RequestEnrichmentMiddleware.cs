using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dackel.Log;

/// <summary>
/// Middleware que adiciona ao escopo de log as propriedades configuradas em
/// <c>StructuredLogging:Enrichments</c>, tornando-as disponíveis para o log de
/// requisição e para o log de exceções.
/// </summary>
/// <remarks>
/// Deve ser registrado depois de <c>UseTraceId()</c> e antes de
/// <c>UseStructuredRequestLogging()</c>.
/// </remarks>
public sealed class RequestEnrichmentMiddleware(
    RequestDelegate next,
    IOptions<StructuredLoggingOptions> options,
    ILogger<RequestEnrichmentMiddleware> logger)
{
    /// <summary>
    /// Executa o middleware.
    /// </summary>
    /// <param name="context">Contexto HTTP da requisição.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var properties = LogEnrichmentResolver.Resolve(context, options.Value.Enrichments);

        if (properties.Count == 0)
        {
            await next(context);
            return;
        }

        using (logger.BeginScope(properties))
        {
            await next(context);
        }
    }
}
