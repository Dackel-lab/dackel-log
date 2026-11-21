namespace Dackel.Log;

/// <summary>
/// Nomes de propriedades e headers que nunca devem ser enriquecidos em logs.
/// </summary>
internal static class SensitiveLogNames
{
    private static readonly HashSet<string> PropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "senha",
        "pwd",
        "secret",
        "clientsecret",
        "token",
        "accesstoken",
        "refreshtoken",
        "authorization",
        "apikey",
        "connectionstring",
        "cpf",
        "cnpj",
        "documento",
        "numerodocumento",
        "cartao",
        "cvv",
        "pin",
        "certificado",
        "certificatepassword",
    };

    private static readonly HashSet<string> HeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "cookie",
        "set-cookie",
        "x-api-key",
        "proxy-authorization",
    };

    public static bool IsSensitiveProperty(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var normalized = propertyName
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return PropertyNames.Contains(normalized);
    }

    public static bool IsSensitiveHeader(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        return HeaderNames.Contains(headerName);
    }
}
