# Release Notes — Dackel.Log


## [1.0.2] - 2026-11-21

### Corrigido (segurança)

- Redaction de configuração (`SensitiveDataDestructuringPolicy`) agora atua em
  qualquer namespace. O que fazia
  `Password`, `ApiKey` e `ConnectionString` serem logados em texto puro nas
  aplicações consumidoras.
- A inspeção de objetos passou a falhar fechada (mascara o objeto) e chaves
  sensíveis em dicionários também são mascaradas.
- `StructuredExceptionLogger` sanitiza argumentos de log do tipo `string`,
  evitando CPF, e-mail e tokens em campos estruturados.
- `LogSanitizer` reconhece CPF/CNPJ com espaços, `Authorization: Basic`,
  `private_key`/`secret_key`/`passphrase` e o separador `token -valor`.
- Lista de nomes sensíveis ampliada e normalizada (ignora `.`, `_`, `-` e
  espaços, inclusive em sufixos): `PrivateKey`, `SecretKey`, `Passphrase`,
  `ApiSecret`, `CardNumber`, `user.password` etc.
- PDB do `.snupkg` não expõe mais o usuário/caminho da máquina de build
  (`ContinuousIntegrationBuild`).
- Removidos identificadores internos da API pública: o nome padrão de aplicação
  agora é `Dackel.Log` e a chave de `HttpContext.Items` passou a
  `Dackel.Log.ExceptionLogged`.

### Compatibilidade

- `net8.0` permanece no pacote, mas está fora de suporte desde 10/11/2026 e não
  recebe correções de segurança do framework; prefira `net10.0`.

## [1.0.1] - 2026-11-21

Primeira versão pública do `Dackel.Log`, componente de logging estruturado para
APIs ASP.NET Core em .NET 8 e .NET 10.

### Informações

- A exceção crua não é serializada como `@x`, evitando vazar PII ou segredos
  presentes em mensagens e stack traces.
- Headers sensíveis (`Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`,
  `Proxy-Authorization`) são rejeitados como fonte de enriquecimento.
- Valores enriquecidos são sanitizados e truncados em 1024 caracteres.

### Enriquecimento

- O tenant (e qualquer valor de serviço) pode ser obtido por código, a cada
  requisição, com `EnrichStructuredLogging("Tenant", (context, services) => ...)`.
- Enriquecimentos programáticos têm precedência sobre
  `StructuredLogging:Enrichments`; falhas na fábrica omitem a propriedade sem
  interromper a requisição.

### Compatibilidade

- `net10.0`: suportado (LTS).
- `net8.0`: compatibilidade (LTS encerrado em 10/11/2026).
- `net6.0` e `net7.0` **não** são suportados (EOL desde 2024).
- Dependências: `Serilog.AspNetCore` 11.0.0 (`net10.0`) / 8.0.3 (`net8.0`) e
  `Serilog.Formatting.Compact` 3.0.0.
