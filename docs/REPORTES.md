# Reportes administrativos — Fase 10

## Acceso y contrato

`GET /api/v1/reportes/{codigo}?formato=json|xlsx|pdf`, exclusivamente ADMINISTRADOR ACTIVO con cambio de contraseña completado. El handler verifica de nuevo el usuario en PostgreSQL; no basta un rol viejo en el JWT. Swagger muestra JSON y descargas binarias. CLIENTE/DUENO_RECINTO reciben 403; anónimo 401.

Filtros comunes: `desde` inclusivo y `hasta` exclusivo (`yyyy-MM-dd`, días UTC), `eventoId`, `categoriaId`, `recintoId`, `duenoId`, `estado`, `moneda` y `metodo`. IDs positivos. No todos aplican a todos los reportes: uno incompatible devuelve 400, nunca se ignora silenciosamente. No se admiten nombres SQL, orden ni agrupaciones arbitrarias desde el cliente.

JSON: `page` 1–100000 y `pageSize` 1–100 (default 20). Respuesta `codigo`, `titulo`, `nota`, `filtros`, `secciones`. Cada sección contiene `columnas` (campo/titulo/tipo), `agrupacion`, `filas` (valores por campo), `totales`, `totalFilas`, `page`, `pageSize`. Los totales cubren todo el conjunto filtrado, incluso si la página está vacía; importes de monedas distintas nunca se suman. Filas y totales usan la misma instantánea Repeatable Read. No se almacenan reportes ni se cambia el ledger.

## Catálogo y filtros

En esta tabla, E = desde/hasta/eventoId/categoriaId/recintoId/duenoId/estado. Estado corresponde al evento salvo indicación. Las fechas no representan el mismo hecho en todos los reportes.

| Código | Agrupación | Filtros | Interpretación |
|---|---|---|---|
| R-01 | Evento, moneda | E + moneda | Ventas brutas de compras COMPLETADA; fecha compra |
| R-02 | Categoría, moneda | E + moneda | Ventas brutas; fecha compra |
| R-03 | Recinto, moneda | E + moneda | Ventas brutas; fecha compra |
| R-04 | Fecha, método, moneda | E + moneda + metodo | Último pago APROBADO por compra; fecha compra |
| R-05 | Evento, localidad | E | Cupo ocupado incluye reservas y bloqueos; fecha inicio evento |
| R-06 | Evento, moneda | E + moneda | Emitidos/vigentes/usados/anulados; fecha emisión |
| R-07 | Dueño, cuenta, moneda | E + moneda | Ledger con signo: ventas/reembolsos/liquidaciones/neto; fecha movimiento |
| R-08 | Fecha, moneda | E + moneda | Comisiones/reembolsos/neto PLATAFORMA; no incluye IVA |
| R-09 | Cliente, moneda | E + moneda | Total bruto descendente por moneda; fecha compra; nombres solo administración |
| R-10 | Evento, estado de compra, moneda | E + moneda | Estado FALLIDA/CANCELADA/EXPIRADA; importe intentado, no cobrado |
| R-11 | Presentador | E | Solo APROBADA; promedio ponderado; fecha reseña |
| R-12 | Estado del evento | E | Cantidad de eventos; fecha inicio |
| R-13 | Evento, distancia | E | Carrera y tipos 5K/10K/21K/42K; boletos VALIDO/USADO; fecha emisión |
| R-14 | Fecha, modelo, estado IA | desde/hasta/estado | Consultas/tokens y tiempo diario; sin conversaciones |
| R-15 | Fecha/entidad/acción y fecha/tipo/estado | desde/hasta | Dos secciones: auditoría y notificaciones |

Ventas brutas incluyen compras COMPLETADA reembolsadas: para netos ver R-07/R-08. R-07 con filtro de evento/categoría/recinto/estado excluye liquidaciones sin compra asociada; su neto no equivale a saldo histórico. R-05 no representa asistencia ni distingue tipos de ocupación. No se agregan tablas por la extensión de sillas: el cupo agregado y los boletos ya están integrados.

R-11 calcula `SUM(suma_calificaciones)/SUM(resenas_aprobadas)`; una reseña aporta a cada presentador asociado, por lo que el total cuenta participaciones por presentador. R-14 conserva el promedio exacto de cada fila diaria de la vista; el total de tiempo es null porque falta el conteo de mediciones no nulas para combinar grupos con precisión. R-15 suma participaciones de usuarios distintos por día/entidad/acción, no usuarios únicos globales. Las notas también viajan en cada respuesta/archivo.

## Exportación

`formato=xlsx` o `formato=pdf` exporta todas las filas filtradas e ignora page/pageSize. Límites por sección: 10000 XLSX y 500 PDF; exceso devuelve 422 `reporte.exportacion_grande`, sin archivo parcial ni auditoría de exportación exitosa. Aplicar filtros para dividir conjuntos mayores. Formato desconocido: 400. Descargas sin caché y con `Content-Disposition: attachment`.

Excel incluye una hoja de información y una hoja por sección, números tipados, texto sin fórmulas, encabezados, filtro y totales. PDF usa A3 horizontal con encabezado repetido, filtros y totales. Los archivos se entregan como stream desde almacenamiento temporal exclusivo y se eliminan al cerrar; ClosedXML mantiene el libro en memoria durante la generación, acotado por el límite, por lo que no es un exportador de volumen ilimitado. La cancelación interrumpe consultas y se comprueba durante la preparación. Cada archivo generado registra `EXPORT`, entidad `reporte`, ID R-xx y actor, antes de comenzar la descarga. Esto acredita generación, no recepción completa por el navegador.

Dependencias nuevas: ClosedXML 0.105.1 ([MIT](https://github.com/ClosedXML/ClosedXML/blob/develop/LICENSE)) y QuestPDF 2026.9.1 ([Community](https://www.questpdf.com/license/community.html)). Community corresponde al uso académico del proyecto; antes de un despliegue comercial se debe verificar elegibilidad y configurar la licencia correspondiente. No se compra ni activa ningún servicio. El backend continúa exclusivamente C#.

## Probar localmente

Desde la raíz del repositorio, en terminales separadas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

Abrir http://localhost:5080/swagger, iniciar sesión con ADMINISTRADOR y colocar el accessToken en Authorize. Si el usuario exige cambio inicial, completar primero el cambio. Las credenciales de prueba existentes siguen en el archivo local privado `.env.test-users`; no se añaden usuarios ni contraseñas con esta fase.

Ejemplos para Try it out:

```text
/api/v1/reportes/R-01?moneda=GTQ&pageSize=20
/api/v1/reportes/R-01?moneda=GTQ&formato=xlsx
/api/v1/reportes/R-07?desde=2026-01-01&hasta=2027-01-01&moneda=GTQ&formato=pdf
/api/v1/reportes/R-14?desde=2026-01-01&hasta=2027-01-01
```

No hace falta encender Worker para consultar datos existentes. Sin compras o eventos, los reportes pueden devolver filas vacías; esto no implica un error. No generar pagos ni reindexar documentos solo para probar reportes.
