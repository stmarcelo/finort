using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

/// <summary>Régua de agendamento de provisões, compartilhada por sincronismo, calendário e fluxo.</summary>
public static class ProvisaoAgenda
{
    public static int IntervaloEmMeses(ProvisaoFrequencia frequencia) => frequencia switch
    {
        ProvisaoFrequencia.Trimestral => 3,
        ProvisaoFrequencia.Semestral => 6,
        ProvisaoFrequencia.Anual => 12,
        _ => 1
    };

    /// <summary>
    /// Projeta em memória ocorrências de provisões ainda não sincronizadas com Data em [inicio, fim].
    /// Réplica da régua do SincronizarAsync: períodos seguintes ao último lançado, alinhados à
    /// frequência, pulando meses fechados. Nada é gravado no banco; quando o login sincronizar,
    /// os lançamentos reais substituirão as projeções.
    /// </summary>
    public static async Task<List<(DateOnly Data, Provisao Provisao)>> ProjetarAsync(
        AppDbContext db, DateOnly inicio, DateOnly fim)
    {
        var provisoes = await db.Provisoes
            .Include(p => p.Pessoa)
            .Include(p => p.Categoria)
            .Include(p => p.Conta)
            .Include(p => p.CartaoCredito)
            .ToListAsync();

        var resultado = new List<(DateOnly, Provisao)>();
        if (provisoes.Count == 0)
            return resultado;

        var mesesFechados = (await db.MesesFechados.ToListAsync())
            .Select(m => m.Ano * 12 + m.Mes)
            .ToHashSet();

        foreach (var provisao in provisoes)
        {
            // Sem último sincronismo: o próximo login materializará; não projetar.
            if (provisao.UltimoAnoLancado is null || provisao.UltimoMesLancado is null)
                continue;

            var intervalo = IntervaloEmMeses(provisao.Frequencia);
            var periodo = new DateOnly(provisao.UltimoAnoLancado.Value, provisao.UltimoMesLancado.Value, 1)
                .AddMonths(intervalo);
            var limite = new DateOnly(fim.Year, fim.Month, 1);

            while (periodo <= limite)
            {
                if (!mesesFechados.Contains(periodo.Year * 12 + periodo.Month))
                {
                    var ultimoDia = DateTime.DaysInMonth(periodo.Year, periodo.Month);
                    var data = new DateOnly(periodo.Year, periodo.Month, Math.Min(provisao.Dia, ultimoDia));

                    if (data >= inicio && data <= fim)
                        resultado.Add((data, provisao));
                }

                periodo = periodo.AddMonths(intervalo);
            }
        }

        return resultado;
    }
}
