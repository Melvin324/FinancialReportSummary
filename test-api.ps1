# 冒烟测试脚本 —— SDK 装好后用这个验证项目能跑
# 用法：.\test-api.ps1

$ErrorActionPreference = 'Stop'
$baseUrl = 'http://localhost:5000'

Write-Host "=== 财报摘要 MVP 冒烟测试 ===" -ForegroundColor Cyan

# 1. 健康检查
Write-Host "`n[1/3] 健康检查..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -Method Get
    Write-Host "✅ 服务在线: $($health.status) | 时间: $($health.time)" -ForegroundColor Green
} catch {
    Write-Host "❌ 服务未启动，请先运行 dotnet run" -ForegroundColor Red
    exit 1
}

# 2. 首页可访问
Write-Host "`n[2/3] 首页可访问..." -ForegroundColor Yellow
try {
    $html = Invoke-WebRequest -Uri "$baseUrl/index.html" -Method Get -UseBasicParsing
    if ($html.StatusCode -eq 200 -and $html.Content -match '财报智能摘要') {
        Write-Host "✅ 前端页面正常 (200, 标题匹配)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  页面返回 200 但内容异常" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ 前端页面无法访问" -ForegroundColor Red
}

# 3. 摘要接口
Write-Host "`n[3/3] 摘要接口测试（600519 茅台）..." -ForegroundColor Yellow
try {
    $body = @{ stockCode = '600519' } | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/summary" -Method Post -Body $body -ContentType 'application/json'
    Write-Host "✅ 接口正常" -ForegroundColor Green
    Write-Host "  公司: $($resp.companyName) ($($resp.stockCode))" -ForegroundColor White
    Write-Host "  期间: $($resp.rawData.period)" -ForegroundColor White
    Write-Host "  营收: $($resp.rawData.revenue) 亿" -ForegroundColor White
    Write-Host "  净利润: $($resp.rawData.netProfit) 亿" -ForegroundColor White
    Write-Host "`n  --- AI 摘要 ---" -ForegroundColor Cyan
    Write-Host $resp.summary -ForegroundColor White
} catch {
    $err = $_.Exception.Message
    Write-Host "❌ 摘要接口失败: $err" -ForegroundColor Red
    if ($err -match '401|unauthorized') {
        Write-Host "  提示: 检查 API_KEY 是否正确设置" -ForegroundColor Yellow
    } elseif ($err -match 'API_KEY') {
        Write-Host "  提示: 请先设置环境变量 `$env:API_KEY='sk-cp-...'" -ForegroundColor Yellow
    }
}

Write-Host "`n=== 测试完成 ===" -ForegroundColor Cyan
