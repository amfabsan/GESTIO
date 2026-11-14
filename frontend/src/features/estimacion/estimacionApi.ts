import { apiClient } from '../../services/apiClient'
import type { Documento } from '../../types/documentos'
import type { EstimacionResponse } from '../../types/estimacion'

/**
 * Obtiene la estimación trimestral de IVA para un autónomo.
 */
export async function obtenerEstimación(
  autonomoId: string,
  ejercicio: number,
  trimestre: number,
  signal?: AbortSignal
): Promise<EstimacionResponse> {
  const { data } = await apiClient.get<EstimacionResponse>(
    `/autonomos/${autonomoId}/estimacion/${ejercicio}/${trimestre}`,
    { signal }
  )
  return data
}

/**
 * Actualiza la inclusión de un gasto en la estimación de IVA.
 * Solo gestores autorizados pueden cambiar esta marca.
 */
export async function actualizarInclusionGasto(
  documentoId: string,
  incluida: boolean,
  signal?: AbortSignal
): Promise<Documento> {
  const { data } = await apiClient.patch<Documento>(
    `/documentos/${documentoId}/inclusion-estimacion`,
    { incluida },
    { signal }
  )
  return data
}
