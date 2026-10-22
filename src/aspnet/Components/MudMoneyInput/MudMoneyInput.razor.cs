using Finort.App;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    private Calculadora? _calculadora;
    private bool _calculadoraAberta;
    private EventCallback<MouseEventArgs> _onAdornmentClick;

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>Casas decimais aceitas (2 padrão; 8 para cripto).</summary>
    [Parameter] public int CasasDecimais { get; set; } = 2;

    /// <summary>Exibe o botão de adornment que abre a calculadora.</summary>
    [Parameter] public bool MostrarCalculadora { get; set; } = true;

    /// <summary>
    /// Verdadeiro quando o botão da calculadora deve aparecer: precisa estar
    /// habilitado, editável e sem adornment definido pelo chamador (o
    /// MudTextField tem apenas um slot de adornment).
    /// </summary>
    private bool MostrarBotaoCalculadora =>
        MostrarCalculadora
        && !Disabled
        && !ReadOnly
        && string.IsNullOrEmpty(AdornmentIcon)
        && string.IsNullOrEmpty(AdornmentText);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _display = Value.HasValue
            ? MoneyInputFormatter.Formatar(MoneyInputFormatter.DigitosDeValor(Value.Value, CasasDecimais), CasasDecimais)
            : string.Empty;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Só registra o handler quando o botão existe: com delegate, o MudBlazor
        // renderiza o ícone do adornment como botão clicável.
        _onAdornmentClick = MostrarBotaoCalculadora
            ? new EventCallback<MouseEventArgs>(this, AlternarCalculadora)
            : default;
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

    /// <summary>Abre ou fecha a calculadora (clique no adornment do campo).</summary>
    private Task AlternarCalculadora(MouseEventArgs _)
    {
        if (!MostrarBotaoCalculadora) return Task.CompletedTask;

        if (_calculadoraAberta)
        {
            return FecharCalculadora();
        }

        _calculadoraAberta = true;

        // O conteúdo do MudPopover só existe enquanto o popover está aberto:
        // ao abrir, a Calculadora é criada do zero e recebe o valor atual do
        // campo por ValorInicial. Nada precisa ser reiniciado aqui.
        return Task.CompletedTask;
    }

    /// <summary>Aplica no campo o resultado confirmado pela calculadora.</summary>
    private async Task AplicarResultado(decimal resultado)
    {
        if (resultado < 0) return; // o componente Calculadora já bloqueia negativos

        var digitos = MoneyInputFormatter.DigitosDeValor(resultado, CasasDecimais);
        var valor = MoneyInputFormatter.ParaValor(digitos, CasasDecimais);

        _display = MoneyInputFormatter.Formatar(digitos, CasasDecimais);

        await SetValueAsync(valor);

        try
        {
            await JSRuntime.InvokeVoidAsync("moneyInterop.setText", _wrapper, _display);
        }
        catch (Exception)
        {
            // Sem JS: o valor já foi aplicado no modelo pelo SetValueAsync.
        }

        _calculadora = null; // a instância é descartada junto com o popover
        _calculadoraAberta = false;
    }

    /// <summary>
    /// Fecha o popover sem confirmação (clique fora ou novo clique no
    /// adornment) e dispara o cancelamento da calculadora.
    /// </summary>
    private async Task FecharCalculadora()
    {
        var calculadora = _calculadora;
        _calculadora = null; // a instância é descartada junto com o popover
        _calculadoraAberta = false;

        // Fechou sem confirmar: o contrato da Calculadora exige o cancelamento.
        if (calculadora is not null)
            await calculadora.OnCancelar.InvokeAsync();
    }

    /// <summary>Cancelamento da calculadora (fechamento sem confirmação).</summary>
    private void CancelarCalculadora()
    {
        _calculadoraAberta = false;
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
