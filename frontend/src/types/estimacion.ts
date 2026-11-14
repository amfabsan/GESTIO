/**
 * Tipos para la estimación trimestral orientativa del IVA.
 * No es ni el resultado de una declaración tributaria ni una cifra definitiva.
 */

export interface EstimacionDocumento {
  documentoId: string
  tipo: 'INGRESO' | 'GASTO'
  emisor: string | null
  nif: string | null
  numeroFactura: string | null
  ivaIncluido: number
}

export interface ElementoPorRevisar {
  documentoId: string
  motivo: string
  severidad: string
}

export interface EstimacionResponse {
  ejercicio: number
  trimestre: number
  disponible: boolean
  motivoNoDisponible: string | null
  ivaIngresos: number
  ivaGastos: number
  diferencia: number
  fechaCalculoUtc: string
  documentosIncluidos: EstimacionDocumento[]
  elementosPorRevisar: ElementoPorRevisar[]
}
