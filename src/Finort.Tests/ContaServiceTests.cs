using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ContaServiceTests
{
    [Fact]
    public async Task CriarAsync_PersisteELista()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new ContaService(db);
            var conta = await service.CriarAsync("Nubank", "Nubank", "0001", "12345-6");

            var carregada = Assert.Single(await service.ListarAsync());
            Assert.Equal(conta.Id, carregada.Id);
            Assert.Equal("Nubank", carregada.Nome);
            Assert.Equal("12345-6", carregada.ContaEDigito);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirAsync_ComLancamento_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var contaService = new ContaService(db);
            var lancamentoService = new LancamentoService(db);
            var conta = await contaService.CriarAsync("Conta", null, null, null);
            var categoria = db.Categorias.First(c => c.Nome == "Receita");

            await lancamentoService.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 50m,
                categoria.Id, null, null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => contaService.ExcluirAsync(conta.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirAsync_ComCartaoOuProvisaoVinculados_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var contaService = new ContaService(db);
            var cartaoService = new CartaoCreditoService(db);
            var contaCartao = await contaService.CriarAsync("Conta cartão", null, null, null);
            await cartaoService.CriarAsync("Nubank", "1234", 5, 10, 1000m, contaCartao.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => contaService.ExcluirAsync(contaCartao.Id));

            var contaProvisao = await contaService.CriarAsync("Conta provisão", null, null, null);
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 10,
                Valor = 100m,
                ValorVariante = false,
                ContaId = contaProvisao.Id,
                CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => contaService.ExcluirAsync(contaProvisao.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AtualizarAsync_AlteraNome()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new ContaService(db);
            var conta = await service.CriarAsync("Antes", null, null, null);

            await service.AtualizarAsync(conta, "Depois", "Banco", "0001", "12345-6");

            var carregada = await service.ObterAsync(conta.Id);
            Assert.NotNull(carregada);
            Assert.Equal("Depois", carregada!.Nome);
            Assert.Equal("12345-6", carregada.ContaEDigito);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
