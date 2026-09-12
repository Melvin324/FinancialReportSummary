using System.Net.Http.Json;
using System.Text.Json;
using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Services;

/// <summary>
/// 东方财富外部数据源 —— 公司库查不到时回退到这里
/// </summary>
public class EastMoneyService : IEastMoneyService
{
    private readonly HttpClient _http;
    private readonly ILogger<EastMoneyService> _logger;

    // 内存缓存（key = 大写代码），避免重复 HTTP
    private static readonly Dictionary<string, CompanyInfo> _cache = new();
    private static readonly object _cacheLock = new();

    public EastMoneyService(HttpClient http, ILogger<EastMoneyService> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(5);
        _logger = logger;
    }

    public async Task<CompanyInfo?> LookupByCodeAsync(string code, CancellationToken ct = default)
    {
        var key = code.ToUpperInvariant().Trim();

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        try
        {
            var url = $"https://searchapi.eastmoney.com/api/suggest/get" +
                      $"?input={Uri.EscapeDataString(key)}&type=14&token=D43BF722C8E33BDC906FB8411883769D";

            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("EastMoney API 返回 {Status}", resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!json.TryGetProperty("QuotationCodeTable", out var table) ||
                !table.TryGetProperty("Data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in data.EnumerateArray())
            {
                var itemCode = item.TryGetProperty("Code", out var c) ? c.GetString() : null;
                if (itemCode != key) continue;

                var name = item.TryGetProperty("Name", out var n) ? n.GetString() : null;
                var py = item.TryGetProperty("PinYin", out var p) ? p.GetString() : "";
                var mkt = item.TryGetProperty("MktNum", out var m) ? m.GetString() : "A";
                if (string.IsNullOrEmpty(name)) continue;

                var info = new CompanyInfo(itemCode, name, py ?? "", $"东方财富-{mkt}");

                lock (_cacheLock)
                {
                    _cache[key] = info;
                }

                _logger.LogInformation("EastMoney: 新增 {Code} → {Name}", key, name);
                return info;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EastMoney 查询 {Code} 失败", key);
            return null;
        }
    }
}

public interface IEastMoneyService
{
    Task<CompanyInfo?> LookupByCodeAsync(string code, CancellationToken ct = default);
}
