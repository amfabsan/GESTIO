import { apiClient } from '../../services/apiClient'

export interface ObligacionFiscal {
  id: string
  autonomoId: string
  modelo: string
  nombre: string
  ejercicio: number
  periodo: string
  fechaPrevista: string
  estado: 'PENDIENTE' | 'INCOMPLETA' | 'LISTA' | 'PRESENTADA'
  obligatoriosConfirmados: number
  totalObligatorios: number
}

export interface CambioChecklist {
  id: string
  usuarioId: string
  usuario: string
  fecha: string
  estadoAnterior: string
  estadoNuevo: string
  origen: 'AUTONOMO' | 'GESTOR'
}

export interface ElementoChecklist {
  id: string
  codigoPlantilla: string
  descripcion: string
  obligatorio: boolean
  estado: 'PENDIENTE' | 'APORTADO' | 'CONFIRMADO'
  origen: 'AUTONOMO' | 'GESTOR' | null
  usuarioUltimoCambioId: string | null
  usuarioUltimoCambio: string | null
  fechaUltimoCambio: string | null
  historial: CambioChecklist[]
}

export interface ChecklistObligacion {
  obligacion: ObligacionFiscal
  avisoFechas: string
  avisoPlantilla: string
  obligatoriosConfirmados: number
  totalObligatorios: number
  elementos: ElementoChecklist[]
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

export async function obtenerChecklist(autonomoId: string, obligacionId: string, signal?: AbortSignal) {
  return (await apiClient.get<ChecklistObligacion>(`${ruta(autonomoId)}/${obligacionId}/checklist`, { signal })).data
}

export async function marcarElementoAportado(autonomoId: string, obligacionId: string, elementoId: string, aportado: boolean) {
  return (await apiClient.patch<ChecklistObligacion>(
    `${ruta(autonomoId)}/${obligacionId}/checklist/elementos/${elementoId}/marca`, { aportado })).data
}

export async function revisarElementoChecklist(autonomoId: string, obligacionId: string, elementoId: string, confirmar: boolean) {
  return (await apiClient.patch<ChecklistObligacion>(
    `${ruta(autonomoId)}/${obligacionId}/checklist/elementos/${elementoId}/revision`, { confirmar })).data
}
