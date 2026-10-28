using Finort.App;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Finort.Components.Lancamentos;

public partial class LancamentoForm : AppComponentBase
{
    [Parameter] public LancamentoTipo Tipo { get; set; }
    [Parameter] public Guid? LancamentoId { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }
    [Parameter] public EventCallback<Lancamento> OnCriado { get; set; }

    private MudForm _form = null!;
    private bool _salvando;

    private Guid? _contaId;
    private Guid? _contaOrigemId;
    private Guid? _contaDestinoId;
    private Guid? _categoriaId;
    private Guid? _subcategoriaId;
    private Guid? _pessoaId;
    private Guid? _projetoId;
    private DateTime? _data;
    private decimal? _valor;
    private bool _repetir;
    private int _quantidadeRepeticoes = 2;
    private bool _temGrupo;
    private bool _confirmado;
    private string? _observacao;
    private bool _reembolso;
    private Guid? _reembolsoPessoaId;
    private DateTime? _reembolsoData;

    protected override async Task OnInitializedAsync()
    {
        if (LancamentoId is null) return;

        var pernas = await LancamentoService.ObterPernasAsync(LancamentoId.Value);
        var principal = pernas.FirstOrDefault(p => p.Id == LancamentoId.Value) ?? pernas[0];
        _data = principal.Data.ToDateTime(TimeOnly.MinValue);
        _valor = Math.Abs(principal.Valor);
        _temGrupo = principal.ParcelamentoId.HasValue || principal.RecorrenciaId.HasValue;

        if (principal.Tipo == LancamentoTipo.Transferencia)
        {
            var origem = pernas.Single(p => p.Valor < 0);
            var destino = pernas.Single(p => p.Valor > 0);
            _contaOrigemId = origem.ContaId;
            _contaDestinoId = destino.ContaId;
        }
        else
        {
            _contaId = principal.ContaId;
            _categoriaId = principal.CategoriaId;
            _pessoaId = principal.PessoaId;
            _projetoId = principal.ProjetoId;
            _subcategoriaId = principal.SubcategoriaId;
            _observacao = principal.Observacao;
        }

        if (principal.Tipo == LancamentoTipo.Despesa && principal.ReferenciaId is not null)
        {
            var espelho = pernas.FirstOrDefault(p => p.Id != principal.Id
                && p.Tipo == LancamentoTipo.Receita);
            if (espelho is not null)
            {
                _reembolso = true;
                _reembolsoPessoaId = espelho.PessoaId;
                _reembolsoData = espelho.Data.ToDateTime(TimeOnly.MinValue);
            }
        }
    }

    private async Task OnCategoriaChanged(Guid? categoriaId)
    {
        _categoriaId = categoriaId;
        await SubcategoriaSelectNotificar(categoriaId);
    }

    private Task OnContaChanged(Guid? contaId)
    {
        _contaId = contaId;
        if (contaId is null) _confirmado = false;
        return Task.CompletedTask;
    }

    private Task SubcategoriaSelectNotificar(Guid? categoriaId)
    {
        _subcategoriaId = null;
        return Task.CompletedTask;
    }

    private async Task Salvar()
    {
        await _form.Validate();
        if (!_form.IsValid || _salvando) return;

        if (_valor is null || _valor <= 0)
        {
            Snackbar.Add("Informe um valor maior que zero.", Severity.Error);
            return;
        }
        if (_data is null)
        {
            Snackbar.Add("Informe a data do lançamento.", Severity.Error);
            return;
        }
        if (_observacao?.Length > 500)
        {
            Snackbar.Add("Observação deve ter no máximo 500 caracteres.", Severity.Error);
            return;
        }
        if (_confirmado && _contaId is null)
        {
            Snackbar.Add("Selecione uma conta para usar \"Já confirmado\".", Severity.Error);
            return;
        }
        if (Tipo == LancamentoTipo.Despesa && _reembolso && _reembolsoPessoaId is null)
        {
            Snackbar.Add("Informe a pessoa do reembolso.", Severity.Error);
            return;
        }

        var data = DateOnly.FromDateTime(_data.Value);
        if (Tipo == LancamentoTipo.Despesa && _reembolso
            && _reembolsoData is not null
            && DateOnly.FromDateTime(_reembolsoData.Value) < data)
        {
            Snackbar.Add("A data do reembolso não pode ser anterior à data da despesa.", Severity.Error);
            return;
        }
        _salvando = true;
        try
        {
            Lancamento? criado = null;
            if (LancamentoId is null)
            {
                if (Tipo == LancamentoTipo.Transferencia)
                {
                    if (_contaOrigemId is null || _contaDestinoId is null)
                    {
                        Snackbar.Add("Informe as duas contas.", Severity.Error);
                        return;
                    }
                    var transferencia = await LancamentoService.CriarTransferenciaAsync(_contaOrigemId.Value, _contaDestinoId.Value, data, _valor.Value);
                    criado = transferencia.Destino;
                }
                else
                {
                    if (_categoriaId is null)
                    {
                        Snackbar.Add("Informe a categoria.", Severity.Error);
                        return;
                    }
                    if (Tipo == LancamentoTipo.Despesa && _reembolso)
                    {
                        var dataReembolso = DateOnly.FromDateTime(_reembolsoData ?? _data!.Value);
                        criado = (await LancamentoService.CriarDespesaComReembolsoAsync(
                            _contaId, data, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId,
                            _projetoId, _confirmado, _observacao,
                            _repetir ? _quantidadeRepeticoes : 1,
                            _reembolsoPessoaId!.Value, dataReembolso))[0];
                    }
                    else if (_repetir)
                        criado = (await LancamentoService.CriarRecorrenteAsync(Tipo, _contaId, data, _valor.Value,
                            RecorrenciaFrequencia.Mensal, _quantidadeRepeticoes, _categoriaId.Value, _subcategoriaId, _pessoaId, _projetoId, _confirmado, _observacao))[0];
                    else if (Tipo == LancamentoTipo.Receita)
                        criado = await LancamentoService.CriarReceitaAsync(_contaId, data, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId, _projetoId, _confirmado, _observacao);
                    else
                        criado = await LancamentoService.CriarDespesaAsync(_contaId, data, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId, _projetoId, _confirmado, _observacao);
                }
            }
            else
            {
                if (Tipo == LancamentoTipo.Transferencia)
                {
                    if (_contaOrigemId is null || _contaDestinoId is null)
                    {
                        Snackbar.Add("Informe as duas contas.", Severity.Error);
                        return;
                    }
                    await LancamentoService.AtualizarTransferenciaAsync(LancamentoId.Value, _contaOrigemId.Value, _contaDestinoId.Value, data, _valor.Value);
                }
                else
                {
                    var atualizarFuturos = await PerguntarAtualizarFuturosAsync();
                    await LancamentoService.AtualizarReceitaDespesaAsync(LancamentoId.Value, _contaId, data,
                        _valor.Value, _categoriaId!.Value, _subcategoriaId, _pessoaId, _projetoId,
                        atualizarFuturos: atualizarFuturos, observacao: _observacao,
                        reembolsoPessoaId: Tipo == LancamentoTipo.Despesa && _reembolso ? _reembolsoPessoaId : null,
                        reembolsoData: Tipo == LancamentoTipo.Despesa && _reembolso
                            ? DateOnly.FromDateTime(_reembolsoData ?? _data!.Value)
                            : null);
                }
            }

            Snackbar.Add("Lançamento salvo.", Severity.Success);
            if (OnCriado.HasDelegate && criado is not null)
                await OnCriado.InvokeAsync(criado);
            else if (OnSaved.HasDelegate)
                await OnSaved.InvokeAsync();
            else
                Navigation.NavigateTo("/lancamentos");
        }
        catch (LancamentoConfirmadoException ex)
        {
            var nomes = string.Join("; ", ex.Confirmados.Select(l => $"{l.Data:dd/MM/yyyy} — {l.Valor.ToString("N2")}"));
            Snackbar.Add($"Não é possível alterar lançamento(s) confirmado(s): {nomes}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _salvando = false;
        }
    }

    private async Task<bool> PerguntarAtualizarFuturosAsync()
    {
        if (LancamentoId is null) return false;

        var lancamento = await LancamentoService.ObterAsync(LancamentoId.Value);
        if (lancamento is null) return false;

        Guid? grupoId = lancamento.ParcelamentoId ?? lancamento.RecorrenciaId;
        if (!grupoId.HasValue) return false;

        var temFuturos = await LancamentoService.TemLancamentosFuturosNoGrupoAsync(LancamentoId.Value, grupoId.Value);
        if (!temFuturos) return false;

        var confirm = await DialogService.ShowMessageBox("Alterar lançamentos futuros",
            "Deseja aplicar as alterações também nos lançamentos futuros não confirmados deste grupo?",
            "Sim", "Não", "Cancelar",
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        return confirm == true;
    }
}
