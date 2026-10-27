using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HtmlAgilityPack;
using Lottery26.Data;
using Lottery26.Models;

namespace Lottery26.Services;

public class ParseOutcome
{


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
        FirstBet = GenerateSingleBet();
        SecondBet = GenerateSingleBet();
        ThirdBet = GenerateSingleBet();
        FouthBet = GenerateSingleBet();
        FiveBet = GenerateSingleBet();
        SixthBet = GenerateSingleBet();
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

    private string GenerateSingleBet()
    {
        var selectedNumbers = new HashSet<int>();

        while (true)
        {
            selectedNumbers.Clear();

            // 1. Gera 6 números únicos de 1 a 60
            while (selectedNumbers.Count < 6)
            {
                // GetSecureRandom returns 0 to 59, so we add 1
                selectedNumbers.Add(GetSecureRandom(60) + 1);
            }

            var betList = selectedNumbers.OrderBy(n => n).ToList();

            // 2. Filtro de Soma (120 a 210)
            var sum = betList.Sum();
            if (sum < 120 || sum > 210)
                continue;

            // Removido Filtro Par/Ímpar a pedido do usuário

            // 4. Filtro de Alta/Baixa (ao menos 3 números > 31 para evitar divisões comuns)
            var highNumbers = betList.Count(n => n > 31);
            if (highNumbers < 3)
                continue;

            // 5. Filtro de Consecutivos (no máximo 2 números consecutivos permitidos, bloqueia trincas)
            if (HasConsecutiveMoreThanTwo(betList))
                continue;

            // Passou em todos os filtros!
            return string.Join(" - ", betList.Select(n => n.ToString("D2")));
        }
    }

    private bool HasConsecutiveMoreThanTwo(List<int> sortedNumbers)
    {
        int consecutiveCount = 1;
        for (int i = 1; i < sortedNumbers.Count; i++)
        {
            if (sortedNumbers[i] == sortedNumbers[i - 1] + 1)
            {
                consecutiveCount++;
                if (consecutiveCount >= 3)
                    return true;
            }
            else
            {
                consecutiveCount = 1;
            }
        }
        return false;
    }

    private static int GetSecureRandom(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        return RandomNumberGenerator.GetInt32(maxExclusive);
    }

    private static string Normalize(string value)
    {
        return HtmlEntity.DeEntitize(value).Trim();
    }
}