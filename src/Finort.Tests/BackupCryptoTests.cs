using System.Security.Cryptography;
using Finort.Services;

namespace Finort.Tests;

public class BackupCryptoTests
{
    [Fact]
    public void Roundtrip_RestauraPayloadOriginal()
    {
        var payload = new byte[5000];
        Random.Shared.NextBytes(payload);

        var blob = BackupCrypto.Encrypt(payload, "senhaForte1");

        var resultado = BackupCrypto.Decrypt(blob, "senhaForte1");
        Assert.Equal(payload, resultado);
    }

    [Fact]
    public void Decrypt_SenhaErrada_LancaCryptographicException()
    {
        var blob = BackupCrypto.Encrypt([1, 2, 3], "correta");
        Assert.ThrowsAny<CryptographicException>(() => BackupCrypto.Decrypt(blob, "errada"));
    }

    [Fact]
    public void Decrypt_HeaderInvalido_LancaInvalidData()
    {
        var blob = BackupCrypto.Encrypt([1, 2, 3], "correta");
        blob[0] = (byte)'X';
        Assert.ThrowsAny<InvalidDataException>(() => BackupCrypto.Decrypt(blob, "correta"));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(3_360_000)]
    public void Decrypt_IteracoesForaDoRange_LancaInvalidData(int iteracoesHostis)
    {
        var blob = BackupCrypto.Encrypt([1, 2, 3], "correta");
        const int offsetIteracoes = 6 + BackupCrypto.SaltSize;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(offsetIteracoes), iteracoesHostis);

        Assert.ThrowsAny<InvalidDataException>(() => BackupCrypto.Decrypt(blob, "correta"));
    }

    [Fact]
    public void NomeArquivo_FormatoEsperado()
    {
        var nome = BackupCrypto.NomeArquivo(new DateTime(2026, 8, 25, 14, 30, 59, 123));
        Assert.Equal("bkp_finort_260825143059123.cfbak", nome);
    }
}
