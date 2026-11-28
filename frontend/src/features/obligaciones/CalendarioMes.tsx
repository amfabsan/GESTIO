import type { ObligacionFiscal } from './obligacionesApi'

export function CalendarioMes({ ejercicio, mes, obligaciones, seleccionar, seleccionada }: {
  ejercicio: number; mes: number; obligaciones: ObligacionFiscal[]; seleccionar: (id: string) => void; seleccionada?: string
}) {
  const fecha = new Date(ejercicio, mes, 1)
  const inicio = (fecha.getDay() + 6) % 7
  const dias = new Date(ejercicio, mes + 1, 0).getDate()
  return <div role="region" aria-label="Calendario mensual" className="p-4">
    <h3 className="mb-4 text-xl first-letter:uppercase">{fecha.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })}</h3>
    <div className="grid grid-cols-7 gap-1 text-center text-xs">
      {['L', 'M', 'X', 'J', 'V', 'S', 'D'].map((dia, i) => <span className="py-2 text-texto-suave" key={i}>{dia}</span>)}
      {Array.from({ length: inicio }, (_, i) => <div key={`vacio-${i}`} />)}
      {Array.from({ length: dias }, (_, i) => {
        const dia = i + 1
        const iso = `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(dia).padStart(2, '0')}`
        const entradas = obligaciones.filter(o => o.fechaPrevista === iso)
        return <div key={dia} className={`min-h-20 rounded-control p-1 text-left ${entradas.length ? 'border border-amber-300 bg-amber-50' : 'bg-fondo'}`}>
          <span className="text-texto-suave">{dia}</span>
          <div className="mt-1 space-y-1">{entradas.map(o => <button type="button" key={o.id} aria-pressed={seleccionada === o.id}
            aria-label={`Modelo ${o.modelo}, ${o.periodo} ${o.ejercicio}, ${o.fechaPrevista}, ${o.estado}`}
            onClick={() => seleccionar(o.id)} className={`block w-full rounded px-1 py-1 text-[10px] font-semibold ${seleccionada === o.id ? 'bg-acento text-white' : 'bg-tinta text-white'}`}>
            <span className="block sm:inline">{o.modelo}</span><span className="block sm:inline"> {o.periodo}</span><span className="hidden text-[9px] sm:block">{o.estado === 'PRESENTADA' ? 'Presentada' : o.estado === 'LISTA' ? 'Lista' : 'Incompleta'}</span><span className="block text-[9px] sm:hidden">{o.estado === 'PRESENTADA' ? 'Pres.' : o.estado === 'LISTA' ? 'Lista' : 'Inc.'}</span>
          </button>)}</div>
        </div>
      })}
    </div>
  </div>
}
