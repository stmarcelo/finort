using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class InvestimentoService
{
    private readonly AppDbContext _db;
    private readonly LancamentoService _lancamentoService;

    public InvestimentoService(AppDbContext db, LancamentoService lancamentoService)
    {
        _db = db;
        _lancamentoService = lancamentoService;
    }

    /// <summary>Cards da página Investimentos: entidade + saldo de reserva (aportes + rendimentos − resgates)
    /// + posição em cotas (compras − vendas). Duas consultas de agregação, sem N+1.</summary>
    public async Task<List<InvestimentoCard>> ListarParaCardsAsync()
    {
        var investimentos = await _db.Investimentos
            .Include(i => i.Conta)
            .OrderBy(i => i.Nome)
            .ToListAsync();

        var saldosRendimento = await _db.InvestimentosProventos
            .Where(p => p.Tipo == ProventoTipo.Rendimento)
            .GroupBy(p => p.InvestimentoId)
            .Select(g => new { g.Key, Total = g.Sum(p => p.Valor) })
            .ToListAsync();
        var mapaRendimentos = saldosRendimento.ToDictionary(s => s.Key, s => s.Total);

        var movimentos = await _db.InvestimentosMovimentos
            .Select(m => new { m.InvestimentoId, m.Tipo, m.Quantidade, m.Valor, m.Data })
            .ToListAsync();
        var mapaMovimentos = movimentos
            .GroupBy(m => m.InvestimentoId)
            .ToDictionary(g => g.Key, g =>
            {
                var aportes = g.Where(m => m.Tipo == MovimentoTipo.Aporte).Sum(m => m.Valor);
                var resgates = g.Where(m => m.Tipo == MovimentoTipo.Resgate).Sum(m => m.Valor);
                var quantidade = g.Sum(m => m.Tipo switch
                {
                    MovimentoTipo.Compra => m.Quantidade ?? 0m,
                    MovimentoTipo.Venda => -(m.Quantidade ?? 0m),
                    _ => 0m
                });
                var ultimoAporte = g.Where(m => m.Tipo == MovimentoTipo.Aporte)
                    .OrderByDescending(m => m.Data)
                    .FirstOrDefault();
                return (Aportes: aportes, Resgates: resgates, Quantidade: quantidade,
                    DataUltimoAporte: ultimoAporte?.Data, ValorUltimoAporte: ultimoAporte?.Valor ?? 0m);
            });

        return investimentos.Select(i =>
        {
            mapaMovimentos.TryGetValue(i.Id, out var mov);
            mapaRendimentos.TryGetValue(i.Id, out var rendimentos);
            return new InvestimentoCard(
                i,
                mov.Aportes + rendimentos - mov.Resgates,
                mov.Quantidade,
                mov.Aportes,
                mov.Resgates,
                rendimentos,
                mov.DataUltimoAporte,
                mov.ValorUltimoAporte);
        }).ToList();
    }

    public async Task<Investimento> ObterAsync(Guid id)
        => await _db.Investimentos.Include(i => i.Conta).FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException("Investimento não encontrado.");

    public async Task<Investimento> CriarAsync(
        string nome, TipoInvestimento tipo, Guid contaVinculadaId,
        string? subtipo, string? descricao, decimal? valorCota, DateTime? dataCotacao,
        DateTime? dataVencimento = null)
    {
        await ValidarCadastro(nome, contaVinculadaId);

        var investimento = new Investimento
        {
            Nome = nome.Trim(),
            Tipo = tipo,
            ContaVinculadaId = contaVinculadaId,
            Subtipo = NullIfVazio(subtipo),
            Descricao = NullIfVazio(descricao),
            ValorCotaAtual = valorCota ?? 0m,
            DataCotacao = dataCotacao,
            DataVencimento = dataVencimento,
            Ativo = true
        };
        _db.Investimentos.Add(investimento);
        await _db.SaveChangesAsync();
        return investimento;
    }

    public async Task AtualizarAsync(
        Guid id, string nome, TipoInvestimento tipo, Guid contaVinculadaId,
        string? subtipo, string? descricao, DateTime? dataVencimento = null)
    {
        await ValidarCadastro(nome, contaVinculadaId);
        var investimento = await ObterAsync(id);

        investimento.Nome = nome.Trim();
        investimento.Tipo = tipo;
        investimento.ContaVinculadaId = contaVinculadaId;
        investimento.Subtipo = NullIfVazio(subtipo);
        investimento.Descricao = NullIfVazio(descricao);
        investimento.DataVencimento = dataVencimento;
        await _db.SaveChangesAsync();
    }

    /// <summary>Edição inline da cotação no card: grava valor e o momento da alteração.</summary>
    public async Task AtualizarCotacaoAsync(Guid id, decimal valorCota, DateTime? dataCotacao)
    {
        if (valorCota < 0m)
            throw new ArgumentException("O valor da cota não pode ser negativo.");

        var investimento = await ObterAsync(id);
        investimento.ValorCotaAtual = valorCota;
        investimento.DataCotacao = dataCotacao ?? DateTime.Now;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Exclusão irreversível confirmada na UI: audita o investimento removido e
    /// apaga os proventos, as movimentações E os lançamentos bancários gerados por
    /// elas. Lançamentos de dividendos permanecem.
    /// </summary>
    public async Task ExcluirAsync(Guid id)
    {
        var investimento = await ObterAsync(id);
        var proventos = await _db.InvestimentosProventos
            .Where(p => p.InvestimentoId == id)
            .ToListAsync();
        var movimentos = await _db.InvestimentosMovimentos
            .Where(m => m.InvestimentoId == id)
            .ToListAsync();
        var lancamentoIds = movimentos
            .Where(m => m.LancamentoId != null)
            .Select(m => m.LancamentoId!.Value)
            .ToList();
        var lancamentosDosMovimentos = await _db.Lancamentos
            .Where(l => lancamentoIds.Contains(l.Id))
            .ToListAsync();

        _db.AuditoriasExclusaoInvestimento.Add(new AuditoriaExclusaoInvestimento
        {
            NomeInvestimento = investimento.Nome,
            Tipo = investimento.Tipo,
            ValorCotaAtual = investimento.ValorCotaAtual,
            DataCotacao = investimento.DataCotacao,
            DataExclusao = DateTime.Now
        });
        _db.InvestimentosProventos.RemoveRange(proventos);
        _db.InvestimentosMovimentos.RemoveRange(movimentos);
        _db.Lancamentos.RemoveRange(lancamentosDosMovimentos);
        _db.Investimentos.Remove(investimento);
        await _db.SaveChangesAsync();
    }

    private async Task ValidarCadastro(string nome, Guid contaVinculadaId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Informe o nome do investimento.");
        if (!await _db.Contas.AnyAsync(c => c.Id == contaVinculadaId))
            throw new ArgumentException("Conta vinculada não encontrada.");
    }

    private static string? NullIfVazio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// Registra um provento: Dividendo também cria Receita na conta vinculada
    /// (categoria Investimento › Dividendos / Rendimentos, herdando as guardas do
    /// LancamentoService); Rendimento apenas alimenta o histórico/saldo.
    /// </summary>
    public async Task<InvestimentoProvento> RegistrarProventoAsync(
        Guid investimentoId, DateOnly data, decimal valor, ProventoTipo tipo)
    {
        if (valor <= 0m)
            throw new ArgumentException("Informe um valor maior que zero.");
        if (data == default)
            throw new ArgumentException("Informe a data do provento.");

        var investimento = await ObterAsync(investimentoId);

        Lancamento? lancamento = null;
        if (tipo == ProventoTipo.Dividendo)
        {
            var categoria = await _db.Categorias.AsNoTracking().SingleAsync(c => c.Nome == "Investimento");
            var subcategoria = await _db.Subcategorias.AsNoTracking()
                .SingleAsync(s => s.Nome == "Dividendos / Rendimentos");
            lancamento = await _lancamentoService.CriarReceitaAsync(
                investimento.ContaVinculadaId, data, valor, categoria.Id, subcategoria.Id, null);
        }

        var provento = new InvestimentoProvento
        {
            InvestimentoId = investimento.Id,
            Data = data,
            Valor = valor,
            Tipo = tipo,
            LancamentoId = lancamento?.Id
        };
        _db.InvestimentosProventos.Add(provento);
        await _db.SaveChangesAsync();
        return provento;
    }

    public async Task<List<InvestimentoProvento>> ListarProventosAsync(Guid investimentoId)
        => await _db.InvestimentosProventos
            .Where(p => p.InvestimentoId == investimentoId)
            .OrderByDescending(p => p.Data)
            .ToListAsync();

    /// <summary>
    /// Registra compra/venda (ativos: quantidade × valor por cota) ou aporte/resgate (reserva: valor).
    /// Para vendas/resgates sempre gera o lançamento bancário na conta vinculada (herdando a
    /// guarda de mês fechado); para compras/aportes o lançamento de despesa só é gerado quando
    /// <paramref name="lancarNaConta"/> for verdadeiro (investimentos pré-existentes dispensam).
    /// Aplica os limites de posição/saldo e atualiza a cotação em compras/vendas.
    /// </summary>
    public async Task<InvestimentoMovimento> RegistrarMovimentoAsync(
        Guid investimentoId, DateOnly data, MovimentoTipo tipo,
        decimal? quantidade, decimal? valorPorCota, decimal? valorReserva,
        bool lancarNaConta = true)
    {
        var investimento = await ObterAsync(investimentoId);

        decimal valorTotal;
        decimal? qtdFinal;
        decimal? cotaFinal;

        if (tipo is MovimentoTipo.Compra or MovimentoTipo.Venda)
        {
            if (quantidade is null or <= 0m)
                throw new ArgumentException("Informe uma quantidade maior que zero.");
            if (valorPorCota is null or <= 0m)
                throw new ArgumentException("Informe o valor por cota maior que zero.");
            qtdFinal = quantidade;
            cotaFinal = valorPorCota;
            valorTotal = quantidade.Value * valorPorCota.Value;
        }
        else
        {
            if (valorReserva is null or <= 0m)
                throw new ArgumentException("Informe um valor maior que zero.");
            qtdFinal = null;
            cotaFinal = null;
            valorTotal = valorReserva.Value;
        }

        if (tipo == MovimentoTipo.Venda)
        {
            var posicao = await PosicaoAtualAsync(investimentoId);
            if (quantidade!.Value > posicao)
                throw new InvalidOperationException("Quantidade insuficiente para venda.");
        }
        else if (tipo == MovimentoTipo.Resgate)
        {
            var saldo = await SaldoReservaAtualAsync(investimentoId);
            if (valorReserva!.Value > saldo)
                throw new InvalidOperationException("Saldo insuficiente para resgate.");
        }

        var categoria = await _db.Categorias.AsNoTracking().SingleAsync(c => c.Nome == "Investimento");
        var ehSaida = tipo is MovimentoTipo.Compra or MovimentoTipo.Aporte;
        var nomeSubcategoria = ehSaida ? "Compra/Aporte" : "Venda / Resgate";
        var subcategoria = await _db.Subcategorias.AsNoTracking()
            .SingleAsync(s => s.Nome == nomeSubcategoria);

        Lancamento? lancamento = null;
        if (!ehSaida || lancarNaConta)
        {
            lancamento = ehSaida
                ? await _lancamentoService.CriarDespesaAsync(
                    investimento.ContaVinculadaId, data, valorTotal, categoria.Id, subcategoria.Id, null)
                : await _lancamentoService.CriarReceitaAsync(
                    investimento.ContaVinculadaId, data, valorTotal, categoria.Id, subcategoria.Id, null);
        }

        var movimento = new InvestimentoMovimento
        {
            InvestimentoId = investimento.Id,
            Data = data,
            Tipo = tipo,
            Quantidade = qtdFinal,
            ValorPorCota = cotaFinal,
            Valor = valorTotal,
            LancamentoId = lancamento?.Id
        };
        _db.InvestimentosMovimentos.Add(movimento);

        if (tipo is MovimentoTipo.Compra or MovimentoTipo.Venda)
        {
            investimento.ValorCotaAtual = valorPorCota!.Value;
            investimento.DataCotacao = data.ToDateTime(TimeOnly.MinValue);
        }

        await _db.SaveChangesAsync();
        return movimento;
    }

    private async Task<decimal> PosicaoAtualAsync(Guid investimentoId)
    {
        var movimentos = await _db.InvestimentosMovimentos
            .Where(m => m.InvestimentoId == investimentoId)
            .Select(m => new { m.Tipo, m.Quantidade })
            .ToListAsync();
        return movimentos.Sum(m => m.Tipo switch
        {
            MovimentoTipo.Compra => m.Quantidade ?? 0m,
            MovimentoTipo.Venda => -(m.Quantidade ?? 0m),
            _ => 0m
        });
    }

    private async Task<decimal> SaldoReservaAtualAsync(Guid investimentoId)
    {
        var aportesResgates = await _db.InvestimentosMovimentos
            .Where(m => m.InvestimentoId == investimentoId &&
                        (m.Tipo == MovimentoTipo.Aporte || m.Tipo == MovimentoTipo.Resgate))
            .Select(m => new { m.Tipo, m.Valor })
            .ToListAsync();
        var rendimentos = await _db.InvestimentosProventos
            .Where(p => p.InvestimentoId == investimentoId && p.Tipo == ProventoTipo.Rendimento)
            .Select(p => p.Valor)
            .ToListAsync();

        return aportesResgates.Sum(m => m.Tipo == MovimentoTipo.Aporte ? m.Valor : -m.Valor)
             + rendimentos.Sum();
    }

    /// <summary>
    /// Estorno dedicado: remove o movimento e seu lançamento bancário e reabre os meses
    /// a partir do mês da data do movimento (mesma regra dos pagamentos de cartão).
    /// </summary>
    public async Task EstornarMovimentoAsync(Guid movimentoId)
    {
        var movimento = await _db.InvestimentosMovimentos
            .FirstOrDefaultAsync(m => m.Id == movimentoId)
            ?? throw new InvalidOperationException("Movimento não encontrado.");

        if (movimento.LancamentoId is not null)
        {
            var lancamento = await _db.Lancamentos.FirstAsync(l => l.Id == movimento.LancamentoId);
            _db.Lancamentos.Remove(lancamento);
        }

        var piso = new DateOnly(movimento.Data.Year, movimento.Data.Month, 1);
        var chave = piso.Year * 12 + piso.Month;
        var mesesFechados = await _db.MesesFechados
            .Where(mf => mf.Ano * 12 + mf.Mes >= chave)
            .ToListAsync();
        _db.MesesFechados.RemoveRange(mesesFechados);

        _db.InvestimentosMovimentos.Remove(movimento);
        await _db.SaveChangesAsync();
    }

    public async Task<List<InvestimentoMovimento>> ListarMovimentosAsync(Guid investimentoId)
        => await _db.InvestimentosMovimentos
            .Where(m => m.InvestimentoId == investimentoId)
            .OrderByDescending(m => m.Data)
            .ToListAsync();

    public async Task ExcluirProventoAsync(Guid proventoId)
    {
        var provento = await _db.InvestimentosProventos.FindAsync(proventoId)
            ?? throw new InvalidOperationException("Provento não encontrado.");

        if (provento.LancamentoId.HasValue)
        {
            var lancamento = await _db.Lancamentos.FindAsync(provento.LancamentoId.Value);
            if (lancamento is not null)
            {
                _db.Lancamentos.Remove(lancamento);
            }
        }

        _db.InvestimentosProventos.Remove(provento);
        await _db.SaveChangesAsync();
    }
}
