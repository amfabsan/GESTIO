export type EventoDocumento = {
  tipo: string
  fecha: string
  detalle: string | null
}

export type Documento = {
  id: string
  nombreArchivo: string
  usuario: string
  fechaSubida: string
  estado: string
  tipoMime: string
  tamanoBytes: number
  origen: string
  tieneOriginal: boolean
  historial: EventoDocumento[]
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