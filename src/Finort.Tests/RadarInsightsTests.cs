using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class RadarInsightsTests
{
    private static RadarVariacaoCategoria Categoria(
        decimal atual, decimal media,
        RadarVariacaoSubcategoria? sub = null,
        RadarTendencia tendencia = RadarTendencia.Indefinida,
        int persistencia = 0)
        => new(Guid.NewGuid(), "Alimentação", atual, media,
            atual - media, media == 0m ? 0m : (atual - media) / media,
            0.25m, tendencia, persistencia, sub);

    private static RadarContexto Contexto(
        decimal consumoAtual = 5000m, decimal mediaConsumo = 5000m,
        decimal receitasAtual = 8000m, decimal mediaReceitas = 8000m,
        decimal investidoAtual = 0m, decimal mediaInvestido = 0m,
        IReadOnlyList<RadarVariacaoCategoria>? categorias = null,
        int mesesBase = 3)
        => new(
            new DateOnly(2026, 10, 1), 20, true,
            mesesBase, mesesBase,
            receitasAtual, consumoAtual,
            investidoAtual, investidoAtual,
            mediaConsumo, mediaInvestido, mediaReceitas,
            receitasAtual > 0m ? consumoAtual / receitasAtual : 0m,
            RadarCalculos.LimiarAbsoluto(consumoAtual),
            categorias ?? Array.Empty<RadarVariacaoCategoria>(),
            new[] { 5000m, 5000m, 5000m });

    [Fact]
    public void Gerar_ConsumoAcimaDaMedia_GeraInsightDeAumento()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 6000m, mediaConsumo: 5000m));

        Assert.Contains(insights, i => i.Tipo == "consumo_total_acima_da_media" && i.Nivel == RadarNivel.Atencao);
    }

    [Fact]
    public void Gerar_ConsumoAbaixoDaMedia_GeraInsightPositivo()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 4000m, mediaConsumo: 5000m));

        Assert.Contains(insights, i => i.Tipo == "consumo_total_abaixo_da_media" && i.Nivel == RadarNivel.Positivo);
    }

    [Fact]
    public void Gerar_VariacaoIrrelevante_NaoGeraInsightDeConsumo()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 5050m, mediaConsumo: 5000m));

        Assert.DoesNotContain(insights, i => i.Tipo.StartsWith("consumo_total_"));
    }

    [Fact]
    public void Gerar_CategoriaAcimaDaMedia_GeraInsightComSubcategoriaResponsavel()
    {
        var sub = new RadarVariacaoSubcategoria(Guid.NewGuid(), "Delivery", 300m, 200m, 100m, 0.5m, 0.35m);
        var categoria = Categoria(1200m, 1000m, sub);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Contains("Delivery representa 35,0% do aumento dos seus gastos com Alimentação neste mês.",
            insight.Descricao);
        Assert.Equal(categoria.Id, insight.CategoriaId);
    }

    [Fact]
    public void Gerar_CategoriaComVariacaoCritica_GeraNivelCritico()
    {
        var categoria = Categoria(3000m, 1000m);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        Assert.Contains(insights, i => i.Tipo == "categoria_aumentou" && i.Nivel == RadarNivel.Critico);
    }

    [Fact]
    public void Gerar_ComprometimentoAlto_GeraInsightCritico()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 9000m, receitasAtual: 10000m));

        Assert.Contains(insights, i => i.Tipo == "comprometimento_alto" && i.Nivel == RadarNivel.Critico);
    }

    [Fact]
    public void Gerar_SaldoNegativo_GeraInsightCritico()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 9000m, receitasAtual: 5000m));

        Assert.Contains(insights, i => i.Tipo == "saldo_negativo" && i.Nivel == RadarNivel.Critico);
    }

    [Fact]
    public void Gerar_ConsumoCresceMaisQueReceitas_GeraInsightCritico()
    {
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 7000m, mediaConsumo: 5000m, receitasAtual: 8200m, mediaReceitas: 8000m));

        Assert.Contains(insights, i => i.Tipo == "consumo_crescem_mais_que_receitas" && i.Nivel == RadarNivel.Critico);
    }

    [Fact]
    public void Gerar_ReceitasCrescemMaisQueConsumo_GeraInsightPositivo()
    {
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 5000m, mediaConsumo: 5000m, receitasAtual: 9500m, mediaReceitas: 8000m));

        Assert.Contains(insights, i => i.Tipo == "receitas_crescem_mais_que_consumo" && i.Nivel == RadarNivel.Positivo);
    }

    [Fact]
    public void Priorizar_ImpactoFinanceiroDominaVariacaoPercentual()
    {
        var grande = new RadarInsight(RadarNivel.Atencao, "categoria_aumentou", "A", "a", 600m, 0.18m, 0, 0.3m, null, null);
        var pequeno = new RadarInsight(RadarNivel.Atencao, "categoria_aumentou", "B", "b", 20m, 0.90m, 0, 0.01m, null, null);

        var priorizados = RadarInsights.Priorizar(new[] { pequeno, grande });

        Assert.Equal("A", priorizados[0].Titulo);
    }

    [Fact]
    public void Priorizar_LimitaAInsightsComInsightsCriticosPreservados()
    {
        var lista = Enumerable.Range(0, 12)
            .Select(i => new RadarInsight(RadarNivel.Positivo, "categoria_diminuiu", $"P{i}", "x", i + 1m, 0.1m, 0, 0.1m, null, null))
            .ToList();
        var critico = new RadarInsight(RadarNivel.Critico, "saldo_negativo", "C", "x", 1m, 0m, 0, 0m, null, null);
        lista.Add(critico);

        var priorizados = RadarInsights.Priorizar(lista);

        Assert.Equal(RadarCalculos.MaximoInsights, priorizados.Count);
        Assert.Contains(priorizados, i => i.Nivel == RadarNivel.Critico);
    }

    [Fact]
    public void Gerar_CategoriaComVariacaoIrrelevante_NaoGeraInsightDeCategoria()
    {
        // Oscilação de 1% e de R$ 10 — abaixo do limiar absoluto: nenhuma categoria deve aparecer.
        var categorias = new[] { Categoria(1010m, 1000m), Categoria(990m, 1000m) };

        var insights = RadarInsights.Gerar(Contexto(categorias: categorias));

        Assert.DoesNotContain(insights, i => i.Tipo is "categoria_aumentou" or "categoria_diminuiu");
    }

    [Fact]
    public void Gerar_CategoriaComVariacaoAltaMasImpactoPequeno_FicaEmAtencao()
    {
        // +30% de variação, mas só R$ 300 — menos de 20% das despesas do mês: não é crítico.
        var categoria = Categoria(1300m, 1000m);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
    }

    [Fact]
    public void Gerar_CategoriaComImpactoGrandeMasVariacaoBaixa_FicaEmAtencao()
    {
        // R$ 1.000 de diferença (20% das despesas do mês), mas apenas +22,2% de variação: não é crítico.
        var categoria = Categoria(5500m, 4500m);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
    }

    [Fact]
    public void Gerar_ComprometimentoMedio_GeraInsightDeAtencao()
    {
        // 70% de comprometimento: acima de 65% (atenção), abaixo de 80% (crítico).
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 7000m, mediaConsumo: 7000m,
            receitasAtual: 10000m, mediaReceitas: 10000m));

        Assert.Contains(insights, i => i.Tipo == "comprometimento_alto" && i.Nivel == RadarNivel.Atencao);
    }

    [Fact]
    public void Gerar_TendenciaDeAlta_IndicaSubindo()
    {
        // A série dos meses fechados subia, mesmo com o mês atual abaixo da média.
        var categoria = Categoria(850m, 1000m, tendencia: RadarTendencia.Alta, persistencia: 3);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_diminuiu");
        Assert.Contains("Vem subindo há 3 meses", insight.Descricao);
        Assert.DoesNotContain("caindo", insight.Descricao);
    }

    [Fact]
    public void Gerar_TendenciaDeBaixa_IndicaCaindo()
    {
        // A série dos meses fechados caía, mesmo com o mês atual acima da média.
        var categoria = Categoria(1150m, 1000m, tendencia: RadarTendencia.Baixa, persistencia: 3);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Contains("Vem caindo há 3 meses", insight.Descricao);
        Assert.DoesNotContain("subindo", insight.Descricao);
    }

    [Fact]
    public void Gerar_SemTendenciaDefinida_NaoFalaEmMesesDePersistencia()
    {
        var categorias = new[]
        {
            Categoria(1200m, 1000m, tendencia: RadarTendencia.Indefinida, persistencia: 3),
            Categoria(1300m, 1000m, tendencia: RadarTendencia.Estavel, persistencia: 3),
        };

        var insights = RadarInsights.Gerar(Contexto(categorias: categorias));

        var deCategoria = insights.Where(i => i.Tipo == "categoria_aumentou").ToList();
        Assert.Equal(2, deCategoria.Count);
        Assert.All(deCategoria, i => Assert.DoesNotContain("Vem", i.Descricao));
    }

    [Fact]
    public void Gerar_SubcategoriaNaQueda_UsaAFormaCanonicaDaReducao()
    {
        var sub = new RadarVariacaoSubcategoria(Guid.NewGuid(), "Delivery", 150m, 200m, -50m, -0.25m, 0.35m);
        var categoria = Categoria(850m, 1000m, sub);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_diminuiu");
        Assert.Contains("Delivery representa 35,0% da redução dos seus gastos com Alimentação neste mês.",
            insight.Descricao);
    }

    [Fact]
    public void Gerar_ComparacaoDeCrescimento_MostraAsDuasTaxas()
    {
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 7000m, mediaConsumo: 5000m, receitasAtual: 8200m, mediaReceitas: 8000m));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_crescem_mais_que_receitas");
        Assert.Contains("(40,0% vs 2,5%)", insight.Descricao);
        // O gap de 37,5 pontos percentuais não pode ser apresentado como percentual.
        Assert.DoesNotContain("37,5%", insight.Descricao);
    }

    [Fact]
    public void Gerar_ComparacaoDeCrescimento_SomenteQuandoAlgumLadoCresceu()
    {
        // Os dois lados caíram, depois apenas as receitas caíram, e por fim as receitas
        // caíram menos que as despesas: em nenhum caso houve "crescimento" a comparar.
        var osDoisCairam = RadarInsights.Gerar(
            Contexto(consumoAtual: 4500m, mediaConsumo: 5000m, receitasAtual: 4000m, mediaReceitas: 5000m));
        var apenasReceitasCairam = RadarInsights.Gerar(
            Contexto(consumoAtual: 5000m, mediaConsumo: 5000m, receitasAtual: 4500m, mediaReceitas: 5000m));
        var receitasCairamMenos = RadarInsights.Gerar(
            Contexto(consumoAtual: 4000m, mediaConsumo: 5000m, receitasAtual: 4500m, mediaReceitas: 5000m));

        Assert.DoesNotContain(osDoisCairam,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
        Assert.DoesNotContain(apenasReceitasCairam,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
        Assert.DoesNotContain(receitasCairamMenos,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
    }

    [Fact]
    public void Gerar_BaseDeUmMes_UsaOSingular()
    {
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 6000m, mediaConsumo: 5000m, mesesBase: 1));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_total_acima_da_media");
        Assert.Contains("da sua média do último mês", insight.Descricao);
        Assert.DoesNotContain("dos últimos 1 mês", insight.Descricao);
    }

    [Fact]
    public void Gerar_SemBaseDeComparacao_NaoGeraInsightsDeMedia()
    {
        // Base de 0 meses: não existe média com que comparar (spec §25).
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 6000m, mediaConsumo: 0m,
            receitasAtual: 8200m, mediaReceitas: 0m,
            categorias: new[] { Categoria(1200m, 0m) },
            mesesBase: 0));

        Assert.DoesNotContain(insights, i => i.Tipo.Contains("da_media"));
        Assert.DoesNotContain(insights, i => i.Tipo is "categoria_aumentou" or "categoria_diminuiu");
        Assert.DoesNotContain(insights,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
    }

    [Fact]
    public void Gerar_CategoriaSemGastoNaBase_NaoAnunciaOPercentualSentinela()
    {
        // A categoria não tinha registro nenhum na base: o 100% de VariacaoPercentual é o
        // sentinela da média zero, não uma medição. A mensagem diz o que ela somou agora.
        var sub = new RadarVariacaoSubcategoria(Guid.NewGuid(), "Delivery", 500m, 0m, 500m, 1m, 1m);
        var categoria = new RadarVariacaoCategoria(
            Guid.NewGuid(), "Alimentação", 500m, 0m, 500m, 1m, 0.10m,
            RadarTendencia.Indefinida, 0, sub);

        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 5000m, mediaConsumo: 5000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            categorias: new[] { categoria }, mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Equal(
            "Seus gastos com Alimentação não tinham registro na média dos últimos 3 meses e somam R$ 500,00.",
            insight.Descricao);
        Assert.DoesNotContain("%", insight.Descricao);
        // Título e nível continuam exatamente os de antes: só o payload muda.
        Assert.Equal("Alimentação aumentou", insight.Titulo);
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
        Assert.Equal(0m, insight.Percentual);
    }

    [Fact]
    public void Gerar_CategoriaComGastoNaBase_ContinuaUsandoOPercentual()
    {
        var categoria = Categoria(1200m, 1000m);

        var insights = RadarInsights.Gerar(Contexto(categorias: new[] { categoria }));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Contains("Seus gastos com Alimentação estão 20,0% acima da sua média dos últimos 3 meses.",
            insight.Descricao);
    }

    [Fact]
    public void Gerar_ParticipacaoTemUmUnicoSignificado()
    {
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 6000m, mediaConsumo: 5000m,
            categorias: new[] { Categoria(1200m, 1000m) }));

        // Total de consumo: cobre 100% do consumo do mês.
        var total = Assert.Single(insights, i => i.Tipo == "consumo_total_acima_da_media");
        Assert.Equal(1m, total.Participacao);
        // Comparação de taxas de crescimento: não é participação de orçamento.
        var crescimento = Assert.Single(insights, i => i.Tipo == "consumo_crescem_mais_que_receitas");
        Assert.Equal(0m, crescimento.Participacao);
        // Categoria: a participação real da categoria no consumo do mês.
        var categoria = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Equal(0.25m, categoria.Participacao);
    }

    [Fact]
    public void Gerar_InsightDeTotal_FalaDeConsumo()
    {
        var insights = RadarInsights.Gerar(Contexto(consumoAtual: 6000m, mediaConsumo: 5000m));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_total_acima_da_media");
        Assert.Contains("Seu consumo está 20,0% acima da sua média dos últimos 3 meses.", insight.Descricao);
        // O Radar mede consumo: a mensagem não fala de despesas.
        Assert.DoesNotContain("despesa", insight.Descricao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gerar_ComparacaoDeCrescimento_FalaDeConsumo()
    {
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 7000m, mediaConsumo: 5000m, receitasAtual: 8200m, mediaReceitas: 8000m));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_crescem_mais_que_receitas");
        Assert.Contains("Seu consumo está crescendo mais rápido que suas receitas", insight.Descricao);
        Assert.Contains("(40,0% vs 2,5%)", insight.Descricao);
    }

    [Fact]
    public void Gerar_InvestimentoAplicado_GeraInsightPositivo()
    {
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            investidoAtual: 900m, mediaInvestido: 500m));

        var insight = Assert.Single(insights, i => i.Tipo == "investimento_aplicado");
        Assert.Equal(RadarNivel.Positivo, insight.Nivel);
        Assert.Equal("Você aplicou em investimentos", insight.Titulo);
        Assert.Contains("R$ 900,00 aplicados em investimentos neste mês.", insight.Descricao);
        // Acima da média: a comparação entra.
        Assert.Contains("R$ 400,00 acima da sua média dos últimos 3 meses.", insight.Descricao);
        Assert.Equal(900m, insight.Impacto);
        Assert.Equal(0m, insight.Participacao);
    }

    [Fact]
    public void Gerar_SemAporte_NaoGeraInsightDeInvestimento()
    {
        var insights = RadarInsights.Gerar(Contexto());

        Assert.DoesNotContain(insights, i => i.Tipo == "investimento_aplicado");
    }

    [Fact]
    public void Gerar_AporteAbaixoDaMedia_NaoDizQueEstaBaixoDaMedia()
    {
        // Um card verde que diz "abaixo da média" seria uma oddidade: o aporte é uma boa notícia.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            investidoAtual: 300m, mediaInvestido: 500m));

        var insight = Assert.Single(insights, i => i.Tipo == "investimento_aplicado");
        Assert.Equal(RadarNivel.Positivo, insight.Nivel);
        Assert.Contains("R$ 300,00 aplicados em investimentos neste mês.", insight.Descricao);
        Assert.DoesNotContain("abaixo", insight.Descricao);
    }

    [Fact]
    public void Gerar_SemBaseDeComparacao_NaoComparaOInvestimentoComUmaMediaInexistente()
    {
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            investidoAtual: 300m, mediaInvestido: 0m, mesesBase: 0));

        var insight = Assert.Single(insights, i => i.Tipo == "investimento_aplicado");
        Assert.Contains("R$ 300,00 aplicados em investimentos neste mês.", insight.Descricao);
        Assert.DoesNotContain("média", insight.Descricao);
    }

    [Fact]
    public void Gerar_SemMediaDeAportes_NaoComparaComUmaMediaInexistente()
    {
        // Sem aporte na base a média é R$ 0,00: o 100% de VariacaoPercentual é o sentinela
        // da média zero, não uma medição. O insight não pode anunciar "R$ 900,00 acima da
        // sua média" de uma média que não existe.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            investidoAtual: 900m, mediaInvestido: 0m));

        var insight = Assert.Single(insights, i => i.Tipo == "investimento_aplicado");
        Assert.Contains("R$ 900,00 aplicados em investimentos neste mês.", insight.Descricao);
        Assert.DoesNotContain("acima da sua média", insight.Descricao);
        Assert.DoesNotContain("%", insight.Descricao);
    }

    [Fact]
    public void Gerar_AporteComMediaNaBase_ContinuaComparandoComAMedia()
    {
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            investidoAtual: 900m, mediaInvestido: 500m));

        var insight = Assert.Single(insights, i => i.Tipo == "investimento_aplicado");
        Assert.Contains("R$ 400,00 acima da sua média dos últimos 3 meses.", insight.Descricao);
    }

    [Fact]
    public void Gerar_SaldoNegativo_ContaOAporteNoSaldo()
    {
        // Consumo 4.000 e aporte 2.000 contra receita 5.000: o saldo real é −1.000.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 5000m, mediaReceitas: 5000m,
            investidoAtual: 2000m, mediaInvestido: 0m));

        var insight = Assert.Single(insights, i => i.Tipo == "saldo_negativo");
        Assert.Equal(RadarNivel.Critico, insight.Nivel);
        Assert.Contains("R$ 1.000,00", insight.Descricao);
    }

    [Fact]
    public void Gerar_SemMediaDeConsumo_NaoAnunciaOPercentualSentinela()
    {
        // A base tem meses com dados, mas o consumo da base é R$ 0,00: o 100% de
        // VariacaoPercentual é o sentinela da média zero, não uma medição. O insight
        // diz o que o consumo somou agora — a mesma forma que a categoria sem registro
        // na base usa.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 500m, mediaConsumo: 0m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_total_acima_da_media");
        Assert.Equal("Seu consumo não tinha registro na média dos últimos 3 meses e soma R$ 500,00.",
            insight.Descricao);
        Assert.DoesNotContain("%", insight.Descricao);
        // Título e descrição continuam os de antes; o payload e o nível não podem
        // carregar o sentinela da média zero.
        Assert.Equal("Consumo acima da média", insight.Titulo);
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
        Assert.Equal(500m, insight.Impacto);
        Assert.Equal(0m, insight.Percentual);
    }

    [Fact]
    public void Gerar_SemMediaDeReceitas_NaoAnunciaOPercentualSentinela()
    {
        // A base tem meses com dados, mas a receita da base é R$ 0,00: o 100% de
        // VariacaoPercentual é o sentinela da média zero, não uma medição. O insight
        // diz o que as receitas somaram agora — a mesma forma que o consumo usa.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 500m, mediaReceitas: 0m,
            mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "receita_total_acima_da_media");
        Assert.Equal("Suas receitas não tinham registro na média dos últimos 3 meses e somam R$ 500,00.",
            insight.Descricao);
        Assert.DoesNotContain("%", insight.Descricao);
        // Título, nível e impacto continuam os de antes: só o payload muda.
        Assert.Equal("Receitas acima da média", insight.Titulo);
        Assert.Equal(RadarNivel.Positivo, insight.Nivel);
        Assert.Equal(500m, insight.Impacto);
        Assert.Equal(0m, insight.Percentual);
    }

    [Fact]
    public void Gerar_SemMediaDeConsumo_PayloadNaoCarregaOSentinela()
    {
        // O payload alimenta o ranqueamento (|Percentual| × 0,5) e o nível decide se o
        // insight é Crítico. Com a média da base em zero não existe medição: o
        // percentual tem de ser 0 e o nível não pode ser Crítico — a mudança é real,
        // mas não há comparação que a tornasse crítica.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 500m, mediaConsumo: 0m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "consumo_total_acima_da_media");
        Assert.Equal(0m, insight.Percentual);
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
    }

    [Fact]
    public void Gerar_SemMediaDeReceitas_PayloadNaoCarregaOSentinela()
    {
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 500m, mediaReceitas: 0m,
            mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "receita_total_acima_da_media");
        Assert.Equal(0m, insight.Percentual);
        Assert.NotEqual(RadarNivel.Critico, insight.Nivel);
    }

    [Fact]
    public void Gerar_CategoriaSemGastoNaBase_PayloadNaoCarregaOSentinela()
    {
        // A categoria não tinha registro na base e o mês inteiro virou consumo dela:
        // com o sentinela de 100% o insight saía Crítico, sem nenhuma comparação.
        var categoria = new RadarVariacaoCategoria(
            Guid.NewGuid(), "Alimentação", 1000m, 0m, 1000m, 1m, 1m,
            RadarTendencia.Indefinida, 0, null);

        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 1000m, mediaConsumo: 1000m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            categorias: new[] { categoria }, mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "categoria_aumentou");
        Assert.Equal(0m, insight.Percentual);
        Assert.Equal(RadarNivel.Atencao, insight.Nivel);
    }

    [Fact]
    public void Gerar_ReceitasComMediaNaBase_ContinuaUsandoOPercentual()
    {
        // Com receita na base a comparação é uma medição e o percentual aparece.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 5000m, mediaReceitas: 4000m,
            mesesBase: 3));

        var insight = Assert.Single(insights, i => i.Tipo == "receita_total_acima_da_media");
        Assert.Equal("O total de receitas está 25,0% acima da sua média dos últimos 3 meses.",
            insight.Descricao);
    }

    [Fact]
    public void Gerar_BaseSemReceita_NaoComparaOCrescimento()
    {
        // A base tem meses com dados, mas nenhuma receita: a média de receitas é zero e o
        // 100% de VariacaoPercentual é o sentinela da média zero. Comparar o crescimento
        // com esse número afirmaria um crescimento de receita que não aconteceu.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 5000m, mediaConsumo: 4000m,
            receitasAtual: 20000m, mediaReceitas: 0m,
            mesesBase: 3));

        Assert.DoesNotContain(insights,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
    }

    [Fact]
    public void Gerar_BaseSemConsumo_NaoComparaOCrescimento()
    {
        // O mesmo pelo outro lado: sem consumo na base não há taxa de consumo a comparar.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 500m, mediaConsumo: 0m,
            receitasAtual: 8000m, mediaReceitas: 8000m,
            mesesBase: 3));

        Assert.DoesNotContain(insights,
            i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
    }

    [Fact]
    public void Gerar_ComparacaoDeCrescimentoDeReceitas_NaoFalaEmEstaoCrescendo()
    {
        // A frase do crescimento das receitas é a mesma que aparecia quando a média de
        // receitas era zero: ela afirmava um crescimento que não aconteceu. A forma nova
        // diz a comparação sem o "estão" que escondia o sentinela da média zero.
        var insights = RadarInsights.Gerar(
            Contexto(consumoAtual: 5000m, mediaConsumo: 5000m, receitasAtual: 9500m, mediaReceitas: 8000m));

        var insight = Assert.Single(insights, i => i.Tipo == "receitas_crescem_mais_que_consumo");
        Assert.Equal("Suas receitas crescem mais rápido que seu consumo (18,8% vs 0,0%).",
            insight.Descricao);
    }

    [Fact]
    public void Gerar_SaldoNegativo_FalaDeSaidasENaoDeDespesas()
    {
        // Consumo 4.000 e aporte 2.000 contra receita 5.000: o saldo real é −1.000.
        // A mensagem não pode falar em "despesas": o usuário leria como se o Radar
        // estivesse acusando o consumo (ou o aporte) — e o aporte não é consumo.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 5000m, mediaReceitas: 5000m,
            investidoAtual: 2000m, mediaInvestido: 0m));

        var insight = Assert.Single(insights, i => i.Tipo == "saldo_negativo");
        Assert.Equal(RadarNivel.Critico, insight.Nivel);
        Assert.Equal("Saldo negativo", insight.Titulo);
        Assert.Equal("Suas saídas superaram suas receitas em R$ 1.000,00 neste mês.",
            insight.Descricao);
        Assert.DoesNotContain("despesa", insight.Descricao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gerar_SaldoPositivo_NAoGeraInsightDeSaldoNegativo()
    {
        // Receita 6.000 contra consumo 4.000 e aporte 2.000: o saldo real é zero.
        var insights = RadarInsights.Gerar(Contexto(
            consumoAtual: 4000m, mediaConsumo: 4000m,
            receitasAtual: 6000m, mediaReceitas: 6000m,
            investidoAtual: 2000m, mediaInvestido: 0m));

        Assert.DoesNotContain(insights, i => i.Tipo == "saldo_negativo");
    }
}
