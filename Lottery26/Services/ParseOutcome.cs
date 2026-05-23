using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
using Lottery26.Data;
using Lottery26.Models;

namespace Lottery26.Services;

public class ParseOutcome
{
    private Dictionary<int, int> Low { get; set; } = new();
    private Dictionary<int, int> Mid { get; set; } = new();
    private Dictionary<int, int> Max { get; set; } = new();

    private List<Outcome> _outcomes = new();
    private List<int> _allNumbers = new();
    private Dictionary<int, int> _orderByOutcome = new();

    public string FilePath { get; set; } = string.Empty;
    public string ParseMessage { get; set; } = string.Empty;

    public string FirstBet { get; set; } = string.Empty;
    public string SecondBet { get; set; } = string.Empty;
    public string ThirdBet { get; set; } = string.Empty;
    public string FouthBet { get; set; } = string.Empty;
    public string FiveBet { get; set; } = string.Empty;
    public string SixthBet { get; set; } = string.Empty;

    public void ParseFile()
    {
        try
        {
            FilePath = Path.Combine(Environment.CurrentDirectory, "D_MEGA.HTM");

            if (!File.Exists(FilePath))
            {
                ParseMessage = "File does not exist: " + FilePath;
                return;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(FilePath);

            _outcomes = new List<Outcome>();

            var table = htmlDoc.DocumentNode.Descendants("table").FirstOrDefault();
            if (table is not null)
            {
                var lines = table.Descendants("tr");
                foreach (var line in lines)
                {
                    var columns = line.Descendants("td").ToList();
                    if (columns.Count < 8)
                        continue;

                    var tmp = new Outcome();

                    try
                    {
                        tmp.Id = int.Parse(Normalize(columns[0].InnerText));
                        tmp.Date = DateTime.ParseExact(Normalize(columns[1].InnerText), "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        tmp.First = int.Parse(Normalize(columns[2].InnerText));
                        tmp.Second = int.Parse(Normalize(columns[3].InnerText));
                        tmp.Third = int.Parse(Normalize(columns[4].InnerText));
                        tmp.Four = int.Parse(Normalize(columns[5].InnerText));
                        tmp.Five = int.Parse(Normalize(columns[6].InnerText));
                        tmp.Sixth = int.Parse(Normalize(columns[7].InnerText));
                    }
                    catch
                    {
                        continue;
                    }

                    if (tmp.Date > DateTime.MinValue && tmp.First > 0)
                    {
                        _outcomes.Add(tmp);
                    }
                }
            }

            _allNumbers = new List<int>();
            foreach (var outcome in _outcomes)
            {
                _allNumbers.Add(outcome.First);
                _allNumbers.Add(outcome.Second);
                _allNumbers.Add(outcome.Third);
                _allNumbers.Add(outcome.Four);
                _allNumbers.Add(outcome.Five);
                _allNumbers.Add(outcome.Sixth);
            }

            Console.WriteLine("[DB] Salvando concursos no banco...");
            foreach (var outcome in _outcomes)
            {
                DatabaseManager.UpsertConcurso(outcome);
            }
            Console.WriteLine($"[DB] {_outcomes.Count} concursos processados para o banco.");

            ParseMessage = $"File successfully parsed. {_outcomes.Count} concursos lidos, {_allNumbers.Count} dezenas contabilizadas.";
        }
        catch (Exception ex)
        {
            ParseMessage = ex.Message;
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }

    public void PrintOccurrencePercentage()
    {
        if (_orderByOutcome.Count == 0)
            OrderOutcomes();

        var total = _allNumbers.Count;
        Console.WriteLine();
        Console.WriteLine("=== Percentual de ocorrência por número (memória) ===");
        Console.WriteLine($"Total de dezenas sorteadas: {total}");
        Console.WriteLine("Número   Qtd        Percentual");
        Console.WriteLine(new string('-', 32));

        foreach (var kv in _orderByOutcome.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
        {
            var pct = (double)kv.Value / total * 100.0;
            Console.WriteLine($"{kv.Key,-8} {kv.Value,-10} {pct:F2}%");
        }
    }

    public void GenerateBet()
    {
        SplitNumbers();

        FirstBet = DoTheMagic();
        SecondBet = DoTheMagic();
        ThirdBet = DoTheMagic();
        FouthBet = DoTheMagic();
        FiveBet = DoTheMagic();
        SixthBet = DoTheMagic();
    }

    private void OrderOutcomes()
    {
        _orderByOutcome = _allNumbers
            .GroupBy(n => n)
            .ToDictionary(g => g.Key, g => g.Count())
            .OrderBy(x => x.Value)
            .ThenBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private void SplitNumbers()
    {
        OrderOutcomes();

        if (_orderByOutcome.Count < 3)
            throw new InvalidOperationException($"Não há números suficientes para dividir em grupos. Encontrados: {_orderByOutcome.Count}");

        Low = new Dictionary<int, int>();
        Mid = new Dictionary<int, int>();
        Max = new Dictionary<int, int>();

        var count = _orderByOutcome.Count;
        var groupSize = count / 3;
        var remainder = count % 3;
        var orderedItems = _orderByOutcome.ToList();

        var index = 0;
        for (var i = 0; i < groupSize; i++, index++)
            Low.Add(orderedItems[index].Key, orderedItems[index].Value);

        for (var i = 0; i < groupSize; i++, index++)
            Mid.Add(orderedItems[index].Key, orderedItems[index].Value);

        for (var i = 0; i < groupSize + remainder; i++, index++)
            Max.Add(orderedItems[index].Key, orderedItems[index].Value);

        LogSplittedOutcomes(Low, "Low-Bets");
        LogSplittedOutcomes(Mid, "Mid-Bets");
        LogSplittedOutcomes(Max, "High-Bets");
    }

    private void LogSplittedOutcomes(Dictionary<int, int> toLog, string betId)
    {
        var path = GetPath(betId);
        var total = _allNumbers.Count;

        using var outputFile = new StreamWriter(path, false, Encoding.UTF8);
        outputFile.WriteLine("Número   Ocorrências Percentual");
        outputFile.WriteLine(new string('-', 35));

        foreach (var line in toLog)
        {
            var pct = (double)line.Value / total * 100.0;
            outputFile.WriteLine($"{line.Key,-8} {line.Value,-11} {pct:F2}%");
        }
    }

    private string DoTheMagic()
    {
        var selectedNumbers = new HashSet<int>();

        var lowKeys = Low.Keys.ToArray();
        var midKeys = Mid.Keys.ToArray();
        var highKeys = Max.Keys.ToArray();

        selectedNumbers.Add(lowKeys[GetSecureRandom(lowKeys.Length)]);

        var attempts = 0;
        while (selectedNumbers.Count < 5 && attempts < 1000)
        {
            selectedNumbers.Add(midKeys[GetSecureRandom(midKeys.Length)]);
            attempts++;
        }

        attempts = 0;
        while (selectedNumbers.Count < 6 && attempts < 1000)
        {
            selectedNumbers.Add(highKeys[GetSecureRandom(highKeys.Length)]);
            attempts++;
        }

        return string.Join(" - ", selectedNumbers.OrderBy(n => n).Select(n => n.ToString("D2")));
    }

    private static int GetSecureRandom(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        return RandomNumberGenerator.GetInt32(maxExclusive);
    }

    private string GetPath(string fileName)
    {
        return Path.Combine(Environment.CurrentDirectory, $"{DateTime.Now:yyyyMMddHHmmssfff}_{fileName}.txt");
    }

    private static string Normalize(string value)
    {
        return HtmlEntity.DeEntitize(value).Trim();
    }
}