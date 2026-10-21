using System.Globalization;
using Finort.Models.Financeiro;

namespace Finort.Services;

/// <summary>
/// Gera os insights do Radar Financeiro (spec §19, §23) e prioriza os mais relevantes (spec §20).
/// </summary>
public static class RadarInsights
{
    /// <summary>
    /// Participação mínima do consumo do mês para uma variação ser considerada crítica.
    /// <para>A base do limiar é o <b>consumo</b> do mês: o Radar mede consumo, não saída
    /// de dinheiro, e o aporte não faz parte do denominador.</para>
    /// <para>Efeito colateral dessa base: um aporte grande encolhe o denominador, então a
    /// mesma diferença absoluta passa a ter participação maior e a categoria pode ficar
    /// crítica mais facilmente. O comportamento é intencional — a base do Radar é o
    /// consumo — e vale para a participação no insight e para o nível do alerta.</para>
    /// </summary>
    public const decimal ParticipacaoCriticaConsumo = 0.20m;

    /// <summary>Diferença de variação percentual que dispara o alerta de crescimento relativo.</summary>
    public const decimal GapCrescimento = 0.05m;

    public static IReadOnlyList<RadarInsight> Gerar(RadarContexto ctx)
    {
        var insights = new List<RadarInsight>();
        var limiar = ctx.LimiarAbsoluto;
        // Sem base de comparação não existe média contra a qual comparar (spec §25):
        // nenhum insight de "acima/abaixo da média" — nem de total, nem de categoria —
        // e nenhuma comparação de crescimento entre consumo e receitas.
        var temBase = ctx.MesesBase > 0;

        // A variação medida: com média zero não existe percentual a medir — o 100% de
        // VariacaoPercentual é o sentinela da média zero, não uma medição. Ele não pode
        // entrar no payload (que alimenta o ranqueamento) nem decidir o nível do alerta.
        var temMediaConsumo = ctx.MediaConsumo > 0m;
        var temMediaReceitas = ctx.MediaReceitas > 0m;
        var variacaoConsumo = temMediaConsumo
            ? RadarCalculos.VariacaoPercentual(ctx.TotalConsumo, ctx.MediaConsumo)
            : 0m;
        var variacaoReceitas = temMediaReceitas
            ? RadarCalculos.VariacaoPercentual(ctx.TotalReceitas, ctx.MediaReceitas)
            : 0m;
        var consumoRelevante = RadarCalculos.IsSignificantChange(ctx.TotalConsumo, ctx.MediaConsumo,
            RadarCalculos.VariacaoPercentualMinima, limiar);
        var receitasRelevantes = RadarCalculos.IsSignificantChange(ctx.TotalReceitas, ctx.MediaReceitas,
            RadarCalculos.VariacaoPercentualMinima, limiar);

        if (temBase && consumoRelevante)
        {
            // A direção vem da comparação com a média, não do sentinela: o mês está
            // acima da média quando o valor do mês supera a média da base.
            var subiu = ctx.TotalConsumo > ctx.MediaConsumo;
            // Sem média de consumo na base não existe percentual a medir: o 100% de
            // VariacaoPercentual é o sentinela da média zero, não uma medição. Aí a
            // mensagem diz, sem julgamento, o que o consumo somou agora — a mesma forma
            // que a categoria sem registro na base usa.
            var descricao = temMediaConsumo
                ? $"Seu consumo está {Pct(Math.Abs(variacaoConsumo))} {(subiu ? "acima" : "abaixo")} da sua média {MesesTexto(ctx.MesesBase)}."
                : $"Seu consumo não tinha registro na média {MesesTexto(ctx.MesesBase)} e soma R$ {ctx.TotalConsumo.ToString("N2", CulturaPtBr)}.";

            insights.Add(new RadarInsight(
                subiu
                    ? (temMediaConsumo
                        ? NivelAumento(variacaoConsumo, Math.Abs(ctx.TotalConsumo - ctx.MediaConsumo), ctx.TotalConsumo)
                        // Sem média na base não existe comparação que tornasse a mudança
                        // crítica: o nível é Atenção — a mudança é real, mas não havia
                        // contra o que compará-la.
                        : RadarNivel.Atencao)
                    : RadarNivel.Positivo,
                subiu ? "consumo_total_acima_da_media" : "consumo_total_abaixo_da_media",
                subiu ? "Consumo acima da média" : "Consumo abaixo da média",
                descricao,
                Math.Abs(ctx.TotalConsumo - ctx.MediaConsumo), variacaoConsumo, 0,
                1m, null, null));
        }

        if (temBase && receitasRelevantes)
        {
            var subiu = ctx.TotalReceitas > ctx.MediaReceitas;
            // Sem média de receita na base não existe percentual a medir: o 100% de
            // VariacaoPercentual é o sentinela da média zero, não uma medição. Aí a
            // mensagem diz, sem julgamento, o que as receitas somaram agora — a mesma
            // forma que o consumo e a categoria sem registro na base usam.
            var descricao = temMediaReceitas
                ? $"O total de receitas está {Pct(Math.Abs(variacaoReceitas))} {(subiu ? "acima" : "abaixo")} da sua média {MesesTexto(ctx.MesesBase)}."
                : $"Suas receitas não tinham registro na média {MesesTexto(ctx.MesesBase)} e somam R$ {ctx.TotalReceitas.ToString("N2", CulturaPtBr)}.";

            insights.Add(new RadarInsight(
                subiu ? RadarNivel.Positivo : RadarNivel.Atencao,
                subiu ? "receita_total_acima_da_media" : "receita_total_abaixo_da_media",
                subiu ? "Receitas acima da média" : "Receitas abaixo da média",
                descricao,
                Math.Abs(ctx.TotalReceitas - ctx.MediaReceitas), variacaoReceitas, 0,
                1m, null, null));
        }

        // O aporte em investimento é uma boa notícia: entra no Radar como tal, e nunca
        // vira consumo a ser apontado. A comparação com a média só aparece quando o
        // aporte está acima dela — dentro de um card verde não se diz que se investiu
        // "abaixo". Sem média na base (R$ 0,00) não há o que comparar: o 100% do
        // VariacaoPercentual é o sentinela da média zero, não uma medição.
        if (ctx.InvestidoAtual > 0m)
        {
            var variacaoInvestido = ctx.MediaInvestido > 0m
                ? RadarCalculos.VariacaoPercentual(ctx.InvestidoAtual, ctx.MediaInvestido)
                : 0m;
            var descricao = $"R$ {ctx.TotalInvestido.ToString("N2", CulturaPtBr)} aplicados em investimentos neste mês.";
            if (temBase && ctx.MediaInvestido > 0m && ctx.InvestidoAtual > ctx.MediaInvestido)
            {
                descricao += $" R$ {(ctx.InvestidoAtual - ctx.MediaInvestido).ToString("N2", CulturaPtBr)}"
                           + $" acima da sua média {MesesTexto(ctx.MesesBase)}.";
            }

            insights.Add(new RadarInsight(
                RadarNivel.Positivo, "investimento_aplicado", "Você aplicou em investimentos",
                descricao,
                ctx.TotalInvestido, variacaoInvestido, 0, 0m, null, null));
        }

        if (temBase)
        {
            foreach (var cat in ctx.Categorias)
            {
                if (!RadarCalculos.IsSignificantChange(cat.ValorAtual, cat.MediaHistorica,
                        RadarCalculos.VariacaoPercentualMinima, limiar)) continue;

                var subiu = cat.VariacaoAbsoluta > 0m;
                // A variação medida da categoria: sem média na base não existe
                // percentual a medir (o 100% de VariacaoPercentual é o sentinela da
                // média zero, não uma medição).
                var temMediaCategoria = cat.MediaHistorica > 0m;
                var variacaoCategoria = temMediaCategoria ? cat.VariacaoPercentual : 0m;
                string descricao;
                if (!temMediaCategoria)
                {
                    // A categoria não tinha registro na base: não existe percentual a medir
                    // (o 100% de VariacaoPercentual é o sentinela da média zero, não uma
                    // medição). A mensagem diz, sem julgamento, o que ela somou agora.
                    descricao = $"Seus gastos com {cat.Nome} não tinham registro na média {MesesTexto(ctx.MesesBase)} "
                              + $"e somam R$ {cat.ValorAtual.ToString("N2", CulturaPtBr)}.";
                }
                else
                {
                    descricao = $"Seus gastos com {cat.Nome} estão {Pct(Math.Abs(variacaoCategoria))} {(subiu ? "acima" : "abaixo")} da sua média {MesesTexto(ctx.MesesBase)}.";
                    if (cat.SubcategoriaResponsavel is not null)
                    {
                        var s = cat.SubcategoriaResponsavel;
                        descricao += $" {s.Nome} representa {Pct(s.ParticipacaoNaVariacao)} {(subiu ? "do aumento" : "da redução")} dos seus gastos com {cat.Nome} neste mês.";
                    }
                    // A direção vem da tendência dos meses fechados, não da variação do mês atual:
                    // uma categoria que sobe há meses pode estar abaixo da média neste mês.
                    if (cat.Persistencia >= RadarCalculos.MinimoMesesParaTendencia
                        && cat.Tendencia is RadarTendencia.Alta or RadarTendencia.Baixa)
                        descricao += $" Vem {(cat.Tendencia == RadarTendencia.Alta ? "subindo" : "caindo")} há {cat.Persistencia} meses.";
                }

                insights.Add(new RadarInsight(
                    subiu
                        ? (temMediaCategoria
                            ? NivelAumento(variacaoCategoria, Math.Abs(cat.VariacaoAbsoluta), ctx.TotalConsumo)
                            // Sem média na base não existe comparação que tornasse a mudança
                            // crítica: o nível é Atenção — a mudança é real, mas não havia
                            // contra o que compará-la.
                            : RadarNivel.Atencao)
                        : RadarNivel.Positivo,
                    subiu ? "categoria_aumentou" : "categoria_diminuiu",
                    $"{cat.Nome} {(subiu ? "aumentou" : "diminuiu")}",
                    descricao,
                    Math.Abs(cat.VariacaoAbsoluta), variacaoCategoria, cat.Persistencia,
                    cat.Participacao, cat.Id, cat.SubcategoriaResponsavel?.Id));
            }
        }

        // A comparação de crescimento só existe quando as DUAS médias são maiores que
        // zero: com a média zero o VariacaoPercentual devolve o sentinela 100%, que não
        // é uma medição — e o gap viraria uma afirmação falsa (a base não tinha receita
        // nenhuma e a mensagem diria que as receitas estavam crescendo).
        if (temBase && ctx.MediaConsumo > 0m && ctx.MediaReceitas > 0m)
        {
            // A comparação só vale quando o lado que "cresce" de fato cresceu: quando o
            // outro lado apenas caiu, o insight de total abaixo da média já informa isso.
            var gap = variacaoConsumo - variacaoReceitas;
            if (gap >= GapCrescimento && variacaoConsumo > 0m)
            {
                insights.Add(new RadarInsight(
                    RadarNivel.Critico, "consumo_crescem_mais_que_receitas",
                    "Consumo crescendo mais rápido que receitas",
                    $"Seu consumo está crescendo mais rápido que suas receitas ({Pct(variacaoConsumo)} vs {Pct(variacaoReceitas)}).",
                    Math.Abs(ctx.TotalConsumo - ctx.MediaConsumo), gap, 0, 0m, null, null));
            }
            else if (gap <= -GapCrescimento && variacaoReceitas > 0m)
            {
                insights.Add(new RadarInsight(
                    RadarNivel.Positivo, "receitas_crescem_mais_que_consumo",
                    "Receitas crescendo mais rápido que consumo",
                    $"Suas receitas crescem mais rápido que seu consumo ({Pct(variacaoReceitas)} vs {Pct(variacaoConsumo)}).",
                    Math.Abs(ctx.TotalReceitas - ctx.MediaReceitas), gap, 0, 0m, null, null));
            }
        }

        if (ctx.TaxaComprometimento > RadarCalculos.ComprometimentoAtencao)
        {
            insights.Add(new RadarInsight(
                ctx.TaxaComprometimento > RadarCalculos.ComprometimentoCritico ? RadarNivel.Critico : RadarNivel.Atencao,
                "comprometimento_alto", "Comprometimento da renda",
                $"Seu consumo consome {Pct(ctx.TaxaComprometimento)} das suas receitas neste mês.",
                ctx.TotalConsumo, ctx.TaxaComprometimento, 0,
                ctx.TotalReceitas > 0m ? ctx.TaxaComprometimento : 0m, null, null));
        }

        // O saldo é o saldo real: receitas − consumo − aporte. O aporte sai do dinheiro
        // da conta, então ele conta no saldo — mas não é consumo, e por isso não vira
        // alerta por si só. A mensagem fala de "saídas" (consumo + aporte): dizer
        // "despesas" faria o usuário ler o aporte como a culpa do saldo negativo.
        var saldo = ctx.TotalReceitas - ctx.TotalConsumo - ctx.TotalInvestido;
        if (saldo < 0m)
        {
            insights.Add(new RadarInsight(
                RadarNivel.Critico, "saldo_negativo", "Saldo negativo",
                $"Suas saídas superaram suas receitas em R$ {(-saldo).ToString("N2", CulturaPtBr)} neste mês.",
                -saldo, 0m, 0, 0m, null, null));
        }

        return insights;
    }

    /// <summary>
    /// Ordena por impacto financeiro e corta no limite, mantendo os críticos no topo da lista.
    /// </summary>
    public static IReadOnlyList<RadarInsight> Priorizar(IReadOnlyList<RadarInsight> insights)
    {
        var limite = RadarCalculos.MaximoInsights;
        var criticos = insights.Where(i => i.Nivel == RadarNivel.Critico)
            .OrderByDescending(Pontuar)
            .Take(limite)
            .ToList();
        var restantes = insights.Where(i => i.Nivel != RadarNivel.Critico)
            .OrderByDescending(Pontuar)
            .Take(Math.Max(0, limite - criticos.Count))
            .ToList();
        return criticos.Concat(restantes).ToList();
    }

    private static decimal Pontuar(RadarInsight insight)
        => Math.Abs(insight.Impacto) + (Math.Abs(insight.Percentual) * 0.5m)
           + (insight.Persistencia * 20m) + (insight.Participacao * 100m);

    /// <summary>
    /// Nível do alerta de aumento: crítico quando a variação percentual é crítica e a
    /// diferença absoluta representa ao menos <see cref="ParticipacaoCriticaConsumo"/> do
    /// <b>consumo</b> do mês. O denominador é o consumo (sem o aporte) — ver
    /// <see cref="ParticipacaoCriticaConsumo"/> para o efeito colateral dessa base.
    /// </summary>
    private static RadarNivel NivelAumento(decimal variacao, decimal deltaAbsoluto, decimal totalConsumoMes)
        => variacao >= RadarCalculos.VariacaoCritica
           && totalConsumoMes > 0m
           && deltaAbsoluto >= totalConsumoMes * ParticipacaoCriticaConsumo
            ? RadarNivel.Critico
            : RadarNivel.Atencao;

    /// <summary>Formata uma razão decimal como percentual pt-BR ("17,6%").</summary>
    private static string Pct(decimal valor)
        => valor.ToString("P1", CulturaPtBr);

    /// <summary>Período da base de comparação ("do último mês" / "dos últimos 3 meses").</summary>
    private static string MesesTexto(int meses) => meses == 1 ? "do último mês" : $"dos últimos {meses} meses";

    private static readonly CultureInfo CulturaPtBr = new("pt-BR");
}
