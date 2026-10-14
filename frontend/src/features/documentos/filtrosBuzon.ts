import type { Documento, EstadoDocumento } from '../../types/documentos'

export type FiltroBuzon = 'TODOS' | 'PENDIENTES' | 'ACLARACION_SOLICITADA' | 'VALIDADO'

// Igual que en el prototipo, "Pendientes de validar" agrupa todo lo que aún no ha revisado el gestor.
const ESTADOS_POR_FILTRO: Record<FiltroBuzon, EstadoDocumento[] | null> = {
  TODOS: null,
  PENDIENTES: ['SUBIDO', 'PROCESANDO', 'PENDIENTE_VALIDACION'],
  ACLARACION_SOLICITADA: ['ACLARACION_SOLICITADA'],
  VALIDADO: ['VALIDADO'],
}

export const FILTROS_BUZON: { id: FiltroBuzon; texto: string }[] = [
  { id: 'TODOS', texto: 'Todos' },
  { id: 'PENDIENTES', texto: 'Pendientes de validar' },
  { id: 'ACLARACION_SOLICITADA', texto: 'Aclaración pedida' },
  { id: 'VALIDADO', texto: 'Validados' },
]

export function cumpleFiltro(documento: Documento, filtro: FiltroBuzon) {
  const estados = ESTADOS_POR_FILTRO[filtro]
  return estados === null || estados.includes(documento.estado)
}

export function tieneAnomaliaAbierta(documento: Documento) {
  return documento.estado !== 'VALIDADO' && documento.anomalias.some((anomalia) => !anomalia.resuelta)
}