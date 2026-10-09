param([switch] $WithApi)
$ErrorActionPreference = 'Stop'
$projectName = 'promotora-test-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$overridePath = Join-Path ([IO.Path]::GetTempPath()) ($projectName + '.yml')
$previousPostgres = $env:POSTGRES_PASSWORD
$previousRabbit = $env:RABBITMQ_PASSWORD
$previousRuntimePassword = $env:PROMOTORA_DB_PASSWORD
$previousConnection = $env:ConnectionStrings__Promotora
$previousAspEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousJwtKey = $env:Jwt__SigningKey
$previousAdminEmail = $env:Bootstrap__AdminEmail
$previousAdminPassword = $env:Bootstrap__AdminPassword
$apiProcess = $null
$stdoutPath = Join-Path ([IO.Path]::GetTempPath()) ($projectName + '-api-out.log')
$stderrPath = Join-Path ([IO.Path]::GetTempPath()) ($projectName + '-api-err.log')
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
    tmpfs:
      - /var/lib/postgresql/data
  rabbitmq:
    volumes: !reset []
    tmpfs:
      - /var/lib/rabbitmq
'@ | Set-Content -LiteralPath $overridePath
    Invoke-Compose @('config', '--quiet')
    Invoke-Compose @('up', '-d', '--wait', '--wait-timeout', '180')
    Invoke-Compose @('run', '--rm', 'flyway', 'migrate')
    Invoke-Compose @('run', '--rm', 'flyway', 'validate')
    Invoke-Compose @('run', '--rm', 'flyway', 'info')
    if ($WithApi) {
        $apiDll = Join-Path $PSScriptRoot '../src/Promotora.WebApi/bin/Release/net10.0/Promotora.WebApi.dll'
        if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Ejecuta dotnet build -c Release antes de probar Kestrel.' }
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 5080)
        try { $listener.Start() } finally { $listener.Stop() }
        $env:PROMOTORA_DB_PASSWORD = [guid]::NewGuid().ToString('N')
        Get-Content (Join-Path $PSScriptRoot '../database/local/provisionar-login.sql') -Raw |
            & docker compose -p $projectName -f $composePath -f $overridePath exec -T -e PROMOTORA_DB_PASSWORD postgres psql -U postgres -d promotora -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el login restringido de la API.' }
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connection['Host'] = '127.0.0.1'
        $connection['Port'] = 5432
        $connection['Database'] = 'promotora'
        $connection['Username'] = 'promotora_runtime'
        $connection['Password'] = $env:PROMOTORA_DB_PASSWORD
        $env:ConnectionStrings__Promotora = $connection.ConnectionString
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:DOTNET_ENVIRONMENT = 'Development'
        $env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $env:Bootstrap__AdminEmail = $projectName + '@example.test'
        $env:Bootstrap__AdminPassword = [guid]::NewGuid().ToString('N')
        $options = @{
            FilePath = (Get-Command dotnet).Source
            ArgumentList = @(('"' + $apiDll + '"'), '--urls', 'http://127.0.0.1:5080')
            PassThru = $true
            RedirectStandardOutput = $stdoutPath
            RedirectStandardError = $stderrPath
        }
        if ($IsWindows) { $options['WindowStyle'] = 'Hidden' }
        $apiProcess = Start-Process @options
        $ready = $false
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            if ($apiProcess.HasExited) { throw 'La API terminó antes de estar preparada.' }
            try {
                $response = Invoke-WebRequest 'http://127.0.0.1:5080/health/ready' -TimeoutSec 2
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 500 }
        }
        if (-not $ready) { throw 'Readiness no alcanzó 200 con el login restringido.' }
        $live = Invoke-WebRequest 'http://127.0.0.1:5080/health/live'
        $ui = Invoke-WebRequest 'http://127.0.0.1:5080/swagger/index.html'
        $document = Invoke-RestMethod 'http://127.0.0.1:5080/swagger/v1/swagger.json'
        if ($live.StatusCode -ne 200 -or $ui.StatusCode -ne 200 -or @($document.paths.PSObject.Properties).Count -lt 3) {
            throw 'El contrato API/Swagger no contiene las operaciones de identidad.'
        }
        Write-Host 'API Kestrel: readiness/liveness 200, login restringido y Swagger con operaciones de identidad.'
        $initial = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/auth/login' -Method Post -ContentType 'application/json' -Body (
            @{ email = $env:Bootstrap__AdminEmail; password = $env:Bootstrap__AdminPassword } | ConvertTo-Json)
        if (-not $initial.debeCambiarPassword -or $null -ne $initial.refreshToken) { throw 'El bootstrap no exige cambio de contraseña.' }
        $limited = Invoke-WebRequest 'http://127.0.0.1:5080/api/v1/usuarios' -SkipHttpErrorCheck -Headers @{ Authorization = "Bearer $($initial.accessToken)" }
        if ($limited.StatusCode -ne 403) { throw 'La sesión inicial permite operaciones privilegiadas.' }
        $changedPassword = [guid]::NewGuid().ToString('N')
        $null = Invoke-RestMethod ("http://127.0.0.1:5080/api/v1/usuarios/$($initial.usuarioId)/password") -Method Put -ContentType 'application/json' -Headers (
            @{ Authorization = "Bearer $($initial.accessToken)" }) -Body (
            @{ passwordActual = $env:Bootstrap__AdminPassword; passwordNuevo = $changedPassword } | ConvertTo-Json)
        $session = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/auth/login' -Method Post -ContentType 'application/json' -Body (
            @{ email = $env:Bootstrap__AdminEmail; password = $changedPassword } | ConvertTo-Json)
        if ($session.debeCambiarPassword -or [string]::IsNullOrWhiteSpace($session.refreshToken)) { throw 'La sesión completa no se emitió.' }
        $users = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/usuarios?page=1&pageSize=1' -Headers @{ Authorization = "Bearer $($session.accessToken)" }
        if ($users.total -ne 1 -or @($users.items).Count -ne 1) { throw 'La consulta de usuarios no coincide con el bootstrap.' }
        Write-Host 'Identidad Kestrel: bootstrap, sesión limitada, cambio obligatorio y consulta autorizada verificados.'
    }
    $mail = Invoke-WebRequest 'http://localhost:8025/api/v2/messages'
    if ($mail.StatusCode -ne 200) { throw 'MailHog no respondió.' }
    $credential = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("promotora:$env:RABBITMQ_PASSWORD"))
    $rabbit = Invoke-WebRequest 'http://localhost:15672/api/overview' -Headers @{ Authorization = "Basic $credential" }
    if ($rabbit.StatusCode -ne 200) { throw 'RabbitMQ management no respondió.' }
    Invoke-Compose @('ps')
    Write-Host 'Compose, Flyway, RabbitMQ management y MailHog verificados.'
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    if (Test-Path -LiteralPath $overridePath) {
        # Solo contenedores y red del proyecto temporal; no se elimina ningún volumen.
        & docker compose -p $projectName -f $composePath -f $overridePath down --remove-orphans
        Remove-Item -LiteralPath $overridePath
    }
    $env:POSTGRES_PASSWORD = $previousPostgres
    $env:RABBITMQ_PASSWORD = $previousRabbit
    $env:PROMOTORA_DB_PASSWORD = $previousRuntimePassword
    $env:ConnectionStrings__Promotora = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousAspEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:Jwt__SigningKey = $previousJwtKey
    $env:Bootstrap__AdminEmail = $previousAdminEmail
    $env:Bootstrap__AdminPassword = $previousAdminPassword
    foreach ($path in @($stdoutPath, $stderrPath)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
}
