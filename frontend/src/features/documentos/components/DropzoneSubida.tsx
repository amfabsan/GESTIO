import { useEffect, useRef, useState, type DragEvent } from 'react'
import { isAxiosError } from 'axios'
import type { ConfiguracionSubida, Documento } from '../../../types/documentos'
import { obtenerConfiguracionSubida, subirDocumento } from '../documentosApi'
import { formatearTamano, validarArchivo } from '../validarArchivo'

type DropzoneSubidaProps = {
  onSubido: (documento: Documento) => void
}

export function DropzoneSubida({ onSubido }: DropzoneSubidaProps) {
  const [configuracion, setConfiguracion] = useState<ConfiguracionSubida | null>(null)
  const [subiendo, setSubiendo] = useState(false)
  const [arrastrando, setArrastrando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exito, setExito] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    obtenerConfiguracionSubida()
      .then(setConfiguracion)
      .catch(() => setError('No se ha podido cargar la configuración de subida.'))
  }, [])

  async function procesarArchivo(archivo: File) {
    if (!configuracion) return

    setError(null)
    setExito(null)

    const errorValidacion = await validarArchivo(archivo, configuracion.tamanoMaximoBytes)
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
      const detalle = isAxiosError<{ detail?: string }>(excepcion) ? excepcion.response?.data?.detail : undefined
      setError(detalle ?? 'No se ha podido subir el documento. Inténtalo de nuevo.')
    } finally {
      setSubiendo(false)
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  function manejarDrop(evento: DragEvent<HTMLLabelElement>) {
    evento.preventDefault()
    setArrastrando(false)
    const archivo = evento.dataTransfer.files[0]
    if (archivo) void procesarArchivo(archivo)
  }

  const deshabilitado = !configuracion || subiendo

  return (
    <section className="space-y-3" aria-label="Subir documento">
      <label
        htmlFor="archivo-documento"
        onDragOver={(evento) => {
          evento.preventDefault()
          setArrastrando(true)
        }}
        onDragLeave={() => setArrastrando(false)}
        onDrop={manejarDrop}
        className={`flex cursor-pointer flex-col items-center gap-2 rounded-xl border-2 border-dashed p-8 text-center transition ${
          arrastrando ? 'border-[#d96c13] bg-[#fff0df]' : 'border-slate-300 bg-white hover:border-slate-400'
        } ${deshabilitado ? 'pointer-events-none opacity-60' : ''}`}
      >
        <span className="font-semibold text-slate-900">
          {subiendo ? 'Subiendo...' : 'Arrastra aquí tu factura o ticket, o haz clic para seleccionarlo'}
        </span>
        {configuracion && (
          <span className="text-sm text-slate-500">PDF, JPG o PNG · Máximo {formatearTamano(configuracion.tamanoMaximoBytes)}</span>
        )}
      </label>
      <input
        id="archivo-documento"
        ref={inputRef}
        type="file"
        className="sr-only"
        accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
        disabled={deshabilitado}
        onChange={(evento) => {
          const archivo = evento.target.files?.[0]
          if (archivo) void procesarArchivo(archivo)
        }}
      />
      {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      {exito && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">{exito}</p>}
    </section>
  )
}