# Cancelaciones y reembolsos en el alcance actual

Un cliente puede solicitar la cancelación de una compra mientras permanezca pendiente. Una compra que ya se está procesando no admite esa cancelación por el cliente. La aplicación debe informar el estado actual y orientar a contactar a la organización cuando corresponda.

El alcance implementado admite reembolsos totales gestionados por un administrador habilitado. No admite reembolsos parciales. El sistema revierte los importes registrados originalmente y no recalcula el importe con tasas nuevas.

Una devolución de cupo corresponde únicamente a boletos válidos que se anulan durante el reembolso. Un boleto ya utilizado conserva el historial de asistencia y no devuelve cupo, aunque la operación sea reembolsada en su totalidad.

El entorno actual utiliza una pasarela de pago simulada. Un registro de reembolso en este entorno no representa una transferencia bancaria ni una devolución efectiva por una pasarela externa.

Este documento explica el comportamiento técnico disponible. No establece una garantía comercial de devolución, un plazo legal o una política específica de cancelación de eventos. Las condiciones comerciales deben publicarse y confirmarse por la organización antes de ofrecerlas al público. José debe indicar cuando no dispone de esa información.
