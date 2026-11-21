using System.Text.RegularExpressions;

namespace Dackel.Log;

/// <summary>
/// Sanitiza textos que serão enviados ao log, mascarando dados pessoais e segredos.
/// </summary>
/// <remarks>
/// A sanitização é aplicada às mensagens das exceções antes de serem estruturadas.
/// Stack traces são preservados integralmente por não conterem dados de entrada do usuário.
/// </remarks>
public static class LogSanitizer
{
    private const string Mask = "***";

    private static readonly Regex CnpjRegex = new(
        @"(?<!\d)\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CpfRegex = new(
        @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerTokenRegex = new(
        @"\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SecretRegex = new(
        @"(?i)\b(password|senha|pwd|secret|client[_-]?secret|token|access[_-]?token|refresh[_-]?token|api[_-]?key|authorization)\b\s*[:=]\s*(""[^""]*""|'[^']*'|[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Mascara CPF, CNPJ, e-mails, bearer tokens e pares chave/valor sensíveis.
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
        sanitized = SecretRegex.Replace(sanitized, match => $"{match.Groups[1].Value}={Mask}");

        return sanitized;
    }
}
