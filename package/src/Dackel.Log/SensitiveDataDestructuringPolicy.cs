using System.Collections;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Dackel.Log;

/// <summary>
/// Política de destructuring do Serilog que substitui propriedades sensíveis por <c>***</c>.
/// </summary>
/// <remarks>
/// A política atua apenas em objetos de configuração/opções da aplicação
/// (<c>*Settings</c>, <c>*Options</c>, <c>*Configuration</c>, <c>*Credentials</c>).
/// Ela não força o destructuring de entidades de domínio, DTOs ou comandos —
/// isso evita exposição de PII e efeitos colaterais como lazy loading do EF Core.
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
            // Uma política de log nunca deve derrubar a aplicação.
            result = null!;
            return false;
        }
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

        var @namespace = type.Namespace;

        if (@namespace is null ||
            (!@namespace.StartsWith("BN.", StringComparison.Ordinal) &&
             !@namespace.StartsWith("BNB.", StringComparison.Ordinal)))
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
