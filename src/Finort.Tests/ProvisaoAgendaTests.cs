using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ProvisaoAgendaTests
{
    private static Guid CategoriaId(AppDbContext db)
        => db.Categorias.First(c => c.Nome == "Contas de casa").Id;

    private static Provisao NovaProvisao(AppDbContext db, int dia, decimal valor) => new()
    {
        Onde = ProvisaoOnde.DebitoConta,
        Frequencia = ProvisaoFrequencia.Mensal,
        Dia = dia,
        Valor = valor,
        ValorVariante = false,
        CategoriaId = CategoriaId(db)
    };

    [Fact]
    public async Task ProjetarAsync_ProvisaoNuncaSincronizada_NaoProjeta()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.Provisoes.Add(NovaProvisao(db, dia: 10, valor: 200m));
            await db.SaveChangesAsync();
            var alvo = hoje.AddMonths(2);
            var inicioJanela = new DateOnly(alvo.Year, alvo.Month, 1);

            var projecoes = await ProvisaoAgenda.ProjetarAsync(db, inicioJanela, inicioJanela.AddMonths(1).AddDays(-1));

            Assert.Empty(projecoes);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ProjetarAsync_MesFechadoNaJanela_PulaPeriodo()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var alvo = hoje.AddMonths(1);
            db.MesesFechados.Add(new MesFechado { Ano = alvo.Year, Mes = alvo.Month, DataFechamento = DateTime.Now });
            var provisao = NovaProvisao(db, dia: 10, valor: 200m);
            provisao.UltimoMesLancado = hoje.Month;
            provisao.UltimoAnoLancado = hoje.Year;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();
            var inicioJanela = new DateOnly(alvo.Year, alvo.Month, 1);

            var projecoes = await ProvisaoAgenda.ProjetarAsync(db, inicioJanela, inicioJanela.AddMonths(1).AddDays(-1));

            Assert.Empty(projecoes);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ProjetarAsync_Dia31_ClampNoUltimoDia()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var alvo = hoje.AddMonths(1);
            var provisao = NovaProvisao(db, dia: 31, valor: 200m);
            provisao.UltimoMesLancado = hoje.Month;
            provisao.UltimoAnoLancado = hoje.Year;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();
            var inicioJanela = new DateOnly(alvo.Year, alvo.Month, 1);

            var projecoes = await ProvisaoAgenda.ProjetarAsync(db, inicioJanela, inicioJanela.AddMonths(1).AddDays(-1));

            var unica = Assert.Single(projecoes);
            Assert.Equal(DateTime.DaysInMonth(alvo.Year, alvo.Month), unica.Data.Day);
            Assert.Equal(provisao.Id, unica.Provisao.Id);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ProjetarAsync_JanelaDeVariosMeses_AcumulaPeriodosAlinhados()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var anoHoje = DateTime.Today.Year;
            var provisao = NovaProvisao(db, dia: 5, valor: 300m);
            provisao.Frequencia = ProvisaoFrequencia.Trimestral;
            provisao.UltimoMesLancado = 1;
            provisao.UltimoAnoLancado = anoHoje;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();
            var inicioJanela = new DateOnly(anoHoje, 1, 1);

            var projecoes = await ProvisaoAgenda.ProjetarAsync(db, inicioJanela, new DateOnly(anoHoje, 12, 31));

            Assert.Equal(new[] { 4, 7, 10 }, projecoes.Select(p => p.Data.Month).OrderBy(m => m).ToArray());
            Assert.All(projecoes, p => Assert.Equal(provisao.Id, p.Provisao.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
