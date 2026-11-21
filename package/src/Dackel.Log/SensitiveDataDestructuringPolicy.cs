using System.Collections;
using System.Globalization;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Dackel.Log;

/// <summary>
/// Política de destructuring do Serilog que substitui propriedades sensíveis por <c>***</c>.
/// </summary>
/// <remarks>
/// A política atua em objetos de configuração/opções da aplicação
/// (<c>*Settings</c>, <c>*Options</c>, <c>*Configuration</c>, <c>*Credentials</c>),
/// independentemente do namespace, e em dicionários (chaves sensíveis são mascaradas).
/// Ela não força o destructuring de entidades de domínio, DTOs ou comandos —
/// isso evita exposição de PII e efeitos colaterais como lazy loading do EF Core.
/// <para>
/// A redaction só ocorre quando o objeto é logado com destructuring explícito
/// (ex.: <c>{@Options}</c>).
/// </para>
/// </remarks>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private const string Mask = "***";

    /// <inheritdoc />
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        result = null!;

        try
        {
            if (value is IDictionary dictionary)
            {
                result = MaskSensitiveEntries(dictionary, propertyValueFactory);
                return true;
            }

            if (!ShouldInspect(value))
            {
                return false;
            }

            var type = value.GetType();

            var properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Select(property => ToLogEventProperty(property, value, propertyValueFactory))
                .ToArray();

            result = new StructureValue(properties, type.Name);
            return true;
        }
        catch
        {
            // Fail-closed: se a inspeção falhar, mascara o objeto inteiro em vez
            // de cair no destructuring padrão e vazar o valor original.
            result = new ScalarValue(Mask);
            return true;
        }
    }

    private static StructureValue MaskSensitiveEntries(
        IDictionary dictionary,
        ILogEventPropertyValueFactory propertyValueFactory)
    {
        var properties = new List<LogEventProperty>(dictionary.Count);

        foreach (DictionaryEntry entry in dictionary)
        {
            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "?";

            var isSensitive = !string.IsNullOrWhiteSpace(key) && SensitiveLogNames.IsSensitiveProperty(key);

            properties.Add(new LogEventProperty(
                key,
                isSensitive
                    ? new ScalarValue(Mask)
                    : propertyValueFactory.CreatePropertyValue(entry.Value, destructureObjects: true)));
        }

        return new StructureValue(properties);
    }

    private static LogEventProperty ToLogEventProperty(
        PropertyInfo property,
        object instance,
        ILogEventPropertyValueFactory propertyValueFactory)
    {
        if (SensitiveLogNames.IsSensitiveProperty(property.Name))
        {
            return new LogEventProperty(property.Name, new ScalarValue(Mask));
        }

        object? propertyValue;

        try
        {
            propertyValue = property.GetValue(instance);
        }
        catch
        {
            propertyValue = Mask;
        }

        return new LogEventProperty(
            property.Name,
            propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: true));
    }

    private static bool ShouldInspect(object value)
    {
        if (value is Exception || value is string || value is IEnumerable)
        {
            return false;
        }

        var type = value.GetType();

        if (type.IsPrimitive || type.IsEnum || type.IsValueType)
        {
            return false;
        }

        var name = type.Name;

        return name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Credentials", StringComparison.OrdinalIgnoreCase);
    }
}
