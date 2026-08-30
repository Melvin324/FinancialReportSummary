# 财报智能摘要 MVP — 后端

> 输入股票代码 / 公司名 / 拼音，AI 自动生成财报投资摘要。.NET 8 + EF Core + PostgreSQL + Ocelot Gateway + MiniMax Text API。

![arch](https://img.shields.io/badge/.NET-8.0-512BD4) ![db](https://img.shields.io/badge/PostgreSQL-18-336791) ![gw](https://img.shields.io/badge/Ocelot-23.4.3-blue) ![ai](https://img.shields.io/badge/MiniMax-Text--01-orange)

## 项目亮点（面试叙事）

- **网关分层**：Ocelot 反向代理 + 30 req/min 限流 + 自定义根页面，模拟生产级流量入口
- **EF Core Code-First**：自动迁移、`OnModelCreating` 显式映射、snake_case 列命名、复合索引（`stock_code`+`expires_at` 摘要命中缓存）
- **双路数据源**：本地 230+ 标的静态库（O(1) 字典查）+ 东方财富 `searchapi` 实时兜底；东方财富 `ZYZBAjaxNew` 财报接口封装为可插拔服务
- **24h 摘要缓存 + 历史去重**：同 `resolved_stock_code` 第二次搜只更新时间戳，避免重复 AI 调用
- **优雅降级**：东方财富 `ZYZBAjaxNew` 失败 → 模拟数据兜底；前端永远能拿到响应
- **生产可观测**：请求日志中间件 + EF Core SQL 日志分级 + Ocelot 完整请求 ID 链路

## 架构

```
Browser (React 19)  →  Ocelot Gateway :5000  →  ASP.NET Core API :5050
                                                  ├─ CompanySearchService  (本地 230+ 字典 → East Money suggest)
                                                  ├─ ReportService         (East Money ZYZBAjaxNew → 模拟数据兜底)
                                                  ├─ MiniMaxService        (AI 摘要生成)
                                                  └─ StorageService        (EF Core → PostgreSQL)
```

## 目录结构

```
backend/
├── Api.csproj                      # ASP.NET Core Minimal API
├── Program.cs                      # 入口、3 个端点、EF 迁移
├── appsettings.json                # 生产配置（已脱敏，key 从 env 读）
├── appsettings.Development.json    # 开发配置
├── Data/
│   ├── AppDbContext.cs             # EF Core 上下文
│   ├── StorageService.cs           # CRUD + 缓存 + 去重
│   ├── Entities/                   # SummaryEntity / SearchHistoryEntity
│   ├── Migrations/                 # EF Core 自动生成
│   └── schema/                     # 拆表后的 SQL（部署给 DBA 用）
├── Services/
│   ├── CompanySearchService.cs     # 本地 230+ 标的公司库
│   ├── EastMoneyService.cs         # 东方财富 suggest API 兜底
│   ├── ReportService.cs            # 财报数据（East Money → mock 兜底）
│   └── MiniMaxService.cs           # MiniMax Text API 调用
├── Gateway/
│   ├── Gateway.csproj              # Ocelot 反向代理项目
│   ├── Program.cs                  # 自定义根页面 + 限流 + 日志
│   └── ocelot.json                 # 路由配置
└── Tools/
    └── split-schema.py             # schema.sql → per-table files
```

## 端点

| 方法 | 路径 | 用途 | 状态码 |
|---|---|---|---|
| `GET`  | `/` (网关根) | 服务状态页 | 200 |
| `GET`  | `/api/health` | 健康检查 | 200 |
| `POST` | `/api/summary` | 生成股票摘要 | 200 / 400（股票未找到）/ 429（限流）/ 500 |
| `GET`  | `/api/search?q=...` | 公司名/代码/拼音/行业 搜索 | 200 |
| `GET`  | `/api/history?page=1&pageSize=10` | 搜索历史分页 | 200 |
| `DELETE` | `/api/history` | 清空历史 | 200 |

## 快速启动

### 前置条件

- .NET 8 SDK
- PostgreSQL 14+ （推荐 18）
- MiniMax API Key（注册地址：https://api.minimax.chat）

### 1. 准备数据库

```bash
# 创建数据库
psql -U postgres -c "CREATE DATABASE financial_report;"
```

### 2. 配置环境变量

```powershell
$env:MINIMAX_API_KEY = "sk-cp-你的key"
```

### 3. 启动（首次会自动跑 EF 迁移建表）

```powershell
# 后端 API
dotnet run --project backend/Api.csproj --urls http://localhost:5050

# 另开终端：网关
dotnet run --project backend/Gateway/Gateway.csproj --urls http://localhost:5000
```

### 4. 验证

```powershell
# 健康检查
curl http://localhost:5000/api/health

# 搜索「茅台」→ 返回 600519 贵州茅台
curl "http://localhost:5000/api/search?q=maotai"

# 生成摘要
curl -X POST http://localhost:5000/api/summary -H "Content-Type: application/json" -d '{"query":"600519"}'
```

## 数据库设计

两张表，均带索引：

- `summaries` — 缓存的 AI 摘要，`(stock_code, expires_at)` 复合索引，TTL 24h
- `search_history` — 用户搜索历史，按 `resolved_stock_code` 去重

EF Core `OnModelCreating` 中显式指定列名（snake_case）：

```csharp
modelBuilder.Entity<SummaryEntity>(b => {
    b.ToTable("summaries");
    b.Property(e => e.StockCode).HasColumnName("stock_code");
    b.HasIndex(e => new { e.StockCode, e.ExpiresAt });
});
```

## 安全

- ✅ API Key 从 `MINIMAX_API_KEY` 环境变量读取，配置文件不留痕
- ✅ `appsettings.json` 包含的 `postgres123` 仅用于本地开发，生产请改用 `appsettings.Production.json` 或 env 注入
- ✅ 网关限流 30 req/min，防止滥用
- ✅ 搜索历史按 `resolved_stock_code` 去重，避免 AI token 浪费

## 已知限制

- 东方财富 `ZYZBAjaxNew` 财报接口近期 `KeyNotPresent`，自动降级到模拟数据（`ReportService.cs` 内置 10 家公司 mock）
- 摘要缓存 24h 硬编码，未来可改 `appsettings` 可配

## Roadmap

- [ ] 接入 Tushare / akshare / BaoStock 替代 East Money ZYZBAjaxNew
- [ ] Redis 替换内存缓存，支持多实例
- [ ] RAG 升级：财务报表 + 公告 + 研报 三源混合检索
- [ ] 结构化输出：摘要分章节（业绩 / 估值 / 风险）
- [ ] 单元测试覆盖 4 个 Service

---

🤖 Powered by [MiniMax Text API](https://api.minimax.chat)
