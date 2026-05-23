using System.Net.Http.Headers;

namespace Lottery26.Services;

public class Downloader
{
    private const string UrlZip = "https://loterias.caixa.gov.br/Loterias/_arquivos/loterias/D_megase.zip";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public string GetDownloadPath()
    {
        return Path.Combine(Environment.CurrentDirectory, $"{DateTime.Now:yyyyMMddHHmmssfff}_D_megase.zip");
    }

    public async Task<string> DownloadAsync(string downloadPath)
    {
        try
        {
            using var response = await HttpClient.GetAsync(UrlZip, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(downloadPath);
            await responseStream.CopyToAsync(fileStream);

            return "Download done";
        }
        catch (Exception ex)
        {
            return $"Download failed: {ex.Message}";
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Lottery26", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }
}