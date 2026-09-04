using Finort.App;
using Finort.Data;
using Finort.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace Finort.Components.Lancamentos;

public partial class DespesaCartaoForm : AppComponentBase
{
    public sealed record DadosDespesaCartao(
        Guid CartaoId, DateOnly Data, decimal Valor,
        Guid CategoriaId, Guid? SubcategoriaId, Guid? PessoaId,
        int? Parcelas, Guid? ReembolsoPessoaId, Guid? ReembolsoContaId,
        bool EhEntrada, Guid? ProjetoId,
        DateOnly? DataVencimentoCartao);

    [Parameter] public Guid? CartaoIdFixo { get; set; }
    [Parameter] public int? AnoFatura { get; set; }
    [Parameter] public int? MesFatura { get; set; }
    [Parameter] public EventCallback<DadosDespesaCartao> OnSalvarValido { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }
    [Parameter] public Guid? LancamentoId { get; set; }

    [Inject] private AppDbContext Db { get; set; } = null!;

    private MudForm _form = null!;
    private Guid? _cartaoId;
    private DateTime? _dataCompra;
    private decimal? _valor;
    private Guid? _categoriaId;
    private Guid? _subcategoriaId;
    private Guid? _pessoaId;
    private Guid? _projetoId;
    private bool _parcelado;
    private int _quantidadeParcelas = 2;
    private bool _comReembolso;
    private Guid? _reembolsoPessoaId;
    private Guid? _reembolsoContaId;
    private bool _ehEntradaBack;
    private DateOnly? _previewVencimento;
    private int _vencimentoAno;
    private int _vencimentoMes;

    /// <summary>Property para @bind-Value: ao marcar entrada, limpa parcelamento e reembolso.</summary>
    private bool EhEntrada
    {
        get => _ehEntradaBack;
        set
        {
            _ehEntradaBack = value;
            if (value)
            {
                _parcelado = false;
                _comReembolso = false;
                _projetoId = null;
            }
        }
    }

    private DateTime? _minDate => AnoFatura is null ? null : new DateTime(AnoFatura.Value, MesFatura!.Value, 1);
    private DateTime? _maxDate => AnoFatura is null
        ? null : new DateTime(AnoFatura.Value, MesFatura.Value, DateTime.DaysInMonth(AnoFatura.Value, MesFatura.Value));

    protected override async Task OnInitializedAsync()
    {
        if (CartaoIdFixo is not null)
        {
            _cartaoId = CartaoIdFixo;
        }

        if (LancamentoId.HasValue)
        {
            await CarregarLancamentoAsync();
        }
    }

    private async Task CarregarLancamentoAsync()
    {
        var lancamento = await Db.Lancamentos.FindAsync(LancamentoId!.Value);
        if (lancamento is null) return;

        _cartaoId = lancamento.CartaoCreditoId;
        _dataCompra = new DateTime(lancamento.Data.Year, lancamento.Data.Month, lancamento.Data.Day);
        _valor = Math.Abs(lancamento.Valor);
        _categoriaId = lancamento.CategoriaId;
        _subcategoriaId = lancamento.SubcategoriaId;
        _pessoaId = lancamento.PessoaId;
        _projetoId = lancamento.ProjetoId;

        await AtualizarPreviewAsync();
    }

    private async Task OnCartaoChanged(Guid? cartaoId)
    {
        _cartaoId = cartaoId;
        await AtualizarPreviewAsync();
    }

    private async Task OnDataChanged(DateTime? value)
    {
        _dataCompra = value;
        await AtualizarPreviewAsync();
    }

    private async Task AtualizarPreviewAsync()
    {
        _previewVencimento = null;
        if (AnoFatura is not null || !_cartaoId.HasValue || !_dataCompra.HasValue) return;

        var cartao = await CartaoCreditoService.ObterAsync(_cartaoId.Value);
        if (cartao is not null)
        {
            _previewVencimento = CartaoCreditoService.CalcularVencimento(
                cartao, DateOnly.FromDateTime(_dataCompra.Value));
            _vencimentoAno = _previewVencimento.Value.Year;
            _vencimentoMes = _previewVencimento.Value.Month;
        }
    }

    private async Task NavegarVencimento(int delta)
    {
        var novoMes = _vencimentoMes + delta;
        var novoAno = _vencimentoAno;
        if (novoMes < 1) { novoMes = 12; novoAno--; }
        else if (novoMes > 12) { novoMes = 1; novoAno++; }

        if (await EhMesFechadoAsync(novoAno, novoMes)) return;

        _vencimentoMes = novoMes;
        _vencimentoAno = novoAno;
        _previewVencimento = new DateOnly(_vencimentoAno, _vencimentoMes,
            Math.Min(_previewVencimento!.Value.Day, DateTime.DaysInMonth(_vencimentoAno, _vencimentoMes)));
        StateHasChanged();
    }

    private async Task<bool> EhMesFechadoAsync(int ano, int mes)
    {
        if (!_cartaoId.HasValue) return false;
        try
        {
            return await FaturaService.EhFechadaAsync(_cartaoId.Value, ano, mes);
        }
        catch
        {
            Snackbar.Add("Erro ao verificar fatura. Navegação bloqueada.", Severity.Warning);
            return true;
        }
    }

    /// <summary>Valida o formulário e, se válido, invoca OnSalvarValido. Retorna true quando salvou.</summary>
    public async Task<bool> ValidarESalvarAsync()
    {
        await _form.Validate();
        if (!_form.IsValid) return false;

        if (_valor is null or <= 0) { Snackbar.Add("Informe um valor maior que zero.", Severity.Error); return false; }
        if (_dataCompra is null) { Snackbar.Add("Informe a data da compra.", Severity.Error); return false; }
        if (_cartaoId is null) { Snackbar.Add("Selecione o cartão.", Severity.Error); return false; }
        if (_categoriaId is null) { Snackbar.Add("Selecione a categoria.", Severity.Error); return false; }

        var dataCompra = DateOnly.FromDateTime(_dataCompra.Value);
        if (AnoFatura is not null &&
            (dataCompra.Year != AnoFatura.Value || dataCompra.Month != MesFatura!.Value))
        {
            Snackbar.Add($"A data deve estar dentro do mês {MesFatura:D2}/{AnoFatura}.", Severity.Error);
            return false;
        }

        await OnSalvarValido.InvokeAsync(new DadosDespesaCartao(
            _cartaoId.Value, dataCompra, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId,
            EhEntrada ? null : _parcelado ? _quantidadeParcelas : null,
            !EhEntrada && _comReembolso ? _reembolsoPessoaId : null,
            !EhEntrada && _comReembolso ? _reembolsoContaId : null,
            EhEntrada,
            EhEntrada ? null : _projetoId,
            _previewVencimento));
        return true;
    }
}
