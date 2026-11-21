namespace Dackel.Log;

/// <summary>
/// Regras compartilhadas de validação do nome de uma propriedade de enriquecimento.
/// </summary>
internal static class EnrichmentPropertyName
{
    public const int MaxLength = 64;

    public static bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxLength)
        {
            return false;
        }

        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            return false;
        }

        return name.All(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }
}
