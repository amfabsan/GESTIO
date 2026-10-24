import type { CambioCampo, EventoHistorial, TipoEventoDocumento } from '../../types/documentos'
import { obtenerNombreCompleto } from '../../utils/usuarios'

const TEXTO_EVENTO: Record<TipoEventoDocumento, string> = {
  SUBIDA: 'Documento subido',
  PROCESAMIENTO_INICIADO: 'Extracción iniciada',
  PROCESAMIENTO_CORRECTO: 'Datos extraídos',
  PROCESAMIENTO_FALLIDO: 'Extracción fallida',
  PROCESAMIENTO_COMPLETADO: 'Procesamiento completado',
  PROCESAMIENTO_ERROR: 'Error de procesamiento',
  VALIDACION_INICIADA: 'Validación iniciada',
  VALIDACION_CORREGIDA: 'Campo corregido',
  VALIDACION_APROBADA: 'Documento validado',
  VALIDACION_RECHAZADA: 'Documento rechazado',
  VALIDACION_ERROR: 'Error de validación',
}

export function textoEvento(evento: EventoHistorial) {
  return TEXTO_EVENTO[evento.tipo] ?? evento.tipo
}

// Los eventos sin actor los genera el sistema (procesamiento automático).
export function esEventoDelSistema(evento: EventoHistorial) {
  return evento.actor === null
}

export function nombreActor(evento: EventoHistorial) {
  return evento.actor
    ? obtenerNombreCompleto(evento.actor.nombre, evento.actor.primerApellido, evento.actor.segundoApellido)
    : 'Sistema'
}

export function valorCorregido(valor: CambioCampo['valorAnterior']) {
  return valor ?? 'vacío'
}
