import { CheckCircle2, X } from 'lucide-react'
import { btnPrimario, btnSecundario } from '../../components/ui/estilos'
import { formatearEuros, formatearFecha } from '../../utils/formato'
import type { Factura } from './facturacionApi'

type Modo = 'revision' | 'emitida' | 'consulta'

type Props = {
  factura: Factura
  modo: Modo
  cargando?: boolean
  error?: string | null
  onVolver?: () => void
  onConfirmar?: () => void
  onCerrar: () => void
  onEditar?: () => void
  onConsultar?: () => void
}

export function FacturaDetalleModal({ factura, modo, cargando = false, error, onVolver, onConfirmar, onCerrar, onEditar, onConsultar }: Props) {
  const emitida = factura.estado === 'EMITIDA'
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-[#101827]/55 p-0 sm:items-center sm:p-4" onMouseDown={evento => { if (evento.target === evento.currentTarget && !cargando) onCerrar() }}>
      <section role="dialog" aria-modal="true" aria-labelledby="titulo-detalle-factura" className="max-h-[95vh] w-full overflow-y-auto rounded-t-2xl bg-[#fbfaf7] p-5 shadow-2xl sm:max-w-3xl sm:rounded-2xl sm:p-7">
        <header className="flex items-start justify-between gap-4">
          <div>
            {modo === 'emitida' && <CheckCircle2 className="mb-2 text-green-700" size={28} aria-hidden="true" />}
            <h2 id="titulo-detalle-factura" className="font-serif text-2xl font-bold text-[#1d2b4f]">
              {modo === 'revision' ? 'Comprobación de factura' : modo === 'emitida' ? `Factura emitida · ${factura.numeroVisible}` : emitida ? factura.numeroVisible : 'Vista previa del borrador'}
            </h2>
            {modo === 'emitida' && <p className="mt-1 text-sm text-green-800">La factura ya no se puede editar.</p>}
          </div>
          <button type="button" aria-label="Cerrar" className="rounded-lg p-2 text-[#6b6656] hover:bg-[#efede6]" onClick={onCerrar}><X size={20} /></button>
        </header>

        {error && <p role="alert" className="mt-4 rounded-lg border border-red-300 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
        {modo === 'emitida' && <p className="mt-5 rounded-xl bg-green-50 p-5 text-sm text-green-900">La factura <strong>{factura.numeroVisible}</strong> se ha emitido correctamente y ya no se puede editar.</p>}
        {modo !== 'emitida' && <>
        <div className="mt-5 grid gap-4 rounded-xl bg-white p-4 sm:grid-cols-2">
          <div><p className="text-xs font-semibold uppercase tracking-wide text-[#777265]">Emisor</p><p className="mt-1 font-semibold">{factura.emisorNombre || 'Datos del emisor'}</p><p className="text-sm text-[#6b6656]">NIF {factura.emisorNif || '—'}</p>{factura.emisorDireccion && <p className="text-sm text-[#6b6656]">{factura.emisorDireccion}</p>}</div>
          <div><p className="text-xs font-semibold uppercase tracking-wide text-[#777265]">Cliente</p><p className="mt-1 font-semibold">{factura.clienteNombre || 'Cliente sin seleccionar'}</p><p className="text-sm text-[#6b6656]">NIF {factura.clienteNif || '—'}</p><p className="text-sm text-[#6b6656]">{factura.clienteDireccion || 'Dirección fiscal sin indicar'}</p></div>
        </div>
        <div className="mt-4 flex flex-wrap gap-x-6 gap-y-2 rounded-xl border border-[#e5e0d6] p-4 text-sm">
          <p><span className="text-[#777265]">Serie:</span> <strong>{factura.serie}</strong></p>
          <p><span className="text-[#777265]">Número:</span> <strong>{factura.numeroVisible || 'Al emitir'}</strong></p>
          <p><span className="text-[#777265]">Fecha:</span> <strong>{formatearFecha(factura.fecha)}</strong></p>
        </div>
        <div className="mt-5 overflow-x-auto rounded-xl border border-[#e5e0d6] bg-white">
          <table className="w-full min-w-[560px] text-left text-sm">
            <thead className="bg-[#f5f3ef] text-xs uppercase text-[#6b6656]"><tr><th className="px-4 py-3">Descripción</th><th className="px-4 py-3 text-right">Cantidad</th><th className="px-4 py-3 text-right">Precio unit.</th><th className="px-4 py-3 text-right">IVA</th><th className="px-4 py-3 text-right">Base</th></tr></thead>
            <tbody>{factura.lineas.map(linea => <tr key={linea.id} className="border-t border-[#eeeae2]"><td className="px-4 py-3">{linea.descripcion || 'Sin descripción'}</td><td className="px-4 py-3 text-right">{linea.cantidad ?? '—'}</td><td className="px-4 py-3 text-right">{linea.precioUnitario === null ? '—' : formatearEuros(linea.precioUnitario)}</td><td className="px-4 py-3 text-right">{linea.ivaPorcentaje === null ? '—' : `${linea.ivaPorcentaje} %`}</td><td className="px-4 py-3 text-right">{linea.base === null ? '—' : formatearEuros(linea.base)}</td></tr>)}</tbody>
          </table>
        </div>
        <section className="mt-5 ml-auto max-w-md rounded-xl bg-white p-4" aria-label="Desglose fiscal">
          <h3 className="mb-3 text-sm font-bold uppercase tracking-wide text-[#6b6656]">Desglose</h3>
          <div className="space-y-2 text-sm">
            {(factura.desgloseIva ?? []).map(grupo => <div key={grupo.tipo} className="flex justify-between gap-3"><span>Base al {grupo.tipo} %</span><span>{formatearEuros(grupo.base)}</span><span>IVA {grupo.tipo} %</span><span>{formatearEuros(grupo.cuota)}</span></div>)}
            <div className="flex justify-between border-t border-[#eeeae2] pt-2"><span>Base imponible</span><span>{factura.baseImponible === null ? '—' : formatearEuros(factura.baseImponible)}</span></div>
            <div className="flex justify-between"><span>Cuotas de IVA</span><span>{factura.cuotaIva === null ? '—' : formatearEuros(factura.cuotaIva)}</span></div>
            {(factura.importeRetencion ?? 0) > 0 && <div className="flex justify-between text-[#9a3412]"><span>Retención IRPF ({factura.retencionPorcentaje} %)</span><span>− {formatearEuros(factura.importeRetencion!)}</span></div>}
            <div className="flex justify-between border-t border-[#eeeae2] pt-2 text-lg font-bold text-[#1d2b4f]"><span>Total</span><span>{factura.total === null ? '—' : formatearEuros(factura.total)}</span></div>
          </div>
        </section>
        </>}
        <div className="mt-6 flex flex-col-reverse justify-end gap-2 sm:flex-row">
          {modo === 'revision' && <>
            <button type="button" className={btnSecundario} disabled={cargando} onClick={onVolver}>Volver a editar</button>
            <button type="button" className={btnPrimario} disabled={cargando} onClick={onConfirmar}>{cargando ? 'Emitiendo…' : 'Confirmar emisión'}</button>
          </>}
          {modo === 'consulta' && factura.estado === 'BORRADOR' && onEditar && <button type="button" className={btnPrimario} onClick={onEditar}>Editar borrador</button>}
          {modo === 'emitida' && <button type="button" className={btnPrimario} onClick={onConsultar}>Consultar factura</button>}
          {modo === 'consulta' && <button type="button" className={btnSecundario} onClick={onCerrar}>Cerrar</button>}
        </div>
      </section>
    </div>
  )
}
