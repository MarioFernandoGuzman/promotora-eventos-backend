param()
$ErrorActionPreference = 'Stop'
$projectName = 'promotora-test-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$overridePath = Join-Path ([IO.Path]::GetTempPath()) ($projectName + '.yml')
$previousPostgres = $env:POSTGRES_PASSWORD
$previousRabbit = $env:RABBITMQ_PASSWORD
$composePath = Join-Path $PSScriptRoot '../docker-compose.yml'
function Invoke-Compose([string[]] $Arguments) {
    & docker compose -p $projectName -f $composePath -f $overridePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Compose falló: $($Arguments[0])" }
}
try {
    $env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N')
    $env:RABBITMQ_PASSWORD = [guid]::NewGuid().ToString('N')
    # Proyecto propiedad de esta prueba: sin datos persistentes, sin secretos en archivos.
    @'
services:
  postgres:
    volumes: !reset []
  rabbitmq:
    volumes: !reset []
'@ | Set-Content -LiteralPath $overridePath
    Invoke-Compose @('config', '--quiet')
    Invoke-Compose @('up', '-d', '--wait', '--wait-timeout', '180')
    Invoke-Compose @('run', '--rm', 'flyway', 'migrate')
    Invoke-Compose @('run', '--rm', 'flyway', 'validate')
    Invoke-Compose @('run', '--rm', 'flyway', 'info')
    $mail = Invoke-WebRequest 'http://localhost:8025/api/v2/messages'
    if ($mail.StatusCode -ne 200) { throw 'MailHog no respondió.' }
    $credential = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("promotora:$env:RABBITMQ_PASSWORD"))
    $rabbit = Invoke-WebRequest 'http://localhost:15672/api/overview' -Headers @{ Authorization = "Basic $credential" }
    if ($rabbit.StatusCode -ne 200) { throw 'RabbitMQ management no respondió.' }
    Invoke-Compose @('ps')
    Write-Host 'Compose, Flyway, RabbitMQ management y MailHog verificados.'
}
finally {
    if (Test-Path -LiteralPath $overridePath) {
        # Solo contenedores y red del proyecto temporal; no se elimina ningún volumen.
        & docker compose -p $projectName -f $composePath -f $overridePath down --remove-orphans
        Remove-Item -LiteralPath $overridePath
    }
    $env:POSTGRES_PASSWORD = $previousPostgres
    $env:RABBITMQ_PASSWORD = $previousRabbit
}
