# GitHub 上传脚本
# 用法：.\upload-to-github.ps1 -RepoUrl "https://github.com/你的用户名/financial-report-summary.git"

param(
    [Parameter(Mandatory=$true)]
    [string]$RepoUrl
)

$ErrorActionPreference = 'Stop'

Write-Host "=== 准备上传到 GitHub ===" -ForegroundColor Cyan

# 1. 初始化
if (-not (Test-Path '.git')) {
    Write-Host "`n[1/4] 初始化 Git 仓库..." -ForegroundColor Yellow
    git init
    git branch -M main
} else {
    Write-Host "`n[1/4] Git 仓库已存在，跳过初始化" -ForegroundColor Yellow
}

# 2. 检查 .gitignore 是否生效
Write-Host "`n[2/4] 验证敏感文件被忽略..." -ForegroundColor Yellow
$tracked = git ls-files
$secrets = $tracked | Where-Object { $_ -match 'appsettings\.json$' -and $_ -notmatch 'example' }
if ($secrets) {
    Write-Host "❌ 检测到敏感文件将要被提交:" -ForegroundColor Red
    $secrets | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
} else {
    Write-Host "✅ 没有敏感文件在 Git 跟踪中" -ForegroundColor Green
}

# 3. 添加并提交
Write-Host "`n[3/4] 添加并提交..." -ForegroundColor Yellow
git add .
git status
$commitMsg = @"
feat: 财报智能摘要 MVP

- .NET 8 Minimal API
- AI 摘要集成
- 静态前端页面
- 模拟数据降级
"@
git commit -m $commitMsg

# 4. 添加远程并推送
Write-Host "`n[4/4] 推送到 GitHub..." -ForegroundColor Yellow
$remote = git remote get-url origin 2>$null
if (-not $remote) {
    git remote add origin $RepoUrl
} elseif ($remote -ne $RepoUrl) {
    git remote set-url origin $RepoUrl
}

git push -u origin main

Write-Host "`n✅ 上传完成！" -ForegroundColor Green
Write-Host "仓库地址: $RepoUrl" -ForegroundColor Cyan
