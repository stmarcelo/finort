using Finort.App;
using Finort.Components;
using Finort.Data;
using Finort.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Diagnostics;
using System.Globalization;

// App single-user pt-BR: formatação independente da cultura do servidor.
var culturaPadrao = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPadrao;
CultureInfo.DefaultThreadCurrentUICulture = culturaPadrao;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://localhost:5298");

builder.Services.AddDataProtection().SetApplicationName("Finort");
builder.Services.AddSingleton<SecretProtector>();

// Seed via command line: dotnet run 'seed-para-teste'
var isSeed = args.Length > 0 && args[0] == "seed-para-teste";
if (isSeed)
{
    var tempSp = builder.Services.BuildServiceProvider();
    var seedSecrets = tempSp.GetRequiredService<SecretProtector>();
    var configSeed = new DatabaseConfigStore(builder.Environment.ContentRootPath, seedSecrets).Get();
    var motivo = SeedCliGuard.Verificar(configSeed, builder.Environment.ContentRootPath);
    if (motivo is not null)
    {
        Console.WriteLine(motivo);
        return 1;
    }
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddSingleton(sp => new SetupKeyStore(builder.Environment.ContentRootPath));
builder.Services.AddScoped<CredenciaisStartup>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationCore();
builder.Services.AddAuthentication("AppAuth")
    .AddCookie("AppAuth", options => options.LoginPath = "/login");
builder.Services.AddAuthorization();
builder.Services.RemoveAll<AuthenticationStateProvider>();
builder.Services.AddScoped<LoginState>();
builder.Services.AddScoped<ScopedAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ScopedAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthLoginService>();
builder.Services.AddScoped<PessoaService>();
builder.Services.AddScoped<CategoriaService>();
builder.Services.AddScoped<ContaService>();
builder.Services.AddScoped<LancamentoService>();
builder.Services.AddScoped<CartaoCreditoService>();
builder.Services.AddScoped<FaturaService>();
builder.Services.AddScoped<ProvisaoService>();
builder.Services.AddScoped<CalendarioService>();
builder.Services.AddScoped<FluxoService>();
builder.Services.AddScoped<FechamentoService>();
builder.Services.AddScoped<InvestimentoService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ProjetoService>();
builder.Services.AddScoped<ProjetoRelatorioService>();
builder.Services.AddScoped<BackupRestoreService>();
builder.Services.AddScoped<LembreteService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<VersionCheckService>();
builder.Services.AddSingleton<AppUpdateService>();
builder.Services.AddSingleton(TurnstileConfig.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<IPageVerifier, TurnstileVerifier>(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddSingleton(sp => new DatabaseConfigStore(
    builder.Environment.ContentRootPath,
    sp.GetRequiredService<SecretProtector>()));

builder.Services.AddScoped<AppDbContext>(sp =>
{
    var config = sp.GetRequiredService<DatabaseConfigStore>().Get();
    if (string.Equals(config.Provider, "MySql", StringComparison.OrdinalIgnoreCase))
        return new MySqlAppDbContext(DbContextOptionsBuilderFactory.Build<MySqlAppDbContext>(
            config, new MySqlServerVersion(new Version(8, 0, 36))));
    return new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(config));
});

builder.Services.AddScoped<DatabaseMigrator>();
builder.Services.AddScoped<DatabaseSwitchService>();
builder.Services.AddScoped<SeedDataService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    migrator.Migrate();

    if (isSeed)
    {
        var seedService = scope.ServiceProvider.GetRequiredService<SeedDataService>();
        await seedService.SeedAsync();
        Console.WriteLine("✓ Dados fictícios inseridos com sucesso!");
        return 0;
    }

    var credenciais = scope.ServiceProvider.GetRequiredService<CredenciaisStartup>();
    await credenciais.ExecutarAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/auth/login", async (HttpContext http, LoginRequest request,
    AuthLoginService loginService, TurnstileConfig turnstile) =>
{
    if (string.IsNullOrWhiteSpace(request.Senha))
        return Results.Unauthorized();

    var origem = http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
    var result = await loginService.ValidarAsync(request.Senha, origem, request.TurnstileToken);
    var siteKey = result.RequireTurnstile && turnstile.Configurado ? turnstile.SiteKey : null;

    if (result.Status == LoginStatus.Ok)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, result.Configuracao!.Nome) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync("AppAuth", new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return Results.Ok(new
        {
            status = 200,
            nome = result.Configuracao.Nome,
            requireTurnstile = false,
            siteKey = (string?)null
        });
    }

    return result.Status switch
    {
        LoginStatus.Bloqueado => Results.Json(new
        {
            status = 423,
            message = "Muitas tentativas",
            requireTurnstile = result.RequireTurnstile,
            siteKey
        }, statusCode: StatusCodes.Status423Locked),
        LoginStatus.DesafioIndisponivel => Results.Json(new
        {
            status = 503,
            message = "Falha na verificação de segurança",
            requireTurnstile = true,
            siteKey
        }, statusCode: StatusCodes.Status503ServiceUnavailable),
        LoginStatus.DesafioInvalido => Results.Json(new
        {
            status = 401,
            requireTurnstile = true,
            siteKey
        }, statusCode: StatusCodes.Status401Unauthorized),
        _ => Results.Json(new
        {
            status = 401,
            requireTurnstile = result.RequireTurnstile,
            siteKey
        }, statusCode: StatusCodes.Status401Unauthorized)
    };
});

app.MapPost("/api/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync("AppAuth");
    return Results.NoContent();
});

app.MapGet("/api/relatorios/projeto/{id:guid}/pdf", async (Guid id, ProjetoRelatorioService service) =>
{
    var pdf = await service.GerarPdfBytesAsync(id);
    return pdf is null
        ? Results.NotFound()
        : Results.File(pdf, "application/pdf", $"relatorio_projeto_{DateTime.Now:yyMMddHHmmss}.pdf");
}).RequireAuthorization();

app.MapGet("/api/seed", async (SeedDataService seedService) =>
{
    await seedService.SeedAsync();
    return Results.Ok(new { message = "Dados fictícios inseridos com sucesso!" });
}).RequireAuthorization();

var openBrowser = builder.Configuration.GetValue<bool>("OpenBrowserOnStart");
if (openBrowser && OperatingSystem.IsWindows())
{
    app.Start();
    try { Process.Start(new ProcessStartInfo { FileName = "http://localhost:5298", UseShellExecute = true }); }
    catch (Exception ex) { Console.WriteLine($"Não foi possível abrir o navegador: {ex.Message}"); }
    app.WaitForShutdown();
}
else
{
    app.Run();
}
return 0;