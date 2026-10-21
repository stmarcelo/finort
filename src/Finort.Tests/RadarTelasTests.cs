using Finort.Components.Dialogs;
using Finort.Components.Pages;
using Finort.Models.Financeiro;

namespace Finort.Tests;

/// <summary>
/// Regras de texto das telas do Radar. O conteúdo vem do arquivo .razor do projeto web —
/// a mesma técnica de <c>TestWebHostEnvironment</c>, que resolve o caminho do projeto
/// a partir da pasta de saída dos testes.
/// </summary>
public class RadarTelasTests
{
    private static string FonteDoWebApp(params string[] caminho)
    {
        var raiz = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "aspnet"));
        return File.ReadAllText(Path.Combine(new[] { raiz }.Concat(caminho).ToArray()));
    }

    [Fact]
    public void Telas_LimitaOsDiasDaComparacao()
    {
        // Com corte dia 29-31 e fevereiro na base, a comparação é de 31 dias contra 28:
        // a legenda precisa dizer que o número de dias é limitado pelos dias de cada mês.
        const string frase = "(limitado pelos dias de cada mês)";

        var pagina = FonteDoWebApp("Components", "Pages", "Radar.razor");
        var dialog = FonteDoWebApp("Components", "Dialogs", "RadarCategoriaDialog.razor");

        Assert.Contains(frase, pagina);
        Assert.Contains(frase, dialog);
    }

    [Fact]
    public void Dialog_UsaOResponsavelDoServico()
    {
        // A regra de quem explica a variação mora só no serviço: o dialog usa o campo
        // devolvido por ele e não recalcula.
        var dialog = FonteDoWebApp("Components", "Dialogs", "RadarCategoriaDialog.razor");

        Assert.Contains("SubcategoriaResponsavel", dialog);
        Assert.DoesNotContain("SelecionarResponsavel", dialog);
    }

    [Fact]
    public void Telas_SemBaseDeComparacao_NaoRotulamMesesZero()
    {
        // Sem nenhum mês fechado com lançamentos a base é 0: "Média 0m" não é uma
        // informação que o usuário consiga ler — o rótulo é só "Média".
        Assert.Equal("Média", Radar.RotuloMediaTexto(0));
        Assert.DoesNotContain("0m", Radar.RotuloMediaTexto(0));
        Assert.Equal("Média 3m", Radar.RotuloMediaTexto(3));

        Assert.Equal("Média", RadarCategoriaDialog.RotuloMediaTexto(0));
        Assert.DoesNotContain("0m", RadarCategoriaDialog.RotuloMediaTexto(0));
        Assert.Equal("Média 3m", RadarCategoriaDialog.RotuloMediaTexto(3));
    }

    [Fact]
    public void Telas_SemBaseDeComparacao_VariacaoDoDialogNaoFalaDeMesesZero()
    {
        // "Variação vs. média de 0 meses" não existe: sem base a variação tem rótulo próprio
        // (o valor já diz "sem gastos anteriores").
        Assert.Equal("Variação (sem base de comparação)", RadarCategoriaDialog.RotuloVariacaoTexto(0));
        Assert.DoesNotContain("0 meses", RadarCategoriaDialog.RotuloVariacaoTexto(0));
        Assert.Equal("Variação vs. média de 3 meses", RadarCategoriaDialog.RotuloVariacaoTexto(3));
        Assert.Equal("Variação vs. média de 1 mês", RadarCategoriaDialog.RotuloVariacaoTexto(1));
    }

    [Fact]
    public void Telas_DialogMostraORotuloDaVariacaoSemBase()
    {
        // A frase que o usuário lê no drill-down sem base de comparação.
        var dialog = FonteDoWebApp("Components", "Dialogs", "RadarCategoriaDialog.razor");

        Assert.Contains("Variação (sem base de comparação)", dialog);
    }

    [Fact]
    public void Dialog_SemBaseDeComparacao_NaoExibeFrasseDoResponsavel()
    {
        // Com 0 meses de base a página não fala em média: o drill-down também não pode
        // afirmar quem explica a variação — nem destacar ninguém como "principal".
        var sub = new RadarVariacaoSubcategoria(Guid.NewGuid(), "Delivery", 500m, 0m, 500m, 1m, 1m);
        var detalhe = new RadarDetalheCategoria(
            Guid.NewGuid(), "Alimentação", 500m, 0m, 1m, RadarTendencia.Indefinida,
            Array.Empty<RadarPontoMensal>(), new[] { sub })
        {
            SubcategoriaResponsavel = sub,
            MesesBase = 0
        };

        Assert.Null(RadarCategoriaDialog.ResponsavelExibido(detalhe));

        // Com base, o responsável continua sendo exibido (nenhuma regressão).
        var comBase = detalhe with { MesesBase = 3 };
        Assert.Same(sub, RadarCategoriaDialog.ResponsavelExibido(comBase));
    }

    [Fact]
    public void Telas_GraficoMostraConsumoEInvestidoComoSeriesSeparadas()
    {
        // As três séries do gráfico são disjuntas: Receitas, Consumo (sem o aporte) e
        // Investido. "Despesas" guardaria o consumo + o aporte — o usuário veria dois
        // valores paralelos, um contendo o outro.
        var pagina = FonteDoWebApp("Components", "Pages", "Radar.razor");

        Assert.Contains("Name = \"Receitas\"", pagina);
        Assert.Contains("Name = \"Consumo\"", pagina);
        Assert.Contains("Name = \"Investido\"", pagina);
        Assert.DoesNotContain("Name = \"Despesas\"", pagina);
    }

    [Fact]
    public void Telas_CartaoDeInvestidoSemMediaDeAportes_NaoMostraOPercentualSentinela()
    {
        // Sem aporte na base a média é R$ 0,00: o 100% de VariacaoPercentual é o sentinela
        // da média zero, não uma medição. O card não pode anunciar um percentual de variação.
        var semMedia = Radar.LegendaInvestidoTexto(
            mediaInvestido: 0m, variacao: 1m, mesesBase: 3, totalInvestido: 900m);

        Assert.DoesNotContain("%", semMedia);
        Assert.DoesNotContain("vs. média", semMedia);
        Assert.Contains("R$ 900,00 investidos no mês", semMedia);

        // Com média de aportes na base a variação continua sendo mostrada.
        var comMedia = Radar.LegendaInvestidoTexto(
            mediaInvestido: 500m, variacao: 0.8m, mesesBase: 3, totalInvestido: 900m);

        Assert.Contains("80,0", comMedia);
        Assert.Contains("vs. média de 3 meses", comMedia);
    }

    [Fact]
    public void Telas_CartaoDeConsumoSemMediaDeConsumo_NaoMostraOPercentualSentinela()
    {
        // A base tem meses com lançamentos, mas consumo zero: o 100% de VariacaoPercentual
        // é o sentinela da média zero, não uma medição. O card não pode anunciar um
        // percentual de variação.
        var semMedia = Radar.LegendaConsumoTexto(mediaConsumo: 0m, variacao: 1m, mesesBase: 3);

        Assert.DoesNotContain("%", semMedia);
        Assert.DoesNotContain("vs. média", semMedia);
        Assert.Contains("sem consumo nos meses anteriores", semMedia);

        // Com média de consumo na base a variação continua sendo mostrada.
        var comMedia = Radar.LegendaConsumoTexto(mediaConsumo: 400m, variacao: 0.25m, mesesBase: 3);

        Assert.Contains("25,0", comMedia);
        Assert.Contains("vs. média de 3 meses", comMedia);
    }

    [Fact]
    public void Telas_CartaoDeReceitasSemMediaDeReceitas_NaoMostraOPercentualSentinela()
    {
        // A base tem meses com lançamentos, mas nenhuma receita: o 100% de VariacaoPercentual
        // é o sentinela da média zero, não uma medição. O card não pode anunciar um
        // percentual de variação.
        var semMedia = Radar.LegendaReceitasTexto(mediaReceitas: 0m, variacao: 1m, mesesBase: 3);

        Assert.DoesNotContain("%", semMedia);
        Assert.DoesNotContain("vs. média", semMedia);
        Assert.Contains("sem receitas nos meses anteriores", semMedia);

        // Com média de receitas na base a variação continua sendo mostrada.
        var comMedia = Radar.LegendaReceitasTexto(mediaReceitas: 4000m, variacao: 0.25m, mesesBase: 3);

        Assert.Contains("25,0", comMedia);
        Assert.Contains("vs. média de 3 meses", comMedia);
    }

    [Fact]
    public void Telas_CartaoDeComprometimentoSemReceita_NaoMostraZeroPorCento()
    {
        // Sem receita no mês a taxa de comprometimento é indefinida — não há denominador.
        // "0,0 %" diria que o usuário não comprometeu nada, e o número não existe.
        var semReceita = Radar.TextoComprometimento(totalReceitas: 0m, taxa: 0m);

        Assert.Equal("—", semReceita);
        Assert.DoesNotContain("%", semReceita);

        // Com receita no mês a taxa é uma medição e aparece como sempre.
        var comReceita = Radar.TextoComprometimento(totalReceitas: 1000m, taxa: 0.65m);

        Assert.Contains("65,0", comReceita);
        Assert.Contains("%", comReceita);
    }

    [Fact]
    public void Telas_TabelaDeMudancas_SemConsumoNoMes_NaoMostraParticipacaoZeroPorCento()
    {
        // Sem consumo no mês a participação é indefinida — não há denominador. "0,0 %"
        // diria que a categoria pesou no consumo quando não houve consumo nenhum.
        var semConsumo = Radar.TextoParticipacao(totalConsumo: 0m, participacao: 0m);

        Assert.Equal("—", semConsumo);
        Assert.DoesNotContain("%", semConsumo);

        // Com consumo no mês a participação é uma medição e aparece como sempre.
        var comConsumo = Radar.TextoParticipacao(totalConsumo: 4000m, participacao: 0.25m);

        Assert.Contains("25,0", comConsumo);
        Assert.Contains("%", comConsumo);
    }

    [Fact]
    public void Telas_RadarMostraOConsumoEOInvestimento()
    {
        // O Radar mede consumo: o card é "Consumo no mês" e o valor investido tem
        // card próprio — investir não é despesa, é uma boa notícia.
        var pagina = FonteDoWebApp("Components", "Pages", "Radar.razor");

        Assert.Contains("Consumo no mês", pagina);
        Assert.Contains("TotalConsumo", pagina);
        Assert.DoesNotContain("Despesas no mês", pagina);
        // A tabela lista consumo: sem consumo não há linhas — e o texto diz isso.
        Assert.Contains("Sem consumo no período analisado.", pagina);
        Assert.Contains("Investido no mês", pagina);
        Assert.Contains("TotalInvestido", pagina);
        // O comprometimento deixa de ser ambíguo: é o consumo sobre as receitas.
        Assert.Contains("consumo sobre as receitas", pagina);
        // O gráfico ganha a série do aporte.
        Assert.Contains("Name = \"Investido\"", pagina);
        Assert.Contains("m.Investido", pagina);
    }
}
