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
