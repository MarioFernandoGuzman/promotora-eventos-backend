# Carga de documentos públicos

Los cuatro documentos de esta carpeta son una base de conocimiento de reglas implementadas. No describen eventos reales ni inventan condiciones comerciales. No subas este README: es una instrucción técnica para administradores.

Con ADMINISTRADOR habilitado en Swagger:

1. POST /api/v1/ia/documentos: título y archivo MD de esta carpeta. Obtén el ID de la respuesta PENDIENTE.
2. POST /api/v1/ia/documentos/{id}/reindexar, sin body: utiliza Gemini y publica el documento como INDEXADO. Ejecuta uno por uno para respetar la cuota gratuita. No reindexar repetidamente si ya está correcto.
3. POST /api/v1/ia/consultas: pregunta pública relacionada. La respuesta debe incluir fuentes del documento.
4. Para actualizar contenido, carga una nueva versión y reindexa su nuevo ID. Solo al publicarse correctamente se retira la versión anterior.

No incluyas información personal o credenciales. Las condiciones comerciales específicas deben ser revisadas por la organización antes de incorporarlas. Los documentos no se siembran automáticamente ni se indexan durante migraciones o arranque, para controlar contenido y consumo cloud.

Si hay 429, espera el tiempo indicado o el restablecimiento de cuota del proveedor. El sistema no compra créditos ni cambia a un servicio de pago. Un documento pendiente cuya indexación falla pasa a ERROR; puede reintentarse explícitamente. Una reindexación fallida conserva un índice ya publicado.
