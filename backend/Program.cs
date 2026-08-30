using FinancialReportSummary.Api.Services;
using FinancialReportSummary.Api.Models;
using FinancialReportSummary.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 注册 EF Core + PostgreSQL
var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("未配置 ConnectionStrings:Postgres");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// 注册服务
builder.Services.AddHttpClient<IMiniMaxService, MiniMaxService>();
builder.Services.AddSingleton<IReportService, ReportService>();
builder.Services.AddHttpClient<IEastMoneyService, EastMoneyService>();
builder.Services.AddSingleton<ICompanySearchService, CompanySearchService>();
builder.Services.AddScoped<IStorageService, StorageService>();

// 注册 CORS
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

// 启动时自动应用 EF Core 迁移
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("应用 EF Core 迁移...");
        await db.Database.MigrateAsync();
        logger.LogInformation("数据库已就绪");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "迁移失败");
        throw;
    }
}

// 启用 CORS
app.UseCors();

// ========== API 路由 ==========

// 健康检查（带存储统计）
app.MapGet("/api/health", async (IStorageService storage) =>
{
    var stats = await storage.GetStatsAsync();
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow,
        cachedSummaries = stats.CachedSummaries,
        searchHistory = stats.SearchHistoryCount
    });
});

// 模糊搜索接口
app.MapGet("/api/search", (string q, ICompanySearchService searchService) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "搜索关键词不能为空" });

    var results = searchService.Search(q);
    return Results.Ok(new
    {
        keyword = q,
        count = results.Count,
        results = results
    });
});

// 财报摘要 API —— 带缓存
app.MapPost("/api/summary", async (
    SummaryRequest request,
    IReportService reportService,
    IMiniMaxService miniMaxService,
    ICompanySearchService searchService,
    IStorageService storage,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
        return Results.BadRequest(new { error = "股票代码或公司名不能为空" });

    logger.LogInformation("[Search] query='{Query}'", request.Query);

    try
    {
        // 1. 智能识别（先验证 code 是否真实存在，不是盲信数字）
        string stockCode = request.Query.Trim();
        string? resolvedName = null;

        if (IsStockCode(stockCode))
        {
            // 5-6 位数字 → 股票代码，先查本地库，未命中回退到东方财富
            var exactCompany = await searchService.FindByCodeAsync(stockCode, ct);
            if (exactCompany == null)
            {
                return Results.BadRequest(new
                {
                    error = $"股票代码「{stockCode}」无法识别（本地库和东方财富都没找到）",
                    suggestion = "试试其他股票代码或公司名（如 茅台、maotai）"
                });
            }
            resolvedName = exactCompany.Name;
        }
        else
        {
            // 不是数字 → 公司名搜索
            var company = searchService.FindByName(stockCode);
            if (company == null)
            {
                return Results.BadRequest(new
                {
                    error = $"未找到匹配「{request.Query}」的公司",
                    suggestion = "试试股票代码（如 600519）或公司全名（如 茅台）"
                });
            }
            stockCode = company.Code;
            resolvedName = company.Name;
        }

        // 2. 查缓存（用真实存在的 code 作 key）
        var cached = await storage.GetCachedSummaryAsync(stockCode, ct);
        if (cached != null)
        {
            // 必须 await，否则响应返回后 DbContext 被释放，保存失败
            await storage.RecordSearchAsync(request.Query, stockCode, cached.CompanyName, ct);

            return Results.Ok(new SummaryResponse
            {
                StockCode = stockCode,
                CompanyName = cached.CompanyName,
                ResolvedFromQuery = request.Query,
                GeneratedAt = cached.GeneratedAt,
                RawData = cached.RawData,
                Summary = cached.Summary
            });
        }

        // 3. 缓存未命中 → 拉数据 + 调 AI
        var rawData = await reportService.GetFinancialReportAsync(stockCode, ct);
        var summary = await miniMaxService.GenerateSummaryAsync(rawData, ct);

        var response = new SummaryResponse
        {
            StockCode = stockCode,
            CompanyName = resolvedName ?? rawData.CompanyName,
            ResolvedFromQuery = request.Query,
            GeneratedAt = DateTime.UtcNow,
            RawData = rawData,
            Summary = summary
        };

        // 4. 存缓存（24h TTL）+ 记历史
        await storage.SaveSummaryAsync(response, TimeSpan.FromHours(24), ct);
        await storage.RecordSearchAsync(request.Query, stockCode, response.CompanyName, ct);

        return Results.Ok(response);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"无法获取财报数据: {ex.Message}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"服务异常: {ex.Message}");
    }
});

// 搜索历史（分页）
app.MapGet("/api/history", async (int? page, int? pageSize, IStorageService storage, CancellationToken ct) =>
{
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 10, 1, 50);
    var data = await storage.GetSearchHistoryAsync(p, ps, ct);
    return Results.Ok(data);
});

app.MapDelete("/api/history", async (IStorageService storage, CancellationToken ct) =>
{
    var n = await storage.ClearHistoryAsync(ct);
    return Results.Ok(new { deleted = n });
});

app.Run();

static bool IsStockCode(string input)
{
    if (input.Length == 6 && input.All(char.IsDigit)) return true;
    if (input.Length == 5 && input.All(char.IsDigit)) return true;
    return false;
}
