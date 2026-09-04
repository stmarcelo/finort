using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Updater;

// updater.exe --pid N --version x.y.z --app-dir dir [--asset setup] [--relayed]
// Fluxo: espera o Finort sair -> backup do .db (SQLite) -> baixa/executa o
// instalador silencioso da Release -> relança o Finort. Banco nunca é substituído;
// migrations rodam no startup do app (já existente).

var log = new List<string>();
var tempDir = Path.Combine(Path.GetTempPath(), "finort-updater");
var appEncerrado = false;

try
{
    var appDir = LerArgumento(args, "--app-dir") ?? AppContext.BaseDirectory!;
    var versao = LerArgumento(args, "--version")
        ?? throw new InvalidOperationException("--version é obrigatório");
    var pidArg = LerArgumento(args, "--pid");
    var assetLocal = LerArgumento(args, "--asset");

    if (!System.Text.RegularExpressions.Regex.IsMatch(versao, @"^\d+(\.\d+)*$"))
        throw new InvalidOperationException($"Versão inválida: {versao}");

    // Relay: rodando de {app}, copia-se para %TEMP% para não travar a instalação.
    var exeAtual = Environment.ProcessPath!;
    var dirAtual = Path.GetDirectoryName(exeAtual)!;
    var relayed = args.Any(a => string.Equals(a, "--relayed", StringComparison.OrdinalIgnoreCase));
    if (!relayed && string.Equals(
            Path.GetFullPath(dirAtual), Path.GetFullPath(appDir),
            StringComparison.OrdinalIgnoreCase))
    {
        Directory.CreateDirectory(tempDir);
        var alvo = Path.Combine(tempDir, "updater.exe");
        File.Copy(exeAtual, alvo, overwrite: true);
        var psi = new ProcessStartInfo { FileName = alvo, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--relayed");
        Process.Start(psi);
        return 0;
    }

    Directory.CreateDirectory(tempDir);

    // 1. Espera o Finort sair (timeout 60 s).
    if (int.TryParse(pidArg, out var pid))
    {
        try
        {
            var finort = Process.GetProcessById(pid);
            Log($"Aguardando o Finort (pid {pid}) sair...");
            if (finort.WaitForExit(60_000))
            {
                appEncerrado = true;
            }
            else
            {
                Log("O Finort não encerrou em 60 s — nada foi instalado.");
                return 1;
            }
        }
        catch (ArgumentException)
        {
            Log("Finort já estava encerrado.");
            appEncerrado = true;
        }
    }

    // 2. Backup do banco (SQLite apenas).
    var settingsPath = Path.Combine(appDir, "database.settings.json");
    var settingsJson = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
    var caminhoDb = UpdatePlan.ResolverCaminhoDb(appDir, settingsJson);

    if (caminhoDb is null)
    {
        Log("Provider MySQL: backup local não é necessário (dados no servidor).");
    }
    else if (File.Exists(caminhoDb))
    {
        var dirBackups = Path.Combine(appDir, "backups");
        Directory.CreateDirectory(dirBackups);
        var backup = Path.Combine(dirBackups, UpdatePlan.NomeBackup(versao, DateTime.Now));
        File.Copy(caminhoDb, backup);
        Log($"Backup criado: {backup}");

        foreach (var sufixo in new[] { "-wal", "-shm" })
        {
            var lateral = caminhoDb + sufixo;
            if (File.Exists(lateral))
            {
                File.Copy(lateral, backup + sufixo);
                Log($"Copiado: {Path.GetFileName(backup + sufixo)}");
            }
        }

        foreach (var removido in UpdatePlan.AplicarRetencao(dirBackups))
            Log($"Retenção: removido {Path.GetFileName(removido)}");
    }
    else
    {
        Log("Banco SQLite não encontrado — nada a copiar.");
    }

    // 3. Obter o instalador (asset local de teste ou download da Release).
    var caminhoSetup = assetLocal ?? await BaixarSetupAsync(versao);
    Log($"Instalador: {caminhoSetup}");

    // 4. Instalação silenciosa (Inno Setup, PrivilegesRequired=lowest).
    var setup = Process.Start(new ProcessStartInfo
    {
        FileName = caminhoSetup,
        Arguments = UpdatePlan.MontarArgumentosSetup(),
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Não foi possível iniciar o instalador.");
    setup.WaitForExit();
    if (setup.ExitCode != 0)
        throw new InvalidOperationException($"Instalador saiu com código {setup.ExitCode}");
    Log("Instalação concluída.");

    // 5. Relança o Finort e limpa o temp.
    RelancarFinort(appDir);
    try
    {
        Directory.Delete(tempDir, recursive: true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // O exe em execução não pode apagar a si mesmo; o Windows limpa %TEMP% depois.
        Log($"Limpeza do diretório temporário pendente ({ex.GetType().Name}): {tempDir}");
    }
    Log("Atualização concluída com sucesso.");
    return 0;
}
catch (Exception ex)
{
    Log($"ERRO: {ex.Message}");
    var appDirFalha = LerArgumento(args, "--app-dir") ?? AppContext.BaseDirectory!;
    if (appEncerrado) RelancarFinort(appDirFalha);
    MostrarErro();
    return 1;
}

string? LerArgumento(string[] args, string nome)
{
    var i = Array.IndexOf(args, nome);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

void Log(string mensagem)
{
    var linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensagem}";
    log.Add(linha);
    Console.WriteLine(linha);
    try
    {
        Directory.CreateDirectory(tempDir);
        File.AppendAllLines(Path.Combine(tempDir, "updater.log"), [linha]);
    }
    catch (IOException) { }
}

void RelancarFinort(string appDir)
{
    var exe = Path.Combine(appDir, "Finort.exe");
    if (!File.Exists(exe)) return;
    Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
    Log("Finort relançado.");
}

void MostrarErro()
{
    try
    {
        var caminhoLog = Path.Combine(tempDir, "updater.log");
        Nativo.MessageBox(IntPtr.Zero,
            $"A atualização não foi concluída.\nSeus dados estão intactos e o Finort foi reaberto.\n\nLog: {caminhoLog}",
            "Finort — Atualização", 0x10);
    }
    catch (DllNotFoundException) { }
}

async Task<string> BaixarSetupAsync(string versao)
{
    Directory.CreateDirectory(tempDir);
    var destino = Path.Combine(tempDir, $"finort-{versao}-win-x64-setup.exe");
    var asset = $"finort-{versao}-win-x64-setup.exe";

    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Finort-Updater/1.0");

    var url = await ResolverUrlAssetAsync(http, versao, asset);
    Log($"Baixando {url}");
    var bytes = await http.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(destino, bytes);
    Log($"Download concluído ({bytes.Length / 1024 / 1024} MB).");
    return destino;
}

async Task<string> ResolverUrlAssetAsync(HttpClient http, string versao, string asset)
{
    try
    {
        var json = await http.GetStringAsync(
            $"https://api.github.com/repos/stmarcelo/finort/releases/tags/v{versao}");
        using var doc = JsonDocument.Parse(json);
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (a.GetProperty("name").GetString() == asset)
                return a.GetProperty("browser_download_url").GetString()!;
        }
    }
    catch (Exception ex)
    {
        Log($"API do GitHub indisponível ({ex.Message}); usando URL direta.");
    }
    return $"https://github.com/stmarcelo/finort/releases/download/v{versao}/{asset}";
}

internal static class Nativo
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);
}
