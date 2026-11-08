import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { PageHeader } from '../components/ui/PageHeader'
import { Vacio } from '../components/ui/Vacio'
import { btnPrimario, btnSecundario, card, etiqueta } from '../components/ui/estilos'
import { obtenerDetalleError } from '../services/apiClient'
import { formatearFecha, formatearFechaHora } from '../utils/formato'
import { generarCalendario, obtenerCalendario, obtenerDetalleObligacion } from '../features/obligaciones/obligacionesApi'
import type { CalendarioFiscal, DetalleObligacion, ObligacionFiscal } from '../features/obligaciones/obligacionesApi'
import { CalendarioMes } from '../features/obligaciones/CalendarioMes'

export function CalendarioFiscalPage() {
  const { autonomoId } = useParams()
  const { perfilActivo, esGestor } = useAuth()
  const id = esGestor ? autonomoId : perfilActivo?.id
  const [ejercicio, setEjercicio] = useState(2026)
  return <Contenido key={`${perfilActivo?.id}-${id}-${ejercicio}`} id={id} ejercicio={ejercicio} seleccionarEjercicio={setEjercicio} esGestor={esGestor} />
}

function Estado({ estado }: { estado: ObligacionFiscal['estado'] }) {
  return <span className={`inline-block rounded-full px-2 py-1 text-[10px] font-bold ${estado === 'PRESENTADA' ? 'bg-slate-100 text-slate-600' : 'bg-amber-100 text-amber-800'}`}>{estado}</span>
}

function Contenido({ id, ejercicio, seleccionarEjercicio, esGestor }: { id?: string; ejercicio: number; seleccionarEjercicio: (n: number) => void; esGestor: boolean }) {
  const [datos, setDatos] = useState<CalendarioFiscal | null>(null)
  const [cargando, setCargando] = useState(true)
  const [generando, setGenerando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [vista, setVista] = useState<'listado' | 'calendario'>('listado')
  const [mostrarPresentadas, setMostrarPresentadas] = useState(false)
  const [seleccion, setSeleccion] = useState<string>()
  const [mes, setMes] = useState(3)
  const [version, setVersion] = useState(0)
  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    obtenerCalendario(id, ejercicio, controller.signal).then(r => { if (!controller.signal.aborted) setDatos(r) })
      .catch(e => { if (!controller.signal.aborted) setError(obtenerDetalleError(e) ?? 'No se ha podido consultar el calendario fiscal.') })
      .finally(() => { if (!controller.signal.aborted) setCargando(false) })
    return () => controller.abort()
  }, [id, ejercicio])

  async function generar() {
    if (!id) return
    setGenerando(true); setError(''); setMensaje('')
    try { setDatos(await generarCalendario(id, ejercicio)); setVersion(v => v + 1); setMensaje('Calendario actualizado correctamente, sin duplicados.') }
    catch (e) { setError(obtenerDetalleError(e) ?? 'No se ha podido generar el calendario.') }
    finally { setGenerando(false) }
  }

  const visibles = datos?.obligaciones.filter(o => mostrarPresentadas || o.estado !== 'PRESENTADA') ?? []
  const seleccionada = visibles.find(o => o.id === seleccion) ?? visibles[0]
  const presentadas = datos?.obligaciones.filter(o => o.estado === 'PRESENTADA').length ?? 0
  const rutaPerfil = esGestor ? `/gestor/clientes/${id}/perfil-fiscal` : '/autonomo/perfil-fiscal'

  return <div className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
    {esGestor && <Link to="/gestor/clientes" className="mb-6 inline-block text-sm text-[#6b6656]">← Volver al panel de clientes</Link>}
    <PageHeader titulo={esGestor && datos ? `Calendario fiscal · ${datos.nombreAutonomo}` : 'Calendario fiscal'} subtitulo="Obligaciones previstas según el perfil fiscal anual configurado por el gestor"
      derecha={datos?.perfil && <div className={`${card} max-w-sm p-4 text-sm`}><p className={etiqueta}>Perfil fiscal {ejercicio} · Solo lectura</p><p className="mt-2 font-semibold">IVA: {datos.perfil.regimenIva} · IRPF: {datos.perfil.regimenIrpf}</p><Link to={rutaPerfil} className="mt-2 inline-block text-xs underline">Consultar perfil fiscal</Link></div>} />
    <div className="my-6 flex flex-wrap items-center gap-3">
      <div className={`${card} inline-flex gap-1 p-1`} aria-label="Ejercicio fiscal">{[2026, 2027].map(a => <button type="button" key={a} disabled={generando} aria-pressed={a === ejercicio} className={a === ejercicio ? btnPrimario : btnSecundario} onClick={() => seleccionarEjercicio(a)}>Ejercicio {a}</button>)}</div>
      {datos?.perfilConfigurado && <><div className={`${card} inline-flex gap-1 p-1`} aria-label="Vista de obligaciones">{(['listado', 'calendario'] as const).map(v => <button type="button" key={v} aria-pressed={vista === v} className={vista === v ? btnPrimario : btnSecundario} onClick={() => setVista(v)}>{v === 'listado' ? 'Listado' : 'Calendario'}</button>)}</div><button type="button" className={`${btnPrimario} sm:ml-auto`} disabled={generando} onClick={() => void generar()}>{generando ? 'Generando…' : 'Generar calendario'}</button></>}
    </div>
    <p className="mb-5 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-900">ⓘ {datos?.avisoFechas ?? 'Fechas configuradas para la demostración'}. Son ficticias y no representan plazos oficiales verificados de la AEAT. Las correspondencias fiscales son simplificaciones académicas. No se presentan declaraciones ante la AEAT.</p>
    {error && <p role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 p-4 text-red-800">{error}</p>}
    {mensaje && <p role="status" className="mb-4 rounded-xl bg-green-50 p-3 text-green-800">{mensaje}</p>}
    {cargando ? <p role="status">Cargando calendario fiscal…</p> : datos && !datos.perfilConfigurado ? <section className={`${card} p-8 text-center`}><h2 className="font-semibold">No hay perfil fiscal configurado para {ejercicio}</h2><p className="mx-auto mt-3 max-w-xl text-sm text-[#6b6656]">Sin un perfil configurado no se generan obligaciones por suposición. Tu gestor debe configurarlo primero.</p><Link to={rutaPerfil} className="mt-4 inline-block text-sm underline">Consultar perfil fiscal</Link></section> : datos && <div className="grid items-start gap-5 lg:grid-cols-[2fr_3fr]">
      <section className={`${card} overflow-hidden`} aria-label="Obligaciones del ejercicio">
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-[#ece9e3] p-4"><h2 className="font-semibold">{datos.obligaciones.length} obligaciones · {ejercicio}</h2>{presentadas > 0 && <button type="button" className="text-xs font-semibold text-[#b65a12]" onClick={() => setMostrarPresentadas(v => !v)}>{mostrarPresentadas ? 'Ocultar presentadas' : `Ver presentadas (${presentadas})`}</button>}</header>
        {vista === 'listado' ? <ul aria-label="Listado de obligaciones">{visibles.map(o => <li key={o.id}><button type="button" aria-pressed={seleccionada?.id === o.id} onClick={() => setSeleccion(o.id)} className={`flex w-full items-start gap-4 border-b border-[#ece9e3] p-4 text-left ${seleccionada?.id === o.id ? 'border-l-2 border-l-[#d96c13] bg-[#f0ede7]' : 'hover:bg-[#f8f7f4]'}`}>
          <time dateTime={o.fechaPrevista} className="w-20 shrink-0 text-xs font-semibold">{formatearFecha(`${o.fechaPrevista}T12:00:00`)}</time><span className="min-w-0 flex-1"><strong className="text-sm">Mod. {o.modelo} · {o.periodo} · {o.ejercicio}</strong><span className="mt-1 block text-xs text-[#6b6656]">{o.nombre}</span></span><Estado estado={o.estado} />
        </button></li>)}</ul> : <><div className="p-4 pb-0"><label className="text-sm">Mes<select className="ml-3 rounded-lg border border-[#d6d2c8] bg-[#f5f3ef] p-2" value={mes} onChange={e => setMes(Number(e.target.value))}>{Array.from({ length: 13 }, (_, n) => <option value={n} key={n}>{new Date(ejercicio, n, 1).toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })}</option>)}</select></label></div><CalendarioMes ejercicio={ejercicio} mes={mes} obligaciones={visibles} seleccionar={setSeleccion} seleccionada={seleccionada?.id} /></>}
        {visibles.length === 0 && <Vacio texto={datos.obligaciones.length ? 'Todas las obligaciones están presentadas. Puedes mostrarlas con «Ver presentadas».' : 'Todavía no hay obligaciones generadas. Pulsa «Generar calendario».'} />}
      </section>
      {seleccionada && id ? <Detalle key={`${seleccionada.id}-${version}`} autonomoId={id} obligacionId={seleccionada.id} /> : <section className={`${card} p-6`}><Vacio texto="Selecciona una obligación para consultar su detalle." /></section>}
    </div>}
  </div>
}

function Detalle({ autonomoId, obligacionId }: { autonomoId: string; obligacionId: string }) {
  const [detalle, setDetalle] = useState<DetalleObligacion | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    obtenerDetalleObligacion(autonomoId, obligacionId, controller.signal).then(r => { if (!controller.signal.aborted) setDetalle(r) })
      .catch(e => { if (!controller.signal.aborted) setError(obtenerDetalleError(e) ?? 'No se ha podido consultar el detalle de la obligación.') })
    return () => controller.abort()
  }, [autonomoId, obligacionId])
  const o = detalle?.obligacion
  return <section className={`${card} p-6`} aria-label="Detalle de obligación">
    {error ? <p role="alert">{error}</p> : !o ? <p role="status">Cargando detalle…</p> : <>
      <div className="flex items-start justify-between gap-3"><div><p className="text-xs text-[#6b6656]">Modelo {o.modelo} · {o.periodo} {o.ejercicio}</p><h2 className="mt-2 font-serif text-2xl font-bold">{o.nombre}</h2></div><Estado estado={o.estado} /></div>
      <dl className="my-6 grid grid-cols-2 gap-5 border-b border-[#ece9e3] pb-6 sm:grid-cols-4">{[['Modelo', o.modelo], ['Ejercicio', o.ejercicio], ['Periodo', o.periodo], ['Fecha prevista', formatearFecha(`${o.fechaPrevista}T12:00:00`)]].map(([nombre, valor]) => <div key={nombre}><dt className={etiqueta}>{nombre}</dt><dd className="mt-2 text-sm font-bold">{valor}</dd></div>)}</dl>
      <p className="rounded-xl bg-blue-50 p-3 text-sm text-blue-900">{detalle.avisoFechas}. Fechas ficticias, sin verificación de plazos oficiales de la AEAT.</p>
      {o.estado === 'PRESENTADA' && <p className="mt-4 rounded-xl bg-slate-50 p-3 text-sm">Obligación presentada: sus datos e historial se conservan aunque cambie el perfil fiscal. GESTIO no envía declaraciones a la AEAT.</p>}
      <h3 className={`${etiqueta} mt-6`}>Historial</h3><ul className="mt-3 space-y-4">{detalle.historial.map(h => <li key={h.id} className="text-sm"><p className="font-semibold">{h.gestor} · {formatearFechaHora(h.fecha)}</p><p className="mt-1 text-[#6b6656]">{h.descripcion}</p></li>)}</ul>
    </>}
  </section>
}
