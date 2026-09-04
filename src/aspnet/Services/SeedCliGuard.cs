using Finort.Models.Configuration;
using Microsoft.Data.Sqlite;

namespace Finort.Services;

/// <summary>Guard do seed de teste via CLI (dotnet run 'seed-para-teste'):
/// suportado apenas em SQLite e apenas quando o arquivo do banco ainda não existe.</summary>
public static class SeedCliGuard
{
    public static string? Verificar(DatabaseConfig config, string contentRootPath)
    {
        if (!string.Equals(config.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            return "O seed de teste é suportado apenas para SQLite.";

        var caminho = ResolverCaminhoBanco(config.Sqlite.ConnectionString, contentRootPath);
        if (File.Exists(caminho))
            return $"Não é possível gerar o seed de teste: já existe um banco de dados " +
                   $"({Path.GetFileName(caminho)}). Exclua o arquivo para criar um banco de teste.";

        return null;
    }

    public static string ResolverCaminhoBanco(string connectionString, string contentRootPath)
    {
        var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(source))
            return source;
        return Path.IsPathRooted(source)
            ? Path.GetFullPath(source)
            : Path.GetFullPath(Path.Combine(contentRootPath, source));
    }
}
