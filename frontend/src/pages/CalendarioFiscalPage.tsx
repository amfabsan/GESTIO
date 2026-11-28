import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { Calculator, CheckCircle2, ExternalLink, FileCheck2, FileText, ListChecks, RotateCcw, Search, Sparkles, X } from 'lucide-react'
import { useAuth } from '../context/useAuth'
import { PageHeader } from '../components/ui/PageHeader'
import { Vacio } from '../components/ui/Vacio'
import { btnPrimario, btnSecundario, card, etiqueta } from '../components/ui/estilos'
import { obtenerDetalleError } from '../services/apiClient'
import { formatearFecha, formatearFechaHora } from '../utils/formato'
import { obtenerIniciales } from '../utils/usuarios'
import { buscarEvidenciasChecklist, generarCalendario, marcarElementoAportado, obtenerCalendario, obtenerChecklist, revisarElementoChecklist, revisarVinculoEvidencia } from '../features/obligaciones/obligacionesApi'
import type { CalendarioFiscal, ChecklistObligacion, ElementoChecklist, ObligacionFiscal, VinculoEvidenciaChecklist } from '../features/obligaciones/obligacionesApi'
import { CalendarioMes } from '../features/obligaciones/CalendarioMes'
import { obtenerOriginal } from '../features/documentos/documentosApi'

type NavegacionCalendarioState = {
    obligacionId?: string
    ejercicio?: number
} | null

export function CalendarioFiscalPage() {
    const { autonomoId } = useParams()
    const location = useLocation()
    const navState = location.state as NavegacionCalendarioState
    const { perfilActivo, esGestor } = useAuth()
    const id = esGestor ? autonomoId : perfilActivo?.id
    const [ejercicio, setEjercicio] = useState(navState?.ejercicio ?? 2026)

    return (
        <Contenido
            key={`${perfilActivo?.id}-${id}-${ejercicio}-${navState?.obligacionId ?? ''}`}
            id={id}
            ejercicio={ejercicio}
            seleccionInicial={navState?.obligacionId}
            seleccionarEjercicio={setEjercicio}
            esGestor={esGestor}
        />
    )
}

function Estado({ estado }: { estado: ObligacionFiscal['estado'] }) {
    const estilo = estado === 'LISTA' ? 'bg-green-100 text-green-800' : estado === 'PRESENTADA' ? 'bg-slate-100 text-slate-600' : 'bg-amber-100 text-amber-800'
    return <span className={`inline-block rounded-full px-2 py-1 text-[10px] font-bold ${estilo}`}>{estado === 'PENDIENTE' ? 'INCOMPLETA' : estado}</span>
}

function Contenido({
    id,
    ejercicio,
    seleccionInicial,
    seleccionarEjercicio,
    esGestor,
}: {
    id?: string
    ejercicio: number
    seleccionInicial?: string
    seleccionarEjercicio: (n: number) => void
    esGestor: boolean
}) {
    const [datos, setDatos] = useState<CalendarioFiscal | null>(null)
    const [cargando, setCargando] = useState(true)
    const [generando, setGenerando] = useState(false)
    const [buscandoEvidencias, setBuscandoEvidencias] = useState(false)
    const [error, setError] = useState('')
    const [mensaje, setMensaje] = useState('')
    const [vista, setVista] = useState<'listado' | 'calendario'>('listado')
    const [mostrarPresentadas, setMostrarPresentadas] = useState<boolean | null>(null)
    const [seleccion, setSeleccion] = useState<{ inicial?: string; id?: string }>({ inicial: seleccionInicial })
    const [mes, setMes] = useState(3)
    const [version, setVersion] = useState(0)

    useEffect(() => {
        if (!id) return
        const controller = new AbortController()
        obtenerCalendario(id, ejercicio, controller.signal)
            .then(r => { if (!controller.signal.aborted) setDatos(r) })
            .catch(e => { if (!controller.signal.aborted) setError(obtenerDetalleError(e) ?? 'No se ha podido consultar el calendario fiscal.') })
            .finally(() => { if (!controller.signal.aborted) setCargando(false) })
        return () => controller.abort()
    }, [id, ejercicio])

    async function generar() {
        if (!id) return
        setGenerando(true); setError(''); setMensaje('')
        try {
            setDatos(await generarCalendario(id, ejercicio))
            setVersion(v => v + 1)
            setMensaje('Calendario actualizado correctamente, sin duplicados.')
        } catch (e) {
            setError(obtenerDetalleError(e) ?? 'No se ha podido generar el calendario.')
        } finally {
            setGenerando(false)
        }
    }

    const mostrarPresentadasPorDefecto = Boolean(datos?.obligaciones.some(o => o.id === seleccionInicial && o.estado === 'PRESENTADA'))
    const mostrarObligacionesPresentadas = mostrarPresentadas ?? mostrarPresentadasPorDefecto
    const visibles = datos?.obligaciones.filter(o => mostrarObligacionesPresentadas || o.estado !== 'PRESENTADA') ?? []
    const seleccionId = seleccion.inicial === seleccionInicial ? seleccion.id ?? seleccionInicial : seleccionInicial
    const seleccionada = visibles.find(o => o.id === seleccionId) ?? visibles[0]
    const presentadas = datos?.obligaciones.filter(o => o.estado === 'PRESENTADA').length ?? 0
    const rutaPerfil = esGestor ? `/gestor/clientes/${id}/perfil-fiscal` : '/autonomo/perfil-fiscal'
    const actualizarObligacion = (actualizada: ObligacionFiscal) =>
        setDatos(actual => actual
            ? { ...actual, obligaciones: actual.obligaciones.map(o => o.id === actualizada.id ? actualizada : o) }
            : actual)

    async function buscarEvidencias() {
        if (!id || !seleccionada || seleccionada.estado === 'PRESENTADA') return
        setBuscandoEvidencias(true); setError(''); setMensaje('')
        try {
            const cantidad = await buscarEvidenciasChecklist(id, seleccionada.id)
            const checklist = await obtenerChecklist(id, seleccionada.id)
            actualizarObligacion(checklist.obligacion)
            setVersion(v => v + 1)
            setMensaje(cantidad > 0
                ? `Búsqueda completada: ${cantidad} evidencia${cantidad === 1 ? '' : 's'} propuesta${cantidad === 1 ? '' : 's'}.`
                : 'Búsqueda completada: no se encontraron nuevas coincidencias.')
        } catch (e) {
            setError(obtenerDetalleError(e) ?? 'No se han podido buscar evidencias para esta obligación.')
        } finally {
            setBuscandoEvidencias(false)
        }
    }

    const nombreCliente = datos?.nombreAutonomo ?? 'Cliente'
    const inicialesCliente = obtenerIniciales(nombreCliente) || 'CL'
    const hayIncompletas = visibles.some(o => o.estado !== 'PRESENTADA' && o.obligatoriosConfirmados < o.totalObligatorios)

    return (
        <div className="min-h-screen bg-fondo px-4 py-7 text-tinta sm:px-8 lg:px-9">
            {esGestor ? (
                <>
                    <Link to="/gestor/clientes" className="inline-block text-sm font-medium text-texto-suave hover:text-tinta">
                        ← Volver al panel de clientes
                    </Link>

                    <div className="mt-5 flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
                        <div className="flex items-center gap-3.5">
                            <span
                                className="grid size-11 shrink-0 place-items-center rounded-xl bg-tinta text-sm font-bold text-white"
                                aria-hidden="true"
                            >
                                {inicialesCliente}
                            </span>
                            <div>
                                <div className="flex flex-wrap items-center gap-2.5">
                                    <h1 className="text-3xl font-bold text-tinta sm:text-[34px]">
                                        {nombreCliente}
                                    </h1>
                                    {datos && (
                                        hayIncompletas ? (
                                            <span className="inline-flex items-center gap-1 rounded-full bg-aviso-fondo px-2.5 py-0.5 text-xs font-bold text-aviso">
                                                ● Requiere atención
                                            </span>
                                        ) : (
                                            <span className="inline-flex items-center gap-1 rounded-full bg-ok-fondo px-2.5 py-0.5 text-xs font-bold text-ok">
                                                ● Sin incidencias
                                            </span>
                                        )
                                    )}
                                </div>
                                {datos?.perfil && (
                                    <p className="mt-1 text-xs text-texto-suave">
                                        IVA: {datos.perfil.regimenIva} · IRPF: {datos.perfil.regimenIrpf}
                                    </p>
                                )}
                            </div>
                        </div>
                    </div>

                    <nav
                        aria-label="Secciones del cliente"
                        className="mt-6 flex gap-6 overflow-x-auto border-b border-borde text-sm font-semibold"
                    >
                        <Link
                            to={`/gestor/clientes/${id}/perfil-fiscal`}
                            className="flex items-center gap-2 whitespace-nowrap pb-3 text-texto-suave hover:text-tinta"
                        >
                            <FileCheck2 className="size-4" aria-hidden="true" />
                            Perfil fiscal
                        </Link>
                        <span className="flex items-center gap-2 whitespace-nowrap border-b-2 border-acento pb-3 text-tinta">
                            <ListChecks className="size-4" aria-hidden="true" />
                            Obligaciones y checklist
                        </span>
                        <Link
                            to={`/gestor/clientes/${id}/estimacion`}
                            className="flex items-center gap-2 whitespace-nowrap pb-3 text-texto-suave hover:text-tinta"
                        >
                            <Calculator className="size-4" aria-hidden="true" />
                            Estimación
                        </Link>
                    </nav>
                </>
            ) : (
                <PageHeader
                    titulo="Calendario fiscal"
                    subtitulo="Obligaciones previstas según el perfil fiscal anual configurado por el gestor"
                    derecha={datos?.perfil && (
                        <div className={`${card} max-w-sm p-4 text-sm`}>
                            <p className={etiqueta}>Perfil fiscal {ejercicio} · Solo lectura</p>
                            <p className="mt-2 font-semibold">IVA: {datos.perfil.regimenIva} · IRPF: {datos.perfil.regimenIrpf}</p>
                            <Link to={rutaPerfil} className="mt-2 inline-block text-xs underline">Consultar perfil fiscal</Link>
                        </div>
                    )}
                />
            )}

            <div className="my-6 flex flex-wrap items-center gap-3">
                <div className={`${card} inline-flex gap-1 p-1`} aria-label="Ejercicio fiscal">
                    {[2026, 2027].map(a => (
                        <button
                            type="button"
                            key={a}
                            disabled={generando}
                            aria-pressed={a === ejercicio}
                            className={a === ejercicio ? btnPrimario : btnSecundario}
                            onClick={() => seleccionarEjercicio(a)}
                        >
                            Ejercicio {a}
                        </button>
                    ))}
                </div>
                {datos?.perfilConfigurado && (
                    <>
                        <div className={`${card} inline-flex gap-1 p-1`} aria-label="Vista de obligaciones">
                            {(['listado', 'calendario'] as const).map(v => (
                                <button
                                    type="button"
                                    key={v}
                                    aria-pressed={vista === v}
                                    className={vista === v ? btnPrimario : btnSecundario}
                                    onClick={() => setVista(v)}
                                >
                                    {v === 'listado' ? 'Listado' : 'Calendario'}
                                </button>
                            ))}
                        </div>
                        <div className="flex flex-wrap gap-2 sm:ml-auto">
                            {esGestor && seleccionada && seleccionada.estado !== 'PRESENTADA' && (
                                <button
                                    type="button"
                                    className={btnSecundario}
                                    disabled={buscandoEvidencias || generando}
                                    onClick={() => void buscarEvidencias()}
                                >
                                    <Search className="mr-2 inline size-4" aria-hidden="true" />
                                    {buscandoEvidencias ? 'Buscando…' : 'Buscar evidencias'}
                                </button>
                            )}
                            <button
                                type="button"
                                className={btnPrimario}
                                disabled={generando || buscandoEvidencias}
                                onClick={() => void generar()}
                            >
                                {generando ? 'Generando…' : 'Generar calendario'}
                            </button>
                        </div>
                    </>
                )}
            </div>

            <p className="mb-5 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-900">
                ⓘ {datos?.avisoFechas ?? 'Fechas configuradas para la demostración'}. Son ficticias y no representan plazos oficiales verificados de la AEAT. Las correspondencias fiscales son simplificaciones académicas. No se presentan declaraciones ante la AEAT.
            </p>
            {error && <p role="alert" className="mb-4 rounded-xl border border-red-200 bg-red-50 p-4 text-red-800">{error}</p>}
            {mensaje && <p role="status" className="mb-4 rounded-xl bg-green-50 p-3 text-green-800">{mensaje}</p>}
            {cargando ? (
                <p role="status">Cargando calendario fiscal…</p>
            ) : datos && !datos.perfilConfigurado ? (
                <section className={`${card} p-8 text-center`}>
                    <h2 className="font-semibold">No hay perfil fiscal configurado para {ejercicio}</h2>
                    <p className="mx-auto mt-3 max-w-xl text-sm text-texto-suave">Sin un perfil configurado no se generan obligaciones por suposición. Tu gestor debe configurarlo primero.</p>
                    <Link to={rutaPerfil} className="mt-4 inline-block text-sm underline">Consultar perfil fiscal</Link>
                </section>
            ) : datos && (
                <div className="grid items-start gap-5 lg:grid-cols-[2fr_3fr]">
                    <section className={`${card} overflow-hidden`} aria-label="Obligaciones del ejercicio">
                        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-borde p-4">
                            <h2 className="font-semibold">{datos.obligaciones.length} obligaciones · {ejercicio}</h2>
                            {presentadas > 0 && (
                                <button
                                    type="button"
                                    className="text-xs font-semibold text-acento-hover"
                                    onClick={() => setMostrarPresentadas(v => !v)}
                                >
                                    {mostrarObligacionesPresentadas ? 'Ocultar presentadas' : `Ver presentadas (${presentadas})`}
                                </button>
                            )}
                        </header>
                        {vista === 'listado' ? (
                            <ul aria-label="Listado de obligaciones">
                                {visibles.map(o => (
                                    <li key={o.id}>
                                        <button
                                            type="button"
                                            aria-pressed={seleccionada?.id === o.id}
                                            onClick={() => setSeleccion({ inicial: seleccionInicial, id: o.id })}
                                            className={`flex w-full items-start gap-4 border-b border-borde p-4 text-left ${seleccionada?.id === o.id ? 'border-l-2 border-l-acento bg-fondo' : 'hover:bg-fondo'}`}
                                        >
                                            <time dateTime={o.fechaPrevista} className="w-20 shrink-0 text-xs font-semibold">
                                                {formatearFecha(`${o.fechaPrevista}T12:00:00`)}
                                            </time>
                                            <span className="min-w-0 flex-1">
                                                <strong className="text-sm">Mod. {o.modelo} · {o.periodo} · {o.ejercicio}</strong>
                                                <span className="mt-1 block text-xs text-texto-suave">{o.nombre}</span>
                                                <span className="mt-1 block text-xs font-semibold text-texto-suave">
                                                    {o.obligatoriosConfirmados}/{o.totalObligatorios} obligatorios confirmados
                                                </span>
                                            </span>
                                            <span className="flex shrink-0 flex-col items-end gap-1">
                                                <Estado estado={o.estado} />
                                                <span className="text-[10px] text-texto-suave">{o.fechaPrevista}</span>
                                            </span>
                                        </button>
                                    </li>
                                ))}
                            </ul>
                        ) : (
                            <>
                                <div className="p-4 pb-0">
                                    <label className="text-sm">
                                        Mes
                                        <select
                                            className="ml-3 rounded-control border border-borde bg-fondo p-2"
                                            value={mes}
                                            onChange={e => setMes(Number(e.target.value))}
                                        >
                                            {Array.from({ length: 13 }, (_, n) => (
                                                <option value={n} key={n}>
                                                    {new Date(ejercicio, n, 1).toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })}
                                                </option>
                                            ))}
                                        </select>
                                    </label>
                                </div>
                                <CalendarioMes
                                    ejercicio={ejercicio}
                                    mes={mes}
                                    obligaciones={visibles}
                                    seleccionar={id => setSeleccion({ inicial: seleccionInicial, id })}
                                    seleccionada={seleccionada?.id}
                                />
                            </>
                        )}
                        {visibles.length === 0 && (
                            <Vacio texto={datos.obligaciones.length ? 'Todas las obligaciones están presentadas. Puedes mostrarlas con «Ver presentadas».' : 'Todavía no hay obligaciones generadas. Pulsa «Generar calendario».'} />
                        )}
                    </section>
                    {seleccionada && id ? (
                        <Detalle
                            key={`${seleccionada.id}-${version}`}
                            autonomoId={id}
                            obligacionId={seleccionada.id}
                            esGestor={esGestor}
                            onActualizado={actualizarObligacion}
                        />
                    ) : (
                        <section className={`${card} p-6`}>
                            <Vacio texto="Selecciona una obligación para consultar su detalle." />
                        </section>
                    )}
                </div>
            )}
        </div>
    )
}

function Detalle({
    autonomoId,
    obligacionId,
    esGestor,
    onActualizado,
}: {
    autonomoId: string
    obligacionId: string
    esGestor: boolean
    onActualizado: (obligacion: ObligacionFiscal) => void
}) {
    const [detalle, setDetalle] = useState<ChecklistObligacion | null>(null)
    const [error, setError] = useState('')
    const [mensaje, setMensaje] = useState('')
    const [procesando, setProcesando] = useState<string | null>(null)
    const [abriendoDocumentoId, setAbriendoDocumentoId] = useState<string | null>(null)

    useEffect(() => {
        const controller = new AbortController()
        obtenerChecklist(autonomoId, obligacionId, controller.signal)
            .then(r => { if (!controller.signal.aborted) setDetalle(r) })
            .catch(e => { if (!controller.signal.aborted) setError(obtenerDetalleError(e) ?? 'No se ha podido consultar el checklist.') })
        return () => controller.abort()
    }, [autonomoId, obligacionId])

    async function cambiar(elemento: ElementoChecklist, accion: 'marcar' | 'desmarcar' | 'confirmar' | 'pendiente') {
        setProcesando(elemento.id); setError(''); setMensaje('')
        try {
            const actualizado = esGestor
                ? await revisarElementoChecklist(autonomoId, obligacionId, elemento.id, accion === 'confirmar')
                : await marcarElementoAportado(autonomoId, obligacionId, elemento.id, accion === 'marcar')
            setDetalle(actualizado)
            onActualizado(actualizado.obligacion)
        } catch (e) {
            setError(obtenerDetalleError(e) ?? 'No se ha podido actualizar el elemento del checklist.')
        } finally {
            setProcesando(null)
        }
    }

    async function cambiarEvidencia(vinculo: VinculoEvidenciaChecklist, accion: 'confirmar' | 'rechazar' | 'retirar') {
        setProcesando(vinculo.id); setError(''); setMensaje('')
        try {
            const actualizado = await revisarVinculoEvidencia(autonomoId, obligacionId, vinculo.id, accion)
            setDetalle(actualizado)
            onActualizado(actualizado.obligacion)
            setMensaje(accion === 'confirmar'
                ? 'Evidencia confirmada por el gestor.'
                : accion === 'rechazar'
                    ? 'Propuesta rechazada; ya no cuenta como evidencia.'
                    : 'Vinculación retirada; se ha recalculado el checklist.')
        } catch (e) {
            setError(obtenerDetalleError(e) ?? 'No se ha podido actualizar la evidencia.')
            try {
                const actualizado = await obtenerChecklist(autonomoId, obligacionId)
                setDetalle(actualizado)
                onActualizado(actualizado.obligacion)
            } catch {
                // Se conserva el error de la decisión original.
            }
        } finally {
            setProcesando(null)
        }
    }

    async function abrirDocumento(vinculo: VinculoEvidenciaChecklist) {
        const ventana = window.open('', '_blank')
        if (!ventana) {
            setError('Permite las ventanas emergentes para abrir el documento vinculado.')
            return
        }

        setAbriendoDocumentoId(vinculo.id); setError('')
        try {
            const archivo = await obtenerOriginal(vinculo.documentoId)
            const url = URL.createObjectURL(archivo)
            ventana.location.href = url
            window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
        } catch (e) {
            ventana.close()
            setError(obtenerDetalleError(e) ?? 'No se ha podido abrir el documento vinculado.')
        } finally {
            setAbriendoDocumentoId(null)
        }
    }

    const o = detalle?.obligacion
    const porcentaje = detalle && detalle.totalObligatorios > 0
        ? Math.round(detalle.obligatoriosConfirmados * 100 / detalle.totalObligatorios)
        : 0

    return (
        <section className={`${card} p-6`} aria-label="Detalle de obligación">
            {error && <p role="alert" className="mb-4 rounded-control bg-red-50 p-3 text-sm text-red-800">{error}</p>}
            {mensaje && <p role="status" className="mb-4 rounded-control bg-green-50 p-3 text-sm text-green-800">{mensaje}</p>}
            {!o ? (
                <p role="status">Cargando checklist…</p>
            ) : (
                <>
                    <div className="flex items-start justify-between gap-3">
                        <div>
                            <p className="text-xs text-texto-suave">Modelo {o.modelo} · {o.periodo} {o.ejercicio}</p>
                            <h2 className="mt-2 text-2xl font-bold">{o.nombre}</h2>
                        </div>
                        <Estado estado={o.estado} />
                    </div>
                    <dl className="my-6 grid grid-cols-2 gap-5 border-b border-borde pb-6 sm:grid-cols-4">
                        {[['Modelo', o.modelo], ['Ejercicio', o.ejercicio], ['Periodo', o.periodo], ['Fecha prevista', formatearFecha(`${o.fechaPrevista}T12:00:00`)]].map(([nombre, valor]) => (
                            <div key={nombre}>
                                <dt className={etiqueta}>{nombre}</dt>
                                <dd className="mt-2 text-sm font-bold">{valor}</dd>
                            </div>
                        ))}
                    </dl>
                    {o.estado === 'LISTA' && (
                        <p className="mb-5 rounded-xl border border-green-200 bg-green-50 p-3 text-sm text-green-900">
                            LISTA: todos los elementos obligatorios están confirmados. No significa que se haya presentado; GESTIO no envía información a la AEAT.
                        </p>
                    )}
                    {o.estado === 'PRESENTADA' && (
                        <p className="mb-5 rounded-xl bg-slate-50 p-3 text-sm">
                            Obligación presentada: el checklist es de solo lectura. GESTIO no envía declaraciones a la AEAT.
                        </p>
                    )}
                    <p className="mb-4 rounded-xl bg-blue-50 p-3 text-xs text-blue-900">
                        {detalle.avisoPlantilla} {detalle.avisoFechas}; no se presentan declaraciones a la AEAT.
                    </p>
                    <div className="mb-4 flex items-center justify-between text-xs text-texto-suave">
                        <h3 className={etiqueta}>Checklist de preparación</h3>
                        <span>{detalle.obligatoriosConfirmados}/{detalle.totalObligatorios} obligatorios confirmados</span>
                    </div>
                    <div
                        role="progressbar"
                        aria-label="Progreso de elementos obligatorios"
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={porcentaje}
                        className="mb-5 h-2 overflow-hidden rounded-full bg-borde"
                    >
                        <div className="h-full rounded-full bg-tinta transition-all" style={{ width: `${porcentaje}%` }} />
                    </div>
                    <ul className="divide-y divide-borde">
                        {detalle.elementos.map(elemento => {
                            const evidenciaConfirmada = elemento.evidencias.some(e => e.estado === 'CONFIRMADA')
                            return (
                            <li key={elemento.id} className="py-4 first:pt-0 last:pb-0">
                                <div className="flex items-start gap-3">
                                    <span
                                        aria-hidden="true"
                                        className={`mt-1 h-3 w-3 shrink-0 rounded-sm ${elemento.estado === 'CONFIRMADO' ? 'bg-green-600' : elemento.estado === 'APORTADO' ? 'bg-amber-400' : 'border-2 border-borde'}`}
                                    />
                                    <div className="min-w-0 flex-1">
                                        <p className="font-semibold text-sm">
                                            {elemento.descripcion}{' '}
                                            <span className="ml-1 rounded bg-fondo px-1.5 py-0.5 text-[9px] uppercase">
                                                {elemento.obligatorio ? 'Obligatorio' : 'Opcional'}
                                            </span>
                                        </p>
                                        <p className={`mt-1 text-xs ${elemento.estado === 'CONFIRMADO' ? 'text-green-700' : elemento.estado === 'APORTADO' ? 'text-amber-700' : 'text-texto-suave'}`}>
                                            {textoEstado(elemento)}
                                        </p>
                                        {elemento.historial.length > 0 && (
                                            <details className="mt-2 text-xs text-texto-suave">
                                                <summary className="cursor-pointer">Historial ({elemento.historial.length})</summary>
                                                <ol className="mt-2 space-y-2 pl-4">
                                                    {elemento.historial.map(c => (
                                                        <li key={c.id}>
                                                            {c.usuario} · {c.estadoAnterior} → {c.estadoNuevo} · {formatearFechaHora(c.fecha)} ({c.origen === 'GESTOR' ? 'revisión del gestor' : c.origen === 'VINCULACION_EVIDENCIA' ? 'evidencia documental' : 'marca manual'})
                                                        </li>
                                                    ))}
                                                </ol>
                                            </details>
                                        )}
                                        {elemento.evidencias.length > 0 && (
                                            <div className="mt-3 space-y-3">
                                                {elemento.evidencias.map(vinculo => {
                                                    const propuesta = vinculo.estado === 'PROPUESTA'
                                                    const confirmada = vinculo.estado === 'CONFIRMADA'
                                                    const colores = propuesta
                                                        ? 'border-violet-200 bg-violet-50 text-violet-950'
                                                        : confirmada
                                                            ? 'border-green-200 bg-green-50 text-green-950'
                                                            : 'border-borde bg-fondo text-texto-suave'
                                                    return (
                                                        <article key={vinculo.id} className={`rounded-xl border p-3 ${colores}`}>
                                                            <div className="flex flex-wrap items-center justify-between gap-2">
                                                                <p className="flex items-center gap-1.5 text-xs font-bold">
                                                                    {propuesta
                                                                        ? <><Sparkles className="size-3.5" aria-hidden="true" /> Propuesta de GESTIO · pendiente de confirmación</>
                                                                        : confirmada
                                                                            ? <><CheckCircle2 className="size-3.5" aria-hidden="true" /> Evidencia documental verificada</>
                                                                            : <><X className="size-3.5" aria-hidden="true" /> {vinculo.estado === 'RECHAZADA' ? 'Propuesta rechazada' : 'Vinculación retirada'}</>}
                                                                </p>
                                                                <span className="rounded-full bg-white/70 px-2 py-0.5 text-[10px] font-semibold">{vinculo.estado}</span>
                                                            </div>
                                                            <button
                                                                type="button"
                                                                disabled={abriendoDocumentoId === vinculo.id}
                                                                onClick={() => void abrirDocumento(vinculo)}
                                                                className="mt-2 inline-flex items-center gap-1.5 text-left text-xs font-semibold underline decoration-current underline-offset-2 disabled:opacity-60"
                                                            >
                                                                <FileText className="size-3.5 shrink-0" aria-hidden="true" />
                                                                {abriendoDocumentoId === vinculo.id ? 'Abriendo documento…' : vinculo.nombreArchivo}
                                                                <ExternalLink className="size-3 shrink-0" aria-hidden="true" />
                                                            </button>
                                                            <p className="mt-1 text-[11px] opacity-80">
                                                                Categoría {vinculo.categoriaDocumento}
                                                                {vinculo.fechaEmision && ` · emitido el ${formatearFecha(`${vinculo.fechaEmision}T12:00:00`)}`}
                                                                {' · coincidencia por categoría y periodo'}
                                                            </p>
                                                            {confirmada && vinculo.confirmadaPor && vinculo.fechaConfirmacion && (
                                                                <p className="mt-1 text-[11px] font-medium">
                                                                    Confirmada por {vinculo.confirmadaPor} · {formatearFechaHora(vinculo.fechaConfirmacion)}
                                                                </p>
                                                            )}
                                                            <details className="mt-2 text-[11px] opacity-80">
                                                                <summary className="cursor-pointer">Historial de evidencia ({vinculo.historial.length})</summary>
                                                                <ol className="mt-1 space-y-1 pl-4">
                                                                    {vinculo.historial.map(c => (
                                                                        <li key={c.id}>
                                                                            {c.usuario} · {c.estadoAnterior ? `${c.estadoAnterior} → ` : ''}{c.estadoNuevo} · {formatearFechaHora(c.fecha)}
                                                                        </li>
                                                                    ))}
                                                                </ol>
                                                            </details>
                                                            {esGestor && o.estado !== 'PRESENTADA' && (
                                                                <div className="mt-3 flex flex-wrap gap-2">
                                                                    {propuesta && <>
                                                                        <button type="button" className="rounded-control bg-violet-700 px-3 py-1.5 text-[11px] font-semibold text-white disabled:opacity-50" disabled={procesando !== null} onClick={() => void cambiarEvidencia(vinculo, 'confirmar')}>
                                                                            {procesando === vinculo.id ? 'Guardando…' : 'Confirmar evidencia'}
                                                                        </button>
                                                                        <button type="button" className="rounded-control border border-violet-300 bg-white px-3 py-1.5 text-[11px] font-semibold disabled:opacity-50" disabled={procesando !== null} onClick={() => void cambiarEvidencia(vinculo, 'rechazar')}>
                                                                            Rechazar
                                                                        </button>
                                                                    </>}
                                                                    {confirmada && <button type="button" className="inline-flex items-center gap-1 rounded-control border border-green-300 bg-white px-3 py-1.5 text-[11px] font-semibold disabled:opacity-50" disabled={procesando !== null} onClick={() => void cambiarEvidencia(vinculo, 'retirar')}>
                                                                        <RotateCcw className="size-3" aria-hidden="true" /> Retirar evidencia
                                                                    </button>}
                                                                </div>
                                                            )}
                                                        </article>
                                                    )
                                                })}
                                            </div>
                                        )}
                                    </div>
                                    {o.estado !== 'PRESENTADA' && (esGestor ? (
                                        <button
                                            type="button"
                                            className="rounded-control bg-green-700 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50"
                                            disabled={procesando !== null || (elemento.estado === 'CONFIRMADO' && evidenciaConfirmada)}
                                            title={elemento.estado === 'CONFIRMADO' && evidenciaConfirmada ? 'Retira primero la evidencia confirmada.' : undefined}
                                            onClick={() => void cambiar(elemento, elemento.estado === 'CONFIRMADO' ? 'pendiente' : 'confirmar')}
                                        >
                                            {procesando === elemento.id ? 'Guardando…' : elemento.estado === 'CONFIRMADO' ? 'A pendiente' : 'Confirmar'}
                                        </button>
                                    ) : (
                                        elemento.estado !== 'CONFIRMADO' && (
                                            <button
                                                type="button"
                                                className={btnSecundario}
                                                disabled={procesando !== null}
                                                onClick={() => void cambiar(elemento, elemento.estado === 'APORTADO' ? 'desmarcar' : 'marcar')}
                                            >
                                                {procesando === elemento.id ? 'Guardando…' : elemento.estado === 'APORTADO' ? 'Desmarcar' : 'Marcar aportado'}
                                            </button>
                                        )
                                    ))}
                                </div>
                            </li>
                        )})}
                    </ul>
                </>
            )}
        </section>
    )
}

function textoEstado(elemento: ElementoChecklist): string {
    if (elemento.estado === 'CONFIRMADO' && elemento.origen === 'VINCULACION_EVIDENCIA') {
        return `Confirmado por evidencia documental · ${elemento.usuarioUltimoCambio ?? 'gestor'} · ${elemento.fechaUltimoCambio ? formatearFechaHora(elemento.fechaUltimoCambio) : ''}`
    }
    if (elemento.estado === 'CONFIRMADO') return `Confirmado · revisión del gestor ${elemento.usuarioUltimoCambio ?? ''} · ${elemento.fechaUltimoCambio ? formatearFechaHora(elemento.fechaUltimoCambio) : ''}`
    if (elemento.estado === 'APORTADO') return `Aportado, pendiente de revisión · marca manual de ${elemento.usuarioUltimoCambio ?? ''} · ${elemento.fechaUltimoCambio ? formatearFechaHora(elemento.fechaUltimoCambio) : ''}`
    if (elemento.origen === 'GESTOR') return `Pendiente · devuelto por ${elemento.usuarioUltimoCambio ?? 'el gestor'} · ${elemento.fechaUltimoCambio ? formatearFechaHora(elemento.fechaUltimoCambio) : ''}`
    return 'Pendiente'
}