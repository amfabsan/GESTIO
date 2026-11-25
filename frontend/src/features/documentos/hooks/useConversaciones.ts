import { useEffect, useState } from 'react'
import { EVENTO_CONVERSACIONES, listarConversaciones, type ConversacionDocumento } from '../mensajesApi'

export function useConversaciones(usuarioId?: string) {
    const [consulta, setConsulta] = useState<{ usuarioId: string; datos: ConversacionDocumento[]; error?: string }>()
    useEffect(() => {
        if (!usuarioId) return
        let controller: AbortController | undefined
        async function actualizar() {
            controller?.abort()
            const solicitud = new AbortController()
            controller = solicitud
            try {
                const datos = await listarConversaciones(solicitud.signal)
                if (!solicitud.signal.aborted && usuarioId) setConsulta({ usuarioId, datos })
            } catch {
                if (!solicitud.signal.aborted && usuarioId) {
                    setConsulta({ usuarioId, datos: [], error: 'No se pudieron cargar las conversaciones.' })
                }
            }
        }

        void actualizar()
        const intervalo = window.setInterval(actualizar, 10000)
        window.addEventListener(EVENTO_CONVERSACIONES, actualizar)
        return () => {
            controller?.abort()
            window.clearInterval(intervalo)
            window.removeEventListener(EVENTO_CONVERSACIONES, actualizar)
        }
    }, [usuarioId])
    return consulta?.usuarioId === usuarioId ? consulta : undefined
}
