using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class PessoaServiceTests
{
    [Fact]
    public async Task CriarAsync_PersisteEPodeListar()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new PessoaService(db);
            var pessoa = await service.CriarAsync("João", "#FF0000", "nota");

            var lista = await service.ListarAsync();

            var carregada = Assert.Single(lista);
            Assert.Equal(pessoa.Id, carregada.Id);
            Assert.Equal("João", carregada.Nome);
            Assert.Equal("#FF0000", carregada.CorDeExibicao);
            Assert.Equal("nota", carregada.Observacao);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AtualizarAsync_AlteraDados()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new PessoaService(db);
            var pessoa = await service.CriarAsync("Antes", null, null);

            await service.AtualizarAsync(pessoa, "Depois", "#00FF00", "obs");

            var carregada = await service.ObterAsync(pessoa.Id);
            Assert.NotNull(carregada);
            Assert.Equal("Depois", carregada!.Nome);
            Assert.Equal("#00FF00", carregada.CorDeExibicao);
            Assert.Equal("obs", carregada.Observacao);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirAsync_SemLancamentos_Remove()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new PessoaService(db);
            var pessoa = await service.CriarAsync("X", null, null);

            await service.ExcluirAsync(pessoa.Id);

            Assert.Null(await service.ObterAsync(pessoa.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirAsync_ComProvisaoVinculada_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var pessoaService = new PessoaService(db);
            var pessoa = await pessoaService.CriarAsync("Pessoa", null, null);
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 10,
                Valor = 100m,
                ValorVariante = false,
                PessoaId = pessoa.Id,
                CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => pessoaService.ExcluirAsync(pessoa.Id));
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
            var pessoaService = new PessoaService(db);
            var contaService = new ContaService(db);
            var lancamentoService = new LancamentoService(db);
            var pessoa = await pessoaService.CriarAsync("Pessoa", null, null);
            var conta = await contaService.CriarAsync("Conta", null, null, null);
            var categoria = db.Categorias.First(c => c.Nome == "Receita");
            var subcategoria = db.Subcategorias.First(s => s.CategoriaId == categoria.Id);

            await lancamentoService.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 100m,
                categoria.Id, subcategoria.Id, pessoa.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => pessoaService.ExcluirAsync(pessoa.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
