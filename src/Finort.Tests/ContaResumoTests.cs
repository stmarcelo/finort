using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ContaResumoTests
{
    [Fact]
    public async Task ListarResumoAsync_CalculaSaldoRealEPrevisto()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var contaService = new ContaService(db);
            var lancamentoService = new LancamentoService(db);
            var conta = await contaService.CriarAsync("Conta", null, null, null);
            var renda = db.Categorias.First(c => c.Nome == "Receita");

            var r1 = await lancamentoService.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 100m, renda.Id, null, null);
            await lancamentoService.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 2), 30m, renda.Id, null, null);
            await lancamentoService.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 3), 50m, renda.Id, null, null);
            await lancamentoService.AlternarConfirmadoAsync(r1.Id);

            var resumo = await contaService.ListarResumoAsync();

            var linha = Assert.Single(resumo);
            Assert.Equal(100m, linha.SaldoReal);
            Assert.Equal(120m, linha.SaldoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
