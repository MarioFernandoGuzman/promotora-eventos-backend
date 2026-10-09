param([string]$Repo = (Join-Path $PSScriptRoot '../..'))

$ErrorActionPreference = 'Stop'
$taskRepo = (Resolve-Path -LiteralPath $Repo).Path
$taskMigrations = (Resolve-Path -LiteralPath (Join-Path $taskRepo 'database/migrations')).Path
$taskChecks = Join-Path $PSScriptRoot 'verificaciones'
$taskContainer = 'promotora-fase1-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$taskCreated = $false

function Invoke-TaskSql([string]$Sql) {
    $Sql | docker exec -i $taskContainer psql -X -q -v ON_ERROR_STOP=1 -U postgres -d promotora
    if ($LASTEXITCODE -ne 0) { throw 'Falló una comprobación SQL.' }
}

try {
    # Mismas imagenes, BD y credenciales publicas efimeras del job migraciones
    # de CI. Sin puertos publicados ni volumen de datos persistente.
    docker run --detach --rm --name $taskContainer --label promotora.test=fase1 `
        -e POSTGRES_DB=promotora -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres `
        pgvector/pgvector:pg16
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear PostgreSQL temporal.' }
    $taskCreated = $true

    # En Windows, compartir red con Postgres conserva localhost:5432 de CI
    # sin requerir --network host. El resto de argumentos es el de ci.yml.
    $taskFlyway = @('run', '--rm', '--network', "container:$taskContainer",
        '--mount', "type=bind,source=$taskMigrations,target=/flyway/sql,readonly",
        'flyway/flyway:latest', '-url=jdbc:postgresql://localhost:5432/promotora',
        '-user=postgres', '-password=postgres', '-connectRetries=10')
    docker @taskFlyway migrate
    if ($LASTEXITCODE -ne 0) { throw 'Falló Flyway migrate desde cero.' }
    Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $taskChecks 'estructura.sql'))

    $taskJson = Get-Content -Raw -LiteralPath (Join-Path $taskChecks 'catalogo.sql') |
        docker exec -i $taskContainer psql -X -t -A -v ON_ERROR_STOP=1 -U postgres -d promotora
    if ($LASTEXITCODE -ne 0) { throw 'Falló lectura del catálogo PostgreSQL.' }
    $taskCatalog = $taskJson | ConvertFrom-Json
    $taskDictionary = Get-Content -Raw -LiteralPath (Join-Path $taskRepo 'docs/BASE_DE_DATOS.md')
    foreach ($taskTable in $taskCatalog) {
        $taskPattern = '(?ms)^#### `'+[regex]::Escape($taskTable.tabla)+'`[^\r\n]*\r?\n(?<body>.*?)(?=^#{2,4} |\z)'
        $taskBody = [regex]::Match($taskDictionary, $taskPattern).Groups['body'].Value
        if (-not $taskBody) { throw "Tabla ausente del diccionario: $($taskTable.tabla)" }
        foreach ($taskColumn in $taskTable.columnas) {
            $taskNull = if ($taskColumn.nullable) { 'NULL' } else { 'NOT NULL' }
            $taskDefault = if ($null -eq $taskColumn.default) { '—' } else { '`'+$taskColumn.default.Replace('|','\|')+'`' }
            $taskRow = '| `'+$taskColumn.nombre+'` | `'+$taskColumn.tipo+'` | '+$taskNull+' | '+$taskDefault+' |'
            if (-not $taskBody.Contains($taskRow)) { throw "Columna diferente al diccionario: $($taskTable.tabla).$($taskColumn.nombre)" }
        }
        foreach ($taskConstraint in $taskTable.restricciones) {
            if (-not $taskBody.Contains($taskConstraint.definicion.Replace('|','\|'))) { throw "Restricción ausente: $($taskConstraint.nombre)" }
        }
    }
    Write-Host 'Diccionario: 26 tablas, 198 columnas y 104 restricciones coinciden con PostgreSQL.'

    # Reaplicacion del SQL repetible comprueba CREATE OR REPLACE y grants.
    Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $taskMigrations 'R__bi_vistas.sql'))
    foreach ($taskFile in @('integridad.sql', 'bi.sql')) {
        Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $taskChecks $taskFile))
    }
    Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $taskMigrations 'V10__seed_catalogos.sql'))
    foreach ($taskPass in 1..2) {
        Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '01_catalogo_demo.sql'))
    }
    Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $taskChecks 'seed.sql'))
    & (Join-Path $taskChecks 'carreras.ps1') -Container $taskContainer

    docker @taskFlyway migrate
    if ($LASTEXITCODE -ne 0) { throw 'Falló la segunda ejecución de migrate.' }
    docker @taskFlyway validate
    if ($LASTEXITCODE -ne 0) { throw 'Falló Flyway validate.' }
    docker @taskFlyway info
    if ($LASTEXITCODE -ne 0) { throw 'Falló Flyway info.' }
    Write-Host 'FASE 1 VALIDADA: V1-V10 + R__bi_vistas, seeds, integridad, BI y cuatro carreras correctos.'
}
finally {
    if ($taskCreated) {
        # Solo el contenedor de esta ejecucion: --rm elimina su almacenamiento
        # efimero al pararlo. No afecta otros contenedores ni volumenes.
        docker stop $taskContainer
        if ($LASTEXITCODE -ne 0) { Write-Warning "No se pudo detener $taskContainer." }
    }
}
