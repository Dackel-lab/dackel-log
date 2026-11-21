using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;

namespace Dackel.Log;

/// <summary>
/// Enriquece o evento de log com a cadeia estruturada de exceções.
/// </summary>
/// <remarks>
/// A cadeia pode vir de <see cref="StructuredExceptionLogger"/> (via
/// <see cref="ExceptionChainContext"/>) ou ser coletada diretamente da exceção
/// anexada ao evento — o que permite usar <c>ILogger.LogError(exception, ...)</c>
/// e <c>Serilog.Log.Error(exception, ...)</c> em handlers sem perder o formato
/// estruturado.
/// <para>
/// A cadeia é adicionada como <see cref="StructureValue"/>/<see cref="SequenceValue"/>
/// para permanecer consultável (e não como o <c>ToString()</c> de um record).
/// </para>
/// </remarks>
public sealed class ExceptionChainEnricher : ILogEventEnricher
{
    private static readonly ExceptionChainCollector DefaultCollector =
        new(Options.Create(new ExceptionLoggingOptions()));

    private readonly ExceptionChainCollector _collector;

    /// <summary>
    /// Cria o enricher com a profundidade padrão de coleta (<c>10</c>).
    /// </summary>
    /// <remarks>
    /// Prefira a sobrecarga que recebe o <see cref="ExceptionChainCollector"/>
    /// registrado no DI para respeitar <c>ExceptionLogging:MaxDepth</c>.
    /// </remarks>
    public ExceptionChainEnricher()
        : this(DefaultCollector)
    {
    }

    /// <summary>
    /// Cria o enricher usando o coletor informado.
    /// </summary>
    /// <param name="collector">Coletor configurado com as opções da aplicação.</param>
    public ExceptionChainEnricher(ExceptionChainCollector collector)
    {
        ArgumentNullException.ThrowIfNull(collector);

        _collector = collector;
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (logEvent.Properties.ContainsKey("ExceptionChain"))
        {
            return;
        }

        var chain = ExceptionChainContext.Current;

        if (chain is null)
        {
            if (logEvent.Exception is null)
            {
                return;
            }

            // Caminho ILogger.LogError/Log.Error: a exceção vem anexada ao evento.
            chain = _collector.Collect(logEvent.Exception);
        }

        logEvent.AddPropertyIfAbsent(
            new LogEventProperty("ExceptionChain", ToSequenceValue(chain.Items)));

        logEvent.AddPropertyIfAbsent(
            new LogEventProperty("ExceptionTruncated", new ScalarValue(chain.Truncated)));

        logEvent.AddPropertyIfAbsent(
            new LogEventProperty("ExceptionDepthLimit", new ScalarValue(chain.MaxDepth)));
    }

    private static SequenceValue ToSequenceValue(IReadOnlyList<ExceptionInfo> items)
    {
        var values = items.Select(item => (LogEventPropertyValue)new StructureValue(
        [
            new LogEventProperty("Depth", new ScalarValue(item.Depth)),
            new LogEventProperty("Type", new ScalarValue(item.Type)),
            new LogEventProperty("Message", new ScalarValue(item.Message)),
            new LogEventProperty("HResult", new ScalarValue(item.HResult)),
            new LogEventProperty("Source", new ScalarValue(item.Source)),
            new LogEventProperty("TargetSite", new ScalarValue(item.TargetSite)),
            new LogEventProperty("StackTrace", new ScalarValue(item.StackTrace)),
        ],
        "ExceptionInfo"));

        return new SequenceValue(values);
    }
}
