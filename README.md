# Finort — Finanças Norteadas

[![GitHub Release](https://img.shields.io/github/v/release/stmarcelo/finort?include_prereleases&label=release&logo=github)](https://github.com/stmarcelo/finort/releases)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Blazor Server](https://img.shields.io/badge/Blazor-Server-593d9c)](https://learn.microsoft.com/aspnet/core/blazor)
[![MudBlazor 8](https://img.shields.io/badge/MudBlazor-8-594AE2)](https://mudblazor.com)
[![EF Core 9](https://img.shields.io/badge/EF%20Core-9-688047)](https://learn.microsoft.com/ef/core)
[![Docker](https://img.shields.io/badge/Docker-x86%20%7C%20arm64-2496ED?logo=docker)](#docker)
[![License](https://img.shields.io/badge/license-see%20LICENSE-red)](#licença)

Aplicação web de controle financeiro pessoal, com foco em privacidade: todos os dados ficam sob seu controle (banco local), com backup criptografado e sem qualquer serviço externo além da verificação opcional de versão.

Construída em **.NET 9 / Blazor Server**, interface responsiva com **MudBlazor**, banco de dados **SQLite ou MySQL** (troca dinâmica em runtime), relatórios em PDF, cartões de crédito com faturas, provisões projetadas, investimentos e lembretes pessoais integrados ao calendário.

## Sumário

- [Stack](#stack)
- [Funcionalidades](#funcionalidades)
- [Como executar](#como-executar)
- [Docker](#docker)
- [Windows (instalador)](#windows-instalador)
- [Download & Install](#download--install)
- [Releases](#releases)
- [Atualizando](#atualizando)
- [Configuração](#configuração)
- [Seed de dados de teste](#seed-de-dados-de-teste)
- [Hospedagem com MySQL](#hospedagem-com-mysql)
- [Testes](#testes)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Segurança](#segurança)
- [Decisões de arquitetura](#decisões-de-arquitetura)
- [Troubleshooting](#troubleshooting)
- [Contribuindo](#contribuindo)
- [Licença](#licença)

## Stack

| Camada | Tecnologia |
|--------|------------|
| Runtime | .NET 9, ASP.NET Core |
| UI | Blazor Server (Interactive Server) + MudBlazor 8.x, tema customizado |
| ORM | Entity Framework Core 9 (Microsoft.EntityFrameworkCore.Sqlite + Pomelo MySQL) |
| PDF | QuestPDF (Community License) + SkiaSharp |
| Email | MailKit |
| Criptografia | AES-256-GCM + PBKDF2-SHA256 (BCL, sem libs externas) |
| Testes | xUnit |
| Cultura | pt-BR (valores em R$) |

## Funcionalidades

### Autenticação e conta
- Primeiro acesso guiado: criação de usuário (nome, email, senha ≥ 8 caracteres) em `/configurar`.
- Login por senha com hash PBKDF2 (ASP.NET Identity `PasswordHasher`) — a senha nunca é armazenada em texto plano.
- Bloqueio contra força bruta: 5 tentativas incorretas = 60 s de bloqueio.
- **Cloudflare Turnstile**: após 2 tentativas com senha incorreta, o login exige um desafio non-interactive do Cloudflare (siteverify server-side); se as chaves não estiverem configuradas, o desafio é desligado e o login funciona apenas com senha (padrão em dev local).
- "Esqueci minha senha": redefinição via link por email (SMTP configurável em `/configurar-smtp`), com tokens de uso único armazenados como hash SHA-256 e validade de 30 minutos.
- Troca de senha com verificação da senha atual.

### Calendário de compromissos (`/`)
- Grade mensal com lançamentos reais, provisões projetadas, faturas do cartão e lembretes pessoais.
- Faturas e lembretes sempre no topo dos itens do dia: fatura com fundo vermelho claro, lembrete com fundo azul claro, ambos com ícone de sino — inclusive no dialog de compromissos do dia.
- Chips de resumo: a pagar, a receber e saldo previsto.
- Navegação por mês e botão "Hoje".

### Lançamentos financeiros
- Tipos: **Receita**, **Despesa**, **Transferência** e **Despesa no Cartão**.
- Parcelamento e recorrência (mensal, trimestral, semestral, anual).
- Vínculo com pessoa, categoria/subcategoria, conta, projeto e flag de reembolso.
- Confirmação item a item (conciliação com extrato) com guarda: lançamentos confirmados de meses fechados não podem ser editados/excluídos.
- Filtros por conta, cartão, pessoa, tipo e mês; subtotais diários.

### Cartões de crédito
- CRUD com banco, últimos 4 dígitos, melhor dia de compra, dia de vencimento, limite e conta vinculada.
- Fatura: conferência de itens, fechamento (exige todos confirmados), pagamento (cria débito na conta + cobre diferença em parcela futura), histórico e estorno.
- A fatura do mês aparece automaticamente no calendário na data de vencimento.

### Contas bancárias
- CRUD com nome, banco, agência e conta/dígito.
- Saldo real (confirmados) e saldo projetado (todos) por conta.

### Pessoas e lembretes
- CRUD de pessoas com cor de exibição e observação; a lista mostra o total de lembretes por pessoa (sino com badge).
- Lembretes por pessoa: **Mensal** (todo dia X) ou **Único** (data específica), com criação, edição e exclusão na página da pessoa.
- Lembretes aparecem no calendário, sempre no topo dos compromissos do dia.

### Categorias e subcategorias
- Seeds com 13 categorias e 57 subcategorias protegidas.
- Bloqueio de exclusão quando há lançamentos vinculados.

### Provisões
- Recorrências previstas (débito em conta, débito em cartão ou receita) com frequência e dia configuráveis.
- Projeção em memória no calendário, fluxo e dashboard — sem gravar no banco.
- Sincronização gera os lançamentos reais dos meses abertos até o mês atual.

### Fluxo mensal (`/fluxo`)
- Carrossel mês anterior / corrente / próximo com receitas, despesas, totais por cartão e saldo acumulado.
- Pagamento de fatura refletido no mês em que ocorreu.
- **Dias de antecipação**: ajuste (0–15) que define quantos dias do início do mês seguinte são incluídos no fluxo do mês atual. Exemplo: com 5 dias em setembro, despesas e faturas com vencimento até 05/10 aparecem no fluxo de setembro, e outubro começa em 06/10. Cada lançamento pertence a apenas um mês. Configuração persistida no banco de dados.

### Dashboard (`/dashboard`)
- Pizza de despesas e receitas por categoria, top-10 maiores lançamentos e patrimônio de investimentos.
- Meses futuros exibidos como projeção.

### Investimentos
- Tipos: Reserva de emergência, Ações, Criptomoedas, FIIs, CDB, Dólar etc.
- Movimentações (compra, venda, aporte, resgate) e proventos (dividendo, rendimento), cada um gerando lançamento vinculado.
- Atualização inline de cotação, desfazer em cascata e registro de auditoria na exclusão.

### Projetos
- Agrupamento de lançamentos por atividade (obra, evento, consultoria...) com pessoa, valor e data de contratação.
- Relatório com totais, tabela de lançamentos, pizza de despesas e exportação em PDF (`GET /api/relatorios/projeto/{id}/pdf`).

### Fechamento de mês (`/fechar-mes`)
- Conciliação do saldo do sistema com o saldo real do banco por conta, com criação de ajuste.
- Fechamento em cascata dos meses anteriores; bloqueio se houver lançamentos não confirmados.

### Acesso rápido
- Campo de busca na AppBar (atalho para todas as páginas), com teclado, sugestões e limpeza automática após a seleção.

### Banco de dados dinâmico (SQLite ↔ MySQL)
- Troca de provider em runtime na página `/configuracoes`, sem reiniciar o app.
- Teste de conexão antes da troca; o destino é reconstruído do zero pelas migrations (todas as tabelas do destino são removidas antes) e todos os dados são copiados em transação única (falha = rollback) — a origem nunca é alterada.
- Persistência da escolha em `database.settings.json`.

### Backup e restauração
- Formato `.cfbak`: container binário com magic header, salt aleatório, nonce, PBKDF2-SHA256 (210.000 iterações) e **AES-256-GCM** — cifra autenticada.
- Geração via `VACUUM INTO` + criptografia + download; restauração com validação em etapas (magic, decrypt, `integrity_check`, tabelas essenciais), dupla confirmação e safety copy do banco atual.
- Logout forçado após restaurar.

### Layout e navegação
- AppBar com logotipo, acesso rápido e menu de usuário (Perfil, Configurações, Ajuda, Sobre, Sair).
- Drawer com navegação completa e FAB de nova transação.
- Manual de uso integrado em `/manual` com busca por seções.
- Verificação de atualização no dialog "Sobre" (informativa, cache de 24 h).

## Como executar

### Quick start

```bash
dotnet restore
dotnet run --project src/aspnet
```

O app inicia em `http://localhost:5298` (ou na porta indicada no output). Na primeira execução, ele redireciona para `/configurar`, onde você cria seu usuário.

### Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- (Opcional) MySQL Server, se preferir MySQL no lugar do SQLite padrão

### Instalação e execução

```bash
git clone https://github.com/stmarcelo/finort.git
cd Finort/src/aspnet
dotnet restore
dotnet run
```

O banco padrão é SQLite (arquivo `finort.db` na pasta do app). Para MySQL, troque o provider em `/configuracoes` — a migração é automática (veja [Hospedagem com MySQL](#hospedagem-com-mysql)).

### Docker

A forma mais simples de rodar é com **Docker Compose**. Baixe o `docker-compose.yml` e execute:

```bash
curl -O https://raw.githubusercontent.com/stmarcelo/finort/main/docker-compose.yml
docker compose up -d
```

O app estará disponível em `http://localhost:5298`. Na primeira execução, o sistema redireciona para `/configurar` onde você cria o usuário administrador.

O banco SQLite é persistido em um volume nomeado (`finort-data`) e sobrevive a atualizações do container.

**Build local (somente para desenvolvimento):**

```bash
docker build -t finort .
docker run -d -p 5298:5298 --name finort finort
```

**Build multi-plataforma (x86 + arm64):**

```bash
docker buildx create --use
docker buildx build --platform linux/amd64,linux/arm64 -t ghcr.io/stmarcelo/finort:latest --push .
```

### Windows (instalador)

**Download do instalador:**

Acesse a página de [Releases](https://github.com/stmarcelo/finort/releases) e baixe o arquivo `finort-x.x.x-win-x64-setup.exe`.

**Build local do instalador:**

```bash
# Publicar para win-x64
dotnet publish src/aspnet/Finort.csproj -c Release -r win-x64 --self-contained true

# Compilar instalador (requer Inno Setup)
iscc installer\finort.iss
```

Ou execute o script `build-windows.bat` na raiz do projeto.

**Configurações do instalador:**
- Abre o navegador automaticamente ao iniciar (configuração `OpenBrowserOnStart`)
- Atalho na área de trabalho (opcional)
- Desinstalador completo

### Download & Install

| Método | Onde |
| :-- | :-- |
| Instalador Windows | [GitHub Releases](https://github.com/stmarcelo/finort/releases) — `finort-x.x.x-win-x64-setup.exe` |
| Container | [`ghcr.io/stmarcelo/finort`](https://github.com/stmarcelo/finort/pkgs/container/finort), multi-arch para `linux/amd64` e `linux/arm64` |
| Build local | `dotnet publish` ou `.\build-windows.bat` (veja abaixo) |

### Releases

As versões são disponibilizadas via [GitHub Releases](https://github.com/stmarcelo/finort/releases).

**Criando uma release:**

1. Atualize a versão em `Version.props` (único lugar necessário)
2. Crie e envie a tag:

```bash
git add Version.props
git commit -m "Bump version to X.Y.Z"
git tag -a v0.1.0 -m "Release 0.1.0"
git push origin v0.1.0
```

3. O GitHub Actions automaticamente:
   - Valida se a tag corresponde à versão em `Version.props`
   - Compila o aplicativo para win-x64
   - Gera o instalador com Inno Setup
   - Cria a release com o instalador anexado

**Versionamento:**

A versão é controlada centralmente em `Version.props` e propagada para:
- `Finort.csproj` (compilação .NET)
- `installer/finort.iss` (instalador Windows)
- `build-windows.bat` (script de build local)

## Atualizando

A atualização **nunca substitui o banco de dados**: as migrations (SQLite ou
MySQL) são aplicadas automaticamente no primeiro start da nova versão.

### Windows — automático (botão no Sobre)

1. Abra o menu do usuário → **Sobre**.
2. Se houver nova versão, o botão **Atualizar agora** aparece (apenas no
   Windows). Em builds sem o `updater.exe` ao lado do executável — ex.: rodando
   via `dotnet run` no desenvolvimento — o botão simplesmente abre a página da
   release no navegador para baixar o instalador.
3. Com o updater disponível, ao confirmar o Finort:
   - fecha o sistema;
   - copia o banco para `{app}\backups\` (SQLite; com MySQL no servidor o
     backup local é dispensado);
   - baixa e executa o instalador oficial da Release em modo silencioso;
   - reabre o Finort (as migrations rodam no startup).

Instalações feitas com versões anteriores a este recurso (ex.: 0.1.0) não têm
o botão **Atualizar agora** — para a primeira atualização, use a seção manual
abaixo.

Se algo falhar, o Finort é reaberto, nada é instalado e um log fica em
`%TEMP%\finort-updater\updater.log`.

### Windows — manual

Baixe o `finort-x.x.x-win-x64-setup.exe` na página de
[Releases](https://github.com/stmarcelo/finort/releases) e execute por cima da
instalação existente. Os dados (`finort.db`, `database.settings.json`) são
preservados.

### Docker

```bash
docker compose pull
docker compose up -d
```

Use **o mesmo volume** da instalação original: o banco montado em
`/app/data` é mantido e as migrations são aplicadas automaticamente no primeiro
start do novo container. Com MySQL, use as variáveis de ambiente
`Database__Provider=MySql` e `Database__MySql__ConnectionString` (o arquivo
`database.settings.json` não persiste dentro do container).

## Configuração

Todas as chaves ficam em `src/aspnet/appsettings.json`:

| Chave | Padrão | Descrição |
|-------|--------|-----------|
| `OpenBrowserOnStart` | `false` | Abre o navegador automaticamente ao iniciar (true automaticamente no build win-x64) |
| `Database:Provider` | `Sqlite` | `Sqlite` ou `MySql` |
| `Database:Sqlite:ConnectionString` | `Data Source=finort.db` | Caminho do arquivo SQLite (relativo à pasta do app) |
| `Database:MySql:ConnectionString` | `Server=localhost;Port=3306;Database=finort;User=root;Password=;` | Conexão MySQL (`Server=...;Database=...;Uid=...;Pwd=...;`) |
| `Turnstile:SiteKey` | *(vazio)* | Site key pública do Cloudflare Turnstile (vazio = desafio desativado) |
| `Turnstile:SecretKey` | *(vazio)* | Secret key do Turnstile para validação server-side; nunca exposta ao cliente (vazio = desafio desativado) |

**Como configurar o Turnstile:**

1. Crie uma conta gratuita em [Cloudflare](https://dash.cloudflare.com/) e acesse **Turnstile** no menu lateral.
2. Crie um novo site com o tipo **Managed** (recomendado) — o widget é não-interativo para usuários legítimos.
3. Copie a **Site Key** e a **Secret Key** e insira no `appsettings.json` (ou use variáveis de ambiente com o padrão ASP.NET: `Turnstile__SiteKey` e `Turnstile__SecretKey`).
4. Chaves vazias ou ausentes = Turnstile desligado (padrão, ideal para dev local).

> Chaves de teste da Cloudflare (siteverify sempre aprova, sem redes): Site Key `1x00000000000000000000AA`, Secret Key `1x0000000000000000000000000000000AA`.

Arquivos de estado criados em runtime (na raiz do projeto):

| Arquivo | Função |
|---------|--------|
| `finort.db` | Banco SQLite padrão |
| `database.settings.json` | Provider ativo após troca em `/configuracoes` (sobrepõe appsettings) |

## Seed de dados de teste

Para criar um banco de demonstração com dados fictícios (login `teste@finort.com`, senha `123456`, senha de backup `backup123`):

```bash
dotnet run --project src/aspnet -- seed-para-teste
```

O comando só roda com provider SQLite e quando o arquivo do banco ainda **não** existe:

- Banco já existente → o comando avisa no console e encerra sem alterar nada.
- Provider configurado como MySQL → o comando avisa que o seed de teste é exclusivo de SQLite e encerra.

## Hospedagem com MySQL

O app funciona em hospedagem compartilhada com usuário MySQL sem privilégios administrativos: a conexão usa um database já existente e o app não precisa de `CREATE DATABASE`.

Ao usar **Salvar e trocar** em `/configuracoes` com destino MySQL, o processo:

1. Testa a conexão com `SELECT 1`.
2. **Remove todas as tabelas do database de destino** (inclusive `__EFMigrationsHistory`) — o destino é sempre reconstruído do zero pelas migrations.
3. Aplica as migrations EF Core.
4. Copia todos os dados do banco atual em uma única transação (falha = rollback).

O banco de **origem permanece intacto** — a troca é uma cópia, não uma mudança destrutiva; a confirmação na tela deixa isso explícito. Privilégios necessários no database de destino: `SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, REFERENCES, ALTER, INDEX`.

## Testes

```bash
dotnet test src/Finort.Tests
```

Suíte xUnit cobrindo autenticação, serviços financeiros, cartões/faturas, provisões, investimentos, dashboard, fluxo, calendário, criptografia de backup, restore, troca de banco, migrações e guardas de meses fechados.

## Estrutura do projeto

```
Finort/
├── src/
│   ├── aspnet/                          # App Blazor Server
│   │   ├── App/                         # Tema, variantes globais, classe base de componentes
│   │   ├── Components/
│   │   │   ├── Dialogs/                 # Dialogs (CRUD + operações)
│   │   │   ├── Lancamentos/             # Formulários de lançamento
│   │   │   ├── Layout/                  # MainLayout, navegação, acesso rápido
│   │   │   ├── Pages/                   # Páginas Razor (rotas da aplicação)
│   │   │   ├── ProjetoSelect/           # Seleção de projeto vinculada a pessoa
│   │   │   └── SelecaoBusca/            # Autocomplete genérico (contas, pessoas...)
│   │   ├── Data/                        # DbContext, factories e migrations (SQLite + MySQL)
│   │   ├── Models/                      # Entidades e records (Auth, Configuration, Financeiro)
│   │   ├── Services/                    # Camada de serviços (financeiro, auth, backup, PDF...)
│   │   └── wwwroot/                     # app.css, auth.js, moneyInterop.js, fontes, imagens
│   └── Finort.Tests/                    # Testes xUnit
├── DESIGN.md                            # Diretrizes de design/UX
└── README.md
```

## Segurança

O projeto é open source e segue o princípio de Kerckhoffs: a segurança não depende do sigilo do código.

- **Senha de login**: armazenada apenas como hash PBKDF2 (ASP.NET Identity), com salt aleatório — irreversível.
- **Senha de backup**: armazenada como hash irreversível; nunca pode ser exibida ou recuperada, apenas substituída. O arquivo `.cfbak` usa AES-256-GCM com chave derivada da senha (salt e nonce aleatórios por arquivo) — o servidor não consegue abrir um backup sem a senha.
- **Senha SMTP**: cifrada com ASP.NET Core Data Protection usando o keyring local da instalação (fora do diretório da aplicação e fora do código-fonte).
- **Tokens de redefinição de senha**: persistidos como hash SHA-256, expiração de 30 min e uso único.
- **Bloqueio por força bruta**: 5 tentativas = 60 s de bloqueio **por endereço de origem** — um atacante remoto não consegue bloquear o dono da conta travando o lockout global.
- **Cloudflare Turnstile (opcional)**: desafio non-interactive que protege contra bots de tentativa de senha. A `SecretKey` nunca é exposta ao cliente; o token é validado por `siteverify` a cada tentativa quando o desafio está ativo. Se as chaves não estiverem configuradas (vazias ou ausentes), o Turnstile é desligado — sem scripts, sem widget, comportamento idêntico ao anterior à integração.
- **Endpoints auxiliares**: `/api/seed` exige autenticação.
- **Single-user**: nenhum dado sai da máquina exceto SMTP (opcional) e a consulta de versão no GitHub (opcional e somente leitura).

> Nota de hospedagem: valores legados (senha de backup reversível ou SMTP em texto plano, de versões anteriores) são migrados automaticamente na primeira execução após o upgrade. Se a aplicação roda atrás de proxy reverso, o bloqueio por login passa a contar o IP do proxy — configure `ForwardedHeaders` a seu critério.

## Decisões de arquitetura

- **DbContext dinâmico**: criado via factory que lê `database.settings.json` em runtime; trocar de provider não exige reinício.
- **Provisões projetadas em memória** (`ProvisaoAgenda`): calendário/fluxo/dashboard mostram o futuro sem escrever no banco; a materialização só ocorre na sincronização.
- **Backup sem libs externas**: container binário próprio (`.cfbak`) com primitivas do BCL; validação de integridade em camadas no restore.
- **PDF server-side**: QuestPDF + SkiaSharp com download direto por endpoint HTTP.
- **Cultura fixa pt-BR**: formatação monetária e de datas consistente em toda a UI.
- **Troca de banco como cópia não-destrutiva**: o destino é zerado (drop de todas as tabelas + migrations) e populado numa transação única; a origem nunca é alterada — funciona em MySQL de hospedagem compartilhada, onde não há privilégio de criar/dropar database.

## Troubleshooting

| Sintoma | Causa/Solução |
|---------|---------------|
| Login responde "Muitas tentativas. Aguarde um minuto." | Bloqueio anti força bruta: 5 falhas = 60 s por IP de origem. Aguarde 1 minuto. |
| O login passou a exigir um desafio de segurança (Turnstile) | Após 2 tentativas com senha incorreta, o login passa a exigir o desafio Turnstile (se configurado). O bloqueio por força bruta (423) permanece em 1 minuto; o desafio reaparece enquanto houver falhas acumuladas por origem (IP). |
| `Table '...' already exists` ao trocar para MySQL | Versões antigas (< 2026-08-31) deixavam schema parcial no destino; basta repetir a troca — o fluxo atual limpa todas as tabelas do destino antes de migrar. |
| `dotnet run -- seed-para-teste` avisa que o banco já existe | O seed de teste exige banco novo: exclua `finort.db` (ou o arquivo apontado pela connection string) antes de rodar. |
| Backup `.cfbak` não restaura | Arquivo gerado com senha de backup anterior à troca de senha — senhas antigas não podem ser recuperadas. |

## Contribuindo

1. Fork e crie uma branch a partir de `main` (`feat/...`, `fix/...`).
2. Setup de desenvolvimento: `dotnet restore` na raiz e `dotnet run --project src/aspnet` para rodar o app; a cultura da aplicação é **pt-BR** (datas e valores em R$).
3. Mantenha os testes passando (`dotnet test src/Finort.Tests`) e adicione cobertura para novas regras de negócio.
4. Siga o padrão visual do tema (tokens em `wwwroot/app.css` e `App/`) e as convenções existentes de services/dialogs.
5. Abra um pull request descrevendo motivação e mudanças.

## Licença

Projeto open source de uso não comercial. Veja o arquivo [LICENSE](LICENSE) para os termos completos.
