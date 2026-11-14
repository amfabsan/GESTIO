import { apiClient } from '../../services/apiClient'

export type LineaFactura = {
  id: string
  orden: number
  descripcion: string | null
  cantidad: number | null
  precioUnitario: number | null
  ivaPorcentaje: number | null
  base: number | null
}

export type DesgloseIva = { tipo: number; base: number; cuota: number }

export type Factura = {
  id: string
  autonomoId: string
  contactoId: string | null
  estado: 'BORRADOR' | 'EMITIDA'
  serie: string
  numero: number | null
  numeroVisible: string | null
  fecha: string
  retencionPorcentaje: number | null
  baseImponible: number | null
  desgloseIva: DesgloseIva[]
  cuotaIva: number | null
  importeRetencion: number | null
  total: number | null
  emisorNombre: string | null
  emisorNif: string | null
  emisorDireccion: string | null
  clienteNombre: string | null
  clienteNif: string | null
  clienteDireccion: string | null
  emitida: string | null
  lineas: LineaFactura[]
  // UT23 (PB-23): generado en el backend. Null en borradores y en facturas emitidas antes de UT23.
  qr: QrFactura | null
}

export type QrFactura = { url: string; imagenPngBase64: string }

export type FacturaLista = {
  id: string
  estado: 'BORRADOR' | 'EMITIDA'
  serie: string
  numero: number | null
  numeroVisible: string | null
  fecha: string
  contactoId: string | null
  clienteNombre: string | null
  clienteNif: string | null
  clienteDireccion: string | null
  numeroLineas: number
  baseImponible: number | null
  total: number | null
}

export type GuardarFactura = {
  contactoId: string | null
  fecha: string
  retencionPorcentaje: number | null
  lineas: { descripcion: string | null; cantidad: number | null; precioUnitario: number | null; ivaPorcentaje: number | null }[]
}

const ruta = (autonomoId: string) => `/autonomos/${autonomoId}/facturas`

export async function listarFacturas(autonomoId: string) {
  const { data } = await apiClient.get<FacturaLista[]>(ruta(autonomoId))
  return data
}

export async function obtenerFactura(autonomoId: string, facturaId: string) {
  const { data } = await apiClient.get<Factura>(`${ruta(autonomoId)}/${facturaId}`)
  return data
}

export async function guardarBorrador(autonomoId: string, datos: GuardarFactura, facturaId?: string) {
  const { data } = facturaId
    ? await apiClient.put<Factura>(`${ruta(autonomoId)}/${facturaId}`, datos)
    : await apiClient.post<Factura>(ruta(autonomoId), datos)
  return data
}

export async function eliminarBorrador(autonomoId: string, facturaId: string) {
  await apiClient.delete(`${ruta(autonomoId)}/${facturaId}`)
}

export async function comprobarFactura(autonomoId: string, facturaId: string) {
  const { data } = await apiClient.post<Factura>(`${ruta(autonomoId)}/${facturaId}/comprobar`)
  return data
}

export async function emitirFactura(autonomoId: string, facturaId: string) {
  const { data } = await apiClient.post<Factura>(`${ruta(autonomoId)}/${facturaId}/emitir`)
  return data
}

export async function guardarDireccionFiscal(autonomoId: string, contactoId: string, direccion: string) {
  const { data } = await apiClient.put<{ id: string; nombre: string; nif: string; direccion: string; activo: boolean }>(
    `/autonomos/${autonomoId}/contactos/${contactoId}/direccion-fiscal`, { direccion })
  return data
}

export async function obtenerDireccionFiscalEmisor(autonomoId: string) {
  const { data } = await apiClient.get<{ direccionFiscal: string | null }>(`/autonomos/${autonomoId}/direccion-fiscal`)
  return data.direccionFiscal ?? ''
}

export async function guardarDireccionFiscalEmisor(autonomoId: string, direccionFiscal: string) {
  const { data } = await apiClient.put<{ direccionFiscal: string }>(
    `/autonomos/${autonomoId}/direccion-fiscal`, { direccionFiscal })
  return data.direccionFiscal
}
