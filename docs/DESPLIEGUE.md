# Preparación del despliegue

La API y el Worker se despliegan como servicios separados. PostgreSQL 16 con pgvector y RabbitMQ deben estar accesibles desde la red privada de ambos servicios. Sirve el frontend y la API por HTTPS. Los pagos de esta versión usan la pasarela simulada.

1. Aplica las migraciones de `database/migrations` con Flyway usando una cuenta de migración. Reserva una cuenta restringida para la API y el Worker. Comprueba que el esquema incluye V14.
2. Construye las imágenes desde la raíz.

   ```sh
   docker build --target api -t promotora-api .
   docker build --target worker -t promotora-worker .
   ```

3. Configura ambos servicios con `ConnectionStrings__Promotora` y la misma `Boletos__SigningKey`. Usa claves aleatorias Base64 de al menos 32 bytes y conserva la firma QR entre despliegues.
4. En la API configura `Jwt__SigningKey` con otra clave aleatoria. Monta almacenamiento persistente con permiso de escritura del usuario de la imagen en `/app/uploads`. Envía tráfico a su puerto 8080. Para un frontend con origen separado, configura `Cors__AllowedOrigins__0` con el origen HTTPS exacto, sin barra final.
5. Para crear el administrador inicial, inyecta `Bootstrap__AdminEmail` y `Bootstrap__AdminPassword` como secretos **solo en la API**. Inicia una réplica después de aplicar las migraciones. Cuando esté lista, verifica `/health/ready`, inicia sesión con el correo inicial y cambia la contraseña temporal. Retira ambos secretos de la configuración tras verificar el acceso. El bootstrap no cambia la contraseña de un administrador existente.
6. Configura el Worker con `RabbitMQ__Host`, `RabbitMQ__Username` y `RabbitMQ__Password`. Para procesar compras activa `Outbox__Enabled`, `Compras__ConsumerEnabled`, `Expiracion__Enabled` y `Recuperacion__Enabled`. Si habilitas notificaciones, configura el SMTP del entorno antes de activar `Notificaciones__Enabled`.
7. Si habilitas IA, configura `IA__Habilitada=true`, `OLLAMA_API_KEY` y `GEMINI_API_KEY` en la API. Las claves se guardan en el entorno del servicio.
8. Comprueba una compra de prueba y su QR, el acceso restringido del validador y la persistencia de imágenes tras reiniciar. En Production usa las rutas de salud y el frontend para la prueba. Swagger se habilita en Development.

Para que las imágenes relativas del API funcionen con cualquier navegador y conservar el contrato de despliegue, el frontend debe enrutar `/api/*` al backend y servir sus rutas web con fallback a `index.html`. Si se usa un host API separado, conserva ese proxy para recursos relativos o configura la base de API del frontend y valida imágenes y CORS.
