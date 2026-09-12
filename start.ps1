# Usage:
#   .\start.ps1           Start Backend (5050) + Gateway (5000)
#   .\start.ps1 build     Build only
#   .\start.ps1 test      Run tests
#   .\start.ps1 clean     Clean bin/obj
#   .\start.ps1 help      Show this help

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateSet('start', 'build', 'test', 'clean', 'help')]
    [string]$Command = 'start'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$sln = Join-Path $root 'FinancialReportSummary.sln'

function Show-Help {
    Write-Host @"
=== Financial Report MVP - Tool Script ===

Usage: .\start.ps1 [command]

Commands:
  start      Start Backend (5050) + Gateway (5000) [default]
  build      Build the solution only
  test       Run tests
  clean      Clean bin/obj directories
  help       Show this help

Examples:
  .\start.ps1
  .\start.ps1 build
  .\start.ps1 test
"@
}

function Test-Prerequisites {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] dotnet command not found" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $sln)) {
        Write-Host "[ERROR] Solution file not found: $sln" -ForegroundColor Red
        exit 1
    }
}

function Start-Postgres {
    Write-Host "[Pre] Checking PostgreSQL..." -ForegroundColor Yellow
    $pgService = Get-Service -Name 'postgresql-x64-*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pgService -and $pgService.Status -eq 'Running') {
        Write-Host "  [OK] PostgreSQL running" -ForegroundColor Green
        return
    }
    try {
        Get-Service -Name 'postgresql-x64-*' -ErrorAction SilentlyContinue | Start-Service
        Write-Host "  [OK] PostgreSQL started" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] PostgreSQL failed to start. Please check manually." -ForegroundColor Red
        exit 1
    }
}

function Ensure-ApiKey {
    if (-not $env:MINIMAX_API_KEY) {
        Write-Host "  [ERROR] MINIMAX_API_KEY not set." -ForegroundColor Red
        Write-Host "  Set it before running:" -ForegroundColor Yellow
        Write-Host "    `$env:MINIMAX_API_KEY = 'sk-cp-你的key'" -ForegroundColor Gray
        Write-Host "  Or put it in backend/appsettings.Development.json (NOT committed)." -ForegroundColor Gray
        exit 1
    }
    Write-Host "  [OK] MINIMAX_API_KEY set" -ForegroundColor Green
}

function Invoke-Build {
    Write-Host "=== Build ===" -ForegroundColor Cyan
    Test-Prerequisites
    Set-Location $root

    # 1. Restore packages (install missing)
    Write-Host "`n[1/3] Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore $sln --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Restore failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Packages restored" -ForegroundColor Green

    # 2. Generate database schema SQL from EF migrations
    Write-Host "`n[2/3] Generating database schema from migrations..." -ForegroundColor Yellow

    # 2.1 Check if schema/ already has all tables -> skip EF generation
    $schemaDir = Join-Path $root 'backend\Data\schema'
    $initSql = Join-Path $schemaDir 'init.sql'
    $existingTables = @()
    if (Test-Path $schemaDir) {
        $existingTables = Get-ChildItem $schemaDir -Filter '*.sql' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d{2}_.*\.sql$' } |
            ForEach-Object { $_.Name }
    }

    # Expected tables (from entity classes)
    $expectedTables = @('EFMigrationsHistory', 'summaries', 'search_history')
    $allTablesExist = $true
    foreach ($t in $expectedTables) {
        $match = $existingTables | Where-Object { $_ -match "_$t\.sql$" }
        if (-not $match) {
            $allTablesExist = $false
            break
        }
    }

    if ($allTablesExist -and (Test-Path $initSql)) {
        Write-Host "  [SKIP] schema/ already has all tables ($($existingTables.Count) files + init.sql)" -ForegroundColor Green
        Write-Host "         Delete schema/ to force regeneration" -ForegroundColor Gray
    } else {
        $efTool = Join-Path $env:USERPROFILE '.dotnet\tools\dotnet-ef.exe'
        $schemaOutput = Join-Path $root 'backend\Data\schema.sql'
        if (Test-Path $efTool) {
            Set-Location (Join-Path $root 'backend')
            & $efTool migrations script --output $schemaOutput --no-build 2>&1 | Select-Object -Last 3
            if (Test-Path $schemaOutput) {
                $size = (Get-Item $schemaOutput).Length
                Write-Host "  [OK] Schema generated: schema.sql ($size bytes)" -ForegroundColor Green

                # 2.5 Split into per-table files
                Write-Host "`n  Splitting schema into per-table files..." -ForegroundColor Yellow
                $pythonCmd = Get-Command python -ErrorAction SilentlyContinue
                if ($pythonCmd) {
                    & python Tools/split-schema.py 2>&1 | Select-Object -Last 10
                } else {
                    Write-Host "  [WARN] python not found, skipping split" -ForegroundColor Yellow
                    Write-Host "  Run manually: python backend\Tools\split-schema.py" -ForegroundColor Gray
                }
            } else {
                Write-Host "  [WARN] Schema not generated (migrations may be empty)" -ForegroundColor Yellow
            }
            Set-Location $root
        } else {
            Write-Host "  [WARN] dotnet-ef not installed, skipping schema generation" -ForegroundColor Yellow
            Write-Host "  Install with: dotnet tool install --global dotnet-ef --version 8.0.10" -ForegroundColor Gray
        }
    }

    # 3. Compile
    Write-Host "`n[3/3] Compiling..." -ForegroundColor Yellow
    dotnet build $sln --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "`n[OK] Build succeeded" -ForegroundColor Green
}

function Invoke-Start {
    Write-Host "=== Backend + Gateway ===" -ForegroundColor Cyan
    Test-Prerequisites
    Start-Postgres
    Ensure-ApiKey

    # Backend (5050)
    Write-Host "`n[1/2] Starting Backend API (5050)..." -ForegroundColor Yellow
    $backendLog = Join-Path $root 'backend.log'
    $dotnetExe = (Get-Command dotnet.cmd -ErrorAction SilentlyContinue).Source
    if (-not $dotnetExe) { $dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source }
    if (-not $dotnetExe) { $dotnetExe = 'dotnet' }
    $backendProc = Start-Process -FilePath $dotnetExe `
        -ArgumentList 'run','--project',"$root\backend\Api.csproj",'--no-build','--urls','http://localhost:5050' `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $backendLog -RedirectStandardError "$backendLog.err"

    $ready = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        try {
            $r = Invoke-WebRequest -Uri 'http://localhost:5050/api/health' -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        } catch {}
    }
    if (-not $ready) {
        Write-Host "  [ERROR] Backend timeout. Log: $backendLog" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Backend ready (PID $($backendProc.Id))" -ForegroundColor Green

    # Gateway (5000)
    Write-Host "`n[2/2] Starting API Gateway (5000)..." -ForegroundColor Yellow
    $gatewayLog = Join-Path $root 'gateway.log'
    $gatewayProc = Start-Process -FilePath $dotnetExe `
        -ArgumentList 'run','--project',"$root\backend\Gateway\Gateway.csproj",'--no-build','--urls','http://localhost:5000' `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $gatewayLog -RedirectStandardError "$gatewayLog.err"

    $ready = $false
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Seconds 1
        try {
            $r = Invoke-WebRequest -Uri 'http://localhost:5000/api/health' -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        } catch {}
    }
    if (-not $ready) {
        Write-Host "  [ERROR] Gateway timeout. Log: $gatewayLog" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Gateway ready (PID $($gatewayProc.Id))" -ForegroundColor Green

    Write-Host "`n=== Started ===" -ForegroundColor Cyan
    Write-Host "  Gateway entry:  http://localhost:5000" -ForegroundColor White
    Write-Host "  Backend:        http://localhost:5050" -ForegroundColor Gray
    Write-Host "  Next: run .\dev.ps1 in FinancialReport_UI repo" -ForegroundColor Yellow
    Write-Host ""

    Write-Host "Press any key to stop..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

    Write-Host "`nStopping..." -ForegroundColor Yellow
    @($backendProc, $gatewayProc) | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Stopped" -ForegroundColor Green
}

function Invoke-Test {
    Write-Host "=== Tests ===" -ForegroundColor Cyan
    Test-Prerequisites
    Set-Location $root
    dotnet test $sln --nologo --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Tests failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "`n[OK] Tests passed" -ForegroundColor Green
}

function Invoke-Clean {
    Write-Host "=== Clean ===" -ForegroundColor Cyan
    Set-Location $root
    Get-ChildItem -Path $root -Recurse -Directory -Filter 'bin' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  $($_.FullName)" -ForegroundColor Gray
    }
    Get-ChildItem -Path $root -Recurse -Directory -Filter 'obj' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  $($_.FullName)" -ForegroundColor Gray
    }
    dotnet clean $sln --nologo
    Write-Host "`n[OK] Cleaned" -ForegroundColor Green
}

switch ($Command) {
    'start'  { Invoke-Start }
    'build'  { Invoke-Build }
    'test'   { Invoke-Test }
    'clean'  { Invoke-Clean }
    'help'   { Show-Help }
    default  { Show-Help }
}
