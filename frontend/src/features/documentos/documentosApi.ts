import { apiClient } from '../../services/apiClient'
import type { CategoriaDocumento, ConfiguracionSubida, Documento, EventoHistorial } from '../../types/documentos'
import type { Pagina } from '../../types/paginacion'

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

export async function obtenerHistorialDocumento(id: string, pagina: number, signal?: AbortSignal) {
    const { data } = await apiClient.get<Pagina<EventoHistorial>>(`/documentos/${id}/historial`, { params: { pagina }, signal })
    return data
}

export async function aprobarDocumento(id: string) {
    const { data } = await apiClient.post<Documento>(`/documentos/${id}/aprobar`)
    return data
}

export async function cambiarCategoriaDocumento(id: string, categoria: CategoriaDocumento) {
    const { data } = await apiClient.patch<Documento>(`/documentos/${id}/categoria`, { nuevaCategoria: categoria })
    return data
}

export async function solicitarAclaracion(id: string, pregunta: string) {
        const { data } = await apiClient.post<Documento>(`/documentos/${id}/aclaracion`, { pregunta })
            return data
            }

export async function enviarNotaDocumento(id: string, nota: string) {
            const { data } = await apiClient.post<Documento>(`/documentos/${id}/nota`, { nota })
                return data
                }