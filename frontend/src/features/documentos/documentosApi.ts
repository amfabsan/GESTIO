import { apiClient } from '../../services/apiClient'
import type { ConfiguracionSubida, Documento } from '../../types/documentos'

export async function obtenerConfiguracionSubida() {
  const { data } = await apiClient.get<ConfiguracionSubida>('/documentos/configuracion-subida')
  return data
}

export async function subirDocumento(archivo: File) {
  const formulario = new FormData()
  formulario.append('archivo', archivo)

  // Sin esta cabecera axios serializaría el FormData como JSON por el Content-Type por defecto del cliente.
  const { data } = await apiClient.post<Documento>('/documentos', formulario, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export async function abrirOriginalDocumento(id: string) {
  const { data } = await apiClient.get<Blob>(`/documentos/${id}/archivo`, { responseType: 'blob' })
  const url = URL.createObjectURL(data)
  window.open(url, '_blank', 'noopener')
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}