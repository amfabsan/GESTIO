import { useState } from 'react'
import { AlertTriangle, FileText } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import { Vacio } from '../../components/ui/Vacio'
import { card, etiqueta } from '../../components/ui/estilos'
import { useAuth } from '../../context/useAuth'
import { DropzoneSubida } from '../../features/documentos/components/DropzoneSubida'
import { EstadoBadge } from '../../features/documentos/components/EstadoBadge'
import { FILTROS_BUZON, cumpleFiltro, tieneAnomaliaAbierta, type FiltroBuzon } from '../../features/documentos/filtrosBuzon'
import { useDocumentos } from '../../features/documentos/hooks/useDocumentos'
import { formatearEuros, formatearFecha } from '../../utils/formato'

export function BuzonPage() {
  const { perfilActivo } = useAuth()
  const { documentos, cargando, error, errorOriginal, agregarDocumento, abrirOriginal } = useDocumentos(perfilActivo?.id)
  const [filtro, setFiltro] = useState<FiltroBuzon>('TODOS')

  const filtrados = documentos.filter((documento) => cumpleFiltro(documento, filtro))
  const contar = (id: FiltroBuzon) => documentos.filter((documento) => cumpleFiltro(documento, id)).length

  return (
    <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f4f0] text-[#1a1916]">
      <div className="mx-auto max-w-6xl space-y-5 px-4 py-6">
        <PageHeader titulo="Buzón de documentos" subtitulo="Sube cualquier factura o ticket: la IA lo lee, lo clasifica y se lo envía a tu gestor" />

        <DropzoneSubida onSubido={agregarDocumento} />

        {error ? (
          <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</p>
        ) : (
          <>
            <div className="flex flex-wrap gap-2">
              {FILTROS_BUZON.map(({ id, texto }) => (
                <button
                  key={id}
                  type="button"
                  aria-pressed={filtro === id}
                  onClick={() => setFiltro(id)}
                  className={`rounded-full px-4 py-1.5 text-sm font-medium transition-colors ${
                    filtro === id ? 'bg-[#1d2b4f] text-white' : 'border border-[#d6d2c8] bg-white text-[#6b6656] hover:border-[#1d2b4f]'
                  }`}
                >
                  {texto} ({contar(id)})
                </button>
              ))}
            </div>

            {errorOriginal && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{errorOriginal}</p>}

            <div className={`${card} overflow-hidden`}>
              {cargando ? (
                <Vacio texto="Cargando documentos…" />
              ) : filtrados.length === 0 ? (
                <Vacio texto="No hay documentos en este estado" />
              ) : (
                <table className="w-full">
                  <thead>
                    <tr className="border-b border-[#ede9e0]">
                      <th className={`${etiqueta} px-5 py-3 text-left`}>Documento</th>
                      <th className={`${etiqueta} hidden px-3 py-3 text-left md:table-cell`}>Categoría IA</th>
                      <th className={`${etiqueta} px-3 py-3 text-right`}>Importe</th>
                      <th className={`${etiqueta} hidden px-3 py-3 text-center sm:table-cell`}>Estado</th>
                      <th className="px-5 py-3"><span className="sr-only">Original</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtrados.map((documento) => (
                      <tr key={documento.id} className="border-b border-[#ede9e0] transition-colors last:border-0 hover:bg-[#f5f4f0]">
                        <td className="px-5 py-3.5">
                          <div className="flex items-center gap-3">
                            <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
                              <FileText aria-hidden="true" className="size-4" />
                            </div>
                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5">
                                <p className="truncate text-sm font-medium">{documento.nombreArchivo}</p>
                                {tieneAnomaliaAbierta(documento) && (
                                  <>
                                    <AlertTriangle aria-hidden="true" className="size-3.5 shrink-0 text-amber-500" />
                                    <span className="sr-only">Tiene anomalías sin resolver</span>
                                  </>
                                )}
                              </div>
                              <p className="text-xs text-[#6b6656]">{formatearFecha(documento.fechaSubida)}</p>
                            </div>
                          </div>
                        </td>
                        <td className="hidden px-3 py-3.5 md:table-cell">
                          <span className="text-sm text-[#3d3a30]">{documento.extraccion?.categoriaSugerida ?? '—'}</span>
                        </td>
                        <td className="px-3 py-3.5 text-right">
                          <span className="text-sm font-semibold text-[#1a1916]">
                            {documento.extraccion?.total != null ? formatearEuros(documento.extraccion.total) : '—'}
                          </span>
                        </td>
                        <td className="hidden px-3 py-3.5 text-center sm:table-cell">
                          <EstadoBadge estado={documento.estado} />
                        </td>
                        <td className="px-5 py-3.5 text-right">
                          {documento.tieneOriginal && (
                            <button
                              type="button"
                              onClick={() => void abrirOriginal(documento.id)}
                              className="inline-flex items-center gap-1 whitespace-nowrap text-xs font-medium text-[#c8651a] hover:underline"
                            >
                              <FileText aria-hidden="true" className="size-3.5" /> Ver original
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}