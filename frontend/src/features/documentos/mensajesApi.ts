import { apiClient } from '../../services/apiClient'

export interface MensajeDocumento {
    id: string
    autorId: string
    autor: string
    contenido: string
    fecha: string
}

export interface ConversacionDocumento {
    documentoId: string
    nombreArchivo: string
    ultimoMensaje: MensajeDocumento
    sinLeer: number
}

export const EVENTO_CONVERSACIONES = 'gestio:conversaciones'
export const rutaConversacion = (id: string) => `/mensajes/${id}`

export async function listarConversaciones(signal: AbortSignal) {
    const { data } = await apiClient.get<ConversacionDocumento[]>('/documentos/conversaciones', { signal })
    return data
}

export async function obtenerMensajes(id: string, signal: AbortSignal) {
    const { data } = await apiClient.get<MensajeDocumento[]>(`/documentos/${id}/mensajes`, { signal })
    return data
}

export async function enviarMensaje(id: string, contenido: string) {
    const { data } = await apiClient.post<MensajeDocumento>(`/documentos/${id}/mensajes`, { contenido })
    return data
}

export async function marcarMensajesLeidos(id: string, mensajeIds: string[], signal: AbortSignal) {
    await apiClient.post(`/documentos/${id}/mensajes/lectura`, { mensajeIds }, { signal })
    window.dispatchEvent(new Event(EVENTO_CONVERSACIONES))
}
