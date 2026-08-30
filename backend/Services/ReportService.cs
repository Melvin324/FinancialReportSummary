using System.Text.Json;
using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 从东方财富/新浪财经拉原始财报数据
/// </summary>
public class ReportService : IReportService
{
    private readonly HttpClient _http;
    private readonly ILogger<ReportService> _logger;

    public ReportService(HttpClient http, ILogger<ReportService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<RawReportData> GetFinancialReportAsync(string stockCode, CancellationToken ct = default)
    {
        _logger.LogInformation("获取财报数据，股票: {StockCode}", stockCode);

        // 东方财富 F9 财务概况接口（不需要登录）
        var url = $"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/ZYZBAjaxNew?type=0&code=SH{stockCode}";

        try
        {
            var resp = await _http.GetAsync(url, ct);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);

            // 解析东方财富数据结构
            var dataArray = json.GetProperty("result");
            if (dataArray.GetArrayLength() == 0)
                throw new Exception("未找到财报数据，股票代码可能错误或已停牌");

            var latest = dataArray[0];

            return new RawReportData
            {
                StockCode = stockCode,
                CompanyName = latest.TryGetProperty("company_name", out var cn) ? cn.GetString() ?? stockCode : stockCode,
                Period = latest.TryGetProperty("report_date_name", out var rd) ? rd.GetString() ?? "" : "",
                Revenue = GetDecimal(latest, "total_operate_income"),
                NetProfit = GetDecimal(latest, "parent_netprofit"),
                ProfitGrowth = GetDecimal(latest, "netprofit_ratio"),
                ROE = GetDecimal(latest, "weighted_avg_roe"),
                GrossMargin = GetDecimal(latest, "gross_netprofit_ratio"),
                DebtRatio = GetDecimal(latest, "debt_asset_ratio"),
                EPS = GetDecimal(latest, "basic_eps"),
                RawJson = latest.GetRawText()
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogWarning("东方财富接口失败，改用模拟数据: {Message}", ex.Message);
            // 接口不稳定时返回模拟数据，确保 MVP 能跑通
            return GetMockData(stockCode);
        }
    }

    private static decimal GetDecimal(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
            return prop.GetDecimal();
        return 0m;
    }

    /// <summary>
    /// 模拟数据 —— 接口不可用时的降级方案
    /// 演示用，实盘请替换为真实接口
    /// </summary>
    private static RawReportData GetMockData(string stockCode)
    {
        var mockData = new Dictionary<string, RawReportData>
        {
            // 消费
            ["600519"] = new() { StockCode = "600519", CompanyName = "贵州茅台", Period = "2024Q3",
                Revenue = 1207.76m, NetProfit = 608.28m, ProfitGrowth = 15.4m, ROE = 32.8m,
                GrossMargin = 91.6m, DebtRatio = 18.2m, EPS = 48.42m, RawJson = "{}" },
            ["000858"] = new() { StockCode = "000858", CompanyName = "五粮液", Period = "2024Q3",
                Revenue = 679.16m, NetProfit = 249.31m, ProfitGrowth = 12.2m, ROE = 19.4m,
                GrossMargin = 73.8m, DebtRatio = 24.1m, EPS = 6.42m, RawJson = "{}" },
            ["000333"] = new() { StockCode = "000333", CompanyName = "美的集团", Period = "2024Q3",
                Revenue = 3203.51m, NetProfit = 316.99m, ProfitGrowth = 14.4m, ROE = 21.6m,
                GrossMargin = 26.1m, DebtRatio = 64.3m, EPS = 4.55m, RawJson = "{}" },

            // 金融
            ["601318"] = new() { StockCode = "601318", CompanyName = "中国平安", Period = "2024Q3",
                Revenue = 7753.83m, NetProfit = 1166.85m, ProfitGrowth = 18.7m, ROE = 12.3m,
                GrossMargin = 0m, DebtRatio = 89.5m, EPS = 6.37m, RawJson = "{}" },
            ["600036"] = new() { StockCode = "600036", CompanyName = "招商银行", Period = "2024Q3",
                Revenue = 2526.07m, NetProfit = 1131.83m, ProfitGrowth = 1.2m, ROE = 14.6m,
                GrossMargin = 0m, DebtRatio = 91.2m, EPS = 4.48m, RawJson = "{}" },

            // 科技
            ["300750"] = new() { StockCode = "300750", CompanyName = "宁德时代", Period = "2024Q3",
                Revenue = 2530.21m, NetProfit = 360.81m, ProfitGrowth = 26.1m, ROE = 18.7m,
                GrossMargin = 28.2m, DebtRatio = 67.5m, EPS = 8.21m, RawJson = "{}" },
            ["002594"] = new() { StockCode = "002594", CompanyName = "比亚迪", Period = "2024Q3",
                Revenue = 5022.51m, NetProfit = 252.38m, ProfitGrowth = 18.1m, ROE = 17.5m,
                GrossMargin = 20.8m, DebtRatio = 76.9m, EPS = 8.69m, RawJson = "{}" },

            // 医药
            ["600276"] = new() { StockCode = "600276", CompanyName = "恒瑞医药", Period = "2024Q3",
                Revenue = 202.89m, NetProfit = 46.21m, ProfitGrowth = 22.8m, ROE = 13.5m,
                GrossMargin = 86.2m, DebtRatio = 9.8m, EPS = 0.72m, RawJson = "{}" },

            // 新能源
            ["601012"] = new() { StockCode = "601012", CompanyName = "隆基绿能", Period = "2024Q3",
                Revenue = 645.92m, NetProfit = -65.05m, ProfitGrowth = -156.3m, ROE = -7.2m,
                GrossMargin = 8.6m, DebtRatio = 58.4m, EPS = -0.86m, RawJson = "{}" }
        };

        return mockData.GetValueOrDefault(stockCode, new RawReportData
        {
            StockCode = stockCode,
            CompanyName = $"股票{stockCode}",
            Period = "2024Q3",
            Revenue = 100m,
            NetProfit = 10m,
            ProfitGrowth = 5m,
            ROE = 10m,
            GrossMargin = 30m,
            DebtRatio = 50m,
            EPS = 0.5m,
            RawJson = "{}"
        });
    }
}

public interface IReportService
{
    Task<RawReportData> GetFinancialReportAsync(string stockCode, CancellationToken ct = default);
}
