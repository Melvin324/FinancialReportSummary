namespace FinancialReportSummary.Api.Data.Entities;

public class SearchHistoryEntity
{
    public int Id { get; set; }
    public string Query { get; set; } = "";
    public string? ResolvedStockCode { get; set; }
    public string? ResolvedCompanyName { get; set; }
    public DateTime SearchedAt { get; set; }
}
