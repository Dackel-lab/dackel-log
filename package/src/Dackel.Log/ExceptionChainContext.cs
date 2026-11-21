namespace Dackel.Log;

/// <summary>
/// Mantém a cadeia de exceções corrente para ser adicionada ao evento de log
/// pelo <see cref="ExceptionChainEnricher"/>.
/// </summary>
internal static class ExceptionChainContext
{
    private static readonly AsyncLocal<ExceptionChainResult?> CurrentChain = new();

    public static ExceptionChainResult? Current => CurrentChain.Value;

    public static IDisposable Push(ExceptionChainResult chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        var previous = CurrentChain.Value;
        CurrentChain.Value = chain;

        return new Scope(previous);
    }

    private sealed class Scope(ExceptionChainResult? previous) : IDisposable
    {
        public void Dispose() => CurrentChain.Value = previous;
    }
}
