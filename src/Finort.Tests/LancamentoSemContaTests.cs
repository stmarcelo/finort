using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoSemContaTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, Categoria Cat)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var cat = db.Categorias.First(c => c.Nome == "Receita");
        return (db, file, new LancamentoService(db), cat);
    }

    [Fact]
    public async Task CriarReceita_SemConta_CriaNaoConfirmado()
    {
        var (db, file, service, cat) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarReceitaAsync(null, new DateOnly(2026, 9, 10), 100m, cat.Id, null, null);
            Assert.Null(lancamento.ContaId);
            Assert.False(lancamento.Confirmado);
            Assert.Equal(100m, lancamento.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesa_SemConta_CriaNaoConfirmado()
    {
        var (db, file, service, cat) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(null, new DateOnly(2026, 9, 10), 50m, cat.Id, null, null);
            Assert.Null(lancamento.ContaId);
            Assert.Equal(-50m, lancamento.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarRecorrente_SemConta_CriaTodasSemConta()
    {
        var (db, file, service, cat) = await SetupAsync();
        try
        {
            var criados = await service.CriarRecorrenteAsync(LancamentoTipo.Receita, null, new DateOnly(2026, 9, 10), 100m,
                RecorrenciaFrequencia.Mensal, 3, cat.Id, null, null);
            Assert.Equal(3, criados.Count);
            Assert.All(criados, l => Assert.Null(l.ContaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Listar_SomenteSemConta_RetornaSoReceitaEDespesaSemContaECartao()
    {
        var (db, file, service, cat) = await SetupAsync();
        try
        {
            var conta = new Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            var cartao = new CartaoCredito
            {
                Banco = "Nu", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
                DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            var mes = new DateOnly(2026, 9, 10);
            await service.CriarReceitaAsync(null, mes, 100m, cat.Id, null, null);
            await service.CriarDespesaAsync(null, mes, 50m, cat.Id, null, null);
            await service.CriarReceitaAsync(conta.Id, mes, 200m, cat.Id, null, null);
            db.Lancamentos.Add(new Lancamento
            {
                Data = mes, Tipo = LancamentoTipo.Despesa, Valor = -30m,
                CartaoCreditoId = cartao.Id, CategoriaId = cat.Id
            });
            await db.SaveChangesAsync();

            var lista = await service.ListarAsync(mes: 9, ano: 2026, somenteSemConta: true);

            Assert.Equal(2, lista.Count);
            Assert.All(lista, l => Assert.Null(l.ContaId));
            Assert.All(lista, l => Assert.Null(l.CartaoCreditoId));
            Assert.Contains(lista, l => l.Tipo == LancamentoTipo.Receita && l.Valor == 100m);
            Assert.Contains(lista, l => l.Tipo == LancamentoTipo.Despesa && l.Valor == -50m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Atualizar_PodeRemoverConta()
    {
        var (db, file, service, cat) = await SetupAsync();
        try
        {
            var conta = new Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var lancamento = await service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 9, 10), 100m, cat.Id, null, null);
            await service.AtualizarReceitaDespesaAsync(lancamento.Id, null, new DateOnly(2026, 9, 10), 100m, cat.Id, null, null);
            Assert.Null((await service.ObterAsync(lancamento.Id))!.ContaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
