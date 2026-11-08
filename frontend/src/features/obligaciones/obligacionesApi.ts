import { apiClient } from '../../services/apiClient'

export interface ObligacionFiscal {
  id: string
  autonomoId: string
  modelo: string
  nombre: string
  ejercicio: number
  periodo: string
  fechaPrevista: string
  estado: 'PENDIENTE' | 'PRESENTADA'
}

export interface CalendarioFiscal {
  ejercicio: number
  nombreAutonomo: string
  perfilConfigurado: boolean
  perfil: { regimenIva: string; regimenIrpf: string } | null
  avisoFechas: string
  obligaciones: ObligacionFiscal[]
}

export interface DetalleObligacion {
  obligacion: ObligacionFiscal
  avisoFechas: string
  historial: { id: string; usuarioId: string; gestor: string; fecha: string; descripcion: string }[]
}

const ruta = (autonomoId: string) => `/autonomos/${autonomoId}/obligaciones-fiscales`

export async function obtenerCalendario(autonomoId: string, ejercicio: number, signal?: AbortSignal) {
  return (await apiClient.get<CalendarioFiscal>(`${ruta(autonomoId)}/${ejercicio}`, { signal })).data
}

export async function generarCalendario(autonomoId: string, ejercicio: number) {
  return (await apiClient.post<CalendarioFiscal>(`${ruta(autonomoId)}/${ejercicio}/generar`)).data
}

export async function obtenerDetalleObligacion(autonomoId: string, id: string, signal?: AbortSignal) {
  return (await apiClient.get<DetalleObligacion>(`${ruta(autonomoId)}/detalle/${id}`, { signal })).data
}
