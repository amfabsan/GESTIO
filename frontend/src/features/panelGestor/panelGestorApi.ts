// src/features/panelGestor/panelGestorApi.ts
import { apiClient } from '../../services/apiClient'
import type { PanelClienteDetalleResponse, PanelGestorResponse } from '../../types/panelGestor'

export interface ParametrosPanelGestor {
    soloRequiereAtencion?: boolean
    ordenarPor?: 'proxima' | 'nombre'
    busqueda?: string
    signal?: AbortSignal
}

export async function obtenerPanelGestor({
    soloRequiereAtencion = false,
    ordenarPor = 'proxima',
    busqueda = '',
    signal,
}: ParametrosPanelGestor = {}): Promise<PanelGestorResponse> {
    const params: Record<string, string | boolean> = {
        soloRequiereAtencion,
        ordenarPor,
    }
    if (busqueda.trim()) {
        params.busqueda = busqueda.trim()
    }

    const { data } = await apiClient.get<PanelGestorResponse>('/gestor/panel', {
        params,
        signal,
    })
    return data
}

export async function obtenerDetalleClienteGestor(
    autonomoId: string,
    signal?: AbortSignal,
): Promise<PanelClienteDetalleResponse> {
    const { data } = await apiClient.get<PanelClienteDetalleResponse>(
        `/gestor/panel/clientes/${autonomoId}`,
        { signal },
    )
    return data
}