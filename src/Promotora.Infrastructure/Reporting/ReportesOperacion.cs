namespace Promotora.Infrastructure.Reporting;

internal static partial class ReporteCatalogo
{
    static partial void AgregarOperacion(Dictionary<string, ReporteDefinition> reportes)
    {
        reportes.Add("R-11", new("R-11", "Calificación promedio por presentador",
            "Solo reseñas APROBADA; promedio ponderado por cantidad de reseñas, no promedio de promedios diarios. Una reseña de evento con varios presentadores aporta a cada uno; total cuenta participaciones por presentador. Fecha de creación de reseña UTC.",
        [new("calificaciones", "vw_calificacion_presentador", [Dim("presentador_id", "presentadorId", "Presentador ID", "numero"), Dim("presentador_nombre", "presentador", "Presentador")],
            [Sum("resenas_aprobadas", "resenas", "Reseñas aprobadas"), Sum("suma_calificaciones", "sumaCalificaciones", "Suma de calificaciones"),
                new("round(sum(suma_calificaciones)::numeric / nullif(sum(resenas_aprobadas), 0), 2)", "promedio", "Calificación promedio", "decimal")], FiltrosEvento)]));
        reportes.Add("R-12", new("R-12", "Eventos por estado", "Eventos agrupados por estado actual; fecha de inicio UTC.",
        [new("eventos", "vw_eventos_por_estado", [Dim("evento_estado", "estado", "Estado del evento")], [new("count(*)", "eventos", "Eventos")], FiltrosEvento)]));
        reportes.Add("R-13", new("R-13", "Inscritos por distancia en carreras",
            "Categoría Carrera y tipos nombrados 5K/10K/21K/42K. Solo boletos VALIDO/USADO; fecha de emisión UTC. No expone información de asistentes.",
        [new("carreras", "vw_inscritos_carreras", [Dim("evento_id", "eventoId", "Evento ID", "numero"), Dim("evento_nombre", "evento", "Evento"), Dim("distancia", "distancia", "Distancia")],
            [Sum("inscritos", "inscritos", "Inscritos vigentes"), Sum("participantes_validados", "validados", "Participantes validados")], FiltrosEvento)]));
        reportes.Add("R-14", new("R-14", "Uso del agente de IA",
            "Agregado por día UTC, modelo y estado; sin preguntas, respuestas ni sesiones. El promedio de tiempo es el exacto de cada grupo diario con medición. Total de tiempo null: la vista no conserva la cantidad de mediciones no nulas necesaria para un promedio global exacto. Tokens desconocidos aportan cero.",
        [new("ia", "vw_uso_agente_ia", [Dim("fecha", "fecha", "Fecha UTC", "fecha"), Dim("modelo", "modelo", "Modelo"), Dim("estado", "estado", "Estado")],
            [Sum("consultas", "consultas", "Consultas"), new("max(tiempo_promedio_ms)", "tiempoPromedioMs", "Tiempo promedio ms", "decimal", "NULL::numeric"),
                Sum("tokens_entrada", "tokensEntrada", "Tokens de entrada"), Sum("tokens_salida", "tokensSalida", "Tokens de salida")], ["desde", "hasta", "estado"], "estado")]));
        reportes.Add("R-15", new("R-15", "Auditoría de operaciones y notificaciones",
            "Dos secciones paginadas independientes, con fechas UTC. Sin IP, destinatario ni detalle privado. Participaciones de usuarios suma usuarios distintos por día/entidad/acción; no equivale a usuarios únicos globales. Solo filtros desde/hasta compartidos por ambas secciones.",
        [new("auditoria", "vw_auditoria_operaciones", [Dim("fecha", "fecha", "Fecha UTC", "fecha"), Dim("entidad_afectada", "entidad", "Entidad"), Dim("accion", "accion", "Acción")],
            [Sum("operaciones", "operaciones", "Operaciones"), Sum("usuarios_identificados", "participacionesUsuarios", "Participaciones de usuarios")], ["desde", "hasta"], null),
         new("notificaciones", "vw_notificaciones_estado", [Dim("fecha", "fecha", "Fecha UTC", "fecha"), Dim("tipo", "tipo", "Tipo"), Dim("estado", "estado", "Estado")],
            [Sum("notificaciones", "notificaciones", "Notificaciones")], ["desde", "hasta"], null)]));
    }
}
