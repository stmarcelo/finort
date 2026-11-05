using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoFiltroCategoriaTests
{
    [Fact]
    public async Task ListarAsync_PorCategoria_TrazDaCategoriaETodaASubcategoria()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var mercado = db.Categorias.First(c => c.Nome == "Alimentação");
            var sub = db.Subcategorias.First(s => s.Nome == "Mercado / Feira / Açougue");
            var energia = db.Categorias.First(c => c.Nome == "Contas de casa");
            var servico = new LancamentoService(db);

            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 10, 5), Tipo = LancamentoTipo.Despesa, Valor = -100m,
                CategoriaId = mercado.Id, SubcategoriaId = sub.Id, Confirmado = true
            });
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 10, 6), Tipo = LancamentoTipo.Despesa, Valor = -50m,
                CategoriaId = energia.Id, Confirmado = true
            });
            db.SaveChanges();

            var daCategoria = await servico.ListarAsync(categoriaId: mercado.Id);
            Assert.Single(daCategoria);
            Assert.Equal(100m, Math.Abs(daCategoria[0].Valor));

            var semFiltro = await servico.ListarAsync();
            Assert.Equal(2, semFiltro.Count);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
