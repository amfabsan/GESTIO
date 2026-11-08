import { useEffect } from 'react'
import type { Documento } from '../../../types/documentos'
import { obtenerDocumentos } from '../documentosApi'

// Consulta también mientras la subida/reextracción HTTP sigue abierta y descubre ejecuciones
// iniciadas desde otra pestaña. No modifica el flujo de procesamiento de PB-06.
export function useSincronizarDocumentos(perfilId: string | undefined, actualizar: (datos: Documento[]) => void) {
  useEffect(() => {
    if (!perfilId) return
    const controller = new AbortController()
    let temporizador: ReturnType<typeof setTimeout>
    async function consultar() {
      try {
        const datos = await obtenerDocumentos(controller.signal)
        if (!controller.signal.aborted) actualizar(datos)
      } catch { /* Conserva la vista y vuelve a consultar en el siguiente ciclo. */ }
      if (!controller.signal.aborted) temporizador = setTimeout(consultar, 2000)
    }
    temporizador = setTimeout(consultar, 1000)
    return () => { controller.abort(); clearTimeout(temporizador) }
  }, [perfilId, actualizar])
}
