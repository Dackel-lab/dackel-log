namespace Dackel.Log;

/// <summary>
/// Opções do handler de exceções embutido no pacote.
/// </summary>
public sealed class DackelLogExceptionHandlerOptions
{
    /// <summary>
    /// Título padrão devolvido no <c>ProblemDetails</c>.
    /// </summary>
    public const string DefaultTitle = "Erro ao processar a requisição";

    /// <summary>
    /// Fábrica opcional que define o status HTTP a partir da exceção.
    /// </summary>
    /// <remarks>
    /// Use para mapear exceções de negócio/validação para 4xx. O valor é validado
    /// na faixa 400–599; valores fora dela caem no padrão <c>500</c>.
    /// A mensagem e o stack trace da exceção nunca são enviados ao cliente.
    /// </remarks>
    public Func<Exception, int>? StatusCodeSelector { get; set; }

    /// <summary>
    /// Título genérico do <c>ProblemDetails</c>. Valor padrão:
    /// <see cref="DefaultTitle"/>.
    /// </summary>
    public string Title { get; set; } = DefaultTitle;
}
