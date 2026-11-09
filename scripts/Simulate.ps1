param([int]$Count = 12, [string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
if ($Count -lt 1 -or $Count -gt 10000) { throw 'Count must be between 1 and 10000.' }
for ($i = 0; $i -lt $Count; $i++) {
    $packet = @{ sensorId = 'MS-01'; payload = (Get-Random -Minimum 25 -Maximum 65); timestamp = [DateTime]::UtcNow.ToString('o') }
    $result = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/telemetry/ingest" -ContentType 'application/json' -Body ($packet | ConvertTo-Json)
    Write-Host "Accepted packet $($result.sequence): $($packet.payload)%"
    Start-Sleep -Milliseconds 500
}
Write-Host 'Complete. Inspect the telemetry dashboard and history query.'
