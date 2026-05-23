using Lottery26.Data;
using Lottery26.Services;

namespace Lottery26;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Lottery26 ===");

        try
        {
            DatabaseManager.Initialize();

            var downloader = new Downloader();
            var zipHandler = new ZipHandler();
            var parser = new ParseOutcome();

            var zipPath = downloader.GetDownloadPath();
            Console.WriteLine($"Download path: {zipPath}");

            var downloadMessage = await downloader.DownloadAsync(zipPath);
            Console.WriteLine(downloadMessage);

            var extractMessage = zipHandler.Extract(zipPath);
            Console.WriteLine(extractMessage);

            parser.ParseFile();
            Console.WriteLine(parser.ParseMessage);

            if (!string.IsNullOrWhiteSpace(parser.ParseMessage) &&
                parser.ParseMessage.StartsWith("File successfully", StringComparison.OrdinalIgnoreCase))
            {
                var total = DatabaseManager.GetTotalConcursos();
                Console.WriteLine($"[DB] Total de concursos no banco: {total}");

                DatabaseManager.PrintTopDezenas(20);
                parser.PrintOccurrencePercentage();
                DatabaseManager.Cleanup();

                parser.GenerateBet();

                Console.WriteLine();
                Console.WriteLine("=== Apostas Geradas ===");
                Console.WriteLine($"Aposta 1: {parser.FirstBet}");
                Console.WriteLine($"Aposta 2: {parser.SecondBet}");
                Console.WriteLine($"Aposta 3: {parser.ThirdBet}");
                Console.WriteLine($"Aposta 4: {parser.FouthBet}");
                Console.WriteLine($"Aposta 5: {parser.FiveBet}");
                Console.WriteLine($"Aposta 6: {parser.SixthBet}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro fatal: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine();
        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}