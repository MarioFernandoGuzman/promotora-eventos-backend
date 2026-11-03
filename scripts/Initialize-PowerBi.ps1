# Provisiona solo el LOGIN local de BI. Nunca guarda ni imprime su contraseña.
param([switch]$SoloVerificar, [switch]$GenerarCredencialLocal)
$ErrorActionPreference = 'Stop'
if ($SoloVerificar -and $GenerarCredencialLocal) { throw 'Selecciona verificación o creación, no ambas.' }
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $usuarioAdmin = $env:POSTGRES_USER
    if ([string]::IsNullOrWhiteSpace($usuarioAdmin)) {
        $envFile = Join-Path $PSScriptRoot '..\.env'
        if (Test-Path -LiteralPath $envFile) {
            foreach ($linea in [IO.File]::ReadLines((Resolve-Path -LiteralPath $envFile).Path)) {
                if ($linea -match '^POSTGRES_USER=(.*)$') { $usuarioAdmin = $Matches[1].Trim().Trim('"').Trim("'"); break }
            }
        }
    }
    if ($usuarioAdmin -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,62}$') { throw 'Configura POSTGRES_USER con el administrador local de PostgreSQL.' }
    $existe = 'SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname = ''promotora_bi'');' |
        & docker compose exec -T postgres psql -U $usuarioAdmin -d promotora -X -A -t -v ON_ERROR_STOP=1 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar PostgreSQL. Inicia primero la infraestructura local.' }
    if (($existe -join '').Trim() -eq 'f') {
        if ($SoloVerificar) { throw 'No existe promotora_bi; ejecuta sin -SoloVerificar para crearlo.' }
        $archivoPrivado = Join-Path $PSScriptRoot '..\.env.powerbi'
        if ($GenerarCredencialLocal) {
            & git check-ignore --quiet .env.powerbi
            if ($LASTEXITCODE -ne 0 -or (Test-Path -LiteralPath $archivoPrivado)) { throw 'El archivo privado debe estar ignorado y no existir; no se sobrescriben credenciales.' }
            $bytes = New-Object byte[] 32
            $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
            try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
            $generada = [Convert]::ToBase64String($bytes)
            $segura = ConvertTo-SecureString $generada -AsPlainText -Force
            $generada = $null; [Array]::Clear($bytes, 0, $bytes.Length)
        }
        else { $segura = Read-Host 'Contraseña nueva de promotora_bi (mínimo 20 caracteres; guárdala en tu gestor privado)' -AsSecureString }
        $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura)
        try {
            $texto = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero)
            if ($texto.Length -lt 20 -or $texto.Length -gt 128 -or $texto -match '[\r\n\x00]') { throw 'Usa entre 20 y 128 caracteres, sin saltos de línea.' }
            $literal = $texto.Replace("'", "''")
            $sql = "CREATE ROLE promotora_bi LOGIN INHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '$literal' IN ROLE bi_reader; ALTER ROLE promotora_bi SET default_transaction_read_only = on;"
            $resultado = $sql | & docker compose exec -T postgres psql -U $usuarioAdmin -d promotora -X -q -v ON_ERROR_STOP=1 2>&1
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el LOGIN BI. No compartas logs ni credenciales; revisa la configuración local.' }
            if ($GenerarCredencialLocal) {
                [IO.File]::WriteAllText([IO.Path]::GetFullPath($archivoPrivado), "BI_HOST=localhost:5432`nBI_DATABASE=promotora`nBI_USER=promotora_bi`nBI_PASSWORD=$texto`n", [Text.UTF8Encoding]::new($false))
            }
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero)
            $texto = $null; $literal = $null; $sql = $null; $resultado = $null
            $segura.Dispose()
        }
    }
    # Comprobar permisos efectivos, no solamente membresía. Nunca alterar un LOGIN existente.
    $validacion = @'
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='promotora_bi' AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls)
   OR NOT pg_has_role('promotora_bi','bi_reader','MEMBER') THEN RAISE EXCEPTION 'LOGIN BI no restringido'; END IF;
 IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public' AND (has_table_privilege('promotora_bi',format('%I.%I',schemaname,tablename),'SELECT') OR has_table_privilege('promotora_bi',format('%I.%I',schemaname,tablename),'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')))
   OR has_schema_privilege('promotora_bi','public','CREATE') OR has_schema_privilege('promotora_bi','bi','CREATE') THEN RAISE EXCEPTION 'Acceso operativo inesperado'; END IF;
 IF EXISTS (SELECT 1 FROM pg_views WHERE schemaname='bi' AND (NOT has_table_privilege('promotora_bi',format('%I.%I',schemaname,viewname),'SELECT') OR has_table_privilege('promotora_bi',format('%I.%I',schemaname,viewname),'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))) THEN RAISE EXCEPTION 'Permisos BI incorrectos'; END IF;
END $$;
SET ROLE promotora_bi;
SELECT count(*) AS filas_visibles FROM bi.vw_ventas_por_evento;
'@
    $salida = $validacion | & docker compose exec -T postgres psql -U $usuarioAdmin -d promotora -X -q -v ON_ERROR_STOP=1 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Los permisos de promotora_bi no son válidos. No conectes Power BI con ese usuario.' }
    $credencialLocal = Join-Path $PSScriptRoot '..\.env.powerbi'
    if (Test-Path -LiteralPath $credencialLocal) {
        $passwordAnterior = $env:PGPASSWORD
        try {
            $lineaPassword = [IO.File]::ReadLines((Resolve-Path -LiteralPath $credencialLocal).Path) | Where-Object { $_.StartsWith('BI_PASSWORD=') } | Select-Object -First 1
            if (-not $lineaPassword) { throw 'Credencial privada incompleta.' }
            $env:PGPASSWORD = $lineaPassword.Substring('BI_PASSWORD='.Length)
            $lineaPassword = $null
            $conexionReal = 'SELECT count(*) FROM bi.vw_ventas_por_evento;' | & docker compose exec -T -e PGPASSWORD postgres psql -h postgres -U promotora_bi -d promotora -X -q -v ON_ERROR_STOP=1 2>&1
            if ($LASTEXITCODE -ne 0) { throw 'La autenticación TCP del LOGIN BI falló; la contraseña privada no fue mostrada.' }
            $preferenciaErrores = $ErrorActionPreference
            try {
                # PowerShell 5 convierte stderr nativo en ErrorRecord: se captura
                # únicamente este rechazo esperado, sin interrumpir la verificación.
                $ErrorActionPreference = 'Continue'
                $denegado = 'SELECT count(*) FROM public.usuarios;' | & docker compose exec -T -e PGPASSWORD postgres psql -h postgres -U promotora_bi -d promotora -X -q -v ON_ERROR_STOP=1 -v VERBOSITY=sqlstate 2>&1
                $codigoDenegado = $LASTEXITCODE
            }
            finally { $ErrorActionPreference = $preferenciaErrores }
            if ($codigoDenegado -eq 0 -or ($denegado -join '') -notmatch '42501') { throw 'No se verificó el rechazo de acceso a usuarios; no uses el LOGIN BI hasta revisarlo.' }
        }
        finally { $env:PGPASSWORD = $passwordAnterior; $conexionReal = $null; $denegado = $null }
    }
    Write-Host 'BI listo: localhost:5432 / promotora / promotora_bi. Solo lectura de vistas bi; sin acceso a tablas operativas. Usuarios existentes no fueron alterados.'
}
finally { Pop-Location }
