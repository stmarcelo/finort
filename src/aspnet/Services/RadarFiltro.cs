using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

/// <summary>
/// Filtro central do Radar Financeiro. A regra de reembolso (spec §4) mora aqui e é aplicada
/// ANTES de qualquer agregação — nenhuma análise posterior precisa conhecê-la.
/// </summary>
public static class RadarFiltro
{
    /// <summary>Categoria em que o app registra aporte/compra de investimento
    /// (InvestimentoService.cs:378). O app já depende desse nome.</summary>
    public const string NomeCategoriaInvestimento = "Investimento";

    /// <summary>
    /// Remove despesas de cartão marcadas como reembolsadas (Reembolso.LancamentoId) e
    /// receitas originadas de reembolso (Reembolso.ReceitaId).
    /// </summary>
    public static IQueryable<Lancamento> SemReembolso(this IQueryable<Lancamento> query, AppDbContext db)
        => query.Where(l => !db.Reembolsos.Any(r => r.LancamentoId == l.Id)
                         && !db.Reembolsos.Any(r => r.ReceitaId == l.Id));

    /// <summary>
    /// Base de lançamentos do Radar: apenas receita e despesa, sem reembolso.
    /// Transferências são movimentação interna e ficam de fora.
    /// </summary>
    public static IQueryable<Lancamento> BaseRadar(this IQueryable<Lancamento> query, AppDbContext db)
        => query.Where(l => l.Tipo == LancamentoTipo.Receita || l.Tipo == LancamentoTipo.Despesa)
                .SemReembolso(db);
}
