using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

/// <summary>Uma linha do relatório: saldo líquido de uma pessoa em um mês.</summary>
public sealed record CategoriaLinha(DateOnly Mes, string PessoaNome, string? Cor, decimal Total);

public sealed record CategoriaRelatorio(DateOnly Inicio, DateOnly Fim, string CategoriaNome, string? SubcategoriaNome,
    decimal TotalGeral, decimal MediaMensal, int Meses, IReadOnlyList<CategoriaLinha> Linhas);

public class CategoriaRelatorioService
{
    private readonly AppDbContext _db;
    public CategoriaRelatorioService(AppDbContext db) => _db = db;

    /// <summary>Do primeiro dia de seis meses atrás ao último dia do mês corrente.</summary>
    public Task<(DateOnly Inicio, DateOnly Fim)> PeriodoPadraoAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-6);
        var fim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
        return Task.FromResult((inicio, fim));
    }

    /// <summary>
    /// Saldo líquido por mês e pessoa de uma categoria (receitas positivas e despesas
    /// negativas se compensam). Transferências ficam de fora; despesas de cartão entram
    /// pelo vencimento da fatura e demais lançamentos pela data do lançamento.
    /// </summary>
    public async Task<CategoriaRelatorio> GerarAsync(DateOnly inicio, DateOnly fim, Guid categoriaId, Guid? subcategoriaId)
    {
        DateOnly DataCriterio(Lancamento l)
            => l.CartaoCreditoId != null ? (l.DataVencimentoCartao ?? l.Data) : l.Data;

        var query = _db.Lancamentos
            .Include(l => l.Pessoa)
            .Where(l => l.Tipo != LancamentoTipo.Transferencia)
            .Where(l => (l.CartaoCreditoId != null ? (l.DataVencimentoCartao ?? l.Data) : l.Data) >= inicio
                     && (l.CartaoCreditoId != null ? (l.DataVencimentoCartao ?? l.Data) : l.Data) <= fim)
            .Where(l => l.CategoriaId == categoriaId
                     || (l.SubcategoriaId.HasValue && l.Subcategoria!.CategoriaId == categoriaId));

        if (subcategoriaId.HasValue)
            query = query.Where(l => l.SubcategoriaId == subcategoriaId.Value);

        var lista = await query.ToListAsync();

        var categoriaNome = (await _db.Categorias.FindAsync(categoriaId))?.Nome ?? "Categoria não encontrada";
        string? subcategoriaNome = subcategoriaId.HasValue
            ? (await _db.Subcategorias.FindAsync(subcategoriaId.Value))?.Nome
            : null;

        var totalGeral = lista.Sum(l => l.Valor);

        var linhas = lista
            .GroupBy(l => new { Mes = new DateOnly(DataCriterio(l).Year, DataCriterio(l).Month, 1), l.PessoaId })
            .Select(g =>
            {
                var pessoa = g.First().Pessoa;
                return new CategoriaLinha(g.Key.Mes, pessoa?.Nome ?? "Sem pessoa",
                    pessoa?.CorDeExibicao, g.Sum(l => l.Valor));
            })
            .OrderBy(l => l.Mes)
            .ThenBy(l => l.PessoaNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A média é sobre os meses que efetivamente têm lançamentos, não sobre o
        // intervalo do filtro (que pode incluir meses sem movimento).
        var meses = linhas.Select(l => l.Mes).Distinct().Count();
        var mediaMensal = meses == 0 ? 0m : totalGeral / meses;

        return new CategoriaRelatorio(inicio, fim, categoriaNome, subcategoriaNome,
            totalGeral, mediaMensal, meses, linhas);
    }
}
