using System.Security.Cryptography;

namespace MuDickLand.Updater;

public static class Security
{
    public static bool VerifyManifestSignature(byte[] manifestBytes, byte[] signatureBytes)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(UpdaterConfig.ManifestPublicKeyPem);
        return rsa.VerifyData(
            manifestBytes,
            signatureBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static async Task<string> Sha256FileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

