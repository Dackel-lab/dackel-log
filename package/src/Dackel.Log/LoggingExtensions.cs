using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Dackel.Log;

/// <summary>
/// Encapsula toda a configuração de logging da API: Serilog, sinks,
/// enriquecimento, redaction de dados sensíveis, correlação e log de requisições.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Nome lógico padrão da aplicação enviado em todos os eventos.
    /// </summary>
    public const string DefaultApplicationName = "Dackel.Log";

    /// <summary>
    /// Registra o Serilog, as opções de logging de exceções e os serviços do componente.
    /// </summary>
    /// <param name="builder">Builder da aplicação.</param>
    /// <param name="applicationName">Nome lógico da aplicação enviado nos eventos.</param>
    /// <param name="suppressFrameworkExceptionLog">
    /// Suprime o log duplicado e com mensagem crua emitido pelo
    /// <c>Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware</c>.
    /// Mantenha <c>true</c> quando <c>UseExceptionLogging()</c> for usado.
    /// </param>
    /// <returns>O próprio builder, para encadeamento.</returns>
    /// <remarks>
    /// O sink de console em JSON compacto é definido aqui para garantir o formato
    /// consumido pela plataforma corporativa (Grafana/Datadog). A seção <c>Serilog</c>
    /// do <c>appsettings</c> deve conter apenas <c>MinimumLevel</c>, <c>Enrich</c> e
    /// <c>Properties</c>; não declare <c>WriteTo</c> para evitar sink duplicado.
    /// </remarks>
    public static WebApplicationBuilder AddStructuredLogging(
        this WebApplicationBuilder builder,
        string applicationName = DefaultApplicationName,
        bool suppressFrameworkExceptionLog = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new ArgumentException("O nome da aplicação é obrigatório.", nameof(applicationName));
        }

        builder.Services
            .AddOptions<ExceptionLoggingOptions>()
            .Bind(builder.Configuration.GetSection(ExceptionLoggingOptions.SectionName))
            .Validate(
                options => options.MaxDepth is >= 1 and <= ExceptionChainCollector.AbsoluteMaxDepth,
                $"ExceptionLogging:MaxDepth deve estar entre 1 e {ExceptionChainCollector.AbsoluteMaxDepth}.")
            .ValidateOnStart();

        builder.Services
            .AddOptions<StructuredLoggingOptions>()
            .Bind(builder.Configuration.GetSection(StructuredLoggingOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<StructuredLoggingOptions>, StructuredLoggingOptionsValidator>();
        builder.Services.AddSingleton<ExceptionChainCollector>();
        builder.Services.AddSingleton<StructuredExceptionLogger>();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<ExceptionChainEnricher>()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Destructure.With<SensitiveDataDestructuringPolicy>();

            if (suppressFrameworkExceptionLog)
            {
                // O middleware nativo loga a exceção crua e duplicada; o componente já loga a cadeia sanitizada.
                configuration.MinimumLevel.Override(
                    "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
                    LogEventLevel.Fatal);
            }

            configuration.WriteTo.Console(new CompactJsonFormatter());
        });

        return builder;
    }

    /// <summary>
    /// Registra um enriquecimento programático: o valor da propriedade é obtido
    /// por código a cada requisição, em vez de vir do <c>appsettings</c>.
    /// </summary>
    /// <param name="builder">Builder da aplicação.</param>
    /// <param name="propertyName">
    /// Nome da propriedade no log (ex.: <c>Tenant</c>).
    /// </param>
    /// <param name="valueFactory">
    /// Fábrica do valor. Recebe o <see cref="HttpContext"/> e o provedor de serviços
    /// do escopo, permitindo resolver serviços da aplicação (ex.: um provedor de tenant).
    /// </param>
    /// <returns>O próprio builder, para encadeamento.</returns>
    /// <remarks>
    /// Chame depois de <c>AddStructuredLogging()</c> e registre
    /// <c>UseRequestEnrichment()</c> no pipeline. Enriquecimentos programáticos
    /// têm precedência sobre os declarados em <c>StructuredLogging:Enrichments</c>.
    /// <para>Exemplo:</para>
    /// <code>
    /// builder.AddStructuredLogging("Minha.Api")
    ///     .EnrichStructuredLogging("Tenant", (context, services) =>
    ///         services.GetRequiredService&lt;ITenantProvider&gt;().CurrentTenant);
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="propertyName"/> é inválido ou representa um dado sensível.
    /// </exception>
    public static WebApplicationBuilder EnrichStructuredLogging(
        this WebApplicationBuilder builder,
        string propertyName,
        StructuredEnrichmentFactory valueFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(valueFactory);

        builder.Services
            .AddOptions<StructuredLoggingOptions>()
            .Configure(options => options.AddEnrichment(propertyName, valueFactory));

        return builder;
    }

    /// <summary>
    /// Sobrecarga de conveniência para enriquecimentos que dependem apenas do
    /// <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="builder">Builder da aplicação.</param>
    /// <param name="propertyName">Nome da propriedade no log.</param>
    /// <param name="valueFactory">Fábrica do valor, executada por requisição.</param>
    /// <returns>O próprio builder, para encadeamento.</returns>
    public static WebApplicationBuilder EnrichStructuredLogging(
        this WebApplicationBuilder builder,
        string propertyName,
        Func<HttpContext, string?> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        return builder.EnrichStructuredLogging(
            propertyName,
            (context, _) => valueFactory(context));
    }

    /// <summary>
    /// Adiciona o middleware que garante <c>X-Trace-Id</c> em todas as respostas
    /// e injeta o trace id no <c>LogContext</c>.
    /// </summary>
    /// <param name="app">Pipeline da aplicação.</param>
    /// <returns>O próprio pipeline, para encadeamento.</returns>
    public static IApplicationBuilder UseTraceId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<TraceIdMiddleware>();
    }

    /// <summary>
    /// Adiciona ao <c>LogContext</c> as propriedades configuradas em
    /// <c>StructuredLogging:Enrichments</c> e as registradas por código com
    /// <c>EnrichStructuredLogging()</c> (ex.: matrícula, tenant, unidade).
    /// </summary>
    /// <param name="app">Pipeline da aplicação.</param>
    /// <returns>O próprio pipeline, para encadeamento.</returns>
    /// <remarks>
    /// Registre depois de <c>UseTraceId()</c> e antes de
    /// <c>UseStructuredRequestLogging()</c> para que o log de requisição e o log
    /// de exceções herdem as propriedades.
    /// </remarks>
    public static IApplicationBuilder UseRequestEnrichment(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestEnrichmentMiddleware>();
    }

    /// <summary>
    /// Adiciona o middleware que loga exceções não tratadas e as repropaga.
    /// </summary>
    /// <param name="app">Pipeline da aplicação.</param>
    /// <returns>O próprio pipeline, para encadeamento.</returns>
    public static IApplicationBuilder UseExceptionLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<ExceptionLoggingMiddleware>();
    }

    /// <summary>
    /// Habilita o log estruturado de requisições HTTP, com nível coerente com o status
    /// e enriquecimento de correlação (<c>TraceId</c>, <c>RequestId</c> e <c>RequestPath</c>).
    /// </summary>
    /// <param name="app">Pipeline da aplicação.</param>
    /// <returns>O próprio pipeline, para encadeamento.</returns>
    public static IApplicationBuilder UseStructuredRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("TraceId", TraceIdMiddleware.GetTraceId(httpContext));
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
            };
        });

        return app;
    }
}
