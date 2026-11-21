using System.Text.RegularExpressions;

namespace Dackel.Log;

/// <summary>
/// Sanitiza textos que serão enviados ao log, mascarando dados pessoais e segredos.
/// </summary>
/// <remarks>
/// A sanitização é aplicada às mensagens das exceções antes de serem estruturadas.
/// Stack traces são preservados integralmente por não conterem dados de entrada do usuário.
/// <para>
/// É uma proteção de melhor esforço: reconhece os formatos mais comuns de CPF/CNPJ
/// (com ou sem máscara/espaços), e-mail, bearer/basic token e pares
/// <c>chave=valor</c> de credenciais. Dados sensíveis em formatos não previstos
/// continuam sendo responsabilidade da aplicação.
/// </para>
/// </remarks>
public static class LogSanitizer
{
    private const string Mask = "***";

    private static readonly Regex CnpjRegex = new(
        @"(?<!\d)\d{2}[.\s]?\d{3}[.\s]?\d{3}[/.\s]?\d{4}[-.\s]?\d{2}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CpfRegex = new(
        @"(?<!\d)\d{3}[.\s]?\d{3}[.\s]?\d{3}[-.\s]?\d{2}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerTokenRegex = new(
        @"\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BasicAuthRegex = new(
        @"\bAuthorization\s*:\s*Basic\s+[A-Za-z0-9+/=]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SecretRegex = new(
        @"(?i)\b(password|senha|pwd|passphrase|secret|client[_-]?secret|api[_-]?secret|" +
        @"private[_-]?key|secret[_-]?key|symmetric[_-]?key|signing[_-]?key|" +
        @"token|access[_-]?token|refresh[_-]?token|id[_-]?token|security[_-]?token|" +
        @"api[_-]?key|authorization)\b(?:\s*[:=]\s*|\s+-\s*)(?!basic\b|bearer\b)(""[^""]*""|'[^']*'|[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Mascara CPF, CNPJ, e-mails, bearer/basic tokens e pares chave/valor sensíveis.
    /// </summary>
    /// <param name="value">Texto original. Pode ser <c>null</c> ou vazio.</param>
    /// <returns>Texto sanitizado, ou o próprio valor quando não há o que mascarar.</returns>
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // CNPJ antes do CPF: um CNPJ sem máscara (14 dígitos) não deve ser
        // parcialmente interpretado como CPF (11 dígitos).
        var sanitized = CnpjRegex.Replace(value, Mask);
        sanitized = CpfRegex.Replace(sanitized, Mask);
        sanitized = EmailRegex.Replace(sanitized, Mask);
        sanitized = BearerTokenRegex.Replace(sanitized, $"Bearer {Mask}");
        sanitized = BasicAuthRegex.Replace(sanitized, $"Authorization: Basic {Mask}");
        sanitized = SecretRegex.Replace(sanitized, match => $"{match.Groups[1].Value}={Mask}");

        return sanitized;
    }
}
