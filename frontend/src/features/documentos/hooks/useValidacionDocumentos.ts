import { useCallback, useEffect, useState } from 'react'
import type { Documento } from '../../../types/documentos'
import { aprobarDocumento, corregirDocumento, obtenerDocumentos } from '../documentosApi'

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
            setDocumentos(datos.filter(documento => documento.estado === 'PENDIENTE_VALIDACION'))
        } catch {
            setError('No se pudo cargar la bandeja de validación.')
        } finally {
            if (!signal?.aborted) setCargando(false)
        }
    }, [])

    useEffect(() => {
        const controller = new AbortController()
        const cargarAsync = async () => {
            await cargar(controller.signal)
        }
        cargarAsync()
        return () => controller.abort()
    }, [cargar])

    const corregir = useCallback(async (id: string, cambios: { campo: string; valor: string | null }[]) => {
        setProcesando(true)
        setError(null)
        try {
            const actualizado = await corregirDocumento(id, cambios)
            setDocumentos((actuales) =>
                actuales.map(documento => documento.id === id ? actualizado : documento))
            return actualizado
        } catch (ex) {
            setError((ex as Error)?.message ?? 'No se pudo guardar la corrección.')
            return null
        } finally {
            setProcesando(false)
        }
    }, [])

    const aprobar = useCallback(async (id: string) => {
        setProcesando(true)
        setError(null)
        try {
            const actualizado = await aprobarDocumento(id)
            setDocumentos((actuales) => actuales.filter(documento => documento.id !== id))
            return actualizado
        } catch (ex) {
            setError((ex as Error)?.message ?? 'No se pudo aprobar el documento.')
            return null
        } finally {
            setProcesando(false)
        }
    }, [])

    return { documentos, cargando, error, procesando, cargar, corregir, aprobar }
}