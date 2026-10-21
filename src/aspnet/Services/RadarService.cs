using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

/// <summary>Lançamento projetado para a análise do Radar (data efetiva já resolvida).</summary>
/// <param name="EhInvestimento">
/// Verdadeiro quando o lançamento está na categoria "Investimento" — é assim que o app
/// registra aporte e compra (InvestimentoService.cs:378). O aporte não é consumo.
/// </param>
public sealed record RadarLinha(
    LancamentoTipo Tipo, decimal Valor, Guid CategoriaId, Guid? SubcategoriaId,
    string CategoriaNome, string? SubcategoriaNome, DateOnly Data, bool EhInvestimento);

/// <summary>
/// Orquestrador do Radar Financeiro. Carrega os lançamentos já filtrados, agrega por mês,
/// categoria e subcategoria, e devolve o DTO consumido pela tela.
/// </summary>
public class RadarService
{
    private readonly AppDbContext _db;

    public RadarService(AppDbContext db) => _db = db;

    public async Task<RadarResultado> ObterAsync(int mesesHistorico, Guid? pessoaId, DateOnly? hoje = null)
    {
        var dataHoje = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var meses = Math.Clamp(mesesHistorico, 1, 12);
        var inicioMesAtual = new DateOnly(dataHoje.Year, dataHoje.Month, 1);
        var diasMesAtual = DateTime.DaysInMonth(dataHoje.Year, dataHoje.Month);
        var diaCorte = Math.Min(dataHoje.Day, diasMesAtual);
        var mesParcial = diaCorte < diasMesAtual;

        var mesesFechados = Enumerable.Range(1, meses)
            .Select(i => inicioMesAtual.AddMonths(-i))
            .OrderBy(m => m)
            .ToList();
        var mesesBase = mesesFechados.TakeLast(3).ToList();

        var lancamentos = await CarregarAsync(mesesFechados[0], new DateOnly(dataHoje.Year, dataHoje.Month, diasMesAtual), pessoaId);

        // Base efetiva: só os meses da base que têm lançamentos na janela truncada.
        // Um mês sem movimento não forma média — dividi-lo por 3 inventaria uma queda
        // que não existe (e diz "100% acima da média" a quem não tem histórico).
        var mesesBaseComDados = mesesBase.Where(m => TemDados(lancamentos, m, diaCorte)).ToList();

        var janelaAtual = Janela(inicioMesAtual, diaCorte);
        var totalReceitas = Somar(lancamentos, LancamentoTipo.Receita, janelaAtual.Inicio, janelaAtual.Fim);
        // O Radar mede consumo: o consumo e o aporte são coisas diferentes — o aporte
        // sai da conta, mas não foi consumido.
        var totalConsumo = SomarConsumo(lancamentos, janelaAtual.Inicio, janelaAtual.Fim);
        var totalInvestido = SomarInvestido(lancamentos, janelaAtual.Inicio, janelaAtual.Fim);

        var receitasBase = new List<decimal>();
        var consumoBase = new List<decimal>();
        var investidoBase = new List<decimal>();
        foreach (var mes in mesesBaseComDados)
        {
            var j = Janela(mes, diaCorte);
            receitasBase.Add(Somar(lancamentos, LancamentoTipo.Receita, j.Inicio, j.Fim));
            consumoBase.Add(SomarConsumo(lancamentos, j.Inicio, j.Fim));
            investidoBase.Add(SomarInvestido(lancamentos, j.Inicio, j.Fim));
        }
        var mediaReceitas = RadarCalculos.Media(receitasBase);
        var mediaConsumo = RadarCalculos.Media(consumoBase);
        var mediaInvestido = RadarCalculos.Media(investidoBase);

        var categorias = ConstruirCategorias(lancamentos, janelaAtual, mesesFechados, mesesBaseComDados, diaCorte, totalConsumo);

        // A série histórica mede o consumo do mês: o aporte não é consumo, então ele não
        // entra na série que mede a estabilidade dos gastos. Um aporte em um mês fechado
        // não pode fazer o consumo parecer instável.
        var consumoMensaisHistorico = mesesFechados
            .Select(m => SomarConsumoMes(lancamentos, m))
            .ToList();

        var taxaComprometimento = totalReceitas > 0m ? totalConsumo / totalReceitas : 0m;
        var contexto = new RadarContexto(
            inicioMesAtual, diaCorte, mesParcial,
            // O score é habilitado pelos meses que alimentam a comparação: os da base, na
            // mesma janela truncada da média. Dados fora da base não habilitam score
            // (spec §18, §25). O tamanho da janela pedida é o MesesHistorico do resultado.
            // MesesBase é a base efetiva (os mesmos meses) e MesesBaseComDados é o contador
            // do portão do score — os dois valem o mesmo número.
            mesesBaseComDados.Count, mesesBaseComDados.Count,
            totalReceitas, totalConsumo, totalInvestido, totalInvestido,
            mediaConsumo, mediaInvestido, mediaReceitas, taxaComprometimento,
            RadarCalculos.LimiarAbsoluto(totalConsumo),
            categorias, consumoMensaisHistorico);

        var todosInsights = RadarInsights.Gerar(contexto);
        var insights = RadarInsights.Priorizar(todosInsights);
        // O score penaliza toda categoria fora do padrão: ele recebe a lista inteira de
        // insights, não a priorizada — a priorização corta em 8 para o que aparece na tela.
        var score = RadarPontuacao.Calcular(contexto, todosInsights);

        var serie = new List<RadarSerieMensal>();
        foreach (var mes in mesesFechados)
        {
            serie.Add(new RadarSerieMensal(mes.Year, mes.Month,
                SomarMes(lancamentos, LancamentoTipo.Receita, mes),
                SomarConsumoMes(lancamentos, mes),
                SomarInvestidoMes(lancamentos, mes),
                false));
        }
        serie.Add(new RadarSerieMensal(inicioMesAtual.Year, inicioMesAtual.Month,
            totalReceitas, totalConsumo, totalInvestido, mesParcial));

        // Sem base de comparação não há variação a medir: o 100% sentinela de
        // VariacaoPercentual não é uma medição, é um número inventado.
        var semBase = mesesBaseComDados.Count == 0;

        return new RadarResultado(
            meses, inicioMesAtual, diaCorte, mesParcial,
            totalReceitas, totalConsumo, totalInvestido,
            // O saldo é o saldo real: receitas − consumo − aporte (o mesmo do Dashboard).
            totalReceitas - totalConsumo - totalInvestido, taxaComprometimento,
            // Sem média de receita na base não há o que comparar: o 100% do
            // VariacaoPercentual é o sentinela da média zero, não uma medição.
            semBase || mediaReceitas <= 0m
                ? 0m
                : RadarCalculos.VariacaoPercentual(totalReceitas, mediaReceitas),
            // Sem média de consumo na base não há o que comparar: o 100% do
            // VariacaoPercentual é o sentinela da média zero, não uma medição.
            semBase || mediaConsumo <= 0m
                ? 0m
                : RadarCalculos.VariacaoPercentual(totalConsumo, mediaConsumo),
            score, insights, categorias, serie)
        {
            MesesBase = mesesBaseComDados.Count,
            // Sem média de aportes na base não há o que comparar: o 100% do
            // VariacaoPercentual é o sentinela da média zero, não uma medição.
            VariacaoPercentualInvestido = semBase || mediaInvestido <= 0m
                ? 0m
                : RadarCalculos.VariacaoPercentual(totalInvestido, mediaInvestido),
            MediaInvestido = mediaInvestido,
            // A média de consumo vai junto: o card "Consumo no mês" precisa saber se a
            // base tem consumo para decidir se há percentual a mostrar.
            MediaConsumo = mediaConsumo,
            // E a média de receita, pelo mesmo motivo: o card "Receitas no mês" precisa
            // distinguir "a base não tem receita" de "a receita do mês é igual à média".
            MediaReceitas = mediaReceitas
        };
    }

    public async Task<RadarDetalheCategoria?> ObterDetalheCategoriaAsync(
        Guid categoriaId, int mesesHistorico, Guid? pessoaId, DateOnly? hoje = null)
    {
        var dataHoje = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var meses = Math.Clamp(mesesHistorico, 1, 12);
        var inicioMesAtual = new DateOnly(dataHoje.Year, dataHoje.Month, 1);
        var diasMesAtual = DateTime.DaysInMonth(dataHoje.Year, dataHoje.Month);
        var diaCorte = Math.Min(dataHoje.Day, diasMesAtual);

        var mesesFechados = Enumerable.Range(1, meses)
            .Select(i => inicioMesAtual.AddMonths(-i))
            .OrderBy(m => m)
            .ToList();
        var mesesBase = mesesFechados.TakeLast(3).ToList();

        var lancamentos = await CarregarAsync(mesesFechados[0], new DateOnly(dataHoje.Year, dataHoje.Month, diasMesAtual), pessoaId);
        var daCategoria = lancamentos.Where(l => l.CategoriaId == categoriaId).ToList();
        if (daCategoria.Count == 0) return null;

        // Mesma base efetiva da página: só os meses da base com lançamentos formam a média.
        var mesesBaseComDados = mesesBase.Where(m => TemDados(lancamentos, m, diaCorte)).ToList();

        var nome = daCategoria[0].CategoriaNome;
        var janelaAtual = Janela(inicioMesAtual, diaCorte);
        var valorAtual = Somar(daCategoria, LancamentoTipo.Despesa, janelaAtual.Inicio, janelaAtual.Fim);
        var media = RadarCalculos.Media(mesesBaseComDados.Select(m =>
        {
            var j = Janela(m, diaCorte);
            return Somar(daCategoria, LancamentoTipo.Despesa, j.Inicio, j.Fim);
        }));

        var serieFechada = mesesFechados
            .Select(m => SomarMes(daCategoria, LancamentoTipo.Despesa, m))
            .ToList();
        var tendencia = RadarCalculos.DetectarTendencia(serieFechada);

        var historico = new List<RadarPontoMensal>();
        foreach (var mes in mesesFechados)
            historico.Add(new RadarPontoMensal(mes.Year, mes.Month, SomarMes(daCategoria, LancamentoTipo.Despesa, mes), false));
        // O mês atual é truncado no dia de corte; só é parcial enquanto o mês não terminou.
        historico.Add(new RadarPontoMensal(inicioMesAtual.Year, inicioMesAtual.Month, valorAtual, diaCorte < diasMesAtual));

        var subcategorias = ConstruirSubcategorias(daCategoria, janelaAtual, mesesBaseComDados, diaCorte, valorAtual - media);

        return new RadarDetalheCategoria(
            categoriaId, nome, valorAtual, media,
            RadarCalculos.VariacaoPercentual(valorAtual, media), tendencia,
            historico, subcategorias.Subcategorias)
        {
            SubcategoriaResponsavel = subcategorias.Responsavel,
            MesesBase = mesesBaseComDados.Count
        };
    }

    private async Task<List<RadarLinha>> CarregarAsync(DateOnly inicio, DateOnly fim, Guid? pessoaId)
    {
        // A janela é filtrada no banco: o filtro precisa vir antes da projeção, senão o EF
        // tenta traduzir a construção do RadarLinha dentro do WHERE.
        return await _db.Lancamentos
            .BaseRadar(_db)
            .Where(l => !pessoaId.HasValue || l.PessoaId == pessoaId)
            .Select(l => new
            {
                l.Tipo,
                l.Valor,
                l.CategoriaId,
                l.SubcategoriaId,
                CategoriaNome = l.Categoria != null ? l.Categoria.Nome : "(sem categoria)",
                SubcategoriaNome = l.Subcategoria != null ? l.Subcategoria.Nome : null,
                // O aporte em investimento é saída de dinheiro, mas não é consumo:
                // a categoria é a mesma que o app usa para registrar aporte e compra.
                EhInvestimento = l.Categoria != null && l.Categoria.Nome == RadarFiltro.NomeCategoriaInvestimento,
                // Data efetiva: a despesa de cartão entra no mês do vencimento da fatura.
                Data = l.CartaoCreditoId != null && l.DataVencimentoCartao.HasValue
                    ? l.DataVencimentoCartao.Value
                    : l.Data
            })
            .Where(x => x.Data >= inicio && x.Data <= fim)
            .Select(x => new RadarLinha(
                x.Tipo,
                x.Valor,
                x.CategoriaId,
                x.SubcategoriaId,
                x.CategoriaNome,
                x.SubcategoriaNome,
                x.Data,
                x.EhInvestimento))
            .ToListAsync();
    }

    private static List<RadarVariacaoCategoria> ConstruirCategorias(
        List<RadarLinha> lancamentos, (DateOnly Inicio, DateOnly Fim) janelaAtual,
        List<DateOnly> mesesFechados, List<DateOnly> mesesBaseComDados, int diaCorte, decimal totalConsumoMes)
    {
        var resultado = new List<RadarVariacaoCategoria>();
        // A categoria "Investimento" não é consumo: fica fora de "Principais mudanças".
        var ids = lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Despesa && !l.EhInvestimento)
            .Select(l => l.CategoriaId)
            .Distinct()
            .ToList();

        foreach (var id in ids)
        {
            var daCategoria = lancamentos.Where(l => l.CategoriaId == id).ToList();
            var nome = daCategoria[0].CategoriaNome;
            var atual = Somar(daCategoria, LancamentoTipo.Despesa, janelaAtual.Inicio, janelaAtual.Fim);
            // A média da categoria usa a mesma base efetiva do total: um mês em que a
            // categoria não teve gasto entra com zero, mas não infla o denominador.
            var media = RadarCalculos.Media(mesesBaseComDados.Select(m =>
            {
                var j = Janela(m, diaCorte);
                return Somar(daCategoria, LancamentoTipo.Despesa, j.Inicio, j.Fim);
            }));

            var serieFechada = mesesFechados
                .Select(m => SomarMes(daCategoria, LancamentoTipo.Despesa, m))
                .ToList();
            var tendencia = RadarCalculos.DetectarTendencia(serieFechada);
            var subcategorias = ConstruirSubcategorias(daCategoria, janelaAtual, mesesBaseComDados, diaCorte, atual - media);

            resultado.Add(new RadarVariacaoCategoria(
                id, nome, atual, media,
                atual - media,
                RadarCalculos.VariacaoPercentual(atual, media),
                totalConsumoMes > 0m ? atual / totalConsumoMes : 0m,
                tendencia,
                MesesDePersistencia(serieFechada, tendencia),
                subcategorias.Responsavel));
        }

        return resultado.OrderByDescending(c => Math.Abs(c.VariacaoAbsoluta)).ToList();
    }

    /// <summary>
    /// Subcategorias de uma categoria e a responsável pela sua variação.
    /// <para>A direção é o sinal da variação da categoria: na alta, a responsável é a
    /// subcategoria que mais subiu; na queda, a que mais caiu. Sem variação, não há
    /// movimento a explicar e a responsável é nula.</para>
    /// <para>A participação é a fração do movimento bruto na direção da mudança: as partes
    /// somam 100% do que aconteceu e nenhuma passa de 100%.</para>
    /// </summary>
    private static (List<RadarVariacaoSubcategoria> Subcategorias, RadarVariacaoSubcategoria? Responsavel)
        ConstruirSubcategorias(
            List<RadarLinha> lancamentos, (DateOnly Inicio, DateOnly Fim) janelaAtual,
            List<DateOnly> mesesBaseComDados, int diaCorte, decimal variacaoDaCategoria)
    {
        var variacoes = new List<(Guid Id, string Nome, decimal Atual, decimal Media, decimal Delta)>();
        var grupos = lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Despesa)
            .GroupBy(l => l.SubcategoriaId ?? Guid.Empty)
            .ToList();

        foreach (var grupo in grupos)
        {
            var atual = Somar(grupo.ToList(), LancamentoTipo.Despesa, janelaAtual.Inicio, janelaAtual.Fim);
            var media = RadarCalculos.Media(mesesBaseComDados.Select(m =>
            {
                var j = Janela(m, diaCorte);
                return Somar(grupo.ToList(), LancamentoTipo.Despesa, j.Inicio, j.Fim);
            }));
            variacoes.Add((grupo.Key, grupo.First().SubcategoriaNome ?? "(sem subcategoria)",
                atual, media, atual - media));
        }

        // Direção da variação da categoria: o movimento que a explica é a alta das
        // subcategorias (quando subiu) ou a queda delas (quando caiu).
        var direcao = Math.Sign(variacaoDaCategoria);
        // Movimento bruto na direção da mudança — o mesmo denominador da responsável.
        var bruto = direcao switch
        {
            > 0 => variacoes.Sum(v => Math.Max(v.Delta, 0m)),
            < 0 => variacoes.Sum(v => Math.Abs(Math.Min(v.Delta, 0m))),
            _ => 0m
        };

        var resultado = variacoes.Select(v => new RadarVariacaoSubcategoria(
            v.Id, v.Nome, v.Atual, v.Media, v.Delta,
            RadarCalculos.VariacaoPercentual(v.Atual, v.Media),
            // Só quem se moveu na direção da mudança participa; as demais recebem 0.
            bruto > 0m && Math.Sign(v.Delta) == direcao ? Math.Abs(v.Delta) / bruto : 0m)).ToList();

        var ordenadas = resultado.OrderByDescending(s => Math.Abs(s.VariacaoAbsoluta)).ToList();
        var responsavel = direcao switch
        {
            > 0 => ordenadas.Where(s => s.VariacaoAbsoluta > 0m)
                .OrderByDescending(s => s.VariacaoAbsoluta).FirstOrDefault(),
            < 0 => ordenadas.Where(s => s.VariacaoAbsoluta < 0m)
                .OrderBy(s => s.VariacaoAbsoluta).FirstOrDefault(),
            _ => null
        };

        return (ordenadas, responsavel);
    }

    private static (DateOnly Inicio, DateOnly Fim) Janela(DateOnly mes, int diaCorte)
    {
        var inicio = new DateOnly(mes.Year, mes.Month, 1);
        var dias = DateTime.DaysInMonth(mes.Year, mes.Month);
        return (inicio, new DateOnly(mes.Year, mes.Month, Math.Min(diaCorte, dias)));
    }

    /// <summary>Janela do mês inteiro (usada na tendência, no gráfico e no histórico).</summary>
    private static (DateOnly Inicio, DateOnly Fim) JanelaMesCompleto(DateOnly mes)
        => Janela(mes, DateTime.DaysInMonth(mes.Year, mes.Month));

    /// <summary>Soma de um mês inteiro (usada na tendência, no gráfico e no histórico).</summary>
    private static decimal SomarMes(List<RadarLinha> lancamentos, LancamentoTipo tipo, DateOnly mes)
    {
        var j = JanelaMesCompleto(mes);
        return Somar(lancamentos, tipo, j.Inicio, j.Fim);
    }

    /// <summary>
    /// Consumo da janela: as despesas que não são aporte em investimento. O aporte sai
    /// do dinheiro da conta, mas não é consumo — é decisão de investir.
    /// </summary>
    private static decimal SomarConsumo(List<RadarLinha> lancamentos, DateOnly inicio, DateOnly fim)
        => lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Despesa && !l.EhInvestimento && l.Data >= inicio && l.Data <= fim)
            .Sum(l => Math.Abs(l.Valor));

    /// <summary>Aporte em investimentos da janela (categoria "Investimento").</summary>
    private static decimal SomarInvestido(List<RadarLinha> lancamentos, DateOnly inicio, DateOnly fim)
        => lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Despesa && l.EhInvestimento && l.Data >= inicio && l.Data <= fim)
            .Sum(l => Math.Abs(l.Valor));

    /// <summary>Aporte em investimentos de um mês inteiro (usado na série).</summary>
    private static decimal SomarInvestidoMes(List<RadarLinha> lancamentos, DateOnly mes)
    {
        var j = JanelaMesCompleto(mes);
        return SomarInvestido(lancamentos, j.Inicio, j.Fim);
    }

    /// <summary>Consumo de um mês inteiro (usado na série do consumo).</summary>
    private static decimal SomarConsumoMes(List<RadarLinha> lancamentos, DateOnly mes)
    {
        var j = JanelaMesCompleto(mes);
        return SomarConsumo(lancamentos, j.Inicio, j.Fim);
    }

    private static decimal Somar(List<RadarLinha> lancamentos, LancamentoTipo tipo, DateOnly inicio, DateOnly fim)
        => lancamentos
            .Where(l => l.Tipo == tipo && l.Data >= inicio && l.Data <= fim)
            .Sum(l => tipo == LancamentoTipo.Despesa ? Math.Abs(l.Valor) : Math.Max(0m, l.Valor));

    /// <summary>Um mês tem dados quando há qualquer lançamento dentro da sua janela truncada.</summary>
    private static bool TemDados(List<RadarLinha> lancamentos, DateOnly mes, int diaCorte)
    {
        var j = Janela(mes, diaCorte);
        return lancamentos.Any(l => l.Data >= j.Inicio && l.Data <= j.Fim);
    }

    /// <summary>
    /// Quantos meses seguidos o recorte vem na direção da tendência, contados a partir do mês
    /// fechado mais recente. Uma série de 3 meses estritamente crescente devolve 3: a
    /// persistência conta meses, não viradas. Tendência estável ou indefinida devolve 0.
    /// </summary>
    private static int MesesDePersistencia(IReadOnlyList<decimal> serie, RadarTendencia tendencia)
    {
        if (tendencia is not (RadarTendencia.Alta or RadarTendencia.Baixa)) return 0;
        if (serie.Count < 2) return 0;

        var crescente = tendencia == RadarTendencia.Alta;
        var passos = 0;
        for (var i = serie.Count - 1; i >= 1; i--)
        {
            var subiu = serie[i] > serie[i - 1];
            if (crescente ? !subiu : subiu) break;
            passos++;
        }
        // O primeiro mês da sequência também conta — é ele que dá início à direção.
        return passos + 1;
    }
}
