import { useEffect, useState } from 'react'
import { apiClient } from '../services/apiClient'
import type { Documento } from '../types/documentos'

const estadoStyles: Record<string, string> = {
  Procesado: 'bg-emerald-100 text-emerald-800',
  Procesando: 'bg-blue-100 text-blue-800',
  Pendiente: 'bg-amber-100 text-amber-800',
  Error: 'bg-red-100 text-red-800',
}

export function DocumentosPage() {
  const [documentos, setDocumentos] = useState<Documento[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<Documento[]>('/documentos')
      .then((respuesta) => setDocumentos(respuesta.data))
      .catch(() => setError('No se han podido cargar los documentos.'))
      .finally(() => setCargando(false))
  }, [])

  return (
    <main className="mx-auto max-w-6xl space-y-6">
      <header>
        <p className="text-sm font-medium uppercase tracking-wide text-slate-500">Buzón fiscal</p>
        <h1 className="text-3xl font-semibold text-slate-900">Documentos</h1>
        <p className="mt-2 text-slate-600">Consulta los documentos recibidos y su estado de procesamiento.</p>
      </header>

      {cargando && <p className="text-slate-600">Cargando documentos...</p>}
      {error && <p className="rounded-lg bg-red-50 p-4 text-red-700">{error}</p>}

      {!cargando && !error && (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Documento</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Usuario</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Fecha de subida</th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Estado</th>
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
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  )
}
