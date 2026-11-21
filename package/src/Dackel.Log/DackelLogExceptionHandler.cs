using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dackel.Log;

/// <summary>
/// <see cref="IExceptionHandler"/> pronto para uso: registra a exceção de forma
/// estruturada (sem duplicar o log de <see cref="ExceptionLoggingMiddleware"/>)
/// e responde <c>ProblemDetails</c> com o <c>traceId</c> da requisição.
/// </summary>
/// <remarks>
/// Registre com <c>AddDackelLogExceptionHandling()</c> e use
/// <c>app.UseExceptionHandler()</c> no pipeline. A mensagem e o stack trace da
/// exceção não são expostos ao cliente; o detalhe fica apenas no log.
/// </remarks>
public sealed class DackelLogExceptionHandler(
    StructuredExceptionLogger exceptionLogger,
    IProblemDetailsService problemDetailsService,
    IOptions<DackelLogExceptionHandlerOptions> options) : IExceptionHandler
{
    /// <summary>
    /// Status HTTP padrão quando nenhum seletor é configurado.
    /// </summary>
    public const int DefaultStatusCode = StatusCodes.Status500InternalServerError;

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var settings = options.Value;
        var traceId = TraceIdMiddleware.GetTraceId(httpContext);

        if (!ExceptionLoggingMiddleware.WasLogged(httpContext))
        {
            exceptionLogger.LogError(
                exception,
                ExceptionLoggingMiddleware.MessageTemplate,
                [httpContext.Request.Method, httpContext.Request.Path.Value],
                traceId);
        }

        var statusCode = ResolveStatusCode(settings, exception);
        var title = string.IsNullOrWhiteSpace(settings.Title)
            ? DackelLogExceptionHandlerOptions.DefaultTitle
            : settings.Title;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = httpContext.Request.Path.Value,
        };

        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private static int ResolveStatusCode(DackelLogExceptionHandlerOptions settings, Exception exception)
    {
        var selected = settings.StatusCodeSelector?.Invoke(exception) ?? DefaultStatusCode;

        return selected is >= StatusCodes.Status400BadRequest and <= 599
            ? selected
            : DefaultStatusCode;
    }
}
