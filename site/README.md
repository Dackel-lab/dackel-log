# Dackel.Log — site

Landing page estática do pacote, com TypeScript, CSS próprio e zero dependências de runtime.

## Estrutura

```
site/dackel-log/
├── public/              # arquivos servidos (HTML, CSS, favicon, headers)
├── src/main.ts          # interações (tema, tabs, copiar, reveal, versão NuGet)
├── scripts/             # cópia de public/ para dist/
├── dist/                # saída do build (gerada)
├── package.json
├── tsconfig.json
└── wrangler.toml
```

## Desenvolvimento

```bash
cd site/dackel-log
npm install
npm run build      # gera dist/ com HTML/CSS + assets/main.js
npm run preview    # serve dist/ localmente
npm run typecheck  # valida o TypeScript sem gerar arquivos
```

## Deploy no Cloudflare Pages

### Via integração com Git

Configurações no painel do Cloudflare Pages:

| Campo | Valor |
|---|---|
| Root directory | `site/dackel-log` |
| Build command | `npm run build` |
| Build output directory | `dist` |
| Node version | `22` (ou superior) |

### Via Wrangler

```bash
cd site/dackel-log
npm install
npm run build
npx wrangler pages deploy dist --project-name dackel-log
```

O `wrangler.toml` já aponta `pages_build_output_dir = "./dist"`.

## URL do site

A URL padrão esperada é `https://dackel-log.pages.dev`. Após configurar um domínio
customizado, atualize:

- `PackageProjectUrl` em `src/Dackel.Log/Dackel.Log.csproj`
- o link do site no `README.md` do pacote (se adicionado)

## Detalhes

- O tema é aplicado antes da primeira pintura por um script inline mínimo; a lógica principal fica em `main.ts`.
- A versão exibida é buscada da API do NuGet (`dackel-log`), com fallback para `1.0.0` enquanto o pacote não estiver publicado.
- `_headers` adiciona cabeçalhos de segurança e cache para `/assets/*`.
- Sem fontes externas, sem CDN e sem cookies.
