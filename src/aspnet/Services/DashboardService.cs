using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly CartaoCreditoService _cartaoService;

    public DashboardService(AppDbContext db, CartaoCreditoService cartaoService)
    {
        _db = db;
        _cartaoService = cartaoService;
    }

    /// <summary>Agregados do período. Em meses futuros ao mês corrente, as provisões projetadas
    /// somam nas respectivas categorias (regra da spec).</summary>
    public async Task<DashboardMes> ObterAsync(DateOnly inicio, DateOnly fim)
    {
        var lancamentos = await _db.Lancamentos
            .Where(l => l.Data >= inicio && l.Data <= fim &&
                        (l.Tipo == LancamentoTipo.Despesa || l.Tipo == LancamentoTipo.Receita))
            .Select(l => new
            {
                l.Data,
                l.Tipo,
                l.Valor,
                CategoriaNome = l.Categoria != null ? l.Categoria.Nome : "",
                SubcategoriaNome = l.Subcategoria != null ? l.Subcategoria.Nome : null,
                PessoaNome = l.Pessoa != null ? l.Pessoa.Nome : null
            })
            .ToListAsync();

        var despesasDoMes = lancamentos.Where(l => l.Tipo == LancamentoTipo.Despesa).ToList();
        var receitasDoMes = lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita).ToList();

        var despesasPorCategoria = despesasDoMes
            .GroupBy(l => l.CategoriaNome)
            .ToDictionary(g => g.Key, g => Math.Max(0m, -g.Sum(l => l.Valor)));
        var receitasPorCategoria = receitasDoMes
            .GroupBy(l => l.CategoriaNome)
            .ToDictionary(g => g.Key, g => Math.Max(0m, g.Sum(l => l.Valor)));
        despesasPorCategoria = despesasPorCategoria.Where(kv => kv.Value > 0m)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        receitasPorCategoria = receitasPorCategoria.Where(kv => kv.Value > 0m)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var futuro = fim > new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));
        if (futuro)
        {
            var projecoes = await ProvisaoAgenda.ProjetarAsync(_db, inicio, fim);
            foreach (var p in projecoes)
            {
                var nomeCategoria = p.Provisao.Categoria?.Nome ?? "";
                if (p.Provisao.Onde == ProvisaoOnde.Receita)
                    receitasPorCategoria[nomeCategoria] =
                        receitasPorCategoria.GetValueOrDefault(nomeCategoria) + p.Provisao.Valor;
                else
                    despesasPorCategoria[nomeCategoria] =
                        despesasPorCategoria.GetValueOrDefault(nomeCategoria) + p.Provisao.Valor;
            }
        }

        var topDespesas = despesasDoMes
            .GroupBy(l => new { l.CategoriaNome, l.SubcategoriaNome, l.PessoaNome })
            .Select(g => new LancamentoTop(
                RotuloCategoria(g.Key.CategoriaNome, g.Key.SubcategoriaNome),
                g.Key.PessoaNome,
                Math.Abs(g.Sum(l => l.Valor))))
            .OrderByDescending(l => l.Valor)
            .Take(10)
            .ToList();
        var topReceitas = receitasDoMes
            .GroupBy(l => new { l.CategoriaNome, l.SubcategoriaNome, l.PessoaNome })
            .Select(g => new LancamentoTop(
                RotuloCategoria(g.Key.CategoriaNome, g.Key.SubcategoriaNome),
                g.Key.PessoaNome,
                g.Sum(l => l.Valor)))
            .OrderByDescending(l => l.Valor)
            .Take(10)
            .ToList();

        var totalReceitas = receitasDoMes.Sum(l => Math.Max(0m, l.Valor));
        var totalDespesas = despesasDoMes.Sum(l => Math.Abs(l.Valor));
        var taxaPoupanca = totalReceitas > 0m ? (totalReceitas - totalDespesas) / totalReceitas * 100m : 0m;

        var tendencia = await ObterTendenciaMensalAsync(fim);
        var utilizacao = await ObterUtilizacaoCartoesAsync();

        return new DashboardMes(inicio, fim,
            Ordenar(despesasPorCategoria), Ordenar(receitasPorCategoria),
            topDespesas, topReceitas,
            await CalcularPatrimoniosAsync(),
            await CalcularContasAsync(),
            totalReceitas, totalDespesas, taxaPoupanca,
            tendencia, utilizacao);
    }

    private async Task<List<InvestimentoPatrimonio>> CalcularPatrimoniosAsync()
    {
        var investimentos = await _db.Investimentos
            .Where(i => i.Ativo)
            .OrderBy(i => i.Nome)
            .ToListAsync();

        var movimentos = await _db.InvestimentosMovimentos
            .Select(m => new { m.InvestimentoId, m.Tipo, m.Quantidade, m.Valor })
            .ToListAsync();

        var rendimentos = await _db.InvestimentosProventos
            .Where(p => p.Tipo == ProventoTipo.Rendimento)
            .Select(p => new { p.InvestimentoId, p.Valor })
            .ToListAsync();

        return investimentos.Select(i =>
        {
            if (i.Tipo == TipoInvestimento.Reserva)
            {
                var movs = movimentos.Where(m => m.InvestimentoId == i.Id).ToList();
                var aportes = movs.Where(m => m.Tipo == MovimentoTipo.Aporte).Sum(m => m.Valor);
                var resgates = movs.Where(m => m.Tipo == MovimentoTipo.Resgate).Sum(m => m.Valor);
                var rendimentosDoInvestimento = rendimentos
                    .Where(r => r.InvestimentoId == i.Id).Sum(r => r.Valor);
                return new InvestimentoPatrimonio(i.Nome, aportes + rendimentosDoInvestimento - resgates);
            }

            var quantidade = movimentos
                .Where(m => m.InvestimentoId == i.Id)
                .Sum(m => m.Tipo switch
                {
                    MovimentoTipo.Compra => m.Quantidade ?? 0m,
                    MovimentoTipo.Venda => -(m.Quantidade ?? 0m),
                    _ => 0m
                });
            return new InvestimentoPatrimonio(i.Nome, quantidade * i.ValorCotaAtual);
        }).ToList();
    }

    private async Task<List<ContaPatrimonio>> CalcularContasAsync()
    {
        var contas = await _db.Contas.OrderBy(c => c.Nome).ToListAsync();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var fimDoMes = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));
        var saldos = await _db.Lancamentos
            .Where(l => l.ContaId != null && l.Data <= fimDoMes)
            .GroupBy(l => l.ContaId!.Value)
            .Select(g => new { ContaId = g.Key, Valor = g.Sum(l => l.Valor) })
            .ToListAsync();
        var mapa = saldos.ToDictionary(s => s.ContaId, s => s.Valor);
        return contas.Select(c => new ContaPatrimonio(c.Nome, c.Banco, mapa.GetValueOrDefault(c.Id))).ToList();
    }

    private static List<CategoriaValor> Ordenar(Dictionary<string, decimal> mapa)
        => mapa.OrderByDescending(kv => kv.Value).Select(kv => new CategoriaValor(kv.Key, kv.Value)).ToList();

    private static string RotuloCategoria(string categoriaNome, string? subcategoriaNome)
        => string.IsNullOrWhiteSpace(subcategoriaNome) ? categoriaNome : $"{categoriaNome} › {subcategoriaNome}";

    private async Task<List<MesTendencia>> ObterTendenciaMensalAsync(DateOnly fim)
    {
        var inicio = fim.AddMonths(-5);
        var lancamentos = await _db.Lancamentos
            .Where(l => l.Data >= new DateOnly(inicio.Year, inicio.Month, 1) && l.Data <= fim &&
                        (l.Tipo == LancamentoTipo.Despesa || l.Tipo == LancamentoTipo.Receita))
            .Select(l => new { l.Data, l.Tipo, l.Valor })
            .ToListAsync();

        var resultado = new List<MesTendencia>();
        for (var i = 0; i < 6; i++)
        {
            var mes = inicio.AddMonths(i);
            var mesInicio = new DateOnly(mes.Year, mes.Month, 1);
            var mesFim = new DateOnly(mes.Year, mes.Month, DateTime.DaysInMonth(mes.Year, mes.Month));

            var lancamentosMes = lancamentos.Where(l => l.Data >= mesInicio && l.Data <= mesFim).ToList();
            var receitas = lancamentosMes.Where(l => l.Tipo == LancamentoTipo.Receita).Sum(l => Math.Max(0m, l.Valor));
            var despesas = lancamentosMes.Where(l => l.Tipo == LancamentoTipo.Despesa).Sum(l => Math.Abs(l.Valor));

            resultado.Add(new MesTendencia(mes.Year, mes.Month, receitas, despesas));
        }
        return resultado;
    }

    private async Task<List<CartaoUtilizacao>> ObterUtilizacaoCartoesAsync()
    {
        var resumos = await _cartaoService.ListarComSaldoAsync();
        return resumos
            .Where(c => c.Ativo)
            .Select(c => new CartaoUtilizacao(
                $"{c.Banco} •••• {c.Ultimos4Digitos}",
                c.Limite,
                Math.Abs(c.TotalNaoPago),
                c.LimiteDisponivel))
            .ToList();
    }
}
