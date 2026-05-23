using System.IO.Compression;

namespace Lottery26.Services;

public class ZipHandler
{
    public string Extract(string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
                return "Zip file does not exist.";

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                var outputPath = Path.Combine(Environment.CurrentDirectory, entry.Name);
                entry.ExtractToFile(outputPath, overwrite: true);
            }

            return "Extract done";
        }
        catch (Exception ex)
        {
            return $"Extract failed: {ex.Message}";
        }
    }
}