import { apiClient } from '../../services/apiClient'
import type { ConfiguracionSubida, Documento } from '../../types/documentos'

export async function obtenerDocumentos(signal?: AbortSignal) {
    const { data } = await apiClient.get<Documento[]>('/documentos', { signal })
    return data
}

export async function obtenerConfiguracionSubida() {
    const { data } = await apiClient.get<ConfiguracionSubida>('/documentos/configuracion-subida')
    return data
}

export async function subirDocumento(archivo: File) {
    const formulario = new FormData()
    formulario.append('archivo', archivo)

    const { data } = await apiClient.post<Documento>('/documentos', formulario, {
        headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
}

export async function obtenerOriginal(id: string) {
    const { data } = await apiClient.get<Blob>(`/documentos/${id}/archivo`, { responseType: 'blob' })
    return data
}

export async function reintentarDocumento(id: string) {
    const { data } = await apiClient.post<Documento>(`/documentos/${id}/reintentar`)
    return data
}

export async function corregirDocumento(id: string, campos: { campo: string, valor: string | null }[]) {
    const { data } = await apiClient.post<Documento>(`/documentos/${id}/corregir`, { campos })
    return data
}

export async function aprobarDocumento(id: string) {
    const { data } = await apiClient.post<Documento>(`/documentos/${id}/aprobar`)
    return data
}