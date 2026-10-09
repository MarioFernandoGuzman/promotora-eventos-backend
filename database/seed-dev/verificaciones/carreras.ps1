param([Parameter(Mandatory)][string]$Container)
$ErrorActionPreference='Stop'
function Invoke-TaskSql([string]$Sql) {
    $Sql | docker exec -i $Container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d promotora
    if ($LASTEXITCODE -ne 0) { throw 'Fallo en preparacion o comprobacion de carrera.' }
}
function Start-TaskSql([string]$Sql) {
    $taskInfo=[System.Diagnostics.ProcessStartInfo]::new('docker')
    foreach ($taskArg in @('exec','-i',$Container,'psql','-X','-q','-v','ON_ERROR_STOP=1','-U','postgres','-d','promotora')) { $taskInfo.ArgumentList.Add($taskArg) }
    $taskInfo.UseShellExecute=$false
    $taskInfo.RedirectStandardInput=$true
    $taskInfo.RedirectStandardOutput=$true
    $taskInfo.RedirectStandardError=$true
    $taskProcess=[System.Diagnostics.Process]::Start($taskInfo)
    $taskProcess.StandardInput.WriteLine($Sql)
    $taskProcess.StandardInput.Close()
    return @{ Process=$taskProcess; Output=$taskProcess.StandardOutput.ReadToEndAsync(); Error=$taskProcess.StandardError.ReadToEndAsync() }
}
function Test-TaskRace([string]$Name,[string]$A,[string]$B,[string]$Check) {
    $taskSessions=@((Start-TaskSql $A),(Start-TaskSql $B))
    $taskSuccess=0
    foreach ($taskSession in $taskSessions) {
        $taskSession.Process.WaitForExit()
        $taskError=$taskSession.Error.GetAwaiter().GetResult()
        if ($taskSession.Process.ExitCode -eq 0) { $taskSuccess++ }
        elseif ($taskError -notmatch '23514|40001|40P01') { throw "Error inesperado en ${Name}: $taskError" }
    }
    if ($taskSuccess -ne 1) { throw "${Name}: se esperaban 1 exito y 1 rechazo, exitos=$taskSuccess" }
    Invoke-TaskSql $Check
    Write-Host "Carrera verificada: $Name (un ganador, un rechazo)."
}
Invoke-TaskSql @'
BEGIN;
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('race@example.invalid','fixture','R','R','DUENO_RECINTO');
INSERT INTO duenos_recinto(usuario_id,empresa) SELECT id,'Race' FROM usuarios WHERE email='race@example.invalid';
INSERT INTO recintos(dueno_id,nombre,capacidad_total) SELECT id,'Race-RC',100 FROM duenos_recinto WHERE empresa='Race';
INSERT INTO recintos(dueno_id,nombre,capacidad_total) SELECT id,'Race-RR',100 FROM duenos_recinto WHERE empresa='Race';
INSERT INTO recintos(dueno_id,nombre,capacidad_total) SELECT id,'Race-tipos',100 FROM duenos_recinto WHERE empresa='Race';
INSERT INTO localidades(recinto_id,nombre,capacidad) SELECT id,'Race-L',100 FROM recintos WHERE nombre='Race-tipos';
INSERT INTO categorias_evento(nombre) VALUES ('Race');
INSERT INTO eventos(recinto_id,categoria_id,creado_por,nombre,fecha_inicio)
SELECT r.id,c.id,u.id,'Race-E',now() FROM recintos r,categorias_evento c,usuarios u
WHERE r.nombre='Race-tipos' AND c.nombre='Race' AND u.email='race@example.invalid';
INSERT INTO cuentas_financieras(tipo,dueno_id,moneda) SELECT 'DUENO',id,'RAC' FROM duenos_recinto WHERE empresa='Race';
COMMIT;
'@
foreach ($taskIsolation in @('READ COMMITTED','REPEATABLE READ')) {
    $taskVenue=if ($taskIsolation -eq 'READ COMMITTED') {'Race-RC'} else {'Race-RR'}
    $taskPrefix="\set VERBOSITY verbose`nBEGIN ISOLATION LEVEL $taskIsolation; SELECT count(*) FROM recintos; SELECT pg_sleep(0.3);"
    $taskA="$taskPrefix INSERT INTO localidades(recinto_id,nombre,capacidad) SELECT id,'A',60 FROM recintos WHERE nombre='$taskVenue'; SELECT pg_sleep(0.5); COMMIT;"
    $taskB="$taskPrefix INSERT INTO localidades(recinto_id,nombre,capacidad) SELECT id,'B',60 FROM recintos WHERE nombre='$taskVenue'; COMMIT;"
    Test-TaskRace "capacidad $taskIsolation" $taskA $taskB "DO `\`$`\`$ BEGIN IF (SELECT sum(l.capacidad) FROM localidades l JOIN recintos r ON r.id=l.recinto_id WHERE r.nombre='$taskVenue') <> 60 THEN RAISE EXCEPTION 'Capacidad invalida'; END IF; END; `\`$`\`$;".Replace('\$','$')
}
$taskTypeSql="INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) SELECT e.id,l.id,'@nombre',1,60,60 FROM eventos e JOIN localidades l ON l.recinto_id=e.recinto_id WHERE e.nombre='Race-E';"
$taskA="\set VERBOSITY verbose`nBEGIN; $($taskTypeSql.Replace('@nombre','A')) SELECT pg_sleep(0.5); COMMIT;"
$taskB="\set VERBOSITY verbose`nBEGIN; $($taskTypeSql.Replace('@nombre','B')) COMMIT;"
Test-TaskRace 'cupo por localidad' $taskA $taskB @'
DO $$ BEGIN IF (SELECT sum(t.cupo_total) FROM tipos_entrada t JOIN eventos e ON e.id=t.evento_id WHERE e.nombre='Race-E') <> 60 THEN RAISE EXCEPTION 'Cupo invalido'; END IF; END; $$;
'@
# Ambos actores usan la misma cuenta; el cobro incluye saldo en la misma tx.
$taskA=@'
\set VERBOSITY verbose
BEGIN;
UPDATE cuentas_financieras SET estado='CERRADA',fecha_cierre=now() WHERE moneda='RAC' AND estado='ACTIVA';
SELECT pg_sleep(0.5);
COMMIT;
'@
$taskB=@'
\set VERBOSITY verbose
BEGIN;
INSERT INTO movimientos_financieros(cuenta_id,tipo,monto) SELECT id,'VENTA',1 FROM cuentas_financieras WHERE moneda='RAC';
UPDATE cuentas_financieras SET saldo=saldo+1 WHERE moneda='RAC';
COMMIT;
'@
Test-TaskRace 'cerrar cuenta versus movimiento' $taskA $taskB @'
DO $$ BEGIN IF EXISTS(SELECT 1 FROM cuentas_financieras c JOIN movimientos_financieros m ON m.cuenta_id=c.id WHERE c.moneda='RAC' AND c.estado='CERRADA') THEN RAISE EXCEPTION 'Movimiento en cuenta cerrada'; END IF; END; $$;
'@
