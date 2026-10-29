using System.IO;
using System.IO.Compression;

/// <summary>QA tooling (2026-10-29): writes a text file sent as gzip+base64 (used by the remote editor bridge, whose
/// command sandbox has no GZipStream). Editor only, touches nothing else.</summary>
public static class QAFileDrop
{
    public static int WriteGz(string path, string base64)
    {
        using (var ms = new MemoryStream(System.Convert.FromBase64String(base64)))
        using (var gz = new GZipStream(ms, CompressionMode.Decompress))
        using (var sr = new StreamReader(gz))
        {
            string text = sr.ReadToEnd();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return text.Length;
        }
    }
}
