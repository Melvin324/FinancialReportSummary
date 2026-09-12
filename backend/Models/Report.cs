namespace FinancialReportSummary.Api.Models;

/// <summary>
/// 摘要请求 —— 既能传股票代码，也能传公司名（自动模糊匹配）
/// </summary>
public record SummaryRequest
{
    /// <summary>
    /// 股票代码（如 "600519"）或公司名（如 "茅台"、"maotai"）
    /// </summary>
    public string Query { get; init; } = "";
}

public record SummaryResponse
{
    public string StockCode { get; init; } = "";
    public string CompanyName { get; init; } = "";
    public string? ResolvedFromQuery { get; init; }
    public DateTime GeneratedAt { get; init; }
    public RawReportData RawData { get; init; } = new();
    public string Summary { get; init; } = "";
}

/// <summary>
/// 从东方财富/新浪拉回来的原始财报数据
/// </summary>
public record RawReportData
{
    /// <summary>股票代码，如 "600519"</summary>
    public string StockCode { get; init; } = "";

    /// <summary>公司简称，如 "贵州茅台"</summary>
    public string CompanyName { get; init; } = "";

    /// <summary>最新季报，如 "2024Q3"</summary>
    public string Period { get; init; } = "";

    /// <summary>营业收入（亿元）</summary>
    public decimal Revenue { get; init; }

    /// <summary>归母净利润（亿元）</summary>
    public decimal NetProfit { get; init; }

    /// <summary>净利润同比增速（%）</summary>
    public decimal ProfitGrowth { get; init; }

    /// <summary>净资产收益率 ROE（%）</summary>
    public decimal ROE { get; init; }

    /// <summary>毛利率（%）</summary>
    public decimal GrossMargin { get; init; }

    /// <summary>资产负债率（%）</summary>
    public decimal DebtRatio { get; init; }

    /// <summary>每股收益 EPS（元）</summary>
    public decimal EPS { get; init; }

    /// <summary>未加工的原始 JSON（供 AI 参考）</summary>
    public string RawJson { get; init; } = "";

    /// <summary>
    /// true = 真实接口不可用/无法识别交易所，当前数据是演示用模拟数据。
    /// 调用方（前端、AI 摘要）必须检查这个字段，不能把模拟数据当真实财报展示。
    /// </summary>
    public bool IsMock { get; init; } = false;
}
