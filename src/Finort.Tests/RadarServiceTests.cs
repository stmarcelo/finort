using System.Globalization;
using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class RadarServiceTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    private static readonly CultureInfo CulturaPtBr = new("pt-BR");

    private static (AppDbContext Db, string File, Conta Conta, CartaoCredito Cartao, Categoria Despesa, Categoria Receita, Subcategoria Sub) Setup()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        var despesa = new Categoria { Nome = "Mercado" };
        var receita = new Categoria { Nome = "Receita" };
        db.Categorias.Add(despesa);
        db.Categorias.Add(receita);
        db.SaveChanges();
        var sub = new Subcategoria { Nome = "Delivery", CategoriaId = despesa.Id };
        db.Subcategorias.Add(sub);
        db.SaveChanges();
        return (db, file, conta, cartao, despesa, receita, sub);
    }

    private static Lancamento CriarLancamento(AppDbContext db, DateOnly data, LancamentoTipo tipo, decimal valor,
        Guid categoriaId, Guid? subcategoriaId = null, Guid? cartaoId = null, DateOnly? vencimento = null)
    {
        var l = new Lancamento
        {
            Data = data,
            Tipo = tipo,
            Valor = tipo == LancamentoTipo.Despesa ? -Math.Abs(valor) : Math.Abs(valor),
            CategoriaId = categoriaId,
            SubcategoriaId = subcategoriaId,
            CartaoCreditoId = cartaoId,
            DataVencimentoCartao = vencimento,
            Confirmado = true
        };
        db.Lancamentos.Add(l);
        db.SaveChanges();
        return l;
    }

    [Fact]
    public async Task ObterAsync_SemLancamentos_DevolveTotaisZerados()
    {
        var (db, file, _, _, _, _, _) = Setup();
        try
        {
            var service = new RadarService(db);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(0m, r.TotalReceitas);
            Assert.Equal(0m, r.TotalConsumo);
            Assert.Empty(r.Categorias);
            Assert.Null(r.Score);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_MesParcial_ComparaMesmoNumeroDeDias()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Meses base (jul/ago/set): 500 no dia 5.
            CriarLancamento(db, new DateOnly(2026, 7, 5), LancamentoTipo.Despesa, 500m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 8, 5), LancamentoTipo.Despesa, 500m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 9, 5), LancamentoTipo.Despesa, 500m, despesa.Id);
            // Setembro também tem 1000 no dia 25, depois do corte do mês atual (dia 20).
            CriarLancamento(db, new DateOnly(2026, 9, 25), LancamentoTipo.Despesa, 1000m, despesa.Id);
            // Outubro (mês atual): 600 no dia 5.
            CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 600m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.True(r.MesParcial);
            Assert.Equal(20, r.DiaCorte);
            // Só o lançamento do dia 5 entra no mês atual.
            Assert.Equal(600m, r.TotalConsumo);
            // A média da base ignora o lançamento de 25/09 (truncamento pelo mesmo nº de dias).
            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(500m, categoria.MediaHistorica);
            Assert.Equal(100m, categoria.VariacaoAbsoluta);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_DespesaDeCartaoUsaVencimentoDaFatura()
    {
        var (db, file, _, cartao, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Compra em setembro, fatura vencendo em 10/10: conta no mês de outubro.
            CriarLancamento(db, new DateOnly(2026, 9, 25), LancamentoTipo.Despesa, 400m, despesa.Id,
                cartaoId: cartao.Id, vencimento: new DateOnly(2026, 10, 10));
            // Compra em outubro, fatura vencendo em 10/11: ainda não entra em nenhum mês da janela.
            CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 900m, despesa.Id,
                cartaoId: cartao.Id, vencimento: new DateOnly(2026, 11, 10));

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(400m, r.TotalConsumo);
            Assert.Equal(400m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 10).Consumo);
            // A série tem os 3 meses fechados (zerados) e o mês atual: a fatura de 10/11
            // não entra em nenhum ponto da série nem nos totais.
            Assert.Equal(4, r.SerieMensal.Count);
            Assert.Equal(400m, r.SerieMensal.Sum(s => s.Consumo));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_DataEfetivaForaDaJanela_NaoEntraNosTotais()
    {
        var (db, file, _, cartao, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Compra dentro da janela, fatura vencendo depois do fim da janela (novembro).
            CriarLancamento(db, new DateOnly(2026, 9, 20), LancamentoTipo.Despesa, 400m, despesa.Id,
                cartaoId: cartao.Id, vencimento: new DateOnly(2026, 11, 15));
            // Compra dentro da janela, fatura vencendo antes do começo da janela (junho).
            CriarLancamento(db, new DateOnly(2026, 8, 20), LancamentoTipo.Despesa, 700m, despesa.Id,
                cartaoId: cartao.Id, vencimento: new DateOnly(2026, 6, 15));
            // Só este lançamento (sem cartão) tem data efetiva dentro do mês atual.
            CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 100m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(100m, r.TotalConsumo);
            Assert.Equal(100m, r.SerieMensal.Sum(s => s.Consumo));
            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(0m, categoria.MediaHistorica);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_ReembolsoNaoEntraNosTotais()
    {
        var (db, file, _, cartao, despesa, receita, _) = Setup();
        try
        {
            var service = new RadarService(db);
            var despesaCartao = CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 100m, despesa.Id,
                cartaoId: cartao.Id, vencimento: new DateOnly(2026, 10, 10));
            var receitaReembolso = CriarLancamento(db, new DateOnly(2026, 10, 6), LancamentoTipo.Receita, 100m, receita.Id);
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = db.Pessoas.Add(new Pessoa { Nome = "A" }).Entity.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesaCartao.Id,
                ReceitaId = receitaReembolso.Id,
                Valor = -100m,
                Vencimento = new DateOnly(2026, 10, 9)
            });
            db.SaveChanges();

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(0m, r.TotalConsumo);
            Assert.Equal(0m, r.TotalReceitas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_TransferenciaNaoEntraNosTotais()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            var l = new Lancamento
            {
                Data = new DateOnly(2026, 10, 5),
                Tipo = LancamentoTipo.Transferencia,
                Valor = -900m,
                CategoriaId = despesa.Id,
                Confirmado = true
            };
            db.Lancamentos.Add(l);
            db.SaveChanges();

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(0m, r.TotalConsumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_ComHistorico_calculaScoreEInsights()
    {
        var (db, file, _, _, despesa, receita, _) = Setup();
        try
        {
            var service = new RadarService(db);
            for (var m = 7; m <= 9; m++)
            {
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Receita, 8000m, receita.Id);
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 5000m, despesa.Id);
            }
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Receita, 8000m, receita.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 7000m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.NotNull(r.Score);
            Assert.Equal(8000m, r.TotalReceitas);
            Assert.Equal(7000m, r.TotalConsumo);
            Assert.Equal(1000m, r.Saldo);
            Assert.Contains(r.Insights, i => i.Tipo == "consumo_total_acima_da_media");
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_FiltroPorPessoa_Aplica()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            var pessoa = db.Pessoas.Add(new Pessoa { Nome = "Ana" }).Entity;
            db.SaveChanges();
            var outro = db.Pessoas.Add(new Pessoa { Nome = "Bia" }).Entity;
            db.SaveChanges();
            var l1 = CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 100m, despesa.Id);
            l1.PessoaId = pessoa.Id;
            var l2 = CriarLancamento(db, new DateOnly(2026, 10, 6), LancamentoTipo.Despesa, 500m, despesa.Id);
            l2.PessoaId = outro.Id;
            db.SaveChanges();

            var r = await service.ObterAsync(3, pessoa.Id, Hoje);

            Assert.Equal(100m, r.TotalConsumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterDetalheCategoriaAsync_RetornaHistoricoESubcategorias()
    {
        var (db, file, _, _, despesa, _, sub) = Setup();
        try
        {
            var service = new RadarService(db);
            for (var m = 7; m <= 10; m++)
            {
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 300m, despesa.Id, sub.Id);
                CriarLancamento(db, new DateOnly(2026, m, 12), LancamentoTipo.Despesa, 700m, despesa.Id);
            }

            var detalhe = await service.ObterDetalheCategoriaAsync(despesa.Id, 3, null, Hoje);

            Assert.NotNull(detalhe);
            Assert.Equal("Mercado", detalhe!.Nome);
            Assert.Equal(4, detalhe.Historico.Count);
            Assert.Contains(detalhe.Subcategorias, s => s.Nome == "Delivery");
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterDetalheCategoriaAsync_CategoriaSemLancamentos_RetornaNulo()
    {
        var (db, file, _, _, _, _, _) = Setup();
        try
        {
            var service = new RadarService(db);

            var detalhe = await service.ObterDetalheCategoriaAsync(Guid.NewGuid(), 3, null, Hoje);

            Assert.Null(detalhe);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_HistoricoInsuficiente_NaoGeraScore()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Só dois meses fechados com dados: menos de 3 meses fechados não há score.
            CriarLancamento(db, new DateOnly(2026, 8, 10), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 9, 10), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 100m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Null(r.Score);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_HistoricoForaDaBaseDeComparacao_NaoGeraScore()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Janela de 6 meses: a base de comparação são jul/ago/set, que estão vazios.
            // Os dados de abr/mai/jun não alimentam a base — logo, não habilitam score.
            for (var m = 4; m <= 6; m++)
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 500m, despesa.Id);

            var r = await service.ObterAsync(6, null, Hoje);

            Assert.Null(r.Score);
            Assert.Equal(500m, r.TotalConsumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_JanelaDe6Meses_ComparaComOs3MesesMaisRecentes()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Os 3 meses mais antigos (abr/mai/jun) valem 1000; a base (jul/ago/set) vale 300.
            for (var m = 4; m <= 6; m++)
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, despesa.Id);
            for (var m = 7; m <= 9; m++)
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 300m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 300m, despesa.Id);

            var r = await service.ObterAsync(6, null, Hoje);

            // A janela pedida é 6 meses...
            Assert.Equal(6, r.MesesHistorico);
            // ...mas a base de comparação são os 3 meses fechados mais recentes (jul/ago/set).
            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(300m, categoria.MediaHistorica);
            Assert.Equal(0m, categoria.VariacaoAbsoluta);
            Assert.Equal(300m, r.TotalConsumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_MesDe30DiasNaBase_UsaOCorteDoMes()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // 31/10: o dia de corte é 31, mas setembro tem 30 dias — a janela do mês base
            // para no dia 30, senão a data 31/09 nem existe.
            var fimDeOutubro = new DateOnly(2026, 10, 31);
            CriarLancamento(db, new DateOnly(2026, 7, 10), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 8, 10), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 9, 30), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 31), LancamentoTipo.Despesa, 100m, despesa.Id);

            var r = await service.ObterAsync(3, null, fimDeOutubro);

            Assert.Equal(31, r.DiaCorte);
            Assert.False(r.MesParcial);
            // Setembro entra inteiro (30 dias): a base vale 100 por mês.
            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(100m, categoria.MediaHistorica);
            Assert.Equal(100m, r.TotalConsumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_TendenciaDeAlta_ContaOsMesesEmPersistencia()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Três meses fechados estritamente crescentes: a persistência conta meses, não viradas.
            CriarLancamento(db, new DateOnly(2026, 7, 10), LancamentoTipo.Despesa, 100m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 8, 10), LancamentoTipo.Despesa, 200m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 9, 10), LancamentoTipo.Despesa, 300m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 400m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(RadarTendencia.Alta, categoria.Tendencia);
            Assert.Equal(3, categoria.Persistencia);
            // A frase do insight só sai a partir de 3 meses de persistência.
            var insight = r.Insights.Single(i => i.Tipo == "categoria_aumentou");
            Assert.Contains("Vem subindo há 3 meses", insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    private static Guid CriarSubcategoria(AppDbContext db, Guid categoriaId, string nome)
    {
        var sub = new Subcategoria { Nome = nome, CategoriaId = categoriaId };
        db.Subcategorias.Add(sub);
        db.SaveChanges();
        return sub.Id;
    }

    /// <summary>
    /// Base (jul/ago/set): Delivery 100/mês e Padaria 400/mês — a categoria vale 500/mês
    /// (média 500). No mês atual: Delivery 300 (sobe 200) e Padaria 100 (cai 300) — a
    /// categoria vale 400 e CAI 20%.
    /// </summary>
    private static void MontarCategoriaQueCaiu(AppDbContext db, Guid categoriaId, Guid deliveryId, Guid padariaId)
    {
        for (var m = 7; m <= 9; m++)
        {
            CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 100m, categoriaId, deliveryId);
            CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 400m, categoriaId, padariaId);
        }
        CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 300m, categoriaId, deliveryId);
        CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 100m, categoriaId, padariaId);
    }

    [Fact]
    public async Task ObterAsync_CategoriaQueCaiu_NaoCitaASubcategoriaQueSubiu()
    {
        var (db, file, _, _, despesa, _, delivery) = Setup();
        try
        {
            var service = new RadarService(db);
            var padaria = CriarSubcategoria(db, despesa.Id, "Padaria");
            MontarCategoriaQueCaiu(db, despesa.Id, delivery.Id, padaria);

            var r = await service.ObterAsync(3, null, Hoje);

            var insight = Assert.Single(r.Insights, i => i.Tipo == "categoria_diminuiu");
            // A categoria caiu: a subcategoria que subiu não explica a redução.
            Assert.DoesNotContain("Delivery", insight.Descricao);
            Assert.Contains("Padaria", insight.Descricao);
            Assert.Contains("da redução", insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterDetalheCategoriaAsync_CategoriaQueCaiu_ResponsavelEASubcategoriaQueCaiu()
    {
        var (db, file, _, _, despesa, _, delivery) = Setup();
        try
        {
            var service = new RadarService(db);
            var padaria = CriarSubcategoria(db, despesa.Id, "Padaria");
            MontarCategoriaQueCaiu(db, despesa.Id, delivery.Id, padaria);

            var detalhe = await service.ObterDetalheCategoriaAsync(despesa.Id, 3, null, Hoje);

            Assert.NotNull(detalhe);
            // A categoria caiu: a responsável é a subcategoria que caiu, não a que subiu.
            Assert.NotNull(detalhe!.SubcategoriaResponsavel);
            Assert.Equal("Padaria", detalhe.SubcategoriaResponsavel!.Nome);
            Assert.True(detalhe.SubcategoriaResponsavel.VariacaoAbsoluta < 0m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterDetalheCategoriaAsync_ParticipacaoEAFacaoDoMovimentoBruto()
    {
        var (db, file, _, _, despesa, _, delivery) = Setup();
        try
        {
            var service = new RadarService(db);
            var padariaId = CriarSubcategoria(db, despesa.Id, "Padaria");
            // Base (jul/ago/set): Delivery 50/mês e Padaria 100/mês — média da categoria 150.
            for (var m = 7; m <= 9; m++)
            {
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 50m, despesa.Id, delivery.Id);
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 100m, despesa.Id, padariaId);
            }
            // Mês atual: Delivery 200 (+150) e Padaria 50 (−50) — a categoria sobe 100.
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 200m, despesa.Id, delivery.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 50m, despesa.Id, padariaId);

            var detalhe = await service.ObterDetalheCategoriaAsync(despesa.Id, 3, null, Hoje);

            Assert.NotNull(detalhe);
            var subDelivery = Assert.Single(detalhe!.Subcategorias, s => s.Nome == "Delivery");
            var subPadaria = Assert.Single(detalhe.Subcategorias, s => s.Nome == "Padaria");
            // Nenhuma parte passa de 100% e as duas somam o movimento bruto (150 de alta).
            Assert.All(detalhe.Subcategorias, s => Assert.InRange(s.ParticipacaoNaVariacao, 0m, 1m));
            Assert.Equal(1m, subDelivery.ParticipacaoNaVariacao);
            Assert.Equal(0m, subPadaria.ParticipacaoNaVariacao);
            Assert.Equal(1m, subDelivery.ParticipacaoNaVariacao + subPadaria.ParticipacaoNaVariacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_SemHistoricoFechado_NaoComparaComUmaMediaInexistente()
    {
        var (db, file, _, _, despesa, receita, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Só o mês atual tem lançamentos: não existe média com que comparar.
            CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Despesa, 1000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 5), LancamentoTipo.Receita, 800m, receita.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(0, r.MesesBase);
            // Nenhum insight de "acima/abaixo da média" — nem de total, nem de categoria.
            Assert.DoesNotContain(r.Insights, i => i.Tipo.Contains("da_media"));
            Assert.DoesNotContain(r.Insights, i => i.Tipo is "categoria_aumentou" or "categoria_diminuiu");
            // E nenhuma comparação de crescimento entre consumo e receitas.
            Assert.DoesNotContain(r.Insights,
                i => i.Tipo is "consumo_crescem_mais_que_receitas" or "receitas_crescem_mais_que_consumo");
            // Sem base não há variação a medir: o 100% sentinela não é uma medição.
            Assert.Equal(0m, r.VariacaoPercentualConsumo);
            Assert.Equal(0m, r.VariacaoPercentualReceitas);
            Assert.Null(r.Score);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_UmMesDeHistorico_UsaOUnicoMesDaMedia()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Só setembro tem lançamentos: a média é o valor de setembro, não 1/3 dele.
            CriarLancamento(db, new DateOnly(2026, 9, 10), LancamentoTipo.Despesa, 1000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 2000m, despesa.Id);

            var r = await service.ObterAsync(3, null,Hoje);

            Assert.Equal(1, r.MesesBase);
            var insight = Assert.Single(r.Insights, i => i.Tipo == "consumo_total_acima_da_media");
            Assert.Contains("da sua média do último mês", insight.Descricao);
            // A média é a do único mês com dados (1.000), então a variação é o dobro:
            // o percentual vem do próprio cálculo — medição, não sentinela da média zero.
            Assert.Contains(1m.ToString("P1", CulturaPtBr), insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_DoisMesesDeHistorico_UsaOsDoisMesesDaMedia()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Agosto 1000 e setembro 3000 → média 2000 (e não 4000/3).
            CriarLancamento(db, new DateOnly(2026, 8, 10), LancamentoTipo.Despesa, 1000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 9, 10), LancamentoTipo.Despesa, 3000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 4000m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(2, r.MesesBase);
            var insight = Assert.Single(r.Insights, i => i.Tipo == "consumo_total_acima_da_media");
            Assert.Contains("da sua média dos últimos 2 meses", insight.Descricao);
            // A média é a dos dois meses com dados (2.000), então a variação é o dobro:
            // o percentual vem do próprio cálculo — medição, não sentinela da média zero.
            Assert.Contains(1m.ToString("P1", CulturaPtBr), insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_MediaDaCategoriaUsaOsMesesComDados()
    {
        var (db, file, _, _, despesa, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Só setembro tem lançamentos: R$ 1.000 na categoria. A média da categoria é
            // 1.000 (1 mês com dados), não 333,33 (3 meses, dois deles vazios).
            CriarLancamento(db, new DateOnly(2026, 9, 10), LancamentoTipo.Despesa, 1000m, despesa.Id);
            CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 1500m, despesa.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            var categoria = Assert.Single(r.Categorias);
            Assert.Equal(1000m, categoria.MediaHistorica);
            Assert.Equal(500m, categoria.VariacaoAbsoluta);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_ScoreContaTodasAsCategoriasAcimaDoPadrao()
    {
        var (db, file, _, _, _, _, _) = Setup();
        try
        {
            var service = new RadarService(db);
            // Base com dados para existir score...
            var fixa = new Categoria { Nome = "Fixos" };
            db.Categorias.Add(fixa);
            db.SaveChanges();
            for (var m = 7; m <= 10; m++)
                CriarLancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, fixa.Id);
            // ...e 9 categorias acima do padrão só no mês atual: a tela mostra 8 insights,
            // mas o score precisa contar todas as categorias fora do padrão.
            for (var i = 0; i < 9; i++)
            {
                var categoria = new Categoria { Nome = $"Extra {i}" };
                db.Categorias.Add(categoria);
                db.SaveChanges();
                CriarLancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 100m, categoria.Id);
            }

            var r = await service.ObterAsync(3, null, Hoje);

            // A tela exibe no máximo 8 insights, mas o score conta todas as categorias fora do padrão.
            Assert.Equal(8, r.Insights.Count);
            Assert.NotNull(r.Score);
            // 9 categorias de atenção: -1 ponto cada (o teto é -15).
            Assert.Contains(r.Score!.Componentes, c => c.Rotulo.Contains("categoria") && c.Pontos == -9);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
