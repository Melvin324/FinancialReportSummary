# 停止 Backend + Gateway
# 用法：.\stop.ps1

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "=== 停止服务 ===" -ForegroundColor Cyan

$backendPid = (Get-NetTCPConnection -LocalPort 5050 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1).OwningProcess
$gatewayPid = (Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1).OwningProcess

if ($backendPid) {
    Stop-Process -Id $backendPid -Force
    Write-Host "  ✅ Backend (PID $backendPid) 已停止" -ForegroundColor Green
} else {
    Write-Host "  - Backend 未运行" -ForegroundColor Gray
}

if ($gatewayPid) {
    Stop-Process -Id $gatewayPid -Force
    Write-Host "  ✅ Gateway (PID $gatewayPid) 已停止" -ForegroundColor Green
} else {
    Write-Host "  - Gateway 未运行" -ForegroundColor Gray
}

# 清理残留 dotnet
Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue | Where-Object { $_.Path -notmatch 'Rider' } | ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "`n完成" -ForegroundColor Green
