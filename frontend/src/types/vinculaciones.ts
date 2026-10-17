export type EstadoVinculacion = 'PENDIENTE' | 'ACTIVA' | 'FINALIZADA'

export interface AutonomoVinculado {
  id: string
  nombre: string
  primerApellido: string
  segundoApellido: string
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
}

export interface SolicitarVinculacionRequest {
  codigoAsesoria: string
}
