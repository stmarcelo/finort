using Finort.App;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Finort.Components;

public partial class MudMoneyInput : MudBaseInput<decimal?>, IDisposable
{
    private ElementReference _wrapper;
    private DotNetObjectReference<MudMoneyInput>? _dotnetRef;
    private string _display = string.Empty;
    private bool _jsAttached;

    private readonly Dictionary<string, object> _userAttributes = new()
    {
        ["inputmode"] = "numeric",
        ["autocomplete"] = "off"
    };

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>Casas decimais aceitas (2 padrão; 8 para cripto).</summary>
    [Parameter] public int CasasDecimais { get; set; } = 2;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _display = Value.HasValue
            ? MoneyInputFormatter.Formatar(MoneyInputFormatter.DigitosDeValor(Value.Value, CasasDecimais), CasasDecimais)
            : string.Empty;
    }

    protected override Task OnParametersSetAsync()
    {
        // Reflete valores definidos externamente (ex.: recarga de grade) sem reescrever
        // o texto enquanto o usuário digita (comparação pelo texto formatado).
        var textoEsperado = Value.HasValue
            ? MoneyInputFormatter.Formatar(MoneyInputFormatter.DigitosDeValor(Value.Value, CasasDecimais), CasasDecimais)
            : string.Empty;

        if (!string.Equals(_display, textoEsperado, StringComparison.Ordinal)
            && MoneyInputFormatter.Formatar(MoneyInputFormatter.FiltrarDigitos(_display), CasasDecimais) != textoEsperado)
        {
            _display = textoEsperado;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !Disabled && !ReadOnly)
        {
            try
            {
                _dotnetRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("moneyInterop.attach", _dotnetRef, _wrapper, CasasDecimais);
                _jsAttached = true;
            }
            catch (Exception)
            {
                // Sem JS (pré-render) ou falha de interop: o fallback servidor em OnInput continua funcionando.
            }
        }
    }

    /// <summary>Chamado pelo moneyInterop.js a cada tecla, já com os dígitos limpos e o DOM formatado.</summary>
    [JSInvokable]
    public async Task OnDigitsJs(string digitos)
    {
        _display = MoneyInputFormatter.Formatar(digitos, CasasDecimais);
        await SetValueAsync(MoneyInputFormatter.ParaValor(digitos, CasasDecimais));
    }

    /// <summary>Fallback: caso o JS não esteja anexado, filtra no servidor.</summary>
    private async Task OnInput(string text)
    {
        if (_jsAttached) return; // JS cuida da formatação; evitar dupla escrita no DOM.

        var digitos = MoneyInputFormatter.FiltrarDigitos(text);
        _display = MoneyInputFormatter.Formatar(digitos, CasasDecimais);
        await SetValueAsync(MoneyInputFormatter.ParaValor(digitos, CasasDecimais));
        StateHasChanged();
    }

    private Dictionary<string, object> MergeUserAttributes()
    {
        foreach (var attr in UserAttributes)
        {
            _userAttributes[attr.Key] = attr.Value;
        }
        return _userAttributes;
    }

    public void Dispose()
        => _dotnetRef?.Dispose();
}
