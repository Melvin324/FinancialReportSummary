using Microsoft.EntityFrameworkCore;
using FinancialReportSummary.Api.Data.Entities;

namespace FinancialReportSummary.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SummaryEntity> Summaries => Set<SummaryEntity>();
    public DbSet<SearchHistoryEntity> SearchHistories => Set<SearchHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SummaryEntity>(b =>
        {
            b.ToTable("summaries");
            b.HasKey(x => x.Id);
            b.Property(x => x.StockCode).HasColumnName("stock_code").HasMaxLength(16).IsRequired();
            b.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(128).IsRequired();
            b.Property(x => x.RawDataJson).HasColumnName("raw_data_json").IsRequired();
            b.Property(x => x.Summary).HasColumnName("summary").IsRequired();
            b.Property(x => x.GeneratedAt).HasColumnName("generated_at").IsRequired();
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            b.HasIndex(x => x.StockCode).HasDatabaseName("idx_summaries_stock_code");
            b.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_summaries_expires_at");
        });

        modelBuilder.Entity<SearchHistoryEntity>(b =>
        {
            b.ToTable("search_history");
            b.HasKey(x => x.Id);
            b.Property(x => x.Query).HasColumnName("query").HasMaxLength(64).IsRequired();
            b.Property(x => x.ResolvedStockCode).HasColumnName("resolved_stock_code").HasMaxLength(16);
            b.Property(x => x.ResolvedCompanyName).HasColumnName("resolved_company_name").HasMaxLength(128);
            b.Property(x => x.SearchedAt).HasColumnName("searched_at").IsRequired();
            b.HasIndex(x => x.SearchedAt).HasDatabaseName("idx_search_history_searched_at");
        });
    }
}
