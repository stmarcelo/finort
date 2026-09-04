using Updater;

namespace Finort.Tests;

public class UpdatePlanTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cf-upd-{Guid.NewGuid():N}");

    public UpdatePlanTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void ResolverCaminhoDb_SemSettings_UsaPadraoFinortDb()
    {
        var caminho = UpdatePlan.ResolverCaminhoDb(_dir, null);
        Assert.Equal(Path.Combine(_dir, "finort.db"), caminho);
    }

    [Fact]
    public void ResolverCaminhoDb_ProviderMySql_RetornaNull()
    {
        var json = """{"Provider":"MySql","MySql":{"ConnectionString":"Server=x"}}""";
        Assert.Null(UpdatePlan.ResolverCaminhoDb(_dir, json));
    }

    [Fact]
    public void ResolverCaminhoDb_ProviderMySqlMaiusculo_RetornaNull()
    {
        var json = """{"provider":"MYSQL"}""";
        Assert.Null(UpdatePlan.ResolverCaminhoDb(_dir, json));
    }

    [Fact]
    public void ResolverCaminhoDb_SqliteRelativo_ResolveContraAppDir()
    {
        var json = """{"Provider":"Sqlite","Sqlite":{"ConnectionString":"Data Source=finort.db"}}""";
        Assert.Equal(Path.Combine(_dir, "finort.db"), UpdatePlan.ResolverCaminhoDb(_dir, json));
    }

    [Fact]
    public void ResolverCaminhoDb_DataSourceSemEspaco_Funciona()
    {
        var json = """{"Provider":"Sqlite","Sqlite":{"ConnectionString":"DataSource=banco.db"}}""";
        Assert.Equal(Path.Combine(_dir, "banco.db"), UpdatePlan.ResolverCaminhoDb(_dir, json));
    }

    [Fact]
    public void ResolverCaminhoDb_CaminhoAbsoluto_Mantem()
    {
        var json = """{"Provider":"Sqlite","Sqlite":{"ConnectionString":"Data Source=C:\\dados\\finort.db"}}""";
        Assert.Equal(@"C:\dados\finort.db", UpdatePlan.ResolverCaminhoDb(_dir, json));
    }

    [Fact]
    public void ResolverCaminhoDb_JsonCorrompido_UsaPadrao()
    {
        Assert.Equal(Path.Combine(_dir, "finort.db"), UpdatePlan.ResolverCaminhoDb(_dir, "{isto não é json"));
    }

    [Fact]
    public void NomeBackup_FormatoEsperado()
    {
        var nome = UpdatePlan.NomeBackup("0.2.0", new DateTime(2026, 9, 2, 15, 30, 0));
        Assert.Equal("finort-preupdate-0.2.0-20260902_153000.db", nome);
    }

    [Fact]
    public void AplicarRetencao_MantemTresMaisRecentes()
    {
        var dir = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(dir);
        for (var i = 1; i <= 5; i++)
        {
            var arquivo = Path.Combine(dir, $"finort-preupdate-0.1.0-2026090{i}_120000.db");
            File.WriteAllText(arquivo, "x");
            File.SetCreationTime(arquivo, new DateTime(2026, 9, i, 12, 0, 0));
        }

        var apagados = UpdatePlan.AplicarRetencao(dir);

        Assert.Equal(2, apagados.Count);
        Assert.False(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db")));
        Assert.False(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260902_120000.db")));
        Assert.True(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260905_120000.db")));
    }

    [Fact]
    public void AplicarRetencao_ExcedenteAntigo_ApagaSidecarsWalShm()
    {
        var dir = Path.Combine(_dir, "backups-wal");
        Directory.CreateDirectory(dir);
        for (var i = 1; i <= 4; i++)
        {
            var arquivo = Path.Combine(dir, $"finort-preupdate-0.1.0-2026090{i}_120000.db");
            File.WriteAllText(arquivo, "x");
            File.SetCreationTime(arquivo, new DateTime(2026, 9, i, 12, 0, 0));
        }
        File.WriteAllText(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db-wal"), "w");
        File.WriteAllText(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db-shm"), "s");

        var apagados = UpdatePlan.AplicarRetencao(dir);

        Assert.Equal(3, apagados.Count);
        Assert.False(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db")));
        Assert.False(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db-wal")));
        Assert.False(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db-shm")));
        Assert.True(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260902_120000.db")));
        Assert.True(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260903_120000.db")));
        Assert.True(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260904_120000.db")));
    }

    [Fact]
    public void AplicarRetencao_PoucosArquivos_NaoApagaNada()
    {
        var dir = Path.Combine(_dir, "backups2");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db"), "x");
        File.WriteAllText(Path.Combine(dir, "outro.txt"), "x");

        var apagados = UpdatePlan.AplicarRetencao(dir);

        Assert.Empty(apagados);
        Assert.True(File.Exists(Path.Combine(dir, "finort-preupdate-0.1.0-20260901_120000.db")));
        Assert.True(File.Exists(Path.Combine(dir, "outro.txt")));
    }

    [Fact]
    public void MontarArgumentosSetup_ContemChavesSilenciosas()
    {
        var args = UpdatePlan.MontarArgumentosSetup();
        Assert.Contains("/VERYSILENT", args);
        Assert.Contains("/NORESTART", args);
        Assert.Contains("/SUPPRESSMSGBOXES", args);
        Assert.Contains("/CLOSEAPPLICATIONS", args);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
