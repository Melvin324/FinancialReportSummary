using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 财报摘要的 prompt 内容 —— 跟具体 AI 厂商无关，任何 IAiSummaryService
/// 实现都可以复用，不用每接一个新厂商就重新抄一遍提示词。
/// </summary>
public static class SummaryPromptBuilder
{
    public const string SystemPrompt = "你是一位专业、客观的金融分析师。回复简洁专业，不超过400字。";

    public static string BuildUserPrompt(RawReportData data) => $"""
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
        ## {data.CompanyName} —— 财报摘要
        **一句话结论**：（一句话描述整体业绩）
        **亮点**：（2-3 个）
        **风险提示**：（1-2 个）
        **综合评级**：（根据以上数据打 1-5 颗星，星数必须反映真实表现，不要固定给某个星级，格式如 ⭐⭐⭐☆☆（3星））
        """;
}
