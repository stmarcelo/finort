using Updater;

namespace Finort.Tests;

/// <summary>
/// Desfechos do relançamento e honestidade das mensagens do updater.
/// O <see cref="UpdatePlan"/> é a lógica pura; o relançamento em si (I/O) fica no Program.cs.
/// </summary>
public class UpdaterTests
{
    private const string CaminhoLog = @"C:\Users\teste\AppData\Local\Temp\finort-updater\updater.log";

    [Fact]
    public void MensagemFalha_AppEncerradoERelancado_DizQueFoiReaberto()
    {
        var msg = UpdatePlan.MensagemFalha(appEncerrado: true, relancadoComSucesso: true, caminhoLog: CaminhoLog);

        Assert.Contains("e o Finort foi reaberto", msg);
        Assert.DoesNotContain("não foi reaberto", msg);
        Assert.DoesNotContain("não foi fechado", msg);
        Assert.Contains(CaminhoLog, msg);
    }

    [Fact]
    public void MensagemFalha_AppEncerradoERelancamentoFalhou_DizQueNaoFoiReabertoEComoAbrir()
    {
        var msg = UpdatePlan.MensagemFalha(appEncerrado: true, relancadoComSucesso: false, caminhoLog: CaminhoLog);

        Assert.Contains("não foi reaberto", msg);
        Assert.Contains("Abra o Finort manualmente", msg);
        Assert.DoesNotContain("e o Finort foi reaberto", msg);
        Assert.DoesNotContain("não foi fechado", msg);
        Assert.Contains(CaminhoLog, msg);
    }

    [Fact]
    public void MensagemFalha_AppNaoEncerrado_DizQueNaoFoiFechado()
    {
        var msg = UpdatePlan.MensagemFalha(appEncerrado: false, relancadoComSucesso: false, caminhoLog: CaminhoLog);

        Assert.Contains("O Finort não foi fechado", msg);
        Assert.Contains("nada foi instalado", msg);
        Assert.DoesNotContain("reaberto", msg);
        Assert.Contains(CaminhoLog, msg);
    }

    [Fact]
    public void DeveTentarNovamente_PrimeiraTentativaMorreu_RetentaUmaVez()
    {
        Assert.True(UpdatePlan.DeveTentarNovamente(tentativa: 1, appPermaneceuRodando: false));
    }

    [Fact]
    public void DeveTentarNovamente_SegundaTentativaMorreu_NaoRetenta()
    {
        Assert.False(UpdatePlan.DeveTentarNovamente(tentativa: 2, appPermaneceuRodando: false));
    }

    [Fact]
    public void DeveTentarNovamente_AppPermaneceuRodando_NaoRetenta()
    {
        Assert.False(UpdatePlan.DeveTentarNovamente(tentativa: 1, appPermaneceuRodando: true));
    }

    [Fact]
    public void DeveRelancarNoCatch_AppJaVoltou_NaoRelancaDeNovo()
    {
        // O app já foi relançado com sucesso: o catch não pode relançar uma segunda vez.
        // A segunda instância morre na porta 5298 e o usuário receberia "não foi reaberto"
        // enquanto o app está rodando.
        Assert.False(UpdatePlan.DeveRelancarNoCatch(appEncerrado: true, relancadoComSucesso: true));
    }

    [Fact]
    public void DeveRelancarNoCatch_AppEncerradoERelancamentoFalhou_Relanca()
    {
        Assert.True(UpdatePlan.DeveRelancarNoCatch(appEncerrado: true, relancadoComSucesso: false));
    }

    [Fact]
    public void DeveRelancarNoCatch_AppNaoEncerrado_NaoRelanca()
    {
        // O app nunca foi fechado: não há o que reabrir.
        Assert.False(UpdatePlan.DeveRelancarNoCatch(appEncerrado: false, relancadoComSucesso: false));
    }

    [Fact]
    public void MensagemExeNaoEncontrado_DizQueOAppNaoFoiReaberto()
    {
        var msg = UpdatePlan.MensagemExeNaoEncontrado(@"C:\Program Files\Finort");

        Assert.Contains("Finort.exe não encontrado", msg);
        Assert.Contains(@"C:\Program Files\Finort", msg);
        Assert.Contains("não foi reaberto", msg);
    }

    [Fact]
    public void MensagemRelancado_ContemOPid()
    {
        var msg = UpdatePlan.MensagemRelancado(4242);

        Assert.Contains("relançado", msg);
        Assert.Contains("4242", msg);
    }

    [Fact]
    public void MensagemNaoManteveEmExecucao_ContemOExitCode()
    {
        var msg = UpdatePlan.MensagemNaoManteveEmExecucao(-1073741510);

        Assert.Contains("não se manteve em execução", msg);
        Assert.Contains("-1073741510", msg);
    }
}
