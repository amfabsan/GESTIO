import { apiClient } from '../../services/apiClient'

// UT22: registros de facturación encadenados (simulación académica de VeriFactu).
// Solo lectura: la API no ofrece ninguna operación para modificarlos ni eliminarlos.

export type RegistroFacturacion = {
  id: string
  secuencia: number
  tipo: 'ALTA'
  facturaId: string
  clienteNombre: string | null
  idEmisorFactura: string
  numSerieFactura: string
  fechaExpedicionFactura: string
  tipoFactura: string
  cuotaTotal: number
  importeTotal: number
  fechaHoraHusoGenRegistro: string
  generado: string
  anteriorId: string | null
  secuenciaAnterior: number | null
  huellaAnterior: string | null
  huella: string
  representacionCanonica: string
}

export type VerificacionCadena = {
  coherente: boolean
  revisados: number
  totalRegistros: number
  registroIncoherenteId: string | null
  secuenciaIncoherente: number | null
  codigo: string | null
  motivo: string | null
}

const ruta = (autonomoId: string) => `/autonomos/${autonomoId}/registros-facturacion`

export async function listarRegistros(autonomoId: string) {
  const { data } = await apiClient.get<RegistroFacturacion[]>(ruta(autonomoId))
  return data
}

export async function verificarCadena(autonomoId: string) {
  const { data } = await apiClient.get<VerificacionCadena>(`${ruta(autonomoId)}/verificacion`)
  return data
}
