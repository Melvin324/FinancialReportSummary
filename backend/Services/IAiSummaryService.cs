using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 财报摘要生成的通用抽象 —— 不绑定具体厂商。
/// AiSummaryService 是当前唯一实现；以后要换/加其他供应商，
/// 只需要新增一个实现类并在 Program.cs 里换掉 DI 注册，调用方（Program.cs 的
/// /api/summary 接口）不用改一行。
/// </summary>
public interface IAiSummaryService
{
    Task<string> GenerateSummaryAsync(RawReportData data, CancellationToken ct = default);
}
