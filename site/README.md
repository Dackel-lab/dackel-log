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
- A versão exibida é buscada da API do NuGet (`dackel-log`), com fallback para `1.0.2` enquanto o pacote não estiver publicado.
- `_headers` adiciona cabeçalhos de segurança (CSP, HSTS, `nosniff`, `X-Frame-Options`) e cache para `/assets/*`.
- Sem fontes externas, sem CDN e sem cookies.

## Content Security Policy

O `_headers` usa `script-src` com hashes SHA-256 dos scripts inline de
`public/index.html`. Ao editar o script de tema ou o JSON-LD, recalcule:

```bash
cd site
node -e '
const fs = require("fs"), crypto = require("crypto");
const html = fs.readFileSync("public/index.html", "utf8");
const re = /<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g;
let match;
while ((match = re.exec(html))) {
  console.log("sha256-" + crypto.createHash("sha256").update(match[1], "utf8").digest("base64"));
}'
```

Copie os valores para `script-src` em `public/_headers`. O `connect-src` já
permite a chamada à API do NuGet.
