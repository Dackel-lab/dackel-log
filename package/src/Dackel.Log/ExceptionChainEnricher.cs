using Serilog.Core;
using Serilog.Events;

namespace Dackel.Log;

/// <summary>
/// Enriquece o evento de log com a cadeia estruturada de exceções coletada
/// durante a chamada de <see cref="StructuredExceptionLogger"/>.
/// </summary>
/// <remarks>
/// A cadeia é adicionada como <see cref="StructureValue"/>/<see cref="SequenceValue"/>
/// para permanecer consultável (e não como o <c>ToString()</c> de um record).
/// </remarks>
public sealed class ExceptionChainEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var chain = ExceptionChainContext.Current;

        if (chain is null || logEvent.Properties.ContainsKey("ExceptionChain"))
        {
            return;
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
