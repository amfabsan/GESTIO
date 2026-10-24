import { useCallback, useEffect, useState } from 'react'
import type { EventoHistorial } from '../../../types/documentos'
import type { Pagina } from '../../../types/paginacion'
import { obtenerDetalleError } from '../../../services/apiClient'
import { obtenerHistorialDocumento } from '../documentosApi'

export function useHistorialDocumento(documentoId: string) {
  const [pagina, setPagina] = useState(1)
  const [historial, setHistorial] = useState<Pagina<EventoHistorial> | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    obtenerHistorialDocumento(documentoId, pagina, controller.signal)
      .then((datos) => {
        if (controller.signal.aborted) return
        setHistorial(datos)
        setError(null)
      })
      .catch((excepcion) => {
        if (!controller.signal.aborted) setError(obtenerDetalleError(excepcion) ?? 'No se ha podido cargar el historial.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })

    return () => controller.abort()
  }, [documentoId, pagina])

  const irAPagina = useCallback((destino: number) => {
    setCargando(true)
    setPagina(destino)
  }, [])

  const hayAnteriores = !cargando && !!historial && historial.pagina > 1
  const haySiguientes = !cargando && !!historial && historial.pagina < historial.totalPaginas

  const anteriores = useCallback(() => irAPagina(pagina - 1), [irAPagina, pagina])
  const siguientes = useCallback(() => irAPagina(pagina + 1), [irAPagina, pagina])

  return { historial, cargando, error, hayAnteriores, haySiguientes, anteriores, siguientes }
}
