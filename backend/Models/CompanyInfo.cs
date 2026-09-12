namespace FinancialReportSummary.Api.Models;

/// <summary>公司基础信息：代码 / 中文名 / 拼音简称 / 所属行业</summary>
public record CompanyInfo(string Code, string Name, string Pinyin, string Industry);
