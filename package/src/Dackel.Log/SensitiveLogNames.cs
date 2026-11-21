namespace Dackel.Log;

/// <summary>
/// Nomes de propriedades e headers que nunca devem ser enriquecidos ou
/// destruturados em logs.
/// </summary>
/// <remarks>
/// A comparação ignora caixa e separadores (<c>_</c>, <c>-</c>, <c>.</c>,
/// espaços etc.) e também reconhece sufixos: <c>DbPassword</c> e
/// <c>user.password</c> são tratados como <c>password</c>.
/// </remarks>
internal static class SensitiveLogNames
{
    private static readonly HashSet<string> PropertyNames = new(StringComparer.Ordinal)
    {
        "password",
        "passwordhash",
        "hashedpassword",
        "senha",
        "pwd",
        "passphrase",
        "secret",
        "secretkey",
        "apisecret",
        "clientsecret",
        "privatekey",
        "symmetrickey",
        "signingkey",
        "token",
        "authtoken",
        "accesstoken",
        "refreshtoken",
        "idtoken",
        "securitytoken",
        "bearertoken",
        "authorization",
        "credential",
        "credentials",
        "apikey",
        "connectionstring",
        "cpf",
        "cnpj",
        "documento",
        "numerodocumento",
        "cartao",
        "cardnumber",
        "creditcard",
        "cvv",
        "pin",
        "certificado",
        "certificatepassword",
    };

    private static readonly HashSet<string> HeaderNames = new(StringComparer.Ordinal)
    {
        "authorization",
        "cookie",
        "setcookie",
        "xapikey",
        "xauthtoken",
        "xaccesstoken",
        "xamzsecuritytoken",
        "xcsrftoken",
        "proxyauthorization",
    };

    public static bool IsSensitiveProperty(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var normalized = Normalize(propertyName);

        if (normalized.Length == 0)
        {
            return false;
        }

        if (PropertyNames.Contains(normalized))
        {
            return true;
        }

        foreach (var candidate in PropertyNames)
        {
            // Sufixos cobrem nomes compostos (ex.: DbPassword, user.password).
            if (normalized.EndsWith(candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsSensitiveHeader(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        return HeaderNames.Contains(Normalize(headerName));
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
