import { useCallback, useEffect, useRef, useState } from 'react'
import { obtenerDetalleError } from '../../../services/apiClient'
import { desvincularWhatsapp, iniciarVerificacionWhatsapp, obtenerNumeroWhatsapp, type NumeroWhatsapp } from '../whatsappApi'

// Mientras hay un código pendiente se consulta cada pocos segundos: la verificación ocurre en el móvil, no en la web.
const INTERVALO_SONDEO_MS = 3000

// Se usa dentro del modal, que solo se monta mientras está abierto: carga al montarse y deja de sondear al cerrarse.
export function useNumeroWhatsapp() {
    const [estado, setEstado] = useState<NumeroWhatsapp | null>(null)
    const [cargando, setCargando] = useState(true)
    const [enviando, setEnviando] = useState(false)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        const control = new AbortController()
        obtenerNumeroWhatsapp(control.signal)
            .then(setEstado)
            .catch((excepcion: unknown) => {
                if (!control.signal.aborted) setError(obtenerDetalleError(excepcion) ?? 'No se ha podido consultar tu número de WhatsApp.')
            })
            .finally(() => { if (!control.signal.aborted) setCargando(false) })
        return () => control.abort()
    }, [])

    // Cada acción del usuario invalida las consultas del sondeo que sigan en vuelo: una respuesta vieja no puede pisar
    // el estado nuevo (p. ej. el código anterior tras pedir otro).
    const version = useRef(0)

    const codigoPendiente = estado?.verificacionPendiente?.codigo
    useEffect(() => {
        if (!codigoPendiente) return
        const control = new AbortController()
        const temporizador = window.setInterval(() => {
            const pedida = version.current
            obtenerNumeroWhatsapp(control.signal)
                .then((nuevo) => { if (pedida === version.current) setEstado(nuevo) })
                .catch(() => { /* se reintenta en el siguiente ciclo */ })
        }, INTERVALO_SONDEO_MS)
        return () => {
            window.clearInterval(temporizador)
            control.abort()
        }
    }, [codigoPendiente])

    const ejecutar = useCallback(async (accion: () => Promise<NumeroWhatsapp>, mensaje: string) => {
        version.current++
        setEnviando(true)
        setError(null)
        try {
            setEstado(await accion())
            return true
        } catch (excepcion) {
            setError(obtenerDetalleError(excepcion) ?? mensaje)
            return false
        } finally {
            setEnviando(false)
        }
    }, [])

    const vincular = useCallback((telefono: string) =>
        ejecutar(() => iniciarVerificacionWhatsapp(telefono), 'No se ha podido generar el código. Inténtalo de nuevo.'), [ejecutar])

    const desvincular = useCallback(() =>
        ejecutar(desvincularWhatsapp, 'No se ha podido desvincular el número. Inténtalo de nuevo.'), [ejecutar])

    return { estado, cargando, enviando, error, vincular, desvincular }
}
