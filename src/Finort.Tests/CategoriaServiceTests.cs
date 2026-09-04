using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class CategoriaServiceTests
{
    private static async Task<(AppDbContext Db, string File, CategoriaService Service)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        return (db, file, new CategoriaService(db));
    }

    [Theory]
    [InlineData("extra", "Extra")]
    [InlineData("financiamento", "Financiamento")]
    [InlineData("manutenção", "Manutenção")]
    public async Task Seed_VemUniformizado(string nomeEsperadoMinusculoIgnorado, string nomeEsperado)
    {
        var (db, file, _) = await SetupAsync();
        try
        {
            Assert.Contains(db.Subcategorias.ToList(), s => s.Nome == nomeEsperado);
            Assert.DoesNotContain(db.Subcategorias.ToList(), s => s.Nome == nomeEsperadoMinusculoIgnorado && s.Nome != nomeEsperado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Subcategoria_CartaoDeCredito_ProtegidaExiste()
    {
        var (db, file, _) = await SetupAsync();
        try
        {
            var sub = db.Subcategorias.Single(s => s.IsProtected && s.Nome == "Cartão de crédito");
            Assert.Equal(db.Categorias.Single(c => c.Nome == "Financeiro").Id, sub.CategoriaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarCategoria_NormalizaPrimeiraLetra()
    {
        var (db, file, service) = await SetupAsync();
        try
        {
            var criada = await service.CriarCategoriaAsync("  mercado livre ");
            Assert.Equal("Mercado livre", criada.Nome);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarSubcategoria_NormalizaPrimeiraLetra()
    {
        var (db, file, service) = await SetupAsync();
        try
        {
            var financeiroId = db.Categorias.First(c => c.Nome == "Financeiro").Id;
            var sub = await service.AdicionarSubcategoriaAsync(financeiroId, "taxas bancárias");
            Assert.Equal("Taxas bancárias", sub.Nome);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarCategoriaAsync_E_ListarComSubcategorias()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new CategoriaService(db);
            var categoria = await service.CriarCategoriaAsync("Nova");
            await service.AdicionarSubcategoriaAsync(categoria.Id, "Sub");

            var lista = await service.ListarAsync();

            var carregada = Assert.Single(lista, c => c.Nome == "Nova");
            Assert.Contains(carregada.Subcategorias, s => s.Nome == "Sub");
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirCategoriaAsync_Protegida_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new CategoriaService(db);
            var financeiro = db.Categorias.First(c => c.Nome == "Financeiro");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExcluirCategoriaAsync(financeiro.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirSubcategoriaAsync_Protegida_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new CategoriaService(db);
            var transferencia = db.Subcategorias.First(s => s.Nome == "Transferência");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExcluirSubcategoriaAsync(transferencia.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ExcluirCategoriaAsync_EmUso_LancaExcecao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var categoriaService = new CategoriaService(db);
            var contaService = new ContaService(db);
            var lancamentoService = new LancamentoService(db);
            var categoria = await categoriaService.CriarCategoriaAsync("EmUso");
            var conta = await contaService.CriarAsync("Conta", null, null, null);

            await lancamentoService.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 10m,
                categoria.Id, null, null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => categoriaService.ExcluirCategoriaAsync(categoria.Id));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AtualizarSubcategoriaAsync_AlteraNome()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new CategoriaService(db);
            var categoria = db.Categorias.First(c => c.Nome == "Receita");
            var subcategoria = db.Subcategorias.First(s => s.CategoriaId == categoria.Id);

            await service.AtualizarSubcategoriaAsync(subcategoria, "Contrato");

            Assert.Equal("Contrato", db.Subcategorias.Single(s => s.Id == subcategoria.Id).Nome);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
