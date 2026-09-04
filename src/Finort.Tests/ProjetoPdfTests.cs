using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class ProjetoPdfTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ProjetoRelatorioService _svc;

    public ProjetoPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        _svc = new ProjetoRelatorioService(_ctx.Db);
    }

    [Fact]
    public async Task GerarPdf_ProduzArquivoValido()
    {
        var db = _ctx.Db;
        var pessoa = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Acme" }).Entity;
        db.SaveChanges();
        var projeto = await new ProjetoService(db).CriarAsync("PDF", new DateOnly(2026, 1, 1), 1000m, pessoa.Id);

        var bytes = await _svc.GerarPdfBytesAsync(projeto.Id);

        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 500);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes[..5]));
    }

    [Fact]
    public async Task GerarPdf_IdInexistente_RetornaNull()
    {
        Assert.Null(await _svc.GerarPdfBytesAsync(Guid.NewGuid()));
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
