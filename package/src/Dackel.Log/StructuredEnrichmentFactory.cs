using Microsoft.AspNetCore.Http;

namespace Dackel.Log;

/// <summary>
/// Fábrica responsável por resolver, em tempo de requisição, o valor de um
/// enriquecimento programático (ex.: o tenant do usuário autenticado).
/// </summary>
/// <param name="context">Contexto HTTP da requisição em andamento.</param>
/// <param name="services">Provedor de serviços do escopo da requisição.</param>
/// <returns>
/// O valor a ser enriquecido no log ou <c>null</c> quando não houver valor.
/// O valor é sanitizado e truncado em 1024 caracteres antes de ser emitido.
/// </returns>
/// <remarks>
/// A fábrica é executada para cada requisição. Exceções lançadas por ela são
/// ignoradas para não interromper a requisição; use o log de diagnóstico da
/// aplicação dentro da própria fábrica se precisar rastrear falhas.
/// </remarks>
public delegate string? StructuredEnrichmentFactory(HttpContext context, IServiceProvider services);
