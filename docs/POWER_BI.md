# Power BI local — F10-06

## Entrega y estado

Power BI Desktop gratuito se instaló desde Microsoft Store por autorización del usuario. El proyecto `bi/Promotora.pbip` contiene un modelo de importación desde 16 vistas `bi`, una página Resumen con gráficos y 15 páginas R-01…R-15 con tablas y filtros. Los archivos pasaron validación con la herramienta oficial `@microsoft/powerbi-report-authoring-cli` 0.5.0 (MIT), utilizada fuera del backend. No hay Node.js ni dependencias JavaScript nuevas en el repositorio.

La primera apertura de Desktop detectó problemas de definición; se corrigieron identificadores, versiones, estructura y tema, y la validación final de archivos pasó. **El usuario solicitó comprobar personalmente la apertura visual y la actualización final. No se acredita todavía un tablero actualizado ni se ha generado un .pbix final. F10-06 conserva su casilla pendiente hasta esa comprobación.**

Se versionan PBIP/PBIR y el modelo JSON (sin datos ni contraseñas). El `.pbix`, las cachés de Power BI y sus ajustes locales quedan ignorados: pueden contener datos privados. No subirlos al PR. Esta representación revisable sustituye al binario en Git, respetando AGENTS.md §12; la copia `.pbix` de ejemplo se guarda localmente después de actualizar.

## Usuario restringido y contraseña

El script `scripts/Initialize-PowerBi.ps1` crea el LOGIN local `promotora_bi`, miembro exclusivamente de `bi_reader`, con transacciones de solo lectura por defecto. Valida permisos efectivos: SELECT sobre vistas `bi`, sin SELECT/DML sobre tablas operativas ni CREATE sobre esquemas. No altera usuarios existentes ni cambia sus contraseñas. Los roles de grupo y vistas proceden de las migraciones existentes; no se modifica ninguna migración ni esquema.

Por solicitud expresa del usuario se creó ese LOGIN con una contraseña aleatoria, guardada únicamente en `.env.powerbi` ignorado por Git. La autenticación TCP real permitió consultar `bi.vw_ventas_por_evento` y rechazó `public.usuarios`. Ninguna contraseña se imprime en scripts, logs ni documentación.

Para consultar tu contraseña local (no enviar el contenido al chat ni compartir capturas):

```powershell
notepad .\.env.powerbi
```

Este archivo es independiente del `.env` del backend: no pasarlo a Start-Local.ps1. El usuario BI no sirve para login JWT, API ni administración de PostgreSQL. No reutilizar las contraseñas del backend.

## Pasos de prueba, en orden

1. En una terminal ubicada en la raíz del proyecto, inicia PostgreSQL y los servicios locales:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

2. Verifica el usuario BI existente (no cambia contraseña):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Initialize-PowerBi.ps1 -SoloVerificar
```

3. Abre Power BI Desktop. Usa Archivo → Abrir → Examinar y selecciona `bi\Promotora.pbip`. Si quedó una sesión anterior con error, ciérrala sin guardar encima del proyecto y vuelve a abrir los archivos finales. Si solicita aplicar cambios externos, aplícalos.

4. Debes ver Resumen y las páginas R-01 a R-15. El panel Datos debe incluir las 16 tablas del modelo. Si solo aparece Page 1 o un error, no guardes esa sesión: conserva el texto del error sin credenciales para revisarlo.

5. Pulsa Inicio → Actualizar. Cuando solicite credenciales de PostgreSQL, elige autenticación **Base de datos**:

```text
Servidor: localhost:5432
Base de datos: promotora
Usuario: promotora_bi
Contraseña: valor BI_PASSWORD de tu archivo privado .env.powerbi
```

Introduce tú las credenciales en Desktop. No uses el usuario postgres, la conexión del API ni tu JWT. No hace falta iniciar sesión en Microsoft/Fabric ni publicar en Internet para usar Desktop localmente. Los parámetros `ServidorBI` y `BaseDatosBI` se cambian desde Transformar datos → Editar parámetros para otro equipo; nunca añadir contraseñas al código M.

Si Desktop presenta advertencias de cifrado o privacidad, revisa la configuración de tu PostgreSQL local antes de decidir. No se desactiva cifrado ni se cambian opciones de privacidad automáticamente. En otros entornos usa TLS y el servidor correspondiente.

6. Espera a que termine la actualización. Selecciona una sola moneda en Resumen y en páginas monetarias. Con varias monedas, las medidas de dinero devuelven vacío para impedir una suma inválida. Las filas con moneda mantienen importes individuales. Los filtros pertenecen a su tabla/página; no hay relaciones automáticas entre hechos ni un filtro global que parezca afectar a todo. En R-15 cada sección tiene su filtro de fecha.

7. Compara R-01 con `/api/v1/reportes/R-01?moneda=GTQ` en Swagger autenticado como administrador. Aplica los mismos filtros de fecha, evento y moneda; las fechas son UTC. Consulta [REPORTES.md](REPORTES.md) para la fecha y significado de cada indicador. Los modelos de detalle exponen dimensiones para filtros adicionales mediante el panel Filtros de Desktop.

8. Si no existen ventas/eventos en la base local, gráficos vacíos son esperables: no se insertan compras simuladas ni se enciende Worker automáticamente. No usar el botón Publicar para probar el tablero.

9. Después de comprobar gráficos, filtros y totales, Archivo → Guardar como → archivo Power BI `.pbix`. Guarda una copia privada, por ejemplo `bi\Promotora.local.pbix` (ignorada). No subir el archivo ni la caché. Comunica que abrió, actualizó y coincidieron los importes para cerrar F10-06.

## Preparar otra PC

Instalar Power BI Desktop desde [Microsoft](https://www.microsoft.com/power-platform/products/power-bi/desktop). Clonar el repo, configurar el `.env` del backend, iniciar infraestructura y ejecutar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Initialize-PowerBi.ps1
```

El script pedirá una contraseña nueva de 20–128 caracteres, sin imprimirla ni guardarla. También admite `-GenerarCredencialLocal` para crear un LOGIN nuevo y guardar la clave aleatoria en `.env.powerbi` ignorado; se niega a sobrescribir archivos o cambiar usuarios existentes. No hace falta ejecutarlo de nuevo si el usuario ya existe.

Power BI consulta PostgreSQL directamente, exclusivamente vistas `bi`; no consume los endpoints del API y no hereda sus policies JWT. La credencial BI concede lectura amplia del esquema de reportes, incluido top clientes, y solo debe entregarse a administradores autorizados. Es un tablero administrativo local; no implementa acceso por dueño ni RLS por JWT. Un archivo importado conserva datos de la última actualización: tratar PBIX/cachés como información privada.

## Interpretación y límites

Las medidas DAX usan las mismas bases que la API: brutos incluyen compras reembolsadas; ledger conserva signo y moneda; ocupación incluye reservas/bloqueos; asistencia separa anulados; reseñas solo aprobadas con promedio ponderado. R-14 muestra tiempos diarios y evita presentar un promedio global inexacto. Participaciones de usuarios no equivalen a usuarios únicos globales.

Importación manual, no tiempo real. No hay refresh programado, gateway, publicación en Power BI Service ni licencias de pago activadas. Verificación del API y permisos de PostgreSQL realizada; apertura/render y actualización final del proyecto pendiente del usuario. No marcar Q11/F10-06 como completos antes de verificarlo.

Formato de proyecto documentado por [Microsoft](https://learn.microsoft.com/power-bi/developer/projects/projects-overview) y [estructura PBIR](https://learn.microsoft.com/power-bi/developer/projects/projects-report).
