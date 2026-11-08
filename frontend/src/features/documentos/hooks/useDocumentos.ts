import { useCallback, useEffect, useState } from 'react'
import type { Documento } from '../../../types/documentos'
import { abrirEnPestanaNueva } from '../../../utils/archivos'
import { obtenerDocumentos, obtenerOriginal, reintentarDocumento } from '../documentosApi'
import { useSincronizarDocumentos } from './useSincronizarDocumentos'

export function useDocumentos(perfilId: string | undefined) {
  const [documentos, setDocumentos] = useState<Documento[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [errorOriginal, setErrorOriginal] = useState<string | null>(null)
  const [reintentando, setReintentando] = useState<string | null>(null)
  useSincronizarDocumentos(perfilId, setDocumentos)

  const reintentar = useCallback(async (id: string) => {
    setReintentando(id)
    setErrorOriginal(null)
    try {
      const actualizado = await reintentarDocumento(id)
      setDocumentos(actuales => actuales.map(d => d.id === id ? actualizado : d))
    } catch { setErrorOriginal('No se pudo solicitar el reintento. Actualiza y comprueba tus permisos.') }
    finally {
      setReintentando(null)
      // También restaura el estado real cuando la petición de reintento es rechazada.
      try { setDocumentos(await obtenerDocumentos()) } catch { /* La sincronización vuelve a intentarlo. */ }
    }
  }, [])

  useEffect(() => {
    if (!perfilId) return

    const controller = new AbortController()
    obtenerDocumentos(controller.signal)
      .then((datos) => {
        if (controller.signal.aborted) return
        setDocumentos(datos)
        setError(null)
      })
      .catch(() => {
        if (!controller.signal.aborted) setError('No se han podido cargar los documentos.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })

    return () => controller.abort()
  }, [perfilId])

  const agregarDocumento = useCallback((documento: Documento) => {
    setError(null)
    setDocumentos((actuales) => [documento, ...actuales.filter(d => d.id !== documento.id)])
  }, [])

  const abrirOriginal = useCallback(async (id: string) => {
    setErrorOriginal(null)
    try {
      abrirEnPestanaNueva(await obtenerOriginal(id))
    } catch {
      setErrorOriginal('No se ha podido abrir el archivo original.')
    }
  }, [])

  return { documentos, cargando, error, errorOriginal, agregarDocumento, abrirOriginal, reintentar, reintentando }
}
