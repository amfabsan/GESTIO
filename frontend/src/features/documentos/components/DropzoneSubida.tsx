import { useRef, useState, type DragEvent } from 'react'
import type { Documento } from '../../../types/documentos'
import { formatearTamano } from '../../../utils/archivos'
import { useUploadDocumento } from '../hooks/useUploadDocumento'
import { describirTiposPermitidos } from '../validarArchivo'

type DropzoneSubidaProps = {
  onSubido: (documento: Documento) => void
}

export function DropzoneSubida({ onSubido }: DropzoneSubidaProps) {
  const { configuracion, subiendo, error, exito, subir } = useUploadDocumento(onSubido)
  const [arrastrando, setArrastrando] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  async function procesarArchivo(archivo: File) {
    await subir(archivo)
    if (inputRef.current) inputRef.current.value = ''
  }

  function manejarDrop(evento: DragEvent<HTMLLabelElement>) {
    evento.preventDefault()
    setArrastrando(false)
    const archivo = evento.dataTransfer.files[0]
    if (archivo) void procesarArchivo(archivo)
  }

  const deshabilitado = !configuracion || subiendo
  const extensionesAceptadas = configuracion?.tiposPermitidos.map((tipo) => tipo.extension).join(',')

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
          <span className="text-sm text-slate-500">
            {describirTiposPermitidos(configuracion)} · Máximo {formatearTamano(configuracion.tamanoMaximoBytes)}
          </span>
        )}
      </label>
      <input
        id="archivo-documento"
        ref={inputRef}
        type="file"
        className="sr-only"
        accept={extensionesAceptadas}
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