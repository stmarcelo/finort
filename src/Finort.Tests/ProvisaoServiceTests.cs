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
            var lancamento = Assert.Single(db.Lancamentos.Where(l => l.ProvisaoId == salva.Id).ToList());
            Assert.Equal(-200m, lancamento.Valor);
            Assert.Equal(LancamentoTipo.Despesa, lancamento.Tipo);
            Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 10), lancamento.Data);
            Assert.Equal(hoje.Month, salva.UltimoMesLancado);
            Assert.Equal(hoje.Year, salva.UltimoAnoLancado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_NuncaLancada_LancaSoMesCorrente()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var provisao = NovaProvisao(conta);
            provisao.CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            await service.CriarAsync(provisao, lancarMesCorrente: false);

            var criados = await service.SincronizarAsync();

            Assert.Equal(1, criados);
            Assert.Single(db.Lancamentos.ToList());
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
    public async Task SincronizarAsync_MesFechado_PulaMasMarcaSincronizado()
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
                Mes = hoje.Month,
                Ano = hoje.Year,
                DataFechamento = DateTime.Now
            });
            await db.SaveChangesAsync();

            await service.SincronizarAsync();

            Assert.Empty(db.Lancamentos.ToList());
            var apos = await service.ObterAsync(provisao.Id);
            Assert.Equal(hoje.Month, apos!.UltimoMesLancado);
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

            var lancamento = Assert.Single(db.Lancamentos.ToList());
            Assert.Equal(LancamentoTipo.Receita, lancamento.Tipo);
            Assert.Equal(200m, lancamento.Valor);
            Assert.Equal(conta.Id, lancamento.ContaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SincronizarAsync_FaturaDoCartaoFechada_PulaLancamentoMasMarcaSincronizado()
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

            Assert.Equal(0, criados);
            Assert.Empty(db.Lancamentos.ToList());
            var apos = await service.ObterAsync(provisao.Id);
            Assert.Equal(hoje.Month, apos!.UltimoMesLancado);
            Assert.Equal(hoje.Year, apos.UltimoAnoLancado);
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
            Assert.Single(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
