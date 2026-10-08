param(
    [string]$BaseUrl = "http://localhost:5212",
    [int]$Repeats = 30,
    [int]$Warmup = 5
)

$ErrorActionPreference = "Stop"

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    $sorted = $Values | Sort-Object

    if ($sorted.Count -eq 1) {
        return $sorted[0]
    }

    $index = [Math]::Ceiling(
        ($Percentile / 100.0) * $sorted.Count
    ) - 1

    if ($index -lt 0) {
        $index = 0
    }

    if ($index -ge $sorted.Count) {
        $index = $sorted.Count - 1
    }

    return $sorted[$index]
}

function Measure-Operation {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "Операция: $Name"
    Write-Host "Прогрев: $Warmup"

    for ($i = 1; $i -le $Warmup; $i++) {
        & $Action | Out-Null
    }

    $times = @()

    Write-Host "Измерения: $Repeats"

    for ($i = 1; $i -le $Repeats; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        & $Action | Out-Null

        $sw.Stop()

        $times += $sw.Elapsed.TotalMilliseconds
    }

    $sorted = $times | Sort-Object

    $p50 = Get-Percentile `
        -Values $sorted `
        -Percentile 50

    $p95 = Get-Percentile `
        -Values $sorted `
        -Percentile 95

    $max = ($sorted | Measure-Object -Maximum).Maximum

    [PSCustomObject]@{
        Operation = $Name
        P50_ms    = [Math]::Round($p50, 2)
        P95_ms    = [Math]::Round($p95, 2)
        Max_ms    = [Math]::Round($max, 2)
    }
}

Write-Host "Авторизация..."

$loginObject = @{
    username = "demo"
    password = "demo"
}

$loginJson = $loginObject | ConvertTo-Json
$loginUtf8 = [System.Text.Encoding]::UTF8.GetBytes($loginJson)

$loginResponse = Invoke-WebRequest `
    -Uri "$BaseUrl/api/login" `
    -Method POST `
    -ContentType "application/json; charset=utf-8" `
    -Body $loginUtf8 `
    -SessionVariable session

if ($loginResponse.StatusCode -ne 200) {
    throw "Ошибка авторизации"
}

Write-Host "Авторизация успешна."
Write-Host ""

$results = @()

$results += Measure-Operation `
    -Name "GET /api/categories" `
    -Action {
        Invoke-WebRequest `
            -Uri "$BaseUrl/api/categories" `
            -Method GET `
            -WebSession $session
    }

$results += Measure-Operation `
    -Name "GET /api/tickets?page=1&size=20" `
    -Action {
        Invoke-WebRequest `
            -Uri "$BaseUrl/api/tickets?page=1&size=20" `
            -Method GET `
            -WebSession $session
    }

$results += Measure-Operation `
    -Name "GET /api/tickets/1" `
    -Action {
        Invoke-WebRequest `
            -Uri "$BaseUrl/api/tickets/1" `
            -Method GET `
            -WebSession $session
    }

$results += Measure-Operation `
    -Name "GET /api/summary" `
    -Action {
        Invoke-WebRequest `
            -Uri "$BaseUrl/api/summary" `
            -Method GET `
            -WebSession $session
    }

Write-Host ""
Write-Host "Результаты:"
Write-Host ""

$results | Format-Table -AutoSize

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

$fileName = "measure_$timestamp.csv"

$results |
    Export-Csv `
        -Path $fileName `
        -NoTypeInformation `
        -Encoding UTF8

Write-Host ""
Write-Host "CSV сохранён:"
Write-Host $fileName