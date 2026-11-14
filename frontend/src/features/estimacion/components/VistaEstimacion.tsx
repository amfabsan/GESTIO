import { AlertCircle, CheckCircle2, Clock, DollarSign } from 'lucide-react'
import type { EstimacionResponse, ElementoPorRevisar } from '../../../types/estimacion'
import { formatearEuros } from '../../../utils/formato'

interface VistaEstimacionProps {
  estimacion: EstimacionResponse | null
  cargando: boolean
  error: string | null
  procesando: boolean
  esGestor: boolean
  onActualizarInclusion: (documentoId: string, incluida: boolean) => Promise<boolean>
}

export function VistaEstimacion({
  estimacion,
  cargando,
  error,
  procesando,
  esGestor,
  onActualizarInclusion,
}: VistaEstimacionProps) {
  if (cargando) {
    return <p role="status">Calculando estimación…</p>
  }

  if (error) {
    return (
      <div
        role="alert"
        className="rounded-xl border border-red-300 bg-red-50 p-4 text-red-800 flex gap-2"
      >
        <AlertCircle className="size-5 flex-shrink-0 mt-0.5" />
        <p>{error}</p>
      </div>
    )
  }

  if (!estimacion) {
    return <p>No se ha cargado la estimación.</p>
  }

  const { disponible, motivoNoDisponible, ivaIngresos, ivaGastos, diferencia, fechaCalculoUtc, documentosIncluidos, elementosPorRevisar } =
    estimacion

  if (!disponible) {
    return (
      <div className="rounded-xl border border-amber-300 bg-amber-50 p-6">
        <div className="flex gap-3">
          <AlertCircle className="size-6 text-amber-600 flex-shrink-0 mt-0.5" />
          <div>
            <h3 className="font-semibold text-amber-900">Estimación no disponible</h3>
            <p className="mt-1 text-sm text-amber-800">{motivoNoDisponible}</p>
          </div>
        </div>
      </div>
    )
  }

  const fechaFormato = new Date(fechaCalculoUtc).toLocaleString('es-ES', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })

  return (
    <div className="space-y-8">
      {/* Resumen de importes */}
      <section className="rounded-xl border border-[#dfdbd3] bg-white p-6">
        <div className="flex items-center justify-between gap-4 border-b border-[#ece9e3] pb-4 mb-6">
          <h2 className="text-lg font-bold text-[#1b2a4a]">Estimación de IVA</h2>
          <div className="flex items-center gap-2 text-sm text-[#6b6656]">
            <Clock className="size-4" />
            {fechaFormato}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
          {/* IVA de ingresos */}
          <div className="flex flex-col items-start gap-2">
            <label className="text-sm font-semibold text-[#6b6656]">IVA de ingresos incluidos</label>
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-bold text-[#247344]">{formatearEuros(ivaIngresos)}</span>
            </div>
          </div>

          {/* IVA de gastos */}
          <div className="flex flex-col items-start gap-2">
            <label className="text-sm font-semibold text-[#6b6656]">IVA de gastos incluidos</label>
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-bold text-[#c62828]">{formatearEuros(ivaGastos)}</span>
            </div>
          </div>

          {/* Diferencia orientativa */}
          <div className="flex flex-col items-start gap-2">
            <label className="text-sm font-semibold text-[#6b6656]">Diferencia orientativa</label>
            <div
              className={`text-3xl font-bold ${diferencia >= 0 ? 'text-[#247344]' : 'text-[#c62828]'}`}
            >
              {formatearEuros(diferencia)}
            </div>
          </div>
        </div>

        {diferencia < 0 && (
          <div className="mt-6 rounded-lg bg-[#fef3e0] p-3 text-sm text-[#a95500] border border-[#f5d4a0]">
            <strong>Nota:</strong> Saldo negativo orientativo. Esta cifra no afirma la existencia de una devolución.
          </div>
        )}

        <p className="mt-6 text-xs text-[#9a8f84]">
          <strong>Advertencia:</strong> Esta es una estimación orientativa calculada a partir de documentos validados.
          No es el resultado de una declaración tributaria ni debe utilizarse como cifra definitiva.
        </p>
      </section>

      {/* Documentos incluidos */}
      {documentosIncluidos.length > 0 && (
        <section className="rounded-xl border border-[#dfdbd3] bg-white overflow-hidden">
          <div className="border-b border-[#ece9e3] bg-[#f9f8f6] px-6 py-4">
            <h3 className="font-bold text-[#1b2a4a]">Documentos incluidos ({documentosIncluidos.length})</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b border-[#ece9e3] bg-[#faf9f7]">
                <tr>
                  <th className="px-6 py-3 text-left font-semibold text-[#6b6656]">Tipo</th>
                  <th className="px-6 py-3 text-left font-semibold text-[#6b6656]">Emisor / Cliente</th>
                  <th className="px-6 py-3 text-left font-semibold text-[#6b6656]">NIF</th>
                  <th className="px-6 py-3 text-left font-semibold text-[#6b6656]">Factura</th>
                  <th className="px-6 py-3 text-right font-semibold text-[#6b6656]">IVA</th>
                </tr>
              </thead>
              <tbody>
                {documentosIncluidos.map((doc, idx: number) => (
                  <tr
                    key={doc.documentoId}
                    className={idx % 2 === 0 ? 'bg-white' : 'bg-[#faf9f7]'}
                  >
                    <td className="px-6 py-3">
                      <span
                        className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold ${
                          doc.tipo === 'INGRESO'
                            ? 'bg-[#e3f6ea] text-[#008b45]'
                            : 'bg-[#fff0df] text-[#c86b1c]'
                        }`}
                      >
                        <CheckCircle2 className="size-3" />
                        {doc.tipo}
                      </span>
                    </td>
                    <td className="px-6 py-3 text-[#1b2a4a]">{doc.emisor || '—'}</td>
                    <td className="px-6 py-3 font-mono text-[#6b6656]">{doc.nif || '—'}</td>
                    <td className="px-6 py-3 text-[#6b6656]">{doc.numeroFactura || '—'}</td>
                    <td className="px-6 py-3 text-right font-semibold text-[#1b2a4a]">
                      {formatearEuros(doc.ivaIncluido)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Elementos por revisar */}
      {elementosPorRevisar.length > 0 && (
        <section className="rounded-xl border border-amber-300 bg-amber-50">
          <div className="border-b border-amber-200 bg-amber-100 px-6 py-4">
            <h3 className="font-bold text-amber-900 flex items-center gap-2">
              <AlertCircle className="size-5" />
              Elementos pendientes de revisión ({elementosPorRevisar.length})
            </h3>
          </div>
          <div className="divide-y divide-amber-200">
            {elementosPorRevisar.map((elemento: ElementoPorRevisar) => (
              <div key={elemento.documentoId} className="px-6 py-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1">
                    <p className="font-semibold text-amber-900">{elemento.motivo}</p>
                    <p className="mt-1 text-sm text-amber-800">
                      Severidad: <strong>{elemento.severidad}</strong>
                    </p>
                  </div>
                  {esGestor && (
                    <button
                      type="button"
                      disabled={procesando}
                      className="whitespace-nowrap rounded-lg bg-amber-900 px-4 py-2 text-sm font-semibold text-white transition hover:bg-amber-800 disabled:opacity-50"
                    >
                      Ir a validación
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Vacío: sin documentos incluidos ni por revisar */}
      {documentosIncluidos.length === 0 && elementosPorRevisar.length === 0 && (
        <div className="rounded-xl border border-[#dfdbd3] bg-white p-8 text-center">
          <DollarSign className="mx-auto size-12 text-[#c9bbb4] mb-3" />
          <p className="text-[#6b6656]">No hay documentos para este trimestre.</p>
        </div>
      )}
    </div>
  )
}
