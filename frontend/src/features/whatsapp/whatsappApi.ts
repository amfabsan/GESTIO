import { apiClient } from '../../services/apiClient'

// UT27: número de WhatsApp del autónomo (GET/POST/DELETE /api/whatsapp/numero). Los teléfonos llegan enmascarados.
export type VerificacionPendiente = {
    telefono: string
    codigo: string
    caducaEn: string
    enlaceCodigo: string | null
    qrCodigoPngBase64: string | null
}

export type NumeroWhatsapp = {
    canalHabilitado: boolean
    numeroCanal: string
    verificado: boolean
    telefonoVerificado: string | null
    fechaVerificacion: string | null
    enlaceCanal: string | null
    qrCanalPngBase64: string | null
    verificacionPendiente: VerificacionPendiente | null
}

export async function obtenerNumeroWhatsapp(signal?: AbortSignal) {
    const { data } = await apiClient.get<NumeroWhatsapp>('/whatsapp/numero', { signal })
    return data
}

export async function iniciarVerificacionWhatsapp(telefono: string) {
    const { data } = await apiClient.post<NumeroWhatsapp>('/whatsapp/numero/verificacion', { telefono })
    return data
}

export async function desvincularWhatsapp() {
    const { data } = await apiClient.delete<NumeroWhatsapp>('/whatsapp/numero')
    return data
}
