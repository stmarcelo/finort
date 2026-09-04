using System.Security.Cryptography;
using System.Text;

namespace Finort.Services;

public static class BackupCrypto
{
    private static readonly byte[] Magic = "CFBK1\0"u8.ToArray();

    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;
    public const int Iterations = 210_000;

    public static byte[] Encrypt(byte[] plaintext, string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var chave = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(chave, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write(salt);
            w.Write(Iterations);
            w.Write(nonce);
            w.Write((long)ciphertext.Length);
            w.Write(ciphertext);
            w.Write(tag);
            w.Flush();
        }
        return ms.ToArray();
    }

    public static byte[] Decrypt(byte[] blob, string senha)
    {
        try
        {
            using var ms = new MemoryStream(blob);
            using var r = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var magic = r.ReadBytes(Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic))
                throw new InvalidDataException("Arquivo de backup inválido.");

            var salt = r.ReadBytes(SaltSize);
            var iterations = r.ReadInt32();
            if (iterations < Iterations / 2 || iterations > Iterations * 8)
                throw new InvalidDataException("Arquivo de backup inválido.");
            var nonce = r.ReadBytes(NonceSize);
            var ctLen = r.ReadInt64();
            var ciphertext = r.ReadBytes((int)ctLen);
            var tag = r.ReadBytes(TagSize);

            var chave = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iterations, HashAlgorithmName.SHA256, KeySize);
            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(chave, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex)
        {
            throw new CryptographicException(
                "Não foi possível descriptografar o backup. Senha incorreta ou arquivo corrompido.", ex);
        }
    }

    public static string NomeArquivo(DateTime agora)
        => $"bkp_finort_{agora:yyMMddHHmmssfff}.cfbak";

    // --- LEGADO: cifra reversível usada apenas para migrar bancos antigos.
    // A senha de backup é armazenada como hash PBKDF2 desde a correção de segurança
    // (AuthService.DefinirSenhaBackupAsync); este código pode ser removido quando
    // não houver mais instalações antigas para migrar. NUNCA exiba ou recriptografe
    // valores a partir daqui.
    private static readonly byte[] SeedKeyLegado = System.Text.Encoding.UTF8.GetBytes(
        "CF-BackupSeed-2026!@#Finort$%^Reversible");

    public static string DecryptStringLegado(string cipherBase64)
    {
        var data = Convert.FromBase64String(cipherBase64);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(SeedKeyLegado, Encoding.UTF8.GetBytes("CF-BkpPwd-Salt"), 100_000, HashAlgorithmName.SHA256, 32);
        var iv = new byte[16];
        var cipher = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        Buffer.BlockCopy(data, 16, cipher, 0, cipher.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
