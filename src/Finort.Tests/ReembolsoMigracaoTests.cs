using Finort.Models.Financeiro;

namespace Finort.Tests;

public class ReembolsoMigracaoTests
{
    [Fact]
    public void Lancamento_NaoPossui_ReembolsoCategoria()
    {
        var prop = typeof(Lancamento).GetProperty("ReembolsoCategoriaId");
        Assert.Null(prop);
    }

    [Fact]
    public void Lancamento_NaoPossui_ReembolsoSubcategoria()
    {
        var prop = typeof(Lancamento).GetProperty("ReembolsoSubcategoriaId");
        Assert.Null(prop);
    }

    [Fact]
    public void Lancamento_NaoPossui_ReembolsoId()
    {
        var prop = typeof(Lancamento).GetProperty("ReembolsoId");
        Assert.Null(prop);
    }
}
