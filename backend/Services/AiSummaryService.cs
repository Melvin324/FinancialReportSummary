using System.Text.Json;
using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// IAiSummaryService 的当前实现。厂商特有的部分只有这里：鉴权 header、
/// 请求/响应的 JSON 形状、model 名字，且这些全部来自配置，代码里不写死
/// 具体供应商。prompt 内容和对外接口都是通用的
/// （见 SummaryPromptBuilder / IAiSummaryService），换供应商只需要
/// 新增一个实现类，不用改调用方。
/// </summary>
public class AiSummaryService : IAiSummaryService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AiSummaryService> _logger;

    public AiSummaryService(HttpClient http, IConfiguration config, ILogger<AiSummaryService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateSummaryAsync(RawReportData data, CancellationToken ct = default)
    {
        // 优先级：环境变量 > appsettings 配置
        // 推荐通过环境变量注入，避免密钥泄露到 Git
        // 注意：appsettings.json 里 ApiKey 默认是 ""（空字符串，不是 null），
        // 用 ?? 判断 null 会漏掉这种情况，必须显式检查空白字符串
        var apiKey = Environment.GetEnvironmentVariable("API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = _config["AiSummary:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "未配置 API Key。请设置环境变量 API_KEY，或在 appsettings.Development.json 中配置 AiSummary:ApiKey");

        // BaseUrl/Model 不设默认值——具体用哪家服务完全由配置决定，代码里不出现供应商名字
        var baseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? _config["AiSummary:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "未配置服务地址。请设置环境变量 BASE_URL，或在 appsettings.Development.json 中配置 AiSummary:BaseUrl");

        var model = _config["AiSummary:Model"];
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "未配置模型名。请在 appsettings.Development.json 中配置 AiSummary:Model");

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = SummaryPromptBuilder.SystemPrompt },
                new { role = "user", content = SummaryPromptBuilder.BuildUserPrompt(data) }
            },
            temperature = 0.3,
            max_tokens = 600
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/text/chatcompletion_v2")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        _logger.LogInformation("调用 AI 摘要服务，股票: {StockCode}", data.StockCode);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        // 上游对业务级错误（配额、鉴权、内容审核等）经常是 HTTP 200 + 错误体，
        // 不会走到上面的 EnsureSuccessStatusCode。这里显式检查 choices 是否存在，
        // 避免 GetProperty 抛出一个对用户没有意义的 KeyNotFoundException。
        if (!json.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            var errMsg = json.TryGetProperty("base_resp", out var baseResp) &&
                         baseResp.TryGetProperty("status_msg", out var msgProp)
                ? msgProp.GetString()
                : "AI 服务返回了非预期的响应格式";
            _logger.LogWarning("AI 摘要响应缺少 choices 字段: {Json}", json.GetRawText());
            throw new InvalidOperationException($"AI 摘要生成失败: {errMsg}");
        }

        var summary = choices[0]
                              .GetProperty("message")
                              .GetProperty("content")
                              .GetString() ?? "摘要生成失败，请稍后重试。";

        // 模拟数据的提示交给前端的 isMock 横幅统一展示（更醒目、不依赖文本解析）；
        // 这里不再往摘要正文里塞一遍，避免橙色横幅 + 正文重复提示两次。
        return summary;
    }
}
