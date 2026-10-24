import { useCallback, useEffect, useState } from 'react'
import type { Documento } from '../../../types/documentos'
import { obtenerDetalleError } from '../../../services/apiClient'
import {
    aprobarDocumento,
    corregirDocumento,
    obtenerDocumentos,
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

            const pendientes = datos.filter(
                (documento) => documento.estado === 'PENDIENTE_VALIDACION',
            )

            setDocumentos(pendientes)
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

    const corregir = useCallback(
        async (id: string, cambios: CambioDocumento[]) => {
            setProcesando(true)
            setError(null)

            try {
                const actualizado = await corregirDocumento(id, cambios)

                if (actualizado) {
                    setDocumentos((actuales) =>
                        actuales.map((documento) =>
                            documento.id === id ? actualizado : documento,
                        ),
                    )
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
            }
        },
        [],
    )

    const aprobar = useCallback(async (id: string) => {
        setProcesando(true)
        setError(null)

        try {
            const actualizado = await aprobarDocumento(id)

            setDocumentos((actuales) =>
                actuales.filter((documento) => documento.id !== id),
            )

            return actualizado
        } catch (excepcion) {
            setError(
                obtenerDetalleError(excepcion) ??
                'No se pudo aprobar el documento.',
            )
            return null
        } finally {
            setProcesando(false)
        }
    }, [])

    return {
        documentos,
        cargando,
        error,
        procesando,
        cargar,
        corregir,
        aprobar,
    }
}