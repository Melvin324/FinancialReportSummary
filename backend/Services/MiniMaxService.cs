using System.Text.Json;
using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 调 MiniMax API 生成财报摘要
/// </summary>
public class MiniMaxService : IMiniMaxService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<MiniMaxService> _logger;

    public MiniMaxService(HttpClient http, IConfiguration config, ILogger<MiniMaxService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateSummaryAsync(RawReportData data, CancellationToken ct)
    {
        // 优先级：环境变量 > appsettings 配置
        // 推荐通过环境变量 MINIMAX_API_KEY 注入，避免密钥泄露到 Git
        var apiKey = Environment.GetEnvironmentVariable("MINIMAX_API_KEY")
                     ?? _config["MiniMax:ApiKey"]
                     ?? throw new InvalidOperationException(
                         "未配置 MiniMax API Key。请设置环境变量 MINIMAX_API_KEY，或在 appsettings.Development.json 中配置 MiniMax:ApiKey");

        var baseUrl = Environment.GetEnvironmentVariable("MINIMAX_BASE_URL")
                     ?? _config["MiniMax:BaseUrl"]
                     ?? "https://api.minimax.chat";

        var prompt = $"""
            你是一位专业的金融分析师，请根据以下财报数据，用中文生成一份简洁的投资摘要。

            公司：{data.CompanyName}（{data.StockCode}）
            报告期：{data.Period}

            关键财务指标：
            - 营业收入：{data.Revenue:N2} 亿元
            - 归母净利润：{data.NetProfit:N2} 亿元
            - 净利润增速：{data.ProfitGrowth:N1}%
            - 净资产收益率(ROE)：{data.ROE:N1}%
            - 毛利率：{data.GrossMargin:N1}%
            - 资产负债率：{data.DebtRatio:N1}%
            - 每股收益(EPS)：{data.EPS:N2} 元

            请按以下结构输出：
            ## {data.CompanyName} 财报摘要
            **一句话结论**：（一句话描述整体业绩）
            **亮点**：（2-3 个）
            **风险提示**：（1-2 个）
            **综合评级**：⭐⭐⭐⭐☆（4星）
            """;

        var requestBody = new
        {
            model = "MiniMax-Text-01",
            messages = new[]
            {
                new { role = "system", content = "你是一位专业、客观的金融分析师。回复简洁专业，不超过400字。" },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 600
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/text/chatcompletion_v2")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        _logger.LogInformation("调用 MiniMax API 生成摘要，股票: {StockCode}", data.StockCode);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var summary = json.GetProperty("choices")[0]
                              .GetProperty("message")
                              .GetProperty("content")
                              .GetString() ?? "摘要生成失败，请稍后重试。";

        return summary;
    }
}

public interface IMiniMaxService
{
    Task<string> GenerateSummaryAsync(RawReportData data, CancellationToken ct = default);
}
