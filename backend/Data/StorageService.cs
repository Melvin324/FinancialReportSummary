using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinancialReportSummary.Api.Data.Entities;
using FinancialReportSummary.Api.Models;

namespace FinancialReportSummary.Api.Data;

/// <summary>
/// EF Core 存储服务 —— 摘要缓存 + 搜索历史
/// </summary>
public class StorageService : IStorageService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StorageService> _logger;

    public StorageService(AppDbContext db, ILogger<StorageService> logger)
    {
        _db = db;
        _logger = logger;
        _logger.LogInformation("EF Core StorageService 初始化");
    }

    // ========== 摘要缓存 ==========

    public async Task<SummaryResponse?> GetCachedSummaryAsync(string stockCode, CancellationToken ct = default)
    {
        var entity = await _db.Summaries
            .Where(s => s.StockCode == stockCode && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefaultAsync(ct);

        if (entity == null) return null;

        var rawData = JsonSerializer.Deserialize<RawReportData>(entity.RawDataJson)
            ?? throw new InvalidOperationException("反序列化 RawReportData 失败");

        return new SummaryResponse
        {
            StockCode = entity.StockCode,
            CompanyName = entity.CompanyName,
            GeneratedAt = entity.GeneratedAt,
            RawData = rawData,
            Summary = entity.Summary
        };
    }

    public async Task SaveSummaryAsync(SummaryResponse response, TimeSpan ttl, CancellationToken ct = default)
    {
        // 先清掉同 code 的旧缓存
        var old = await _db.Summaries
            .Where(s => s.StockCode == response.StockCode)
            .ToListAsync(ct);
        _db.Summaries.RemoveRange(old);

        var rawJson = JsonSerializer.Serialize(response.RawData);
        _db.Summaries.Add(new SummaryEntity
        {
            StockCode = response.StockCode,
            CompanyName = response.CompanyName,
            RawDataJson = rawJson,
            Summary = response.Summary,
            GeneratedAt = response.GeneratedAt,
            ExpiresAt = DateTime.UtcNow.Add(ttl)
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("摘要已缓存: {Code}, TTL={Ttl}", response.StockCode, ttl);
    }

    // ========== 搜索历史 ==========

    public async Task RecordSearchAsync(string query, string? stockCode, string? companyName, CancellationToken ct = default)
    {
        // 同 code → 去重，更新 timestamp
        if (!string.IsNullOrEmpty(stockCode))
        {
            var existing = await _db.SearchHistories
                .Where(h => h.ResolvedStockCode == stockCode)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                existing.Query = query;
                existing.ResolvedCompanyName = companyName;
                existing.SearchedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "[SearchHistory] UPDATE  query='{Query}' -> {Code} ({Company})",
                    query, stockCode, companyName);
                return;
            }
        }

        _db.SearchHistories.Add(new SearchHistoryEntity
        {
            Query = query,
            ResolvedStockCode = stockCode,
            ResolvedCompanyName = companyName,
            SearchedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "[SearchHistory] INSERT  query='{Query}' -> {Code} ({Company})",
            query, stockCode ?? "-", companyName ?? "-");
    }

    public async Task<HistoryPage> GetSearchHistoryAsync(int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var total = await _db.SearchHistories.CountAsync(ct);
        var items = await _db.SearchHistories
            .OrderByDescending(h => h.SearchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new SearchHistoryItem
            {
                Query = h.Query,
                ResolvedStockCode = h.ResolvedStockCode,
                ResolvedCompanyName = h.ResolvedCompanyName,
                SearchedAt = h.SearchedAt
            })
            .ToListAsync(ct);

        return new HistoryPage
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            Items = items
        };
    }

    public async Task<int> ClearHistoryAsync(CancellationToken ct = default)
    {
        return await _db.SearchHistories.ExecuteDeleteAsync(ct);
    }

    // ========== 统计 ==========

    public async Task<StorageStats> GetStatsAsync(CancellationToken ct = default)
    {
        return new StorageStats
        {
            CachedSummaries = await _db.Summaries.CountAsync(ct),
            SearchHistoryCount = await _db.SearchHistories.CountAsync(ct)
        };
    }
}

public record SearchHistoryItem
{
    public string Query { get; init; } = "";
    public string? ResolvedStockCode { get; init; }
    public string? ResolvedCompanyName { get; init; }
    public DateTime SearchedAt { get; init; }
}

public record HistoryPage
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public int TotalPages { get; init; }
    public List<SearchHistoryItem> Items { get; init; } = new();
}

public record StorageStats
{
    public int CachedSummaries { get; set; }
    public int SearchHistoryCount { get; set; }
}

public interface IStorageService
{
    Task<SummaryResponse?> GetCachedSummaryAsync(string stockCode, CancellationToken ct = default);
    Task SaveSummaryAsync(SummaryResponse response, TimeSpan ttl, CancellationToken ct = default);
    Task RecordSearchAsync(string query, string? stockCode, string? companyName, CancellationToken ct = default);
    Task<HistoryPage> GetSearchHistoryAsync(int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<int> ClearHistoryAsync(CancellationToken ct = default);
    Task<StorageStats> GetStatsAsync(CancellationToken ct = default);
}
