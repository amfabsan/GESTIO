import { useCallback, useEffect, useState } from 'react'
import type { EstimacionResponse } from '../../../types/estimacion'
import { obtenerEstimación, actualizarInclusionGasto } from '../estimacionApi'
import { obtenerDetalleError } from '../../../services/apiClient'

interface EstadoEstimacion {
    estimacion: EstimacionResponse | null
    cargando: boolean
    error: string | null
    procesando: boolean
}

export function useEstimacion(autonomoId: string | undefined, ejercicio: number, trimestre: number) {
    const [estado, setEstado] = useState<EstadoEstimacion>({
        estimacion: null,
        cargando: true,
        error: null,
        procesando: false,
    })

    const cargar = useCallback(
        async (signal?: AbortSignal) => {
            if (!autonomoId) {
                setEstado((s) => ({ ...s, cargando: false }))
                return
            }

            setEstado((s) => ({ ...s, cargando: true, error: null }))

            try {
                const estimacion = await obtenerEstimación(autonomoId, ejercicio, trimestre, signal)
                if (!signal?.aborted) {
                    setEstado((s) => ({ ...s, estimacion, cargando: false }))
                }
            } catch (excepcion) {
                if (!signal?.aborted) {
                    setEstado((s) => ({
                        ...s,
                        error: obtenerDetalleError(excepcion) ?? 'No se pudo cargar la estimación.',
                        cargando: false,
                    }))
                }
            }
        },
        [autonomoId, ejercicio, trimestre]
    )

    const refrescar = useCallback(async () => {
        if (!autonomoId) return
        const estimacion = await obtenerEstimación(autonomoId, ejercicio, trimestre)
        setEstado((s) => ({ ...s, estimacion }))
    }, [autonomoId, ejercicio, trimestre])

    const actualizarInclusion = useCallback(
        async (documentoId: string, incluida: boolean) => {
            setEstado((s) => ({ ...s, procesando: true, error: null }))

            try {
                await actualizarInclusionGasto(documentoId, incluida)
                await refrescar()
                setEstado((s) => ({ ...s, procesando: false }))
                return true
            } catch (excepcion) {
                setEstado((s) => ({
                    ...s,
                    error: obtenerDetalleError(excepcion) ?? 'No se pudo actualizar la inclusión.',
                    procesando: false,
                }))
                return false
            }
        },
        [refrescar]
    )

    useEffect(() => {
        const controller = new AbortController()
        const cargarAsync = async () => {
            await cargar(controller.signal)
        }
        void cargarAsync()

        return () => {
            controller.abort()
        }
    }, [cargar])

    return {
        estimacion: estado.estimacion,
        cargando: estado.cargando,
        error: estado.error,
        procesando: estado.procesando,
        refrescar,
        actualizarInclusion,
    }
}