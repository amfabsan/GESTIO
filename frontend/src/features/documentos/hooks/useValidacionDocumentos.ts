import { useCallback, useEffect, useState } from 'react'
import type { Documento } from '../../../types/documentos'
import { obtenerDetalleError } from '../../../services/apiClient'
import {
    aprobarDocumento,
    corregirDocumento,
    enviarNotaDocumento,
    obtenerDocumentos,
    solicitarAclaracion,
} from '../documentosApi'

type CambioDocumento = {
    campo: string
    valor: string | null
}

export function useValidacionDocumentos() {
    const [documentos, setDocumentos] = useState<Documento[]>([])
    const [cargando, setCargando] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [procesando, setProcesando] = useState(false)

    const cargar = useCallback(async (signal?: AbortSignal) => {
        setCargando(true)
        setError(null)

        try {
            const datos = await obtenerDocumentos(signal)

            // Se guardan todos los estados: la pantalla filtra por pestaña
            // y calcula los contadores (Pendientes, En aclaración, Aprobados).
            setDocumentos(datos)
        } catch (excepcion) {
            if (!signal?.aborted) {
                setError(
                    obtenerDetalleError(excepcion) ??
                    'No se pudo cargar la bandeja de validación.',
                )
            }
        } finally {
            if (!signal?.aborted) {
                setCargando(false)
            }
        }
    }, [])

    // Recarga la lista desde el servidor sin mostrar el spinner de pantalla completa.
    // Es la fuente de verdad: deja los contadores correctos pase lo que pase.
    const refrescar = useCallback(async () => {
        try {
            const datos = await obtenerDocumentos()
            setDocumentos(datos)
        } catch {
            // Se mantiene lo que había; el error de la acción ya se muestra aparte.
        }
    }, [])

    useEffect(() => {
        const controller = new AbortController();
        const cargarAsync = async () => {
            await cargar(controller.signal);
        };
        cargarAsync();

        return () => {
            controller.abort();
        };
    }, [cargar]);

    // Al volver a la pestaña del navegador se sincroniza con el servidor.
    useEffect(() => {
        const alVolver = () => {
            if (document.visibilityState === 'visible') {
                void refrescar()
            }
        }

        document.addEventListener('visibilitychange', alVolver)

        return () => document.removeEventListener('visibilitychange', alVolver)
    }, [refrescar])

    const reemplazar = useCallback((id: string, actualizado: Documento) => {
        setDocumentos((actuales) =>
            actuales.map((documento) =>
                documento.id === id ? actualizado : documento,
            ),
        )
    }, [])

    const corregir = useCallback(
        async (id: string, cambios: CambioDocumento[]) => {
            setProcesando(true)
            setError(null)

            try {
                const actualizado = await corregirDocumento(id, cambios)

                if (actualizado) {
                    reemplazar(id, actualizado)
                }

                return actualizado
            } catch (excepcion) {
                setError(
                    obtenerDetalleError(excepcion) ??
                    'No se pudo guardar la corrección.',
                )
                return null
            } finally {
                setProcesando(false)
                void refrescar()
            }
        },
        [reemplazar, refrescar],
    )

    const aprobar = useCallback(async (id: string) => {
        setProcesando(true)
        setError(null)

        try {
            const actualizado = await aprobarDocumento(id)

            // El documento pasa a VALIDADO y sigue en la lista (pestaña Aprobados).
            reemplazar(id, actualizado)

            return actualizado
        } catch (excepcion) {
            setError(
                obtenerDetalleError(excepcion) ??
                'No se pudo aprobar el documento.',
            )
            return null
        } finally {
            setProcesando(false)
            void refrescar()
        }
    }, [reemplazar, refrescar])

    const pedirAclaracion = useCallback(
        async (id: string, pregunta: string) => {
            setProcesando(true)
            setError(null)

            try {
                const actualizado = await solicitarAclaracion(id, pregunta)

                reemplazar(id, actualizado)

                return actualizado
            } catch (excepcion) {
                setError(
                    obtenerDetalleError(excepcion) ??
                    'No se pudo enviar la pregunta al cliente.',
                )
                return null
            } finally {
                setProcesando(false)
                void refrescar()
            }
        },
        [reemplazar, refrescar],
    )

    const enviarNota = useCallback(
        async (id: string, nota: string) => {
            setProcesando(true)
            setError(null)

            try {
                const actualizado = await enviarNotaDocumento(id, nota)

                reemplazar(id, actualizado)

                return actualizado
            } catch (excepcion) {
                setError(
                    obtenerDetalleError(excepcion) ??
                    'No se pudo enviar la nota al cliente.',
                )
                return null
            } finally {
                setProcesando(false)
                void refrescar()
            }
        },
        [reemplazar, refrescar],
    )

    return {
        documentos,
        cargando,
        error,
        procesando,
        cargar,
        refrescar,
        corregir,
        aprobar,
        pedirAclaracion,
        enviarNota,
    }
}