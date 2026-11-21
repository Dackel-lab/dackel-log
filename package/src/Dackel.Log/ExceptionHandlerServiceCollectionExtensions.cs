using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dackel.Log;

/// <summary>
/// Extensões de registro do handler de exceções embutido.
/// </summary>
public static class ExceptionHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="DackelLogExceptionHandler"/> como <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
    /// e garante o serviço de <c>ProblemDetails</c>.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configure">Configuração opcional de status/título.</param>
    /// <returns>A própria coleção, para encadeamento.</returns>
    /// <remarks>
    /// Requer <c>AddStructuredLogging()</c> (que registra o
    /// <see cref="StructuredExceptionLogger"/>) e a chamada a
    /// <c>app.UseExceptionHandler()</c> no pipeline. Se
    /// <c>UseExceptionLogging()</c> também estiver no pipeline, o handler não
    /// duplica o log graças a <see cref="ExceptionLoggingMiddleware.WasLogged"/>.
    /// </remarks>
    public static IServiceCollection AddDackelLogExceptionHandling(
        this IServiceCollection services,
        Action<DackelLogExceptionHandlerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddOptions<DackelLogExceptionHandlerOptions>();

        // Garante o mesmo trace id do header X-Trace-Id também no ProblemDetails,
        // preservando qualquer CustomizeProblemDetails registrado pela aplicação.
        services.PostConfigure<ProblemDetailsOptions>(options =>
        {
            var existing = options.CustomizeProblemDetails;

            options.CustomizeProblemDetails = context =>
            {
                existing?.Invoke(context);
                context.ProblemDetails.Extensions["traceId"] = TraceIdMiddleware.GetTraceId(context.HttpContext);
            };
        });

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddExceptionHandler<DackelLogExceptionHandler>();

        return services;
    }
}
