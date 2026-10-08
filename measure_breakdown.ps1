param(
    [string]$BaseUrl = "http://localhost:5212",
    [int]$Repeats = 30,
    [int]$Warmup = 5
)

$ErrorActionPreference = "Stop"

function Get-Median {
    param([double[]]$Values)

    $sorted = @($Values | Sort-Object)
    $count = $sorted.Count

    if ($count -eq 0) {
        return 0
    }

    if ($count % 2 -eq 1) {
        return $sorted[[int]($count / 2)]
    }

    $left = $sorted[($count / 2) - 1]
    $right = $sorted[$count / 2]

    return ($left + $right) / 2
}

function Measure-Breakdown {
    param(
        [string]$Name,
        [string]$Uri,
        $Session
    )

    Write-Host ""
    Write-Host "Операция: $Name"
    Write-Host "Прогрев: $Warmup"

    for ($i = 1; $i -le $Warmup; $i++) {
        Invoke-WebRequest `
            -Uri $Uri `
            -WebSession $Session |
            Out-Null
    }

    $totalTimes = @()
    $dbTimes = @()
    $appTimes = @()
    $queryCounts = @()

    Write-Host "Измерения: $Repeats"

    for ($i = 1; $i -le $Repeats; $i++) {

        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        $response = Invoke-WebRequest `
            -Uri $Uri `
            -WebSession $Session

        $sw.Stop()

        $totalMs = $sw.Elapsed.TotalMilliseconds

        $dbText =
            [string]$response.Headers["X-Db-Time-Ms"]

        $dbMs =
            [double]::Parse(
                $dbText,
                [System.Globalization.CultureInfo]::InvariantCulture
            )

        $queryCount =
            [int]$response.Headers["X-Db-Queries"]

        $appMs = $totalMs - $dbMs

        if ($appMs -lt 0) {
            $appMs = 0
        }

        $totalTimes += $totalMs
        $dbTimes += $dbMs
        $appTimes += $appMs
        $queryCounts += $queryCount
    }

    $uniqueQueryCounts =
        $queryCounts |
        Sort-Object -Unique

    $queryText =
        $uniqueQueryCounts -join ","

    return [PSCustomObject]@{
        Operation      = $Name
        Total_ms       = [Math]::Round(
            (Get-Median $totalTimes), 2)

        DB_ms          = [Math]::Round(
            (Get-Median $dbTimes), 2)

        App_ms         = [Math]::Round(
            (Get-Median $appTimes), 2)

        Queries        = $queryText
    }
}

Write-Host "Авторизация..."

$loginObject = @{
    username = "demo"
    password = "demo"
}

$loginJson =
    $loginObject | ConvertTo-Json

$loginUtf8 =
    [System.Text.Encoding]::UTF8.GetBytes(
        $loginJson)

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

$results = @()

$results += Measure-Breakdown `
    -Name "GET /api/tickets?page=1&size=20" `
    -Uri "$BaseUrl/api/tickets?page=1&size=20" `
    -Session $session

$results += Measure-Breakdown `
    -Name "GET /api/summary" `
    -Uri "$BaseUrl/api/summary" `
    -Session $session

Write-Host ""
Write-Host "Разложение времени:"
Write-Host ""

$results | Format-Table -AutoSize

$results |
    Export-Csv `
        -Path "breakdown_work.csv" `
        -NoTypeInformation `
        -Encoding UTF8

Write-Host ""
Write-Host "Результат сохранён в breakdown_work.csv"