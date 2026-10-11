import { useEffect, useState } from 'react'
import { apiClient } from '../services/apiClient'
import { useAuth } from '../context/useAuth'
import { DropzoneSubida } from '../features/documentos/components/DropzoneSubida'
import { abrirOriginalDocumento } from '../features/documentos/documentosApi'
import type { Documento } from '../types/documentos'

const estadoStyles: Record<string, string> = {
  SUBIDO: 'bg-slate-100 text-slate-800',
  PROCESANDO: 'bg-blue-100 text-blue-800',
  PENDIENTE_VALIDACION: 'bg-amber-100 text-amber-800',
  VALIDADO: 'bg-emerald-100 text-emerald-800',
  ERROR: 'bg-red-100 text-red-800',
  ACLARACION_SOLICITADA: 'bg-orange-100 text-orange-800',
}

export function DocumentosPage() {
  const { perfilActivo } = useAuth()
  const [documentos, setDocumentos] = useState<Documento[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [errorOriginal, setErrorOriginal] = useState<string | null>(null)

  useEffect(() => {
    if (!perfilActivo) return

    const controller = new AbortController()
    apiClient.get<Documento[]>('/documentos', { signal: controller.signal })
      .then((respuesta) => setDocumentos(respuesta.data))
      .catch(() => {
        if (!controller.signal.aborted) setError('No se han podido cargar los documentos.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })

    return () => controller.abort()
  }, [perfilActivo])

  const verOriginal = (id: string) => {
    setErrorOriginal(null)
    abrirOriginalDocumento(id).catch(() => setErrorOriginal('No se ha podido abrir el archivo original.'))
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-6 py-8">
      <header>
        <p className="text-sm font-medium uppercase tracking-wide text-slate-500">Buzón fiscal</p>
        <h1 className="text-3xl font-semibold text-slate-900">Documentos</h1>
        <p className="mt-2 text-slate-600">Sube tus facturas y tickets y consulta su estado de procesamiento.</p>
      </header>

      <DropzoneSubida onSubido={(documento) => setDocumentos((actuales) => [documento, ...actuales])} />

      {cargando && <p className="text-slate-600">Cargando documentos...</p>}
      {error && <p className="rounded-lg bg-red-50 p-4 text-red-700">{error}</p>}
      {errorOriginal && <p role="alert" className="rounded-lg bg-red-50 p-4 text-red-700">{errorOriginal}</p>}

      {!cargando && !error && (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Documento</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Usuario</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Fecha de subida</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Estado</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Original</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {documentos.map((documento) => (
                <tr key={documento.id}>
                  <td className="whitespace-nowrap px-6 py-4 font-medium text-slate-900">{documento.nombreArchivo}</td>
                  <td className="whitespace-nowrap px-6 py-4 text-slate-600">{documento.usuario}</td>
                  <td className="whitespace-nowrap px-6 py-4 text-slate-600">
                    {new Date(documento.fechaSubida).toLocaleDateString('es-ES')}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    <span className={`rounded-full px-3 py-1 text-xs font-semibold ${estadoStyles[documento.estado] ?? 'bg-slate-100 text-slate-700'}`}>
                      {documento.estado}
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    {documento.tieneOriginal ? (
                      <button className="text-sm font-semibold text-[#1b2a4a] underline-offset-2 hover:underline" type="button" onClick={() => verOriginal(documento.id)}>
                        Ver
                      </button>
                    ) : (
                      <span className="text-sm text-slate-400">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}