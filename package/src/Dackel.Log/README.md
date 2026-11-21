# Dackel.Log

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://licenses.nuget.org/MIT)

Logging estruturado para APIs .NET 8: Serilog em JSON compacto, correlação por `traceId` (W3C), sanitização de dados sensíveis, cadeia recursiva de exceções e middlewares prontos para ASP.NET Core.

Disponível no NuGet como **`Dackel.Log`**: https://www.nuget.org/packages/Dackel.Log

Código-fonte e issues: https://github.com/Dackel-lab/dackel-log

Site: https://dackel-log.pages.dev

## Recursos

- **Problem-free logging:** não anexa a exceção crua como `@x`; a mensagem/stack pode conter PII ou segredos.
- **Cadeia recursiva:** registra `InnerException` e `AggregateException` em `ExceptionChain` estruturado, com profundidade configurável (padrão 10) e proteção contra ciclos.
- **Correlação:** `X-Trace-Id` em todas as respostas (sucesso e erro), com o mesmo valor no log e no `ProblemDetails`.
- **Sanitização:** mascara CPF/CNPJ, e-mail, bearer token e pares `password=...` nas mensagens.
- **Redaction de configuração:** mascara propriedades sensíveis em objetos `*Settings`, `*Options`, `*Configuration` e `*Credentials`.
- **Console JSON:** `CompactJsonFormatter` no `stdout`, pronto para coleta por Grafana/Datadog.
- **Sem duplicidade:** suprime o log cru do `ExceptionHandlerMiddleware` por padrão.

## Requisitos

- .NET 8 (`net8.0`) ou .NET 10 (`net10.0`)
- ASP.NET Core 8 ou 10
- `Serilog.AspNetCore` (8.0.3 em net8; 11.0.0 em net10) e `Serilog.Formatting.Compact` 3.x como dependências transitivas

## Compatibilidade

| TFM | Status | Asset no pacote |
|---|---|---|
| `net10.0` | **Suportado** (LTS até nov/2028) | `lib/net10.0` |
| `net8.0` | Compatibilidade (LTS encerrado em 10/11/2026) | `lib/net8.0` |

> `net6.0` e `net7.0` **não são suportados** (EOL desde 2024).

## Instalação

```bash
dotnet add package Dackel.Log
```

Versão específica:

```bash
dotnet add package Dackel.Log --version 1.0.0
```

## Início rápido

```csharp
using Dackel.Log;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.AddStructuredLogging("Minha.Api");

var app = builder.Build();

app.UseTraceId();                  // mais externo: X-Trace-Id em toda resposta
app.UseRequestEnrichment();        // propriedades configuráveis no LogContext
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
| 2 | `UseRequestEnrichment()` | Adiciona as propriedades de `StructuredLogging:Enrichments` ao escopo |
| 3 | `UseStructuredRequestLogging()` | Registra status/duração sem anexar a exceção crua |
| 4 | `UseExceptionHandler()` | Converte a exceção em `ProblemDetails` |
| 5 | `UseExceptionLogging()` | Loga a cadeia sanitizada e repropaga ao handler |

## Configuração

```json
{
  "ExceptionLogging": {
    "MaxDepth": 10
  },
  "StructuredLogging": {
    "Enrichments": {
      "Matricula": "User:Name",
      "Tenant": "Header:X-Tenant"
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
- `StructuredLogging:Enrichments`: propriedades adicionadas ao log de toda requisição. Exemplo acima: `Matricula` a partir de `User.Identity.Name` e `Tenant` do header `X-Tenant`.
- **Não** declare `Serilog:WriteTo` no `appsettings`: o sink de console JSON é definido pelo pacote. Declarar novamente gera eventos duplicados.

### Enriquecimento configurável

| Origem | Exemplo | Valor |
|---|---|---|
| `Claim:<tipo>` | `"Matricula": "Claim:matricula"` | Primeira claim do tipo informado |
| `Header:<nome>` | `"Tenant": "Header:X-Tenant"` | Valor do header (sanitizado e truncado) |
| `Item:<chave>` | `"Unidade": "Item:unidade"` | Valor de `HttpContext.Items` |
| `User:Name` | `"Matricula": "User:Name"` | `HttpContext.User.Identity.Name` |

Regras de segurança:

- Propriedades com nomes sensíveis (`Password`, `Token`, `Cpf`, `Cnpj`, `Authorization` etc.) são **rejeitadas no startup**.
- Headers sensíveis (`Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `Proxy-Authorization`) são rejeitados.
- Valores são sanitizados pelo `LogSanitizer` e truncados em 1024 caracteres.
- A configuração é validada com `ValidateOnStart()`; erro de configuração derruba a aplicação na inicialização.

## Uso no handler de exceções

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

Se `UseExceptionLogging()` também estiver no pipeline, evite duplicidade:

```csharp
if (!ExceptionLoggingMiddleware.WasLogged(httpContext))
{
    exceptionLogger.LogError(exception, "...");
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
  "Tenant": "s303",
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

> `Tenant` é um exemplo de propriedade adicionada por `StructuredLogging:Enrichments`; sem configuração, apenas `TraceId`, `RequestId`, `RequestPath` e os campos da exceção são emitidos.

## Consultando erros

- O cliente recebe `X-Trace-Id` em **toda** resposta; em erros, o mesmo valor está em `ProblemDetails.extensions.traceId`.
- A plataforma de observabilidade (Grafana/Datadog) indexa `TraceId`, permitindo localizar todos os eventos da requisição.
- Propriedades como matrícula, tenant ou unidade podem ser adicionadas via `StructuredLogging:Enrichments`; nomes sensíveis são rejeitados na validação.

## Estrutura do pacote

| Arquivo | Responsabilidade |
|---|---|
| `LoggingExtensions.cs` | `AddStructuredLogging()`, `UseTraceId()`, `UseRequestEnrichment()`, `UseStructuredRequestLogging()`, `UseExceptionLogging()` |
| `TraceIdMiddleware.cs` | `X-Trace-Id`, `LogContext` e `HttpContext.Items` |
| `RequestEnrichmentMiddleware.cs` | Propriedades configuráveis no escopo de log |
| `StructuredLoggingOptions.cs` | Seção `StructuredLogging:Enrichments` |
| `LogEnrichmentResolver.cs` | Resolve `Claim`, `Header`, `Item` e `User:Name` |
| `ExceptionLoggingMiddleware.cs` | Log de exceção não tratada + `WasLogged()` |
| `StructuredExceptionLogger.cs` | Geração padronizada do evento de exceção |
| `ExceptionChainCollector.cs` | Coleta recursiva com `MaxDepth` e proteção contra ciclo |
| `ExceptionChainEnricher.cs` | Adiciona a cadeia como objetos estruturados |
| `LogSanitizer.cs` | Sanitização de PII/segredos nas mensagens |
| `SensitiveDataDestructuringPolicy.cs` | Redaction de propriedades sensíveis |

## Publicação (mantenedores)

O projeto tem solution própria (`Dackel.Log.sln`), independente da solution consumidora.

```bash
dotnet pack Dackel.Log.sln -c Release -o ./artifacts

dotnet nuget push ./artifacts/Dackel.Log.1.0.0.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push ./artifacts/Dackel.Log.1.0.0.snupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

- O pacote inclui `README.md`, `LICENSE`, XML docs e símbolos (`.snupkg`).
- A API key deve ser criada em https://www.nuget.org/account/apikeys com escopo **Push new packages and package versions**.
- Use `--skip-duplicate` se o pipeline puder repetir a publicação.

## Licença

Distribuído sob a licença **MIT**. Consulte o arquivo `LICENSE` incluído no pacote ou https://licenses.nuget.org/MIT.

Copyright (c) 2026 Dackel.Log contributors.
