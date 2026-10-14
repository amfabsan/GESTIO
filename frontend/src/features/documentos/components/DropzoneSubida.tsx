import { useRef, useState, type DragEvent } from 'react'
import { CheckCircle2, Loader2, Upload } from 'lucide-react'
import { btnPrimario } from '../../../components/ui/estilos'
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

  const deshabilitado = !configuracion || subiendo
  const extensionesAceptadas = configuracion?.tiposPermitidos.map((tipo) => tipo.extension).join(',')

  async function procesarArchivo(archivo: File) {
    await subir(archivo)
    if (inputRef.current) inputRef.current.value = ''
  }

  function manejarDrop(evento: DragEvent<HTMLDivElement>) {
    evento.preventDefault()
    setArrastrando(false)
    if (deshabilitado) return
    const archivo = evento.dataTransfer.files[0]
    if (archivo) void procesarArchivo(archivo)
  }

  return (
    <section className="space-y-3" aria-label="Subir documento">
      <div
        onDragOver={(evento) => {
          evento.preventDefault()
          if (!deshabilitado) setArrastrando(true)
        }}
        onDragLeave={() => setArrastrando(false)}
        onDrop={manejarDrop}
        onClick={() => {
          if (!deshabilitado) inputRef.current?.click()
        }}
        className={`rounded-2xl border-2 border-dashed p-8 text-center transition-all sm:p-10 ${
          arrastrando ? 'border-[#c8651a] bg-amber-50' : 'border-[#d6d2c8] bg-white hover:border-[#1d2b4f]'
        } ${deshabilitado ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}
      >
        <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-[#ede9e0]">
          {subiendo
            ? <Loader2 aria-hidden="true" className="size-6 animate-spin text-[#1d2b4f]" />
            : <Upload aria-hidden="true" className="size-6 text-[#1d2b4f]" />}
        </div>
        <p className="font-semibold text-[#1d2b4f]">
          {subiendo ? 'Subiendo…' : 'Arrastra tu archivo aquí o haz clic para seleccionarlo'}
        </p>
        {configuracion && (
          <p className="mt-1 text-sm text-[#6b6656]">
            {describirTiposPermitidos(configuracion)} · Facturas, tickets, recibos · Máximo {formatearTamano(configuracion.tamanoMaximoBytes)}
          </p>
        )}
        <div className="mt-4 flex justify-center">
          <button type="button" className={btnPrimario} disabled={deshabilitado}>
            <Upload aria-hidden="true" className="size-4" /> Seleccionar archivo
          </button>
        </div>
      </div>

      <input
        ref={inputRef}
        type="file"
        className="hidden"
        accept={extensionesAceptadas}
        disabled={deshabilitado}
        onChange={(evento) => {
          const archivo = evento.target.files?.[0]
          if (archivo) void procesarArchivo(archivo)
        }}
      />

      {error && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      {exito && (
        <p role="status" className="flex items-center gap-2 rounded-xl border border-green-200 bg-green-50 p-3 text-sm text-green-800">
          <CheckCircle2 aria-hidden="true" className="size-4 shrink-0" /> {exito}
        </p>
      )}
    </section>
  )
}