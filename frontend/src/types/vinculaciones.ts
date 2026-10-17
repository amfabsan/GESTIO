export type EstadoVinculacion = 'PENDIENTE' | 'ACTIVA' | 'FINALIZADA' | 'RECHAZADA'

export interface AutonomoVinculado {
  id: string
  nombre: string
  primerApellido: string
  segundoApellido: string
  nif: string
}

export interface Vinculacion {
  id: string
  autonomoId: string
  autonomo: AutonomoVinculado | null
  codigoAsesoria: string
  estado: EstadoVinculacion
  fechaSolicitud: string
  fechaAceptacion: string | null
  aceptadaPorId: string | null
  fechaFinalizacion: string | null
  finalizadaPorId: string | null
  fechaRechazo: string | null
  rechazadaPorId: string | null
}

export interface SolicitarVinculacionRequest {
  codigoAsesoria: string
}
