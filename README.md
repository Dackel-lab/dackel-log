# Dackel.Log

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://licenses.nuget.org/MIT)

Logging estruturado para APIs .NET 8 e .NET 10: Serilog em JSON compacto, correlação por `traceId` (W3C), sanitização de dados sensíveis, cadeia recursiva de exceções e middlewares prontos para ASP.NET Core.

Disponível no NuGet como **`Dackel.Log`**: https://www.nuget.org/packages/Dackel.Log

Código-fonte e issues: https://github.com/Dackel-lab/dackel-log

Site: https://dackel-log.pages.dev

## Recursos

- **Problem-free logging:** `ILogger.LogError(ex, ...)` e `Log.Error(ex, ...)` geram a cadeia estruturada automaticamente e o `@x` cru é omitido no console JSON; a mensagem/stack pode conter PII ou segredos.
- **Cadeia recursiva:** registra `InnerException` e `AggregateException` em `ExceptionChain` estruturado, com profundidade configurável (padrão 10) e proteção contra ciclos.
- **Correlação:** `X-Trace-Id` em todas as respostas (sucesso e erro), com o mesmo valor no log e no `ProblemDetails`.
- **Tenant programático:** enriquecimento calculado por código a cada requisição (`EnrichStructuredLogging`), com precedência sobre o `appsettings`.
- **Sanitização:** mascara CPF/CNPJ (com ou sem pontuação/espaços), e-mail, bearer/basic token e pares `chave=valor` de credenciais nas mensagens de exceção e nos argumentos de log.
- **Redaction de configuração:** mascara propriedades sensíveis em objetos `*Settings`, `*Options`, `*Configuration`, `*Credentials` e em dicionários, em qualquer namespace (aplicada a objetos logados com destructuring, ex.: `{@Options}`).
- **Console JSON:** JSON compacto no `stdout` (formatter do pacote), sem o `@x` cru, pronto para coleta por Grafana/Datadog.
- **Sem duplicidade:** suprime o log cru do `ExceptionHandlerMiddleware` por padrão.

## Requisitos

- .NET 8 (`net8.0`) ou .NET 10 (`net10.0`) — atenção: `net8.0` está fora do suporte desde 10/11/2026 e não recebe correções de segurança do framework; prefira `net10.0`.
- ASP.NET Core 8 ou 10
- `Serilog.AspNetCore` (8.0.3 em net8; 11.0.0 em net10) e `Serilog.Formatting.Compact` 3.x como dependências transitivas

## Compatibilidade

| TFM | Status | Asset no pacote |
|---|---|---|
| `net10.0` | **Suportado** (LTS até nov/2028) | `lib/net10.0` |
| `net8.0` | Compatibilidade (sem patches desde 10/11/2026) | `lib/net8.0` |

> `net6.0` e `net7.0` **não são suportados** (EOL desde 2024).

## Instalação

```bash
dotnet add package Dackel.Log
```

Versão específica:

```bash
dotnet add package Dackel.Log --version 1.0.4
```

## Início rápido

```csharp
using Dackel.Log;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.AddStructuredLogging("Minha.Api")
    // Tenant resolvido por código (serviço da aplicação), a cada requisição.
    .EnrichStructuredLogging("Tenant", (context, services) =>
        services.GetRequiredService<ITenantProvider>().GetTenant(context));

var app = builder.Build();

app.UseTraceId();                  // mais externo: X-Trace-Id em toda resposta
app.UseRequestEnrichment();        // opcional: enriquecimentos no LogContext
app.UseStructuredRequestLogging(); // log HTTP (envolve o handler)
app.UseExceptionHandler();         // converte a exceção em ProblemDetails
app.UseExceptionLogging();         // loga a cadeia sanitizada e repropaga

app.MapControllers();
app.Run();
```

### Ordem dos middlewares

| Ordem | Middleware | Motivo |
|---|---|---|
| 1 | `UseTraceId()` | Precisa ser o mais externo para garantir `X-Trace-Id` mesmo em erro |
| 2 | `UseRequestEnrichment()` | **Opcional.** Adiciona as propriedades declaradas em `StructuredLogging:Enrichments` e as registradas via `EnrichStructuredLogging()` (ex.: tenant) ao escopo |
| 3 | `UseStructuredRequestLogging()` | Registra status/duração sem anexar a exceção crua |
| 4 | `UseExceptionHandler()` | Converte a exceção em `ProblemDetails` |
| 5 | `UseExceptionLogging()` | Loga a cadeia sanitizada e repropaga ao handler |

### Configuração mínima (sem enriquecimentos)

Enriquecimentos são opcionais. Sem `StructuredLogging:Enrichments` e sem
`EnrichStructuredLogging()`, basta registrar o componente e os middlewares
essenciais — `UseRequestEnrichment()` não é necessário:

```csharp
builder.AddStructuredLogging("Minha.Api");

var app = builder.Build();

app.UseTraceId();
app.UseStructuredRequestLogging();
app.UseExceptionHandler();
app.UseExceptionLogging();

app.MapControllers();
app.Run();
```

Todas as seções do `appsettings` também são opcionais; os padrões
(`MaxDepth: 10`, sem enriquecimentos) já são seguros para produção.

## Configuração

```json
{
  "ExceptionLogging": {
    "MaxDepth": 10
  },
  "StructuredLogging": {
    "Enrichments": {
      "Usuario": "User:Name"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System.Net.Http.HttpClient": "Warning"
      }
    },
    "Enrich": [ "FromLogContext" ],
    "Properties": { "Application": "Minha.Api" }
  }
}
```

- `ExceptionLogging:MaxDepth`: profundidade da cadeia (1–50, padrão **10**). Ao exceder, `ExceptionTruncated` é `true`.
- `StructuredLogging:Enrichments`: propriedades declarativas adicionadas ao log de toda requisição. Exemplo acima: `Usuario` a partir de `User.Identity.Name`. O **tenant** normalmente é obtido por código — veja [Enriquecimento programático](#enriquecimento-programático-tenant).
- **Não** declare `Serilog:WriteTo` no `appsettings`: o sink de console JSON é definido pelo pacote. Declarar novamente gera eventos duplicados.

### Enriquecimento programático (tenant)

Quando o valor não vem diretamente da requisição (o caso típico de tenant: resolvido por um serviço, cache ou repositório), registre-o em código. O valor é calculado uma vez por requisição e entra no mesmo escopo de log do `TraceId`:

```csharp
builder.AddStructuredLogging("Minha.Api")
    .EnrichStructuredLogging("Tenant", (context, services) =>
        services.GetRequiredService<ITenantProvider>().GetTenant(context));
```

Também há a sobrecarga que recebe apenas o `HttpContext`:

```csharp
builder.AddStructuredLogging("Minha.Api")
    .EnrichStructuredLogging("Tenant", context => context.Items["tenant"] as string);
```

Regras:

- A fábrica roda por requisição, **depois** de `UseTraceId()` e **antes** do log HTTP; a propriedade aparece no log de requisição e no de exceções.
- Enriquecimentos programáticos **têm precedência** sobre os declarados em `StructuredLogging:Enrichments` com o mesmo nome.
- O valor passa pelo `LogSanitizer` e é truncado em 1024 caracteres.
- Nomes inválidos ou sensíveis lançam `ArgumentException` no startup (e também falham na validação de opções).
- Se a fábrica lançar exceção, a propriedade é omitida e a requisição continua — logging não derruba a API.

### Enriquecimento declarativo

| Origem | Exemplo | Valor |
|---|---|---|
| `Claim:<tipo>` | `"Usuario": "Claim:usuario"` | Primeira claim do tipo informado |
| `Header:<nome>` | `"Canal": "Header:X-Canal"` | Valor do header (sanitizado e truncado) |
| `Item:<chave>` | `"Unidade": "Item:unidade"` | Valor de `HttpContext.Items` |
| `User:Name` | `"Usuario": "User:Name"` | `HttpContext.User.Identity.Name` |

Regras de segurança:

- Propriedades com nomes sensíveis (`Password`, `Token`, `Cpf`, `PrivateKey`, `ApiSecret`, `Authorization` etc.) são **rejeitadas no startup**.
- Headers sensíveis (`Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `X-Auth-Token`, `X-Amz-Security-Token`, `Proxy-Authorization`) são rejeitados.
- Valores declarativos e programáticos são sanitizados pelo `LogSanitizer` e truncados em 1024 caracteres.
- A configuração é validada com `ValidateOnStart()`; erro de configuração derruba a aplicação na inicialização.

## Uso no handler de exceções

Use o `ILogger` normalmente. A exceção anexada ao evento é convertida em
`ExceptionChain` sanitizada e o `@x` cru não é emitido no console JSON:

```csharp
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Falha não tratada ao processar {HttpMethod} {RequestPath}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value);

        // ... gravar ProblemDetails e retornar true
        return true;
    }
}
```

O mesmo vale para `Serilog.Log.Error(exception, ...)`. O `TraceId` já entra por
meio do `LogContext` do `UseTraceId()`.

Se `UseExceptionLogging()` também estiver no pipeline, evite duplicidade:

```csharp
if (!ExceptionLoggingMiddleware.WasLogged(httpContext))
{
    logger.LogError(exception, "...");
}
```

### StructuredExceptionLogger (opcional)

Para sanitizar também os argumentos do template e informar o `traceId`
explicitamente:

```csharp
public sealed class GlobalExceptionHandler(
    StructuredExceptionLogger exceptionLogger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        exceptionLogger.LogError(
            exception,
            "Falha não tratada ao processar {HttpMethod} {RequestPath}",
            [httpContext.Request.Method, httpContext.Request.Path.Value],
            traceId: TraceIdMiddleware.GetTraceId(httpContext));

        // ... gravar ProblemDetails e retornar true
        return true;
    }
}
```

## Formato do log

```json
{
  "@l": "Error",
  "@mt": "Falha não tratada ao processar {HttpMethod} {RequestPath}",
  "HttpMethod": "GET",
  "RequestPath": "/api/v1/contratos/123",
  "TraceId": "9f1c2b3d4e5f60718293a4b5c6d7e8f9",
  "Tenant": "acme company",
  "ExceptionChain": [
    {
      "Depth": 0,
      "Type": "System.InvalidOperationException",
      "Message": "Falha com CPF ***",
      "HResult": -2146233079,
      "Source": "Minha.Infrastructure",
      "TargetSite": "ObterDados",
      "StackTrace": "   at ..."
    }
  ],
  "ExceptionTruncated": false,
  "ExceptionDepthLimit": 10
}
```

> `Tenant` é um exemplo de propriedade adicionada por enriquecimento programático (`EnrichStructuredLogging`); sem configuração nem registro no código, apenas `TraceId`, `RequestId`, `RequestPath` e os campos da exceção são emitidos.

## Consultando erros

- O cliente recebe `X-Trace-Id` em **toda** resposta; em erros, o mesmo valor está em `ProblemDetails.extensions.traceId`.
- A plataforma de observabilidade (Grafana/Datadog) indexa `TraceId`, permitindo localizar todos os eventos da requisição.
- Propriedades como usuário ou unidade podem ser adicionadas via `StructuredLogging:Enrichments`; o tenant, por depender de serviços da aplicação, deve ser registrado com `EnrichStructuredLogging("Tenant", ...)`. Nomes sensíveis são rejeitados na validação.

## Estrutura do pacote

| Arquivo | Responsabilidade |
|---|---|
| `LoggingExtensions.cs` | `AddStructuredLogging()`, `UseTraceId()`, `UseRequestEnrichment()`, `UseStructuredRequestLogging()`, `UseExceptionLogging()` |
| `TraceIdMiddleware.cs` | `X-Trace-Id`, `LogContext` e `HttpContext.Items` |
| `RequestEnrichmentMiddleware.cs` | Propriedades declarativas e programáticas no escopo de log |
| `StructuredEnrichmentFactory.cs` | Fábrica do valor de um enriquecimento programático |
| `EnrichmentPropertyName.cs` | Validação compartilhada do nome da propriedade |
| `StructuredLoggingOptions.cs` | Seção `StructuredLogging:Enrichments` e `AddEnrichment()` |
| `StructuredLoggingOptionsValidator.cs` | Validação dos enriquecimentos no startup |
| `LogEnrichmentResolver.cs` | Resolve `Claim`, `Header`, `Item` e `User:Name` |
| `LogEnrichmentExpression.cs` | Parser das expressões `fonte:chave` |
| `ExceptionLoggingMiddleware.cs` | Log de exceção não tratada + `WasLogged()` |
| `StructuredExceptionLogger.cs` | Geração padronizada do evento de exceção |
| `ExceptionLoggingOptions.cs` | `MaxDepth` e coleta recursiva (`ExceptionChainCollector`) |
| `ExceptionChainContext.cs` | Transporta a cadeia coletada até o enricher |
| `ExceptionChainEnricher.cs` | Adiciona a cadeia (de `StructuredExceptionLogger` ou da exceção do evento) como objetos estruturados |
| `SanitizedCompactJsonFormatter.cs` | Console JSON compacto sem o `@x` cru quando há exceção |
| `LogSanitizer.cs` | Sanitização de PII/segredos nas mensagens e argumentos de exceção |
| `SensitiveDataDestructuringPolicy.cs` | Redaction de propriedades e chaves de dicionário sensíveis (qualquer namespace) |
| `SensitiveLogNames.cs` | Lista de propriedades/headers sensíveis (comparação normalizada) |

## Publicação (mantenedores)

O projeto tem solution própria (`Dackel.Log.sln`), independente da solution consumidora.

```bash
dotnet pack Dackel.Log.sln -c Release -o ./artifacts

dotnet nuget push ./artifacts/Dackel.Log.1.0.4.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push ./artifacts/Dackel.Log.1.0.4.snupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

- O pacote inclui `README.md`, `LICENSE`, XML docs e símbolos (`.snupkg`).
- A API key deve ser criada em https://www.nuget.org/account/apikeys com escopo **Push new packages and package versions**.
- Use `--skip-duplicate` se o pipeline puder repetir a publicação.

## Notas de segurança

- **Tenant e campos de auditoria:** obtenha por código com `EnrichStructuredLogging()`, a partir de claims autenticadas ou serviço confiável. Não use `Header:` para dados usados em auditoria — headers são controlados pelo cliente.
- **Trace id:** por padrão o valor vem do `traceparent`/`Activity`; um cliente pode enviar o próprio id. Para auditoria, correlacione também com o `RequestId` gerado pelo servidor.
- **Sanitização é melhor esforço:** cobre os formatos mais comuns, mas não envie PII/segredos ao log esperando que o filtro os remova.
- **Redaction:** atua em objetos logados com destructuring (`{@Objeto}`), nos tipos `*Settings`/`*Options`/`*Configuration`/`*Credentials` e em dicionários; `ToString()` padrão não é mascarado.
- **`@x` cru:** a omissão vale para o sink de console JSON configurado pelo pacote. Sinks adicionais (arquivo, OTLP etc.) recebem a exceção original; para evitar o `@x` em qualquer sink, use `StructuredExceptionLogger`.
- **Configuração do Serilog:** não permita que usuários finais controlem `Serilog__*`/seção `Serilog` do appsettings — ela define sinks e enriquecimentos.

## Licença

Distribuído sob a licença **MIT**. Consulte o arquivo `LICENSE` incluído no pacote ou https://licenses.nuget.org/MIT.

Copyright (c) 2026 Dackel Lab.
