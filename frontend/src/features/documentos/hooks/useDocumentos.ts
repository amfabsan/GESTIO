import { useCallback, useEffect, useState } from 'react'
import type { Documento } from '../../../types/documentos'
import { abrirEnPestanaNueva } from '../../../utils/archivos'
import { obtenerDocumentos, obtenerOriginal } from '../documentosApi'

export function useDocumentos(perfilId: string | undefined) {
  const [documentos, setDocumentos] = useState<Documento[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [errorOriginal, setErrorOriginal] = useState<string | null>(null)

  useEffect(() => {
    if (!perfilId) return

    const controller = new AbortController()
    obtenerDocumentos(controller.signal)
      .then(setDocumentos)
      .catch(() => {
        if (!controller.signal.aborted) setError('No se han podido cargar los documentos.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })

    return () => controller.abort()
  }, [perfilId])

  const agregarDocumento = useCallback((documento: Documento) => {
    setDocumentos((actuales) => [documento, ...actuales])
  }, [])

  const abrirOriginal = useCallback(async (id: string) => {
    setErrorOriginal(null)
    try {
      abrirEnPestanaNueva(await obtenerOriginal(id))
    } catch {
      setErrorOriginal('No se ha podido abrir el archivo original.')
    }
  }, [])

  return { documentos, cargando, error, errorOriginal, agregarDocumento, abrirOriginal }
}