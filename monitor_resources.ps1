param(
    [int]$Seconds = 40,
    [int]$Interval = 1
)

$results = @()

$process = Get-Process SupportService -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $process) {
    Write-Host "Процесс SupportService не найден."
    exit 1
}

$previousCpu = $process.CPU
$previousTime = Get-Date

for ($i = 0; $i -lt $Seconds; $i += $Interval) {

    $process.Refresh()

    $now = Get-Date

    $currentCpu = $process.CPU

    $elapsed =
        ($now - $previousTime).TotalSeconds

    $cpuDelta =
        $currentCpu - $previousCpu

    $appCpu =
        ($cpuDelta / $elapsed) * 100

    $appMemoryMb =
        $process.WorkingSet64 / 1MB

    $dockerLine = docker stats `
        fyodor_kartashov_postgres `
        --no-stream `
        --format "{{.CPUPerc}}|{{.MemUsage}}"

    $parts = $dockerLine -split "\|"

    $dbCpu =
        $parts[0].Replace("%", "")

    $dbMemory =
        ($parts[1] -split "/")[0].Trim()

    $connections = docker exec `
        fyodor_kartashov_postgres `
        psql -U app -d fyodor_kartashov `
        -t -A `
        -c "SELECT count(*), count(*) FILTER (WHERE state='active') FROM pg_stat_activity WHERE datname='fyodor_kartashov';"

    $connectionParts =
        $connections.Trim() -split "\|"

    $results += [PSCustomObject]@{
        Time = $now.ToString("HH:mm:ss")
        App_CPU_Percent = [Math]::Round($appCpu, 2)
        App_Memory_MB = [Math]::Round($appMemoryMb, 2)
        DB_CPU_Percent = $dbCpu
        DB_Memory = $dbMemory
        DB_Connections = $connectionParts[0]
        DB_Active = $connectionParts[1]
    }

    $previousCpu = $currentCpu
    $previousTime = $now

    Start-Sleep -Seconds $Interval
}

$results |
    Format-Table -AutoSize

$results |
    Export-Csv `
        .\load\results\resources_heavy.csv `
        -NoTypeInformation `
        -Encoding UTF8

Write-Host ""
Write-Host "Сохранено: load\results\resources_heavy.csv"