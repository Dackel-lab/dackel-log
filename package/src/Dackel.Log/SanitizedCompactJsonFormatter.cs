using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace Dackel.Log;

/// <summary>
/// Formatter JSON compacto que omite o campo cru <c>@x</c> do Serilog.
/// </summary>
/// <remarks>
/// A exceção crua pode conter PII/segredos na mensagem e no stack trace. Quando
/// o evento traz <see cref="LogEvent.Exception"/> (ex.: <c>ILogger.LogError(ex, ...)</c>),
/// a informação segura já está em <c>ExceptionChain</c>, produzida por
/// <see cref="ExceptionChainEnricher"/>. O <c>@x</c> é removido da saída JSON do
/// console do pacote; demais sinks configurados pela aplicação não são alterados.
/// </remarks>
internal sealed class SanitizedCompactJsonFormatter : ITextFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly CompactJsonFormatter _inner = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        if (logEvent.Exception is null)
        {
            _inner.Format(logEvent, output);
            return;
        }

        using var buffer = new StringWriter();
        _inner.Format(logEvent, buffer);

        var json = buffer.ToString();

        try
        {
            if (JsonNode.Parse(json) is JsonObject root && root.Remove("@x"))
            {
                output.Write(root.ToJsonString(SerializerOptions));
                return;
            }
        }
        catch (JsonException)
        {
            // Formato inesperado: preserva a saída original para não perder o evento.
        }

        output.Write(json);
    }
}
