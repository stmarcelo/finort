using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ProvisaoServiceTests
{
    private static async Task<(AppDbContext Db, string File, ProvisaoService Service, Conta Conta)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        await db.SaveChangesAsync();
        return (db, file, new ProvisaoService(db), conta);
    }

    private static Provisao NovaProvisao(Conta conta) => new()
    {
        Onde = ProvisaoOnde.DebitoConta,
        Frequencia = ProvisaoFrequencia.Mensal,
        Dia = 10,
        Valor = 200m,
        ValorVariante = false,
        ContaId = conta.Id
    };

    [Fact]
    public async Task CriarAsync_ComLancarAgora_CriaLancamentoEMarcaUltimo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;

            var salva = await service.CriarAsync(provisao, lancarMesCorrente: true);

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lancamentos = db.Lancamentos.Where(l => l.ProvisaoId == salva.Id).OrderBy(l => l.Data).ToList();
            Assert.Equal(2, lancamentos.Count);
            Assert.All(lancamentos, lancamento =>
            {
                Assert.Equal(-200m, lancamento.Valor);
                Assert.Equal(LancamentoTipo.Despesa, lancamento.Tipo);
                Assert.Equal(10, lancamento.Data.Day);
            });
            Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 10), lancamentos[0].Data);
            Assert.Equal(new DateOnly(hoje.AddMonths(1).Year, hoje.AddMonths(1).Month, 10), lancamentos[1].Data);
            Assert.Equal(hoje.AddMonths(1).Month, salva.UltimoMesLancado);
            Assert.Equal(hoje.AddMonths(1).Year, salva.UltimoAnoLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_NuncaLancada_LancaMesCorrenteEProximo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            await service.CriarAsync(provisao, lancarMesCorrente: false);

            var criados = await service.SincronizarAsync();

            Assert.Equal(2, criados);
            Assert.Equal(2, db.Lancamentos.Count());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_JaLancada_TrimestralAvancaDeTresEmTres()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            provisao.Frequencia = ProvisaoFrequencia.Trimestral;
            provisao.UltimoMesLancado = 1;
            provisao.UltimoAnoLancado = hoje.Year;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();

            await service.SincronizarAsync();

            var datas = db.Lancamentos.Select(l => l.Data).OrderBy(d => d).ToList();
            Assert.Contains(datas, d => d.Month == 4 && d.Year == hoje.Year);
            Assert.DoesNotContain(datas, d => d.Month == 2 && d.Year == hoje.Year);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_MesFechado_PulaMesFechadoELancaProximo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var mesAnterior = hoje.AddMonths(-1);

            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            provisao.UltimoMesLancado = mesAnterior.Month;
            provisao.UltimoAnoLancado = mesAnterior.Year;
            db.Provisoes.Add(provisao);
            db.MesesFechados.Add(new MesFechado
            {
                ContaId = conta.Id,
                Mes = hoje.Month,
                Ano = hoje.Year,
                DataFechamento = DateTime.Now
            });
            await db.SaveChangesAsync();

            await service.SincronizarAsync();

            // Current month is fechado, but next month should be launched
            var lancamentos = db.Lancamentos.ToList();
            Assert.Single(lancamentos);
            var proximoMes = hoje.AddMonths(1);
            Assert.Equal(proximoMes.Month, lancamentos[0].Data.Month);
            var apos = await service.ObterAsync(provisao.Id);
            Assert.Equal(proximoMes.Month, apos!.UltimoMesLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_Receita_GravaPositivaNaConta()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var provisao = NovaProvisao(conta);
            provisao.Onde = ProvisaoOnde.Receita;
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Receita").Id;
            await service.CriarAsync(provisao, lancarMesCorrente: false);

            await service.SincronizarAsync();

            var lancamentos = db.Lancamentos.ToList();
            Assert.Equal(2, lancamentos.Count);
            Assert.All(lancamentos, l => Assert.Equal(LancamentoTipo.Receita, l.Tipo));
            Assert.All(lancamentos, l => Assert.Equal(200m, l.Valor));
            Assert.All(lancamentos, l => Assert.Equal(conta.Id, l.ContaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_FaturaDoCartaoFechada_PulaMesFechadoELancaProximo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cartao = new CartaoCredito
            {
                Banco = "Nubank",
                Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5,
                DiaVencimento = 10,
                Limite = 1000m,
                Ativo = true,
                ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            var provisao = NovaProvisao(conta);
            provisao.Onde = ProvisaoOnde.DebitoCartao;
            provisao.CartaoCreditoId = cartao.Id;
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            db.Provisoes.Add(provisao);
            db.Faturas.Add(new Fatura
            {
                CartaoCreditoId = cartao.Id,
                AnoReferencia = hoje.Year,
                MesReferencia = hoje.Month,
                ValorTotal = 0m,
                Fechada = true,
                DataFechamento = DateTime.Now
            });
            await db.SaveChangesAsync();

            var criados = await service.SincronizarAsync();

            // Current month fatura fechada, but next month should be launched
            Assert.Equal(1, criados);
            var lancamentos = db.Lancamentos.ToList();
            Assert.Single(lancamentos);
            var proximoMes = hoje.AddMonths(1);
            Assert.Equal(proximoMes.Month, lancamentos[0].Data.Month);
            Assert.Equal(
                new DateOnly(proximoMes.Year, proximoMes.Month, cartao.DiaVencimento),
                lancamentos[0].DataVencimentoCartao);
            var apos = await service.ObterAsync(provisao.Id);
            Assert.Equal(proximoMes.Month, apos!.UltimoMesLancado);
            Assert.Equal(proximoMes.Year, apos.UltimoAnoLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_MensalComUltimoMesAnterior_GaranteCorrenteEProximo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cartao = new CartaoCredito
            {
                Banco = "Cartão 1", Ultimos4Digitos = "0001",
                MelhorDiaCompra = 27, DiaVencimento = 4, Limite = 1000m, Ativo = true,
                ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            var provisao = NovaProvisao(conta);
            provisao.Onde = ProvisaoOnde.DebitoCartao;
            provisao.CartaoCreditoId = cartao.Id;
            provisao.Dia = 1;
            provisao.Valor = 19.90m;
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var mesAnterior = hoje.AddMonths(-1);
            provisao.UltimoMesLancado = mesAnterior.Month;
            provisao.UltimoAnoLancado = mesAnterior.Year;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();

            await service.SincronizarAsync();

            var lancamentos = db.Lancamentos.Where(l => l.CartaoCreditoId == cartao.Id).OrderBy(l => l.Data).ToList();
            Assert.Equal(2, lancamentos.Count);
            Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 1), lancamentos[0].Data);
            Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 4), lancamentos[0].DataVencimentoCartao);
            var proximo = hoje.AddMonths(1);
            Assert.Equal(new DateOnly(proximo.Year, proximo.Month, 1), lancamentos[1].Data);
            Assert.Equal(new DateOnly(proximo.Year, proximo.Month, 4), lancamentos[1].DataVencimentoCartao);
            Assert.Equal(proximo.Month, (await service.ObterAsync(provisao.Id))!.UltimoMesLancado);
            Assert.Equal(proximo.Year, (await service.ObterAsync(provisao.Id))!.UltimoAnoLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_MensalComUltimoLancamentoNoMesAtual_LancaSomenteProximo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            provisao.UltimoMesLancado = hoje.Month;
            provisao.UltimoAnoLancado = hoje.Year;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();

            var criados = await service.SincronizarAsync();

            Assert.Equal(1, criados);
            var proximo = hoje.AddMonths(1);
            var lancamento = Assert.Single(db.Lancamentos);
            Assert.Equal(new DateOnly(proximo.Year, proximo.Month, provisao.Dia), lancamento.Data);
            Assert.Equal(proximo.Month, (await service.ObterAsync(provisao.Id))!.UltimoMesLancado);
            Assert.Equal(proximo.Year, (await service.ObterAsync(provisao.Id))!.UltimoAnoLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_CartaoNaoDuplicaLancamentoComMesmosDados()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cartao = new CartaoCredito
            {
                Banco = "Nubank", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true,
                ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            var categoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var provisao = NovaProvisao(conta);
            provisao.Onde = ProvisaoOnde.DebitoCartao;
            provisao.CartaoCreditoId = cartao.Id;
            provisao.CategoriaId = categoriaId;
            provisao.UltimoMesLancado = hoje.AddMonths(-1).Month;
            provisao.UltimoAnoLancado = hoje.AddMonths(-1).Year;
            db.Provisoes.Add(provisao);
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(hoje.Year, hoje.Month, provisao.Dia),
                Tipo = LancamentoTipo.Despesa,
                Valor = -provisao.Valor,
                CategoriaId = categoriaId,
                CartaoCreditoId = cartao.Id,
                DataVencimentoCartao = CartaoCreditoService.CalcularVencimento(cartao, new DateOnly(hoje.Year, hoje.Month, provisao.Dia))
            });
            await db.SaveChangesAsync();

            var criados = await service.SincronizarAsync();

            Assert.Equal(1, criados);
            Assert.Equal(2, db.Lancamentos.Count(l => l.CartaoCreditoId == cartao.Id));
            Assert.Single(db.Lancamentos.Where(l => l.CartaoCreditoId == cartao.Id && l.Data.Month == hoje.Month));
            Assert.Single(db.Lancamentos.Where(l => l.CartaoCreditoId == cartao.Id && l.Data.Month == hoje.AddMonths(1).Month));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirAsync_RemoveCadastroEMantemLancamentos()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            await service.CriarAsync(provisao, lancarMesCorrente: true);

            await service.ExcluirAsync(provisao.Id);

            Assert.Null(await service.ObterAsync(provisao.Id));
            Assert.Equal(2, db.Lancamentos.Count());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
