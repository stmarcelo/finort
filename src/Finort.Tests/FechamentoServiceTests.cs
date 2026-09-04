using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class FechamentoServiceTests
{
    private static Guid CategoriaId(AppDbContext db)
        => db.Categorias.First(c => c.Nome == "Contas de casa").Id;

    private static async Task<(AppDbContext Db, string File, Conta Conta)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Banco" };
        db.Contas.Add(conta);
        await db.SaveChangesAsync();
        return (db, file, conta);
    }

    private static Lancamento Novo(DateOnly data, decimal valor, bool confirmado, Guid cat, Guid contaId)
        => new() { Data = data, Valor = valor, Confirmado = confirmado, CategoriaId = cat, ContaId = contaId };

    [Fact]
    public async Task ObterConferencia_SomaSomenteConfirmadosDaConta()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cat = CategoriaId(db);
            db.Lancamentos.AddRange(
                Novo(inicioMes, 100m, true, cat, conta.Id),
                Novo(inicioMes.AddDays(1), -30m, true, cat, conta.Id),
                Novo(inicioMes.AddMonths(1), 500m, false, cat, conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            var c = await service.ObterConferenciaAsync(conta.Id, hoje.Year, hoje.Month);

            Assert.Equal(70m, c.SaldoAcumulado);
            Assert.False(c.TemPendencias);
            Assert.False(c.MesFechado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterConferencia_DetectaPendenciasNoMesInclusiveCartao()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cat = CategoriaId(db);
            var cartao = new CartaoCredito
            {
                Banco = "Nubank",
                Ultimos4Digitos = "4321",
                MelhorDiaCompra = 1,
                DiaVencimento = 10,
                Limite = 5000m,
                Ativo = true
            };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            db.Lancamentos.AddRange(
                new Lancamento { Data = inicioMes.AddDays(4), Valor = -80m, Confirmado = false, CategoriaId = cat, ContaId = conta.Id },
                new Lancamento { Data = inicioMes.AddDays(5), Valor = -60m, Confirmado = false, CategoriaId = cat, CartaoCreditoId = cartao.Id });
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            var c = await service.ObterConferenciaAsync(conta.Id, hoje.Year, hoje.Month);

            Assert.True(c.TemPendencias);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_MesJaFechado_LancaInvalidOperationException()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.MesesFechados.Add(new MesFechado { Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.FecharAsync(conta.Id, hoje.Year, hoje.Month, 0m));

            Assert.Contains("já está fechado", ex.Message);
            Assert.Equal(1, db.MesesFechados.Count());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_MesComPendencias_LancaENaoFecha()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            db.Lancamentos.Add(Novo(inicioMes.AddDays(2), -90m, false, CategoriaId(db), conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.FecharAsync(conta.Id, hoje.Year, hoje.Month, 0m));

            Assert.Contains("não confirmado", ex.Message);
            Assert.Empty(db.MesesFechados.ToList());
            Assert.Single(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_ComDiferencaNegativa_CriaAcertoConfirmado()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            db.Lancamentos.Add(Novo(inicioMes.AddDays(1), 200m, true, CategoriaId(db), conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            await service.FecharAsync(conta.Id, hoje.Year, hoje.Month, saldoReal: 150m);

            var ultimoDia = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));
            var acerto = Assert.Single(db.Lancamentos.Where(l => l.Categoria.Nome == "Acerto de saldo").ToList());
            Assert.Equal(LancamentoTipo.Despesa, acerto.Tipo);
            Assert.Equal(-50m, acerto.Valor);
            Assert.True(acerto.Confirmado);
            Assert.Equal(ultimoDia, acerto.Data);
            Assert.Equal(conta.Id, acerto.ContaId);
            Assert.NotNull(acerto.SubcategoriaId);
            Assert.True(db.MesesFechados.Any(m => m.Ano == hoje.Year && m.Mes == hoje.Month));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_ComDiferencaPositiva_CriaReceitaDeAcerto()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            db.Lancamentos.Add(Novo(inicioMes.AddDays(1), 100m, true, CategoriaId(db), conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            await service.FecharAsync(conta.Id, hoje.Year, hoje.Month, saldoReal: 175m);

            var acerto = Assert.Single(db.Lancamentos.Where(l => l.Categoria.Nome == "Acerto de saldo").ToList());
            Assert.Equal(LancamentoTipo.Receita, acerto.Tipo);
            Assert.Equal(75m, acerto.Valor);
            Assert.True(acerto.Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_SemDiferenca_NaoCriaLancamentoEFecha()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            db.Lancamentos.Add(Novo(inicioMes.AddDays(1), 300m, true, CategoriaId(db), conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            await service.FecharAsync(conta.Id, hoje.Year, hoje.Month, saldoReal: 300m);

            Assert.Single(db.Lancamentos.ToList());
            Assert.True(db.MesesFechados.Any(m => m.Ano == hoje.Year && m.Mes == hoje.Month));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fechar_CascataFechaTodosOsMesesAnterioresAbertos()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var tresMesesAtras = inicioMes.AddMonths(-3);
            db.Lancamentos.Add(Novo(tresMesesAtras, 1000m, true, CategoriaId(db), conta.Id));
            await db.SaveChangesAsync();
            var service = new FechamentoService(db);

            await service.FecharAsync(conta.Id, hoje.Year, hoje.Month, saldoReal: 1000m);

            var esperados = new HashSet<int>();
            for (var p = new DateOnly(tresMesesAtras.Year, tresMesesAtras.Month, 1); p <= inicioMes; p = p.AddMonths(1))
                esperados.Add(p.Year * 12 + p.Month);
            var reais = db.MesesFechados.ToList().Select(m => m.Ano * 12 + m.Mes).ToHashSet();
            Assert.True(esperados.SetEquals(reais));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
