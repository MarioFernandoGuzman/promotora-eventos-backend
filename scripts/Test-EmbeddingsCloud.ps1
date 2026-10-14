# Prueba manual opcional: una solicitud real de embeddings, nunca se ejecuta en CI.
# Usar un proyecto Gemini Free Tier sin facturación. No muestra claves ni vectores.
param([string]$EnvPath = (Join-Path $PSScriptRoot '..\.env'))

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvPath)) { throw 'Falta el .env local con GEMINI_API_KEY.' }
$geminiClavePrueba = $null
foreach ($linea in Get-Content -LiteralPath $EnvPath -Encoding UTF8) {
    if ($linea -cmatch '^\s*GEMINI_API_KEY\s*=(.*)$') {
        if ($null -ne $geminiClavePrueba) { throw 'GEMINI_API_KEY está duplicada.' }
        $geminiClavePrueba = $Matches[1].Trim()
        if ($geminiClavePrueba.Length -ge 2) {
            $inicio = $geminiClavePrueba.Substring(0, 1)
            if (($inicio -eq "'" -or $inicio -eq '"') -and $geminiClavePrueba.EndsWith($inicio)) {
                $geminiClavePrueba = $geminiClavePrueba.Substring(1, $geminiClavePrueba.Length - 2)
            }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($geminiClavePrueba) -or $geminiClavePrueba -match '\s') { throw 'GEMINI_API_KEY debe ser un valor simple no vacío.' }

$pruebaHeaders = @{ 'x-goog-api-key' = $geminiClavePrueba }
$pruebaBody = @{
    model = 'models/gemini-embedding-2'
    content = @{ parts = @(@{ text = 'title: none | text: Prueba pública de embeddings.' }) }
    outputDimensionality = 1536
} | ConvertTo-Json -Depth 5
try {
    $pruebaRespuesta = Invoke-RestMethod -Method Post `
        -Uri 'https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent' `
        -Headers $pruebaHeaders -ContentType 'application/json' -Body $pruebaBody `
        -TimeoutSec 30 -MaximumRedirection 0
    [double[]]$pruebaVector = $pruebaRespuesta.embedding.values
    if ($pruebaVector.Count -ne 1536) { throw 'Dimensión inesperada.' }
    $pruebaNorma = 0.0
    foreach ($componente in $pruebaVector) {
        if ([double]::IsNaN($componente) -or [double]::IsInfinity($componente)) { throw 'Vector inválido.' }
        $pruebaNorma += $componente * $componente
    }
    if ($pruebaNorma -le 0) { throw 'Vector cero.' }
    Write-Output 'Gemini OK: 1536 dimensiones válidas. Se realizó una solicitud; no se probaron chat ni RAG completo.'
}
catch {
    throw 'Falló la prueba de Gemini. Revisa clave, modelo, disponibilidad y cuota Free Tier en Google AI Studio; no habilites facturación para resolverlo.'
}
finally {
    $pruebaHeaders.Clear()
    $geminiClavePrueba = $null
}
