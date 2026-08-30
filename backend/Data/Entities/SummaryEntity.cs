namespace FinancialReportSummary.Api.Data.Entities;

public class SummaryEntity
{
    public int Id { get; set; }
    public string StockCode { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string RawDataJson { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
