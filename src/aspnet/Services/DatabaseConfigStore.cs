using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finort.Models.Configuration;

namespace Finort.Services;

public class DatabaseConfigStore
{
    private const string NomeArquivo = "database.settings.json";
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _caminho;
    private readonly string _appSettings;
    private readonly SecretProtector? _secrets;
    private readonly object _lock = new();

    public DatabaseConfigStore(string contentRootPath, SecretProtector? secrets = null)
    {
        var dataDir = Environment.GetEnvironmentVariable("FINORT_DATA_DIR");
        var baseDir = !string.IsNullOrEmpty(dataDir) ? dataDir : contentRootPath;
        _caminho = Path.Combine(baseDir, NomeArquivo);
        _appSettings = Path.Combine(contentRootPath, "appsettings.json");
        _secrets = secrets;
    }

    public DatabaseConfig Get()
    {
        lock (_lock)
        {
            DatabaseConfig config;
            if (File.Exists(_caminho))
            {
                var json = File.ReadAllText(_caminho);
                config = JsonSerializer.Deserialize<DatabaseConfig>(json) ?? LerAppsettings();
            }
            else
            {
                config = LerAppsettings();
            }

            if (_secrets is not null)
            {
                // Keyring perdido (troca de máquina/perfil): mantém o valor bruto em vez de derrubar o startup;
                // o usuário regrava a senha pela tela de configuração. Padrão tolerante de CredenciaisStartup.
                try
                {
                    config.MySql.ConnectionString =
                        _secrets.Unprotect(config.MySql.ConnectionString) ?? config.MySql.ConnectionString;
                }
                catch (CryptographicException)
                {
                }
            }

            var dataDir = Environment.GetEnvironmentVariable("FINORT_DATA_DIR");
            if (!string.IsNullOrEmpty(dataDir) && config.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var cs = config.Sqlite.ConnectionString;
                var dsIdx = cs.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
                if (dsIdx >= 0)
                {
                    var afterDs = cs.Substring(dsIdx + "Data Source=".Length);
                    var semiIdx = afterDs.IndexOf(';');
                    var fileName = semiIdx >= 0 ? afterDs.Substring(0, semiIdx) : afterDs;
                    var fullPath = Path.Combine(dataDir, Path.GetFileName(fileName));
                    config.Sqlite.ConnectionString = cs.Substring(0, dsIdx + "Data Source=".Length)
                        + fullPath
                        + (semiIdx >= 0 ? afterDs.Substring(semiIdx) : "");
                }
            }

            return config;
        }
    }

    public void Set(DatabaseConfig config)
    {
        lock (_lock)
        {
            var paraGravar = new DatabaseConfig
            {
                Provider = config.Provider,
                Sqlite = config.Sqlite,
                MySql = new DatabaseConnectionSettings
                {
                    ConnectionString = _secrets is not null && !SecretProtector.PareceProtegido(config.MySql.ConnectionString)
                        ? _secrets.Protect(config.MySql.ConnectionString)
                        : config.MySql.ConnectionString
                }
            };

            var temp = _caminho + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(paraGravar, JsonOpts));
            File.Move(temp, _caminho, overwrite: true);
        }
    }

    private DatabaseConfig LerAppsettings()
    {
        if (!File.Exists(_appSettings)) return new DatabaseConfig();
        var json = File.ReadAllText(_appSettings);
        return JsonSerializer.Deserialize<JsonElement>(json)
                .TryGetProperty("Database", out var db)
            ? (db.Deserialize<DatabaseConfig>() ?? new DatabaseConfig())
            : new DatabaseConfig();
    }
}
