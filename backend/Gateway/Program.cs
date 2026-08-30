using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 加载 ocelot.json 配置
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 注册 Ocelot 服务
builder.Services.AddOcelot();

// 跨域（让前端 5173 能访问 Gateway 5000）
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

app.UseCors();

// 简单的请求日志中间件（所有经过 Gateway 的请求都打日志）
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "[Gateway] {Method} {Path} from {IP}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress);

    await next();

    var elapsed = DateTime.UtcNow - start;
    logger.LogInformation(
        "[Gateway] {Method} {Path} → {Status} ({Ms}ms)",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        elapsed.TotalMilliseconds);
});

// 根路径：API 状态展示页（用 middleware 而不是 MapGet，避开 Ocelot 拦截）
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && context.Request.Method == "GET")
    {
        var html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8" />
        <title>Financial Report API Gateway</title>
        <style>
          :root { --bg: #0f172a; --card: #1e293b; --primary: #38bdf8; --text: #e2e8f0; --muted: #94a3b8; --ok: #4ade80; }
          * { box-sizing: border-box; margin: 0; padding: 0; }
          body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", monospace;
                 background: var(--bg); color: var(--text); min-height: 100vh;
                 display: flex; align-items: center; justify-content: center; padding: 24px; }
          .card { background: var(--card); border-radius: 12px; padding: 32px 40px;
                  max-width: 640px; width: 100%; box-shadow: 0 10px 40px rgba(0,0,0,.4); }
          h1 { font-size: 1.5rem; margin-bottom: 6px; display: flex; align-items: center; gap: 10px; }
          .badge { background: var(--ok); color: #052e16; padding: 2px 10px;
                   border-radius: 4px; font-size: .75rem; font-weight: 600; }
          .subtitle { color: var(--muted); font-size: .85rem; margin-bottom: 24px; }
          table { width: 100%; border-collapse: collapse; font-size: .85rem; }
          th, td { text-align: left; padding: 8px 0; border-bottom: 1px solid #334155; }
          th { color: var(--muted); font-weight: 500; }
          code { background: #0f172a; padding: 2px 6px; border-radius: 4px;
                 color: var(--primary); font-size: .8rem; }
          .endpoints { margin-top: 20px; }
          .endpoints li { list-style: none; padding: 8px 0; border-bottom: 1px solid #334155;
                          display: flex; justify-content: space-between; font-size: .8rem; }
          .method { background: #0f172a; padding: 2px 8px; border-radius: 3px;
                    color: var(--primary); font-weight: 600; min-width: 50px; text-align: center; }
          .get { color: #4ade80; }
          .post { color: #facc15; }
          .delete { color: #f87171; }
          a { color: var(--primary); text-decoration: none; }
          a:hover { text-decoration: underline; }
        </style>
        </head>
        <body>
          <div class="card">
            <h1>API Gateway <span class="badge">ONLINE</span></h1>
            <p class="subtitle">Financial Report Summary - Ocelot Reverse Proxy</p>
            <table>
              <tr><th>Service</th><td>gateway</td></tr>
              <tr><th>Status</th><td><span style="color: var(--ok)">healthy</span></td></tr>
              <tr><th>Server Time</th><td>__TIME__</td></tr>
              <tr><th>Framework</th><td>.NET 8 + Ocelot 23.4.1</td></tr>
              <tr><th>Downstream</th><td>Backend API @ <code>localhost:5050</code></td></tr>
            </table>
            <h3 style="margin-top: 20px; font-size: 1rem; color: var(--muted);">Service Info</h3>
            <p style="color: var(--muted); font-size: .85rem; margin-top: 8px;">
              All API endpoints under <code>/api/*</code> are proxied to the backend service.
              Use <a href="/api/health">/api/health</a> to verify connectivity.
            </p>
          </div>
        </body>
        </html>
        """.Replace("__TIME__", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
        return;
    }
    await next();
});

// 健康检查（直接由 Gateway 响应，不转发）
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/api/health" && context.Request.Method == "GET")
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "ok",
                time = DateTime.UtcNow,
                service = "gateway"
            }));
        return;
    }
    await next();
});

await app.UseOcelot();

app.Run();
