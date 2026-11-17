using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ProjetoPdfTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ProjetoRelatorioService _svc;

    public ProjetoPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        _svc = new ProjetoRelatorioService(_ctx.Db, new TestWebHostEnvironment());
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

    private static ProjetoRelatorio RelatorioComPrazo(DateOnly? prazo, bool concluido = false, DateOnly? conclusao = null)
    {
        var projeto = new Projeto
        {
            Descricao = "Site",
            DataContratacao = new(2026, 1, 1),
            Prazo = prazo,
            ValorContratado = 1000m
        };
        return new ProjetoRelatorio(projeto, "Site", "Acme", new(2026, 1, 1), prazo, 1000m,
            concluido, conclusao, 0m, 0m, 0m, 0m,
            Array.Empty<RelatorioLinha>(), Array.Empty<PizzaFatia>(), null, null);
    }

    [Fact]
    public void LinhasCabecalho_ComPrazo_ContratacaoEValorEmLinhasSeparadas()
    {
        var linhas = ProjetoRelatorioService.LinhasCabecalho(RelatorioComPrazo(new(2026, 11, 30)));

        Assert.Equal(2, linhas.Count);
        Assert.Equal("Contratação: 01/01/2026 — Prazo: 30/11/2026", linhas[0]);
        Assert.Equal($"Valor contratado: R$ {1000m:N2}", linhas[1]);
    }

    [Fact]
    public void LinhasCabecalho_SemPrazo_NaoIncluiRotulo()
    {
        var linhas = ProjetoRelatorioService.LinhasCabecalho(RelatorioComPrazo(null));

        Assert.Equal(2, linhas.Count);
        Assert.DoesNotContain("Prazo", linhas[0]);
        Assert.DoesNotContain("Prazo", linhas[1]);
    }

    [Fact]
    public void LinhasCabecalho_Concluido_ConclusaoFicaNaLinhaDoValor()
    {
        var linhas = ProjetoRelatorioService.LinhasCabecalho(
            RelatorioComPrazo(new(2026, 11, 30), concluido: true, conclusao: new(2026, 10, 15)));

        Assert.Equal("Contratação: 01/01/2026 — Prazo: 30/11/2026", linhas[0]);
        Assert.Contains("Valor contratado:", linhas[1]);
        Assert.Contains("Conclusão: 15/10/2026", linhas[1]);
        Assert.DoesNotContain("Conclusão", linhas[0]);
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
