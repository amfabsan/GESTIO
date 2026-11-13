import { apiClient } from '../../services/apiClient'

export type ClasificacionContacto = 'CLIENTE' | 'PROVEEDOR' | 'AMBOS'

export type Contacto = {
  id: string
  autonomoId: string
  clasificacion: ClasificacionContacto
  nombre: string
  nif: string
  email: string | null
  telefono: string | null
  direccion: string | null
  activo: boolean
}

export type GuardarContacto = {
  clasificacion: ClasificacionContacto
  nombre: string
  nif: string
  email: string | null
  telefono: string | null
  direccion: string | null
}

const ruta = (autonomoId: string) => `/autonomos/${autonomoId}/contactos`

export async function listarContactos(autonomoId: string) {
  const { data } = await apiClient.get<Contacto[]>(ruta(autonomoId))
  return data
}

export async function crearContacto(autonomoId: string, contacto: GuardarContacto) {
  const { data } = await apiClient.post<Contacto>(ruta(autonomoId), contacto)
  return data
}

export async function editarContacto(autonomoId: string, id: string, contacto: GuardarContacto) {
  const { data } = await apiClient.put<Contacto>(`${ruta(autonomoId)}/${id}`, contacto)
  return data
}

export async function cambiarEstadoContacto(autonomoId: string, id: string, activo: boolean) {
  const accion = activo ? 'activar' : 'desactivar'
  const { data } = await apiClient.post<Contacto>(`${ruta(autonomoId)}/${id}/${accion}`)
  return data
}

export async function eliminarContacto(autonomoId: string, id: string) {
  await apiClient.delete(`${ruta(autonomoId)}/${id}`)
}
