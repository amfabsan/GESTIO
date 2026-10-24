export type EstadoDocumento =
  | 'SUBIDO'
  | 'PROCESANDO'
  | 'PENDIENTE_VALIDACION'
  | 'VALIDADO'
  | 'ERROR'
  | 'ACLARACION_SOLICITADA'

export type EventoDocumento = {
  tipo: string
  fecha: string
  detalle: string | null
}

export type TipoEventoDocumento =
  | 'SUBIDA'
  | 'PROCESAMIENTO_INICIADO'
  | 'PROCESAMIENTO_CORRECTO'
  | 'PROCESAMIENTO_FALLIDO'
  | 'PROCESAMIENTO_COMPLETADO'
  | 'PROCESAMIENTO_ERROR'
  | 'VALIDACION_INICIADA'
  | 'VALIDACION_CORREGIDA'
  | 'VALIDACION_APROBADA'
  | 'VALIDACION_RECHAZADA'
  | 'VALIDACION_ERROR'

export type ActorEvento = {
  id: string
  nombre: string
  primerApellido: string
  segundoApellido: string
}

export type CambioCampo = {
  campo: string
  valorAnterior: string | null
  valorNuevo: string | null
}

export type EventoHistorial = {
  id: string
  tipo: TipoEventoDocumento
  fecha: string
  detalle: string | null
  actor: ActorEvento | null
  cambio: CambioCampo | null
}

export type ExtraccionDocumento = {
  numeroFactura: string | null
  emisorNombre: string | null
  emisorNif: string | null
  fechaEmision: string | null
  baseImponible: number | null
  tipoIva: number | null
  cuotaIva: number | null
  total: number | null
  categoriaSugerida: string | null
  nivelConfianza: string | null
  datosCrudosJson: string | null
  desglosesIva: { baseImponible: number | null; tipoIva: number | null; cuotaIva: number | null }[]
  advertencias: string[]
}

export type HistorialValidacion = {
  id: string
  campo: string
  valorAnterior: string | null
  valorNuevo: string | null
  tipoCambio: string
  usuarioId: string
  fechaCambio: string
}

export type AnomaliaDocumento = {
  tipo: string
  descripcion: string
  severidad: string
  resuelta: boolean
  fechaDeteccion: string
}

export type Documento = {
  id: string
  nombreArchivo: string
  usuario: string
  fechaSubida: string
  estado: EstadoDocumento
  tipoMime: string
  tamanoBytes: number
  origen: string
  tieneOriginal: boolean
  extraccion: ExtraccionDocumento | null
  anomalias: AnomaliaDocumento[]
  historial: EventoDocumento[]
  intentos: { id: string; inicio: string; fin: string | null; procesador: string; version: string | null; resultado: string; error: string | null; valoresJson: string | null }[]
  validacion?: HistorialValidacion[]
}

export type TipoArchivoPermitido = {
  extension: string
  tipoMime: string
  firmaHex: string
}

export type ConfiguracionSubida = {
  tamanoMaximoBytes: number
  tiposPermitidos: TipoArchivoPermitido[]
}

export type CampoValidacion = {
  campo: string
  valor: string | null
}