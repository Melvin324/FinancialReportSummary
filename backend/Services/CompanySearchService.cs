using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 公司名称模糊搜索 —— 把"茅台"映射到"600519"
/// 支持中文名、股票代码、简称、行业的模糊匹配
/// 优先查本地库（CompanyDirectory，~250 家），未命中时回退到东方财富
/// </summary>
public class CompanySearchService : ICompanySearchService
{
    private readonly IEastMoneyService? _eastMoney;
    private readonly ILogger<CompanySearchService>? _logger;

    public CompanySearchService(IEastMoneyService? eastMoney = null, ILogger<CompanySearchService>? logger = null)
    {
        _eastMoney = eastMoney;
        _logger = logger;
    }

    public List<CompanyInfo> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<CompanyInfo>();

        keyword = keyword.Trim().ToLowerInvariant();
        var results = new List<(CompanyInfo company, int score)>();

        foreach (var (code, company) in CompanyDirectory.All)
        {
            int score = MatchScore(keyword, code, company);
            if (score > 0)
                results.Add((company, score));
        }

        return results
            .OrderByDescending(x => x.score)
            .Take(10)
            .Select(x => x.company)
            .ToList();
    }

    public async Task<CompanyInfo?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        // 1. 本地库查
        var local = CompanyDirectory.All.GetValueOrDefault(code);
        if (local != null) return local;

        // 2. 外部回退（东方财富）
        if (_eastMoney == null) return null;
        _logger?.LogInformation("本地库未命中 {Code}，查东方财富", code);
        var external = await _eastMoney.LookupByCodeAsync(code, ct);
        return external;
    }

    public CompanyInfo? FindByName(string name)
    {
        var results = Search(name);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// 评分：数字代码精确 = 100，代码包含 = 80，
    /// 名称精确 = 90，名称包含 = 70，拼音 = 60，行业 = 50
    /// </summary>
    private static int MatchScore(string keyword, string code, CompanyInfo company)
    {
        if (code == keyword) return 100;
        if (code.StartsWith(keyword)) return 80;
        if (string.Equals(company.Name, keyword, StringComparison.OrdinalIgnoreCase)) return 90;
        if (company.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 70;
        if (company.Pinyin.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 60;
        if (company.Industry.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 50;
        return 0;
    }
}
