import { useEffect, useState } from 'react'
import { obtenerDetalleError } from '../../../services/apiClient'
import type { ConfiguracionSubida, Documento } from '../../../types/documentos'
import { obtenerConfiguracionSubida, subirDocumento } from '../documentosApi'
import { validarArchivo } from '../validarArchivo'

export function useUploadDocumento(onSubido: (documento: Documento) => void) {
  const [configuracion, setConfiguracion] = useState<ConfiguracionSubida | null>(null)
  const [subiendo, setSubiendo] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exito, setExito] = useState<string | null>(null)

  useEffect(() => {
    obtenerConfiguracionSubida()
      .then(setConfiguracion)
      .catch(() => setError('No se ha podido cargar la configuración de subida.'))
  }, [])

  async function subir(archivo: File) {
    if (!configuracion) return

    setError(null)
    setExito(null)

    const errorValidacion = await validarArchivo(archivo, configuracion)
    if (errorValidacion) {
      setError(errorValidacion)
      return
    }

    setSubiendo(true)
    try {
      const documento = await subirDocumento(archivo)
      setExito(`«${documento.nombreArchivo}» se ha subido correctamente.`)
      onSubido(documento)
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido subir el documento. Inténtalo de nuevo.')
    } finally {
      setSubiendo(false)
    }
  }

  return { configuracion, subiendo, error, exito, subir }
}