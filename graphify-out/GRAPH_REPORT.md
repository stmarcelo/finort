# Graph Report - ControleFinanceiro  (2026-09-11)

## Corpus Check
- 263 files · ~186,098 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3193 nodes · 5769 edges · 161 communities (130 shown, 31 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 211 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d3d43c1d`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Cleanup
- LancamentoService
- CartaoCredito
- InvestimentoService
- Finort.Data
- .SetupAsync
- Lancamentos.razor
- Provisao
- FaturaConferencia.razor
- IDisposable
- Categoria
- Investimentos.razor
- MainLayout.razor
- Configuracoes.razor
- ContaResumo
- Projeto
- Dashboard.razor
- MudMoneyInput
- Finort
- PessoaDialog.razor
- .SetupAsync
- Pessoa
- UpdatePlanTests
- Build and Release Job
- Pessoas.razor
- SobreDialog.razor
- Provisoes.razor
- Projetos.razor
- Categorias.razor
- Home.razor
- FecharMes.razor
- RelatorioProjeto.razor
- PrimeiroAcesso.razor
- Cartoes.razor
- .CriarConfiguracaoAsync
- .SetupAsync
- Contas.razor
- Fluxo.razor
- ProvisaoDialog.razor
- Finort.Data.Migrations.MySql
- .RecordFailure
- .Create
- Manual.razor
- AuthLoginService
- EmailService
- MovimentacoesDialog.razor
- .ObterMesAsync
- Finort.Migrations
- CartaoDialog.razor
- InvestimentoDialog.razor
- TransacaoDialog.razor
- DespesaCartaoForm.razor
- Finort.csproj
- Login.razor
- ProventosDialog.razor
- LancamentoForm.razor
- .SetupAsync
- AppUpdateServiceTests
- Finort.App
- AppDbContext
- TurnstileVerifierTests
- FaturaHistoricoDialog.razor
- _Imports.razor
- BackupRestoreService
- LancamentoNovoCartao.razor
- Perfil.razor
- PagamentoDialog.razor
- .CreateAsync
- .GerarAsync
- DatabaseConfig
- FaturaHistorico.razor
- ConfigurarSmtp.razor
- CompromissoDiaDialog.razor
- MovimentoDialog.razor
- ProjetoDialog.razor
- LancamentoEditar.razor
- http
- FaturaService
- ScopedAuthenticationStateProvider
- VersionCheckService
- AdicionarCamposVersao
- ConclusaoProjetoDialog.razor
- NovoLancamentoFaturaDialog.razor
- ProventoDialog.razor
- RelatorioDespesas.razor
- EsqueciSenha.razor
- LancamentoNovo.razor
- ContaDialog.razor
- DatabaseSwitchServiceTests
- Finort.Models.Configuration
- RelatorioReceitas.razor
- RedefinirSenha.razor
- AtualizarCotacaoDialog.razor
- CategoriaDialog.razor
- SubcategoriaDialog.razor
- SubcategoriaSelect.razor
- SeedCliGuardTests
- Finort.Models.Auth
- CartaoSelect.razor
- CategoriaSelect.razor
- ContaSelect.razor
- SenhaBackupDialog.razor
- DashboardContasDialog.razor
- PessoaSelect.razor
- SelecaoBusca.razor
- TurnstileVerifier
- AddTaxaInvestimentoMovimento
- Error.razor
- DatabaseMigrator
- SenhaLoginDialog.razor
- DashboardInvestimentosDialog.razor
- DashboardItensDialog.razor
- AppDbContextModelSnapshot.cs
- LoginLayout.razor
- ProjetoSelect.razor
- BackupRestoreServiceTests
- Routes.razor
- AppDbContextFactory
- FluxoCard.razor
- UnirSubcategoriasAlimentacaoMySql
- App.razor
- Initial
- AdicionarCamposVersao
- RemoverDataCompra
- AdicionarDiasAntecipacao
- Migration
- Handler
- AddContaIdToMesFechado
- UnirSubcategoriasAlimentacao
- DespesaRelatorioService.cs
- AppThemeTests.cs
- SeedDadosCompletos
- AddReembolsoCategoriaSubcategoriaMySql
- RemoverDataCompra
- AddTaxaInvestimentoMovimentoMySql
- Finort Docker Service
- MudMoneyInput.razor
- ReceitaRelatorioService.cs
- Fase5aMigrationTests
- 20260906015149_SeedDadosCompletos.Designer.cs
- TestWebHostEnvironment
- 20260901214003_AdicionarDiasAntecipacao.Designer.cs
- MesFechado
- SeedDataService
- .Migrate_CreatesConfiguracaoAndPasswordResetTokenTables
- MySqlAppDbContextTests.cs
- .Migrate_CriaTabelaProjetos_EColunaProjetoId
- RedirectToLogin.razor
- DataPicker.razor
- PasswordResetToken
- Setup Icon
- Login Page Screenshot
- Finort Logo (Gold, H100)
- Finort Logo
- 20260901214035_AdicionarDiasAntecipacaoMySql.Designer.cs
- 20260906015231_SeedDadosCompletos.Designer.cs
- 20260907014704_AddReembolsoCategoriaSubcategoriaMySql.Designer.cs
- .Migrate_CreatesFase3Tables
- .Migrate_CriaTabelasFase4a

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 103 edges
2. `Finort.Data` - 87 edges
3. `Finort.Services` - 84 edges
4. `Finort.Models.Financeiro` - 68 edges
5. `Finort.Tests` - 56 edges
6. `LancamentoService` - 40 edges
7. `InvestimentoServiceTests` - 37 edges
8. `Lancamento` - 36 edges
9. `Finort` - 27 edges
10. `CartaoCredito` - 24 edges

## Surprising Connections (you probably didn't know these)
- `Finort` --uses--> `SkiaSharp`  [EXTRACTED]
  src/aspnet/Components/_Imports.razor → README.md
- `Finort` --uses--> `AES-256-GCM`  [EXTRACTED]
  src/aspnet/Components/_Imports.razor → README.md
- `Finort` --uses--> `ASP.NET Core Data Protection`  [EXTRACTED]
  src/aspnet/Components/_Imports.razor → README.md
- `Finort` --implements--> `Backup and Restore`  [EXTRACTED]
  src/aspnet/Components/_Imports.razor → README.md
- `Finort` --manages--> `Bank Accounts Management`  [EXTRACTED]
  src/aspnet/Components/_Imports.razor → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Finort Technology Stack** — finort, dotnet_9, blazor_server, mudblazor_8, ef_core_9, sqlite, mysql, questpdf, skiasharp, mailkit, aes_256_gcm, pbkdf2_sha256, xunit [EXTRACTED 1.00]
- **Finort Security Features** — finort, authentication, pbkdf2_sha256, aes_256_gcm, sha_256, aspnet_core_data_protection, cloudflare_turnstile, backup_restore [EXTRACTED 1.00]
- **Finort Release Pipeline** — github_workflows_release_setup_workflow, release_workflow, build_setup_job, dotnet_publish, iscc, softprops_action_gh_release_v2, finort_csproj, updater_csproj, installer_finort_iss, finort_win_x64_setup_exe [EXTRACTED 1.00]
- **GitHub Release Process** — _github_workflows_release_get_version_step, _github_workflows_release_create_github_release_step, github_ref_name, secrets_github_token [INFERRED 0.85]

## Communities (161 total, 31 thin omitted)

### Community 0 - ".Cleanup"
Cohesion: 0.07
Nodes (30): Ativo, Reserva, Guid, Conta, Guid, List, Task, ContaService (+22 more)

### Community 1 - "LancamentoService"
Cohesion: 0.06
Nodes (42): Destino, IEnumerable, InvalidOperationException, IReadOnlyCollection, Origem, Perna, ReferenciaId, bool (+34 more)

### Community 2 - "CartaoCredito"
Cohesion: 0.05
Nodes (41): ComponentBase, DadosDespesaCartao, Variant, AppComponentBase, bool, DateOnly, DateTime, decimal (+33 more)

### Community 3 - "InvestimentoService"
Cohesion: 0.08
Nodes (30): DateTime, Guid, AuditoriaExclusaoInvestimento, DateTime, Guid, Investimento, TipoInvestimento, InvestimentoCard (+22 more)

### Community 4 - "Finort.Data"
Cohesion: 0.10
Nodes (4): Finort.Services, Finort.Models.Financeiro, Finort.Data, Finort.Tests

### Community 5 - ".SetupAsync"
Cohesion: 0.29
Nodes (8): Cartao, Db, Fact, File, Guid, Service, Task, FaturaServiceTests

### Community 6 - "Lancamentos.razor"
Cohesion: 0.04
Nodes (50): LinhaGrid, MudExpansionPanel, MudExpansionPanels, route:/lancamentos, AlternarConfirmado, CarregarAsync, CorValor, EditarTransacao (+42 more)

### Community 7 - "Provisao"
Cohesion: 0.09
Nodes (24): Guid, Provisao, ProvisaoFrequencia, ProvisaoOnde, RecorrenciaFrequencia, Data, DateOnly, List (+16 more)

### Community 8 - "FaturaConferencia.razor"
Cohesion: 0.04
Nodes (47): FaturaHistoricoDialog, NovoLancamentoFaturaDialog, PagamentoDialog, route:/cartoes/{Id:guid}/fatura, AbrirHistorico, AbrirPagamento, AdicionarLancamento, AlternarConfirmado (+39 more)

### Community 9 - "IDisposable"
Cohesion: 0.11
Nodes (18): IDisposable, Size, SKCanvas, SkiaSharp, Guid, IReadOnlyList, IWebHostEnvironment, Task (+10 more)

### Community 10 - "Categoria"
Cohesion: 0.09
Nodes (24): Guid, List, Categoria, Guid, Subcategoria, Guid, List, Task (+16 more)

### Community 11 - "Investimentos.razor"
Cohesion: 0.04
Nodes (46): AtualizarCotacaoDialog, InvestimentoDialog, MovimentacoesDialog, MovimentoDialog, ProventoDialog, ProventosDialog, route:/investimentos, AbrirDialogAtualizarCotacao (+38 more)

### Community 12 - "MainLayout.razor"
Cohesion: 0.04
Nodes (46): ChildContent, MudAppBar, MudAvatar, MudContainer, MudDrawer, MudNavGroup, MudNavLink, MudNavMenu (+38 more)

### Community 13 - "Configuracoes.razor"
Cohesion: 0.04
Nodes (46): Finort.Models.Configuration, MudRadio, MudRadioGroup, route:/configuracoes, SenhaLoginDialog, AoMudarProvider, ArquivoSelecionado, AutenticarBackup (+38 more)

### Community 15 - "Projeto"
Cohesion: 0.11
Nodes (19): Guid, IReadOnlyList, ProjetoSelecaoLogica, DateOnly, Guid, Projeto, DateOnly, Guid (+11 more)

### Community 16 - "Dashboard.razor"
Cohesion: 0.05
Nodes (39): ChartSeries, DashboardContasDialog, DashboardInvestimentosDialog, DashboardItensDialog, route:/dashboard, AbrirDialogContas, AbrirDialogInvestimentos, AbrirDialogItens (+31 more)

### Community 17 - "MudMoneyInput"
Cohesion: 0.11
Nodes (16): CultureInfo, DotNetObjectReference, ElementReference, JSInvokable, MudBaseInput, MoneyInputFormatter, bool, Dictionary (+8 more)

### Community 18 - "Finort"
Cohesion: 0.07
Nodes (31): AES-256-GCM, ASP.NET Core Data Protection, Authentication, Backup and Restore, Bank Accounts Management, Blazor Server, .cfbak format, Cloudflare Turnstile (+23 more)

### Community 19 - "PessoaDialog.razor"
Cohesion: 0.05
Nodes (37): MudBlazor.Utilities, MudColorPicker, Cancelar, CancelarLembrete, CarregarLembretesAsync, EditarLembrete, ExcluirLembreteAsync, IniciarNovoLembrete (+29 more)

### Community 20 - ".SetupAsync"
Cohesion: 0.17
Nodes (14): Ano, Mes, ConferenciaMes, Guid, List, Task, FechamentoService, DateOnly (+6 more)

### Community 21 - "Pessoa"
Cohesion: 0.12
Nodes (18): DateOnly, Guid, Lembrete, LembreteTipo, Guid, Pessoa, Dictionary, Guid (+10 more)

### Community 22 - "UpdatePlanTests"
Cohesion: 0.10
Nodes (12): Updater, DllImport, IntPtr, JsonElement, Fact, string, UpdatePlanTests, Nativo (+4 more)

### Community 23 - "Build and Release Job"
Cohesion: 0.07
Nodes (32): Release Workflow, Create GitHub Release Step, Get version Step, Build and Release Job, actions/checkout@v4, actions/setup-dotnet@v4, build-setup Job, choco install innosetup (+24 more)

### Community 24 - "Pessoas.razor"
Cohesion: 0.06
Nodes (32): route:/pessoas, AbrirDialog, CarregarAsync, Excluir, ObterTextosLembretes, OnInitializedAsync, AppComponentBase, Finort.Components.Dialogs (+24 more)

### Community 25 - "SobreDialog.razor"
Cohesion: 0.06
Nodes (30): AppUpdateService, Finort.Models, IHostApplicationLifetime, Microsoft.Extensions.Hosting, AtualizarAgora, AtualizarUltimaVerificacao, AvaliarBotaoAtualizacao, CacheValido (+22 more)

### Community 26 - "Provisoes.razor"
Cohesion: 0.06
Nodes (30): ProvisaoDialog, route:/provisoes, AbrirDialog, CarregarAsync, CorValor, Excluir, OnInitializedAsync, AppComponentBase (+22 more)

### Community 27 - "Projetos.razor"
Cohesion: 0.07
Nodes (29): ConclusaoProjetoDialog, ProjetoDialog, route:/projetos, AbrirDialog, CarregarAsync, Concluir, Excluir, OnInitializedAsync (+21 more)

### Community 28 - "Categorias.razor"
Cohesion: 0.07
Nodes (29): route:/categorias, CarregarAsync, DialogCategoria, DialogSubcategoria, ExcluirCategoria, ExcluirSubcategoria, OnInitializedAsync, AppComponentBase (+21 more)

### Community 29 - "Home.razor"
Cohesion: 0.07
Nodes (28): CalendarioService, Celula, CompromissoDia, CompromissoDiaDialog, route:/, AbrirDia, AgruparItens, CarregarAsync (+20 more)

### Community 30 - "FecharMes.razor"
Cohesion: 0.07
Nodes (28): CellTemplate, Columns, FechamentoService, MudDataGrid, PropertyColumn, route:/fechar-mes, CarregarAsync, FecharConta (+20 more)

### Community 31 - "RelatorioProjeto.razor"
Cohesion: 0.07
Nodes (28): route:/relatorios/projeto/{ProjetoId:guid}, CorResultadoCss, CorTipoCss, EstiloCorTipo, EstiloResultado, OnInitializedAsync, AppComponentBase, HeaderContent (+20 more)

### Community 32 - "PrimeiroAcesso.razor"
Cohesion: 0.07
Nodes (27): IBrowserFile, route:/configurar, ArquivoSelecionado, IniciarRestauracao, OnAfterRenderAsync, OnInitializedAsync, AppComponentBase, AuthService (+19 more)

### Community 33 - "Cartoes.razor"
Cohesion: 0.07
Nodes (27): route:/cartoes, AbrirDialog, CarregarAsync, Excluir, OnInitializedAsync, AppComponentBase, CartaoCreditoService, CartaoDialog (+19 more)

### Community 34 - ".CriarConfiguracaoAsync"
Cohesion: 0.18
Nodes (9): DateTime, Configuracao, PasswordHasher, string, Task, AuthService, Fact, Task (+1 more)

### Community 35 - ".SetupAsync"
Cohesion: 0.17
Nodes (14): FluxoMensal, TotalCartao, Data, DateOnly, Guid, Task, FluxoService, DateOnly (+6 more)

### Community 36 - "Contas.razor"
Cohesion: 0.07
Nodes (26): route:/contas, AbrirDialog, CarregarAsync, Excluir, OnInitializedAsync, AppComponentBase, Conta, ContaDialog (+18 more)

### Community 37 - "Fluxo.razor"
Cohesion: 0.08
Nodes (25): FluxoCard, FluxoService, MudSlider, route:/fluxo, CarregarAsync, CarregarDiasAntecipacaoAsync, EhPassado, Navegar (+17 more)

### Community 38 - "ProvisaoDialog.razor"
Cohesion: 0.08
Nodes (25): Cancelar, OnCategoriaChanged, OnInitialized, OnOndeChanged, AppComponentBase, CartaoSelect, CategoriaSelect, ContaSelect (+17 more)

### Community 39 - "Finort.Data.Migrations.MySql"
Cohesion: 0.09
Nodes (11): Finort.Data.Migrations.MySql, ModelBuilder, Initial, ModelBuilder, AdicionarCamposVersao, ModelBuilder, RemoverDataCompra, MigrationBuilder (+3 more)

### Community 40 - ".RecordFailure"
Cohesion: 0.22
Nodes (8): ConcurrentDictionary, DateTime, int, Estado, LoginAttemptGuard, Fact, int, LoginAttemptGuardTests

### Community 41 - ".Create"
Cohesion: 0.28
Nodes (8): Task, Fact, string, Task, AuthLoginServiceTests, Db, File, TestDbContext

### Community 42 - "Manual.razor"
Cohesion: 0.09
Nodes (22): route:/manual, SecaoManual, Buscar, IrParaTopo, OnAfterRenderAsync, OnScrollChanged, OnSecaoSelecionada, AppComponentBase (+14 more)

### Community 43 - "AuthLoginService"
Cohesion: 0.11
Nodes (13): IConfiguration, int, AuthLoginService, LoginRequest, LoginResult, LoginStatus, CancellationToken, Task (+5 more)

### Community 44 - "EmailService"
Cohesion: 0.15
Nodes (10): MimeMessage, SmtpSettings, SecureSocketOptions, Task, EmailService, Fact, InlineData, SecureSocketOptions (+2 more)

### Community 45 - "MovimentacoesDialog.razor"
Cohesion: 0.09
Nodes (21): CarregarAsync, Estornar, OnInitializedAsync, AppComponentBase, DialogActions, DialogContent, HeaderContent, IDialogService (+13 more)

### Community 46 - ".ObterMesAsync"
Cohesion: 0.16
Nodes (14): Item, CalendarioMes, CompromissoAgrupado, CompromissoDia, CompromissoItem, Data, DateOnly, List (+6 more)

### Community 47 - "Finort.Migrations"
Cohesion: 0.08
Nodes (13): Finort.Migrations, ModelBuilder, Initial, ModelBuilder, AdicionarCamposVersao, ModelBuilder, RemoverDataCompra, ModelBuilder (+5 more)

### Community 48 - "CartaoDialog.razor"
Cohesion: 0.10
Nodes (20): Cancelar, OnInitializedAsync, AppComponentBase, CartaoCreditoService, Conta, ContaService, DialogActions, DialogContent (+12 more)

### Community 49 - "InvestimentoDialog.razor"
Cohesion: 0.10
Nodes (20): Cancelar, OnInitialized, AppComponentBase, ContaSelect, DataPicker, DialogActions, DialogContent, Finort.Components.Shared (+12 more)

### Community 50 - "TransacaoDialog.razor"
Cohesion: 0.10
Nodes (20): Cancelar, FecharAsync, OnAfterRenderAsync, OnInitialized, AppComponentBase, DespesaCartaoForm, DialogActions, DialogContent (+12 more)

### Community 51 - "DespesaCartaoForm.razor"
Cohesion: 0.10
Nodes (20): AppComponentBase, CartaoCreditoService, CartaoSelect, CategoriaSelect, ContaSelect, DataPicker, FaturaService, Finort.Components.Shared (+12 more)

### Community 52 - "Finort.csproj"
Cohesion: 0.09
Nodes (20): coverlet.collector (6.0.2), MailKit (4.16.0), Microsoft.EntityFrameworkCore.Design (9.0.0), Microsoft.EntityFrameworkCore.Sqlite (9.0.0), Microsoft.EntityFrameworkCore.Tools (9.0.0), Microsoft.Extensions.Configuration (9.0.0), Microsoft.Extensions.Configuration.Binder (9.0.0), Microsoft.NET.Test.Sdk (17.12.0) (+12 more)

### Community 53 - "Login.razor"
Cohesion: 0.10
Nodes (19): route:/login, Entrar, IrEsqueciSenha, OnAfterRenderAsync, OnInitializedAsync, OnSenhaKeyDown, AppComponentBase, AuthService (+11 more)

### Community 54 - "ProventosDialog.razor"
Cohesion: 0.10
Nodes (19): ExcluirProvento, OnInitializedAsync, AppComponentBase, DialogActions, DialogContent, HeaderContent, IDialogService, InvestimentoProvento (+11 more)

### Community 55 - "LancamentoForm.razor"
Cohesion: 0.10
Nodes (19): AppComponentBase, CategoriaSelect, CategoriaService, ContaSelect, ContaService, DataPicker, Finort.Components.Shared, IDialogService (+11 more)

### Community 56 - ".SetupAsync"
Cohesion: 0.12
Nodes (22): CartaoUtilizacao, CategoriaValor, ContaPatrimonio, DashboardMes, InvestimentoPatrimonio, LancamentoTop, MesTendencia, DateOnly (+14 more)

### Community 57 - "AppUpdateServiceTests"
Cohesion: 0.18
Nodes (6): string, AppUpdateService, Fact, InlineData, Theory, AppUpdateServiceTests

### Community 58 - "Finort.App"
Cohesion: 0.11
Nodes (11): Finort.Components.Lancamentos, Finort.Components, Finort.App, Microsoft.AspNetCore.Components, Microsoft.AspNetCore.Components.Authorization, MudTheme, string, AppTheme (+3 more)

### Community 59 - "AppDbContext"
Cohesion: 0.21
Nodes (10): DbConnection, DbContext, DbSet, DbTransaction, ModelBuilder, AppDbContext, ILogger, Task (+2 more)

### Community 60 - "TurnstileVerifierTests"
Cohesion: 0.47
Nodes (4): Fact, Task, Resposta, TurnstileVerifierTests

### Community 61 - "FaturaHistoricoDialog.razor"
Cohesion: 0.11
Nodes (18): Fechar, OnInitializedAsync, AppComponentBase, CartaoCreditoService, DialogActions, DialogContent, FaturaService, FaturaSituacao (+10 more)

### Community 62 - "_Imports.razor"
Cohesion: 0.12
Nodes (16): Finort.App, Finort.Components, Finort.Components.Layout, Finort.Models.Auth, Microsoft.AspNetCore.Authorization, Microsoft.AspNetCore.Components.Forms, Microsoft.AspNetCore.Components.Routing, Microsoft.AspNetCore.Components.Web (+8 more)

### Community 63 - "BackupRestoreService"
Cohesion: 0.05
Nodes (32): byte, IDataProtector, JsonSerializerOptions, object, DateTime, int, BackupCrypto, DatabaseConfig (+24 more)

### Community 64 - "LancamentoNovoCartao.razor"
Cohesion: 0.11
Nodes (17): route:/lancamentos/novo/cartao, AppComponentBase, DespesaCartaoForm, Finort.Components.Lancamentos, Finort.Services, ISnackbar, LancamentoService, MudButton (+9 more)

### Community 65 - "Perfil.razor"
Cohesion: 0.11
Nodes (17): route:/perfil, OnInitializedAsync, AppComponentBase, AuthService, ISnackbar, MudButton, MudGrid, MudItem (+9 more)

### Community 66 - "PagamentoDialog.razor"
Cohesion: 0.11
Nodes (17): Cancelar, OnInitialized, Pagar, AppComponentBase, ContaSelect, DataPicker, DialogActions, DialogContent (+9 more)

### Community 67 - ".CreateAsync"
Cohesion: 0.30
Nodes (6): Task, TokenService, Fact, Task, TokenServiceTests, TimeSpan

### Community 68 - ".GerarAsync"
Cohesion: 0.07
Nodes (30): CartaoId, CategoriaId, ContaId, IContainer, DateOnly, Fim, Guid, Inicio (+22 more)

### Community 69 - "DatabaseConfig"
Cohesion: 0.23
Nodes (8): DbContextOptions, DbContextOptionsBuilder, ServerVersion, DbContextOptionsBuilderFactory, DatabaseConfig, DatabaseConnectionSettings, Fact, DbContextOptionsBuilderFactoryTests

### Community 70 - "FaturaHistorico.razor"
Cohesion: 0.12
Nodes (16): route:/cartoes/{Id:guid}/faturas, OnInitializedAsync, AppComponentBase, FaturaService, FaturaSituacao, HeaderContent, MudGrid, MudIcon (+8 more)

### Community 71 - "ConfigurarSmtp.razor"
Cohesion: 0.12
Nodes (16): route:/configurar-smtp, BuildSettings, IrLogin, OnInitializedAsync, AppComponentBase, AuthService, EmailService, ISnackbar (+8 more)

### Community 72 - "CompromissoDiaDialog.razor"
Cohesion: 0.12
Nodes (16): AgruparItens, Fechar, AppComponentBase, CompromissoAgrupado, CompromissoItem, DialogActions, DialogContent, MudButton (+8 more)

### Community 73 - "MovimentoDialog.razor"
Cohesion: 0.10
Nodes (20): Cancelar, OnCotaChanged, OnQuantidadeChanged, OnTaxaChanged, OnTotalChanged, AppComponentBase, DataPicker, DialogActions (+12 more)

### Community 74 - "ProjetoDialog.razor"
Cohesion: 0.12
Nodes (16): Cancelar, OnInitialized, AppComponentBase, DataPicker, DialogActions, DialogContent, Finort.Components.Shared, ISnackbar (+8 more)

### Community 75 - "LancamentoEditar.razor"
Cohesion: 0.12
Nodes (15): route:/lancamentos/editar/{Id:guid}, OnInitializedAsync, AppComponentBase, Finort.Components.Lancamentos, LancamentoForm, LancamentoService, MudCard, MudCardContent (+7 more)

### Community 76 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 77 - "FaturaService"
Cohesion: 0.08
Nodes (26): CartaoService, DateTime, Guid, Fatura, FaturaResumoCalendario, FaturaSituacao, PagamentoFatura, DateOnly (+18 more)

### Community 78 - "ScopedAuthenticationStateProvider"
Cohesion: 0.15
Nodes (10): AuthenticationState, AuthenticationStateProvider, ClaimsPrincipal, LoginState, ClaimsPrincipal, Task, ScopedAuthenticationStateProvider, Fact (+2 more)

### Community 79 - "VersionCheckService"
Cohesion: 0.24
Nodes (7): HttpClient, DateTime, AppVersion, string, Task, GitHubRelease, VersionCheckService

### Community 81 - "ConclusaoProjetoDialog.razor"
Cohesion: 0.13
Nodes (14): Cancelar, OnDataChanged, OnInitialized, AppComponentBase, DataPicker, DialogActions, DialogContent, Finort.Components.Shared (+6 more)

### Community 82 - "NovoLancamentoFaturaDialog.razor"
Cohesion: 0.13
Nodes (14): Cancelar, AppComponentBase, DespesaCartaoForm, DialogActions, DialogContent, Finort.Components.Lancamentos, ISnackbar, LancamentoService (+6 more)

### Community 83 - "ProventoDialog.razor"
Cohesion: 0.13
Nodes (14): Cancelar, AppComponentBase, DataPicker, DialogActions, DialogContent, Finort.Components.Shared, ISnackbar, MudButton (+6 more)

### Community 84 - "RelatorioDespesas.razor"
Cohesion: 0.06
Nodes (31): DespesaPessoaSubtotal, route:/relatorios/despesas, AplicarPeriodoPadraoAsync, CarregarAsync, EstiloValor, IrParaPessoaAsync, LimparAsync, OnInitializedAsync (+23 more)

### Community 85 - "EsqueciSenha.razor"
Cohesion: 0.14
Nodes (13): route:/esqueci-senha, Enviar, IrLogin, OnAfterRenderAsync, OnInitializedAsync, AppComponentBase, AuthService, EmailService (+5 more)

### Community 86 - "LancamentoNovo.razor"
Cohesion: 0.14
Nodes (13): route:/lancamentos/novo/{Tipo:regex(^receita$|^despesa$|^transferencia$)}, OnParametersSet, AppComponentBase, Finort.Components.Lancamentos, LancamentoForm, MudCard, MudCardContent, MudIcon (+5 more)

### Community 87 - "ContaDialog.razor"
Cohesion: 0.14
Nodes (13): Cancelar, OnInitialized, ContaService, DialogActions, DialogContent, MudButton, MudDialog, MudForm (+5 more)

### Community 88 - "DatabaseSwitchServiceTests"
Cohesion: 0.33
Nodes (5): Fact, string, Task, DatabaseSwitchServiceTests, ServicoComFalhaNaCopia

### Community 89 - "Finort.Models.Configuration"
Cohesion: 0.12
Nodes (3): Finort.Models.Configuration, Fact, DatabaseConfigTests

### Community 90 - "RelatorioReceitas.razor"
Cohesion: 0.07
Nodes (29): ReceitaPessoaSubtotal, route:/relatorios/receitas, AplicarPeriodoPadraoAsync, CarregarAsync, EstiloValor, IrParaPessoaAsync, LimparAsync, OnInitializedAsync (+21 more)

### Community 91 - "RedefinirSenha.razor"
Cohesion: 0.15
Nodes (12): route:/redefinir-senha, IrEsqueciSenha, OnInitialized, AppComponentBase, AuthService, ISnackbar, MudButton, MudText (+4 more)

### Community 92 - "AtualizarCotacaoDialog.razor"
Cohesion: 0.15
Nodes (12): Cancelar, AppComponentBase, DataPicker, DialogActions, DialogContent, Finort.Components.Shared, MudButton, MudDialog (+4 more)

### Community 93 - "CategoriaDialog.razor"
Cohesion: 0.15
Nodes (12): Cancelar, OnInitialized, CategoriaService, DialogActions, DialogContent, MudButton, MudDialog, MudForm (+4 more)

### Community 94 - "SubcategoriaDialog.razor"
Cohesion: 0.15
Nodes (12): Cancelar, OnInitialized, CategoriaService, DialogActions, DialogContent, MudButton, MudDialog, MudForm (+4 more)

### Community 95 - "SubcategoriaSelect.razor"
Cohesion: 0.15
Nodes (12): AbrirNovaSubcategoriaAsync, OnInitializedAsync, OnParametersSetAsync, OnValueChanged, AppComponentBase, CategoriaService, Finort.Components.Dialogs, IDialogService (+4 more)

### Community 96 - "SeedCliGuardTests"
Cohesion: 0.31
Nodes (4): SeedCliGuard, Fact, string, SeedCliGuardTests

### Community 98 - "CartaoSelect.razor"
Cohesion: 0.18
Nodes (10): AbrirNovoCartaoAsync, OnInitializedAsync, OnValueChanged, AppComponentBase, CartaoCreditoService, CartaoDialog, Finort.Components.Dialogs, IDialogService (+2 more)

### Community 99 - "CategoriaSelect.razor"
Cohesion: 0.18
Nodes (10): AbrirNovaCategoriaAsync, OnInitializedAsync, OnValueChanged, AppComponentBase, CategoriaDialog, CategoriaService, Finort.Components.Dialogs, IDialogService (+2 more)

### Community 100 - "ContaSelect.razor"
Cohesion: 0.18
Nodes (10): AbrirNovaContaAsync, OnInitializedAsync, OnValueChanged, AppComponentBase, ContaDialog, ContaService, Finort.Components.Dialogs, IDialogService (+2 more)

### Community 101 - "SenhaBackupDialog.razor"
Cohesion: 0.18
Nodes (10): Cancelar, AppComponentBase, DialogActions, DialogContent, MudButton, MudDialog, MudText, MudTextField (+2 more)

### Community 102 - "DashboardContasDialog.razor"
Cohesion: 0.18
Nodes (10): CorSaldo, ContaPatrimonio, DialogContent, Finort.Models.Financeiro, MudDialog, MudList, MudListItem, MudText (+2 more)

### Community 103 - "PessoaSelect.razor"
Cohesion: 0.18
Nodes (10): AbrirNovaPessoaAsync, OnInitializedAsync, OnValueChanged, AppComponentBase, Finort.Components.Dialogs, IDialogService, PessoaDialog, PessoaService (+2 more)

### Community 104 - "SelecaoBusca.razor"
Cohesion: 0.18
Nodes (10): AbrirCadastroAsync, Buscar, Normalizar, OnParametersSet, OnSelecionadoChanged, AppComponentBase, MudAutocomplete, MudIconButton (+2 more)

### Community 105 - "TurnstileVerifier"
Cohesion: 0.18
Nodes (7): Finort.Models, CancellationToken, string, Task, SiteVerifyResponse, TurnstileVerifier, System.Net.Http.Json

### Community 106 - "AddTaxaInvestimentoMovimento"
Cohesion: 0.22
Nodes (5): Finort.Data.Migrations, MigrationBuilder, ModelBuilder, AddTaxaInvestimentoMovimento, AddTaxaInvestimentoMovimento

### Community 107 - "Error.razor"
Cohesion: 0.33
Nodes (4): route:/Error, OnInitialized, PageTitle, System.Diagnostics

### Community 108 - "DatabaseMigrator"
Cohesion: 0.26
Nodes (6): Exception, SqliteConnection, ILogger, DatabaseMigrator, Fact, DatabaseMigratorTests

### Community 109 - "SenhaLoginDialog.razor"
Cohesion: 0.20
Nodes (9): Cancelar, Confirmar, AppComponentBase, DialogActions, DialogContent, MudButton, MudDialog, MudText (+1 more)

### Community 110 - "DashboardInvestimentosDialog.razor"
Cohesion: 0.20
Nodes (9): DialogContent, Finort.Models.Financeiro, InvestimentoPatrimonio, MudDialog, MudList, MudListItem, MudText, System.Globalization (+1 more)

### Community 111 - "DashboardItensDialog.razor"
Cohesion: 0.20
Nodes (9): CategoriaValor, DialogContent, Finort.Models.Financeiro, MudDialog, MudList, MudListItem, MudText, System.Globalization (+1 more)

### Community 112 - "AppDbContextModelSnapshot.cs"
Cohesion: 0.22
Nodes (5): ModelSnapshot, ModelBuilder, AppDbContextModelSnapshot, ModelBuilder, MySqlAppDbContextModelSnapshot

### Community 113 - "LoginLayout.razor"
Cohesion: 0.22
Nodes (8): LayoutComponentBase, MudBlazor, MudDialogProvider, MudLayout, MudMainContent, MudPopoverProvider, MudSnackbarProvider, MudThemeProvider

### Community 114 - "ProjetoSelect.razor"
Cohesion: 0.22
Nodes (8): Buscar, EmitirValor, OnParametersSetAsync, OnSelecionadoChanged, AppComponentBase, MudAutocomplete, Projeto, ProjetoService

### Community 115 - "BackupRestoreServiceTests"
Cohesion: 0.42
Nodes (4): Fact, string, Task, BackupRestoreServiceTests

### Community 116 - "Routes.razor"
Cohesion: 0.29
Nodes (6): AuthorizeRouteView, FocusOnNavigate, Found, NotAuthorized, RedirectToLogin, Router

### Community 117 - "AppDbContextFactory"
Cohesion: 0.33
Nodes (4): IDesignTimeDbContextFactory, AppDbContextFactory, MySqlAppDbContext, MySqlAppDbContextFactory

### Community 118 - "FluxoCard.razor"
Cohesion: 0.29
Nodes (6): MudCardHeader, MudCard, MudCardContent, MudIconButton, MudText, System.Globalization

### Community 119 - "UnirSubcategoriasAlimentacaoMySql"
Cohesion: 0.22
Nodes (5): Finort.Data.MySql, MigrationBuilder, ModelBuilder, UnirSubcategoriasAlimentacaoMySql, UnirSubcategoriasAlimentacaoMySql

### Community 120 - "App.razor"
Cohesion: 0.40
Nodes (4): CascadingAuthenticationState, HeadOutlet, ImportMap, Routes

### Community 125 - "Migration"
Cohesion: 0.16
Nodes (7): Migration, MigrationBuilder, SeedDadosCompletos, MigrationBuilder, AddReembolsoCategoriaSubcategoria, MigrationBuilder, Initial

### Community 126 - "Handler"
Cohesion: 0.25
Nodes (7): FormUrlEncodedContent, HttpMessageHandler, HttpRequestMessage, HttpResponseMessage, CancellationToken, int, Handler

### Community 129 - "DespesaRelatorioService.cs"
Cohesion: 0.33
Nodes (5): DespesaCategoriaSubtotal, DespesaLinha, DespesaOrigemSubtotal, DespesaPessoaSubtotal, DespesaRelatorio

### Community 130 - "AppThemeTests.cs"
Cohesion: 0.33
Nodes (3): Fact, AppThemeTests, xUnit

### Community 135 - "Finort Docker Service"
Cohesion: 0.50
Nodes (3): finort-data Docker Volume, Finort Docker Service, ghcr.io/stmarcelo/finort:latest

### Community 136 - "MudMoneyInput.razor"
Cohesion: 0.50
Nodes (3): MudBaseInput<decimal, IJSRuntime, MudTextField

### Community 137 - "ReceitaRelatorioService.cs"
Cohesion: 0.40
Nodes (4): ReceitaLinha, ReceitaOrigemSubtotal, ReceitaPessoaSubtotal, ReceitaRelatorio

### Community 140 - "TestWebHostEnvironment"
Cohesion: 0.50
Nodes (3): IFileProvider, IWebHostEnvironment, TestWebHostEnvironment

### Community 142 - "MesFechado"
Cohesion: 0.50
Nodes (3): DateTime, Guid, MesFechado

### Community 143 - "SeedDataService"
Cohesion: 0.50
Nodes (3): PasswordHasher, Task, SeedDataService

## Knowledge Gaps
- **1470 isolated node(s):** `net9.0`, `coverlet.collector (6.0.2)`, `Microsoft.Extensions.Configuration (9.0.0)`, `Microsoft.Extensions.Configuration.Binder (9.0.0)`, `Microsoft.NET.Test.Sdk (17.12.0)` (+1465 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **31 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `.Cleanup`, `LancamentoService`, `CartaoCredito`, `InvestimentoService`, `.SetupAsync`, `Provisao`, `IDisposable`, `Categoria`, `MesFechado`, `Projeto`, `SeedDataService`, `.SetupAsync`, `PasswordResetToken`, `Pessoa`, `.CriarConfiguracaoAsync`, `.SetupAsync`, `.Create`, `EmailService`, `.ObterMesAsync`, `.SetupAsync`, `BackupRestoreService`, `.CreateAsync`, `.GerarAsync`, `DatabaseConfig`, `FaturaService`, `DatabaseSwitchServiceTests`, `Finort.Models.Auth`, `DatabaseMigrator`, `BackupRestoreServiceTests`, `AppDbContextFactory`?**
  _High betweenness centrality (0.111) - this node is a cross-community bridge._
- **Why does `Finort.Data` connect `Finort.Data` to `DespesaRelatorioService.cs`, `IDisposable`, `ReceitaRelatorioService.cs`, `20260906015149_SeedDadosCompletos.Designer.cs`, `20260901214003_AdicionarDiasAntecipacao.Designer.cs`, `MySqlAppDbContextTests.cs`, `20260901214035_AdicionarDiasAntecipacaoMySql.Designer.cs`, `20260906015231_SeedDadosCompletos.Designer.cs`, `20260907014704_AddReembolsoCategoriaSubcategoriaMySql.Designer.cs`, `Finort.Data.Migrations.MySql`, `Finort.Migrations`, `Finort.App`, `BackupRestoreService`, `Finort.Models.Configuration`, `Finort.Models.Auth`, `AddTaxaInvestimentoMovimento`, `AppDbContextModelSnapshot.cs`, `AppDbContextFactory`, `UnirSubcategoriasAlimentacaoMySql`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Why does `Finort.Services` connect `Finort.Data` to `Finort.Models.Auth`, `DespesaRelatorioService.cs`, `.GerarAsync`, `.RecordFailure`, `IDisposable`, `ReceitaRelatorioService.cs`, `AuthLoginService`, `Error.razor`, `TurnstileVerifier`, `Finort.Models.Configuration`, `Finort.App`, `BackupRestoreService`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **What connects `net9.0`, `coverlet.collector (6.0.2)`, `Microsoft.Extensions.Configuration (9.0.0)` to the rest of the system?**
  _1470 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `.Cleanup` be split into smaller, more focused modules?**
  _Cohesion score 0.06872393661384488 - nodes in this community are weakly interconnected._
- **Should `LancamentoService` be split into smaller, more focused modules?**
  _Cohesion score 0.06495726495726496 - nodes in this community are weakly interconnected._
- **Should `CartaoCredito` be split into smaller, more focused modules?**
  _Cohesion score 0.05094905094905095 - nodes in this community are weakly interconnected._