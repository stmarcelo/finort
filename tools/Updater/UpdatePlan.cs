using System.Text.Json;

namespace Updater;

/// <summary>
/// Lógica pura do processo de atualização: localizar o banco SQLite,
/// nomear/retirar backups e montar os argumentos do instalador silencioso.
/// </summary>
public static class UpdatePlan
{
    private const string BancoPadrao = "finort.db";

    public static string? ResolverCaminhoDb(string appDir, string? settingsJson)
    {
        var provider = "";
        var connectionString = "";

        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    provider = LerTexto(root, "provider");

                    if (root.TentarPropriedade("sqlite") is { } sqlite && sqlite.ValueKind == JsonValueKind.Object)
                        connectionString = LerTexto(sqlite, "connectionstring");
                }
            }
            catch (JsonException)
            {
                // JSON corrompido: assume SQLite padrão (cópia só acontece se o arquivo existir).
            }
        }

        if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase))
            return null;

        var arquivo = ExtrairDataSources(connectionString) is { } origem && origem.Length > 0
            ? origem
            : BancoPadrao;

        return Path.IsPathRooted(arquivo)
            ? arquivo
            : Path.GetFullPath(Path.Combine(appDir, arquivo));
    }

    private static JsonElement? TentarPropriedade(this JsonElement objeto, string nome)
    {
        foreach (var propriedade in objeto.EnumerateObject())
        {
            if (string.Equals(propriedade.Name, nome, StringComparison.OrdinalIgnoreCase))
                return propriedade.Value;
        }
        return null;
    }

    private static string LerTexto(JsonElement objeto, string nome) =>
        objeto.TentarPropriedade(nome) is { ValueKind: JsonValueKind.String } valor
            ? valor.GetString() ?? ""
            : "";

    private static string? ExtrairDataSources(string connectionString)
    {
        foreach (var parte in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var chave = parte.Trim();
            foreach (var prefixo in new[] { "Data Source=", "DataSource=" })
            {
                if (chave.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                    return chave[prefixo.Length..].Trim();
            }
        }
        return null;
    }

    public static string NomeBackup(string versao, DateTime agora) =>
        $"finort-preupdate-{versao}-{agora:yyyyMMdd_HHmmss}.db";

    public static List<string> AplicarRetencao(string dirBackups, int manter = 3)
    {
        var apagados = new List<string>();
        var backups = Directory.GetFiles(dirBackups, "finort-preupdate-*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach (var excedente in backups.Skip(manter))
        {
            try
            {
                excedente.Delete();
                apagados.Add(excedente.FullName);
            }
            catch (IOException)
            {
                // Arquivo em uso: mantém; a retenção tenta de novo na próxima atualização.
                continue;
            }

            foreach (var sufixo in new[] { "-wal", "-shm" })
            {
                var lateral = excedente.FullName + sufixo;
                try
                {
                    if (!File.Exists(lateral)) continue;
                    File.Delete(lateral);
                    apagados.Add(lateral);
                }
                catch (IOException)
                {
                    // Sidecar em uso: mantém; a retenção tenta de novo na próxima atualização.
                }
            }
        }
        return apagados;
    }

    public static string MontarArgumentosSetup() =>
        "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS";
}
