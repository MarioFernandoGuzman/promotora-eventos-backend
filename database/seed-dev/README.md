# Datos de desarrollo — Fase 1

Aplicar primero V1–V10 y `R__bi_vistas.sql` con Flyway. `01_catalogo_demo.sql` es opcional y queda fuera de las migraciones de producción.

El seed crea dos usuarios INACTIVO (cliente y dueño), sus perfiles, un recinto de 1000 personas, dos localidades con geometría, dos presentadores, un concierto y una carrera EN_VENTA con cuatro tipos de entrada. Los precios son datos sintéticos en GTQ. Las fechas se fijan respecto a la primera ejecución y no se actualizan al repetirlo.

Los usuarios no permiten login: `password_hash` contiene un marcador inutilizable, sin contraseña asociada. No se crea ningún administrador. El SUPERADMIN se creará en F3-01 mediante bootstrap con secretos del entorno. Los datos del seed sirven para explorar el catálogo; las compras, boletos y finanzas se probarán mediante los casos de uso cuando existan.

El script solo inserta datos ausentes. Repetirlo no restaura cupos, precios ni estados modificados. El bloqueo transaccional impide que dos ejecuciones simultáneas del mismo seed inserten duplicados. Si las identidades reservadas existen con otro rol, estado o marcador, falla y revierte toda la transacción.

Con un contenedor de desarrollo ya migrado, desde la raíz del repositorio:

```powershell
Get-Content -Raw .\database\seed-dev\01_catalogo_demo.sql |
    docker exec -i <nombre-del-contenedor> psql -X -v ON_ERROR_STOP=1 -U <usuario-db> -d promotora
```

Comprobaciones SQL:

```sql
SELECT nombre FROM categorias_evento ORDER BY nombre;
SELECT tipo, moneda, saldo, estado FROM cuentas_financieras;
SELECT email, rol, estado FROM usuarios WHERE email LIKE '%.seed@example.invalid';
SELECT e.nombre, t.nombre, t.precio, t.cupo_total, t.cupo_disponible
FROM eventos e JOIN tipos_entrada t ON t.evento_id = e.id
WHERE e.nombre IN ('Concierto Demo', 'Carrera Demo');
SELECT * FROM bi.vw_ocupacion_localidad;
```

Resultado esperado en una BD nueva: seis categorías, dos cuentas base GTQ con saldo cero, dos usuarios inactivos y cuatro tipos de entrada. Ocupación inicial: 0 %. No hay compras, pagos, movimientos ni boletos generados por este seed.
