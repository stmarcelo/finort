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

    protected override async Task OnInitializedAsync()
    {
        if (LancamentoId is null) return;

        var pernas = await LancamentoService.ObterPernasAsync(LancamentoId.Value);
        var principal = pernas.FirstOrDefault(p => p.Id == LancamentoId.Value) ?? pernas[0];
        _data = principal.Data.ToDateTime(TimeOnly.MinValue);
        _valor = Math.Abs(principal.Valor);

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
        }
    }

    private async Task OnCategoriaChanged(Guid? categoriaId)
    {
        _categoriaId = categoriaId;
        await SubcategoriaSelectNotificar(categoriaId);
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

        var data = DateOnly.FromDateTime(_data.Value);
        _salvando = true;
        try
        {
            if (LancamentoId is null)
            {
                if (Tipo == LancamentoTipo.Transferencia)
                {
                    if (_contaOrigemId is null || _contaDestinoId is null)
                    {
                        Snackbar.Add("Informe as duas contas.", Severity.Error);
                        return;
                    }
                    await LancamentoService.CriarTransferenciaAsync(_contaOrigemId.Value, _contaDestinoId.Value, data, _valor.Value);
                }
                else
                {
                    if (_contaId is null || _categoriaId is null)
                    {
                        Snackbar.Add("Informe a conta e a categoria.", Severity.Error);
                        return;
                    }
                    if (Tipo == LancamentoTipo.Receita)
                        await LancamentoService.CriarReceitaAsync(_contaId.Value, data, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId, _projetoId);
                    else
                        await LancamentoService.CriarDespesaAsync(_contaId.Value, data, _valor.Value, _categoriaId.Value, _subcategoriaId, _pessoaId, _projetoId);
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
                    await LancamentoService.AtualizarReceitaDespesaAsync(LancamentoId.Value, _contaId!.Value, data, _valor.Value, _categoriaId!.Value, _subcategoriaId, _pessoaId, _projetoId);
                }
            }

            Snackbar.Add("Lançamento salvo.", Severity.Success);
            if (OnSaved.HasDelegate)
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
}
