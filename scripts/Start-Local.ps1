# Carga únicamente configuración del proyecto. El archivo .env nunca se ejecuta ni imprime.
param(
    [ValidateSet('Api', 'Worker', 'Infra')][string]$Servicio = 'Api',
    [string]$EnvPath = (Join-Path $PSScriptRoot '..\.env')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvPath)) { throw 'Falta el archivo .env local.' }
if ((Get-Item -LiteralPath $EnvPath).Length -gt 262144) { throw '.env excede el tamaño permitido.' }
$valores = @{}
foreach ($linea in Get-Content -LiteralPath $EnvPath -Encoding UTF8) {
    $texto = $linea.Trim()
    if ($texto.Length -eq 0 -or $texto.StartsWith('#')) { continue }
    if ($texto -cnotmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Formato .env inválido; usa NOMBRE=valor.' }
    $nombre = $Matches[1]
    $valor = $Matches[2].Trim()
    if ($nombre -notmatch '^(POSTGRES_(USER|PASSWORD)|RABBITMQ_(USER|PASSWORD)|OLLAMA_API_KEY|GEMINI_API_KEY|ConnectionStrings__Promotora|Jwt__[A-Za-z0-9]+|Bootstrap__(AdminEmail|AdminPassword)|Boletos__[A-Za-z0-9]+|RabbitMQ__[A-Za-z0-9]+|Smtp__[A-Za-z0-9]+|Outbox__[A-Za-z0-9]+|Compras__[A-Za-z0-9]+|Notificaciones__[A-Za-z0-9]+|Expiracion__[A-Za-z0-9]+|Recuperacion__[A-Za-z0-9]+|IA__Habilitada|Ollama__[A-Za-z0-9]+|Gemini__TimeoutSegundos|DOTNET_ENVIRONMENT|ASPNETCORE_ENVIRONMENT)$') {
        throw 'Variable no permitida en el archivo .env local.'
    }
    if ($valores.ContainsKey($nombre)) { throw 'Variable duplicada en el archivo .env local.' }
    if ($valor.StartsWith("'")) {
        if ($valor.Length -lt 2 -or -not $valor.EndsWith("'")) { throw 'Comillas sin cerrar en .env.' }
        $valor = $valor.Substring(1, $valor.Length - 2).Replace("\'", "'")
    }
    elseif ($valor.StartsWith('"')) {
        if ($valor.Length -lt 2 -or -not $valor.EndsWith('"')) { throw 'Comillas sin cerrar en .env.' }
        $valor = $valor.Substring(1, $valor.Length - 2)
    }
    $valores[$nombre] = $valor
}
# Las claves proporcionadas en .env tienen prioridad sobre user-secrets anteriores en este arranque.
if ($valores.ContainsKey('OLLAMA_API_KEY')) { $valores['Ollama__ApiKey'] = $valores['OLLAMA_API_KEY'] }
if ($valores.ContainsKey('GEMINI_API_KEY')) { $valores['Gemini__ApiKey'] = $valores['GEMINI_API_KEY'] }
$anteriores = @{}
foreach ($nombre in $valores.Keys) {
    $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
    [Environment]::SetEnvironmentVariable($nombre, $valores[$nombre], 'Process')
}
Push-Location (Join-Path $PSScriptRoot '..')
try {
    switch ($Servicio) {
        'Infra' { & docker compose up -d --wait }
        'Api' { & dotnet run --project src/Promotora.WebApi }
        'Worker' { & dotnet run --project src/Promotora.Worker }
    }
    if ($LASTEXITCODE -ne 0) { throw 'El servicio local terminó con error. Revisa el mensaje del servicio sin compartir credenciales.' }
}
finally {
    Pop-Location
    foreach ($nombre in $anteriores.Keys) { [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre], 'Process') }
    $valores.Clear()
    $anteriores.Clear()
}
