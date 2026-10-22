using System.Globalization;

namespace Finort.App;

/// <summary>
/// Núcleo de cálculo da calculadora usada no <c>MudMoneyInput</c>.
/// Lógica pura (sem UI): visor, operandos, operadores e erros.
/// Avaliação da esquerda para a direita, sem precedência de operadores,
/// como nas calculadoras simples.
/// </summary>
public class CalculadoraMatematica
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly int _casasDecimais;

    private string _visor = "0";
    private decimal _valor;
    private decimal? _acumulado;
    private char? _operador;
    private bool _novoNumero = true;
    private bool _repetirIgual;
    private char? _ultimoOperador;
    private decimal? _ultimoOperando;
    private string? _erro;

    public CalculadoraMatematica(int casasDecimais = 2)
        => _casasDecimais = Math.Max(0, casasDecimais);

    /// <summary>Texto do visor formatado em pt-BR (ex.: "1.500,50").</summary>
    public string Visor => Formatar(_visor);

    /// <summary>Mensagem do último erro (divisão por zero) ou null.</summary>
    public string? Erro => _erro;

    /// <summary>Define o valor inicial (ex.: o valor atual do campo).</summary>
    public void DefinirValorInicial(decimal valor)
    {
        Limpar();
        _valor = Arredondar(valor);
        _visor = Texto(_valor);
        _novoNumero = true;
    }

    /// <summary>Insere um dígito no visor.</summary>
    public void Digito(char digito)
    {
        if (!char.IsAsciiDigit(digito)) return;
        if (_erro is not null) Limpar();

        if (_novoNumero)
        {
            _visor = digito.ToString();
            _novoNumero = false;
        }
        else
        {
            if (_visor.Contains(','))
            {
                if (Decimais(_visor) >= _casasDecimais) return;
            }
            else if (_visor.Length >= MaxDigitosInteiros) return;

            _visor += digito;
        }

        _valor = Converter(_visor);
        EsquecerUltimaOperacao();
    }

    /// <summary>Insere a vírgula decimal (ignorada quando não há casas decimais).</summary>
    public void Virgula()
    {
        if (_casasDecimais == 0) return;
        if (_erro is not null) Limpar();

        if (_novoNumero)
        {
            _visor = "0,";
            _novoNumero = false;
        }
        else if (!_visor.Contains(','))
        {
            _visor += ",";
        }

        _valor = Converter(_visor);
        EsquecerUltimaOperacao();
    }

    /// <summary>Define o operador pendente, encadeando o cálculo pela esquerda.</summary>
    public void Operador(char operador)
    {
        if (operador is not ('+' or '-' or '*' or '/')) return;
        if (_erro is not null) Limpar();

        if (_operador.HasValue && !_novoNumero)
        {
            var resultado = Calcular(_acumulado!.Value, _valor, _operador.Value);
            if (resultado is null)
            {
                Falhar(_operador.Value);
                return;
            }

            if (!DentroDoLimiteDoCampo(resultado.Value))
            {
                FalharLimite();
                return;
            }

            _acumulado = resultado.Value;
            _valor = resultado.Value;
            _visor = Texto(resultado.Value);
        }
        else
        {
            _acumulado = _valor;
        }

        _operador = operador;
        _novoNumero = true;
        EsquecerUltimaOperacao();
    }

    /// <summary>Aplica o percentual: com '+'/'-' usa a base acumulada; com '*'/'/' divide por 100.</summary>
    public void Porcento()
    {
        if (_erro is not null) Limpar();

        decimal percentual;

        if (_operador is '+' or '-' && _acumulado.HasValue)
        {
            // A multiplicação passa pelo mesmo caminho protegido das demais
            // operações: um produto que estoura o decimal vira erro, não exceção.
            var produto = Calcular(_acumulado.Value, _valor, '*');
            if (produto is null)
            {
                Falhar('*');
                return;
            }

            percentual = produto.Value / 100m;
        }
        else
        {
            percentual = _valor / 100m;
        }

        if (!DentroDoLimiteDoCampo(percentual))
        {
            FalharLimite();
            return;
        }

        _valor = Arredondar(percentual);
        _visor = Texto(_valor);
        _novoNumero = true;
        EsquecerUltimaOperacao();
    }

    /// <summary>Apaga o último dígito do visor.</summary>
    public void Apoiar()
    {
        if (_erro is not null) Limpar();

        _visor = _visor.Length > 1 ? _visor[..^1] : "0";
        _valor = Converter(_visor);
        _novoNumero = false;
        EsquecerUltimaOperacao();
    }

    /// <summary>Zera a calculadora (tecla C).</summary>
    public void Limpar()
    {
        _visor = "0";
        _valor = 0m;
        _acumulado = null;
        _operador = null;
        _novoNumero = true;
        _repetirIgual = false;
        _ultimoOperador = null;
        _ultimoOperando = null;
        _erro = null;
    }

    /// <summary>Executa a operação pendente. Retorna null em caso de erro.</summary>
    public decimal? Igual()
    {
        if (_erro is not null) return null;

        if (!_operador.HasValue)
        {
            if (!DentroDoLimiteDoCampo(_valor))
            {
                FalharLimite();
                return null;
            }

            _novoNumero = true;
            _repetirIgual = false;
            return _valor;
        }

        // "=" repetido: reaplica a última operação sobre o acumulado atual.
        if (_repetirIgual && _ultimoOperador.HasValue && _ultimoOperando.HasValue)
        {
            var repetido = Calcular(_acumulado!.Value, _ultimoOperando.Value, _ultimoOperador.Value);
            if (repetido is null)
            {
                Falhar(_ultimoOperador.Value);
                return null;
            }

            if (!DentroDoLimiteDoCampo(repetido.Value))
            {
                FalharLimite();
                return null;
            }

            _valor = repetido.Value;
            _visor = Texto(repetido.Value);
            _acumulado = repetido.Value;
            _novoNumero = true;
            return repetido;
        }

        var resultado = Calcular(_acumulado!.Value, _valor, _operador.Value);
        if (resultado is null)
        {
            Falhar(_operador.Value);
            return null;
        }

        if (!DentroDoLimiteDoCampo(resultado.Value))
        {
            FalharLimite();
            return null;
        }

        _ultimoOperador = _operador;
        _ultimoOperando = _valor;

        _valor = resultado.Value;
        _visor = Texto(resultado.Value);
        _acumulado = resultado.Value;
        _novoNumero = true;
        _repetirIgual = true;
        return resultado;
    }

    private void Falhar(char operador)
        => FalharComMensagem(operador == '/'
            ? "Não é possível dividir por zero."
            : "Cálculo fora do limite suportado.");

    /// <summary>Interrompe o cálculo porque o resultado não cabe no campo de destino.</summary>
    private void FalharLimite()
        => FalharComMensagem("Cálculo fora do limite suportado.");

    private void FalharComMensagem(string mensagem)
    {
        _erro = mensagem;
        _operador = null;
        _acumulado = null;
        _novoNumero = true;
        _repetirIgual = false;
    }

    private void EsquecerUltimaOperacao()
    {
        _repetirIgual = false;
        _ultimoOperador = null;
        _ultimoOperando = null;
    }

    private static decimal? Calcular(decimal esquerdo, decimal direito, char operador)
    {
        try
        {
            return operador switch
            {
                '+' => esquerdo + direito,
                '-' => esquerdo - direito,
                '*' => esquerdo * direito,
                '/' => direito == 0m ? null : esquerdo / direito,
                _ => null
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private string Texto(decimal valor)
        => Arredondar(valor).ToString("F" + _casasDecimais, PtBr);

    private string Formatar(string texto)
    {
        var valor = Converter(texto);
        var casas = Decimais(texto);
        var formatado = valor.ToString("N" + casas, PtBr);
        return texto.EndsWith(',') ? formatado + "," : formatado;
    }

    private decimal Arredondar(decimal valor)
        => Math.Round(valor, _casasDecimais, MidpointRounding.AwayFromZero);

    private static decimal Converter(string texto)
    {
        var limpo = texto.Replace(",", ".").TrimEnd('.');
        if (string.IsNullOrEmpty(limpo)) return 0m;
        return decimal.Parse(limpo, CultureInfo.InvariantCulture);
    }

    private static int Decimais(string texto)
    {
        var i = texto.IndexOf(',');
        return i < 0 ? 0 : texto.Length - i - 1;
    }

    private int MaxDigitosInteiros => Math.Max(1, MaxDigitosTotais - _casasDecimais);

    /// <summary>
    /// Limite total de dígitos (inteiro + decimais) aceitos pelo campo,
    /// igual ao limite da máscara de dinheiro (<c>moneyInterop.js</c>).
    /// </summary>
    private int MaxDigitosTotais => _casasDecimais > 2 ? 24 : 15;

    /// <summary>
    /// Verifica se o valor cabe no campo: o total de dígitos (inteiro + decimais)
    /// não pode passar do limite da máscara.
    /// </summary>
    private bool DentroDoLimiteDoCampo(decimal valor)
    {
        var texto = Math.Abs(valor).ToString("F" + _casasDecimais, PtBr);
        return texto.Count(char.IsAsciiDigit) <= MaxDigitosTotais;
    }
}
