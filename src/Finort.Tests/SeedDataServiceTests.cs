using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class SeedDataServiceTests
{
    [Fact]
    public async Task SeedAsync_PopulaSeisMesesInvestimentosEFaturasPagas()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            await new SeedDataService(db).SeedAsync();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioSeisMeses = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-5);
            Assert.True(await db.Lancamentos.CountAsync(l => l.Data >= inicioSeisMeses) > 50);

            Assert.Equal(6, await db.Investimentos.CountAsync());
            Assert.Contains(await db.Investimentos.ToListAsync(),
                i => i.Nome == "FII HGLG11" && i.Tipo == TipoInvestimento.Fii);
            Assert.Contains(await db.Investimentos.ToListAsync(),
                i => i.Nome == "Bitcoin" && i.Tipo == TipoInvestimento.Criptomoeda);
            Assert.True(await db.InvestimentosMovimentos.CountAsync() >= 10);

            var dividendos = await db.InvestimentosProventos
                .Where(p => p.Tipo == ProventoTipo.Dividendo)
                .ToListAsync();
            Assert.True(dividendos.Count >= 5);
            Assert.All(dividendos, p => Assert.True(p.Percentual > 0m));
            Assert.All(dividendos, p => Assert.NotNull(p.LancamentoId));
            var receita = await db.Lancamentos.FindAsync(dividendos[0].LancamentoId!.Value);
            Assert.NotNull(receita);
            Assert.Equal(LancamentoTipo.Receita, receita!.Tipo);
            Assert.True(receita.Confirmado);

            Assert.Equal(10, await db.Faturas.CountAsync(f => f.Fechada));
            Assert.Equal(10, await db.Lancamentos.CountAsync(l =>
                l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId != null && l.Valor > 0m));

            var ultimoDiaPassado = new DateOnly(hoje.Year, hoje.Month, 1).AddDays(-1);
            Assert.False(await db.Lancamentos.AnyAsync(l => l.Data <= ultimoDiaPassado && !l.Confirmado));

            var contas = await db.Contas.ToListAsync();
            foreach (var conta in contas)
            {
                var saldoFinal = await db.Lancamentos
                    .Where(l => l.ContaId == conta.Id)
                    .SumAsync(l => (decimal?)l.Valor) ?? 0m;
                Assert.True(saldoFinal >= 0m, $"saldo final de '{conta.Nome}' negativo: {saldoFinal}");

                var porDia = await db.Lancamentos
                    .Where(l => l.ContaId == conta.Id)
                    .GroupBy(l => l.Data)
                    .Select(g => new { Data = g.Key, Soma = g.Sum(l => l.Valor) })
                    .OrderBy(x => x.Data)
                    .ToListAsync();
                decimal acumulado = 0m;
                foreach (var dia in porDia)
                {
                    acumulado += dia.Soma;
                    Assert.True(acumulado >= 0m,
                        $"saldo acumulado de '{conta.Nome}' negativo em {dia.Data:dd/MM/yyyy}: {acumulado}");
                }
            }
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
