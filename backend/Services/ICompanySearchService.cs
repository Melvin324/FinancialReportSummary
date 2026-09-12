using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

public interface ICompanySearchService
{
    List<CompanyInfo> Search(string keyword);
    Task<CompanyInfo?> FindByCodeAsync(string code, CancellationToken ct = default);
    CompanyInfo? FindByName(string name);
}
