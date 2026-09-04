using System.Security.Cryptography;

namespace Finort.Services;

/// <summary>Chave efêmera gerada no servidor e exibida no console, exigida para concluir
/// o primeiro acesso. Impede que terceiros tomem a conta em deployments expostos.</summary>
public class SetupKeyStore
{
    private const string NomeArquivo = "setup.key";

    private readonly string _caminho;

    public SetupKeyStore(string contentRootPath)
    {
        var dataDir = Environment.GetEnvironmentVariable("FINORT_DATA_DIR");
        var baseDir = !string.IsNullOrEmpty(dataDir) ? dataDir : contentRootPath;
        _caminho = Path.Combine(baseDir, NomeArquivo);
    }

    public string? Ler()
    {
        if (!File.Exists(_caminho)) return null;
        try
        {
            var texto = File.ReadAllText(_caminho).Trim();
            return string.IsNullOrEmpty(texto) ? null : texto;
        }
        catch { return null; }
    }

    public string GerarSeNecessario()
    {
        var atual = Ler();
        if (!string.IsNullOrEmpty(atual)) return atual;

        var chave = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        File.WriteAllText(_caminho, chave);
        return chave;
    }

    public void Remover()
    {
        try
        {
            if (File.Exists(_caminho)) File.Delete(_caminho);
        }
        catch { }
    }
}
