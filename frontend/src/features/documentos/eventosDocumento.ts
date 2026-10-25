import type { EventoHistorial, TipoEventoDocumento } from '../../types/documentos'
import { formatearEuros, formatearFecha } from '../../utils/formato'
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
  POSIBLE_DUPLICADO: 'Posible duplicado detectado respecto a otro documento',
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

// La API identifica el campo por su nombre interno; se muestra con la etiqueta de la bandeja de validación.
const ETIQUETA_CAMPO: Record<string, string> = {
  EmisorNombre: 'Proveedor',
  EmisorNif: 'NIF',
  NumeroFactura: 'N.º factura',
  FechaEmision: 'Fecha',
  BaseImponible: 'Base imponible',
  TipoIva: 'IVA',
  CuotaIva: 'Cuota de IVA',
  Total: 'Total',
  CategoriaSugerida: 'Categoría',
}

const CAMPOS_IMPORTE = ['BaseImponible', 'CuotaIva', 'Total']

export function etiquetaCampo(campo: string) {
  return ETIQUETA_CAMPO[campo] ?? campo
}

// Los valores llegan en formato neutro (96.00, 2026-09-01) y se muestran como en el resto de la app.
export function valorCorregido(campo: string, valor: string | null) {
  if (valor === null) return 'vacío'
  if (CAMPOS_IMPORTE.includes(campo)) return formatearEuros(Number(valor))
  if (campo === 'TipoIva') return `${valor}%`
  if (campo === 'FechaEmision') return formatearFecha(valor)
  return valor
}
