// src/pages/gestor/ClientesPage.tsx
import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
    AlertTriangle,
    Bell,
    CalendarClock,
    ChevronRight,
    FileCheck2,
    Filter,
    Info,
    ListChecks,
    Loader2,
    Search,
} from 'lucide-react'
import { obtenerPanelGestor } from '../../features/panelGestor/panelGestorApi'
import { obtenerDetalleError } from '../../services/apiClient'
import type { PanelAvisoOrigen, PanelClienteFila, PanelGestorResponse } from '../../types/panelGestor'

function formatearFechaCorta(fechaIso: string | null) {
    if (!fechaIso) return '—'
    const fecha = new Date(`${fechaIso}T12:00:00`)
    if (Number.isNaN(fecha.getTime())) return fechaIso
    return new Intl.DateTimeFormat('es-ES', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
    })
        .format(fecha)
        .replace('.', '')
}

export function ClientesPage() {
    const navigate = useNavigate()
    const [panel, setPanel] = useState<PanelGestorResponse | null>(null)
    const [cargando, setCargando] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [soloRequiereAtencion, setSoloRequiereAtencion] = useState(false)
    const [ordenarPor, setOrdenarPor] = useState<'proxima' | 'nombre'>('proxima')
    const [busqueda, setBusqueda] = useState('')
    const [avisoRecordatorio, setAvisoRecordatorio] = useState<string | null>(null)

    useEffect(() => {
        const controller = new AbortController()

        obtenerPanelGestor({
            soloRequiereAtencion,
            ordenarPor,
            busqueda,
            signal: controller.signal,
        })
            .then((datos) => {
                if (!controller.signal.aborted) setPanel(datos)
            })
            .catch((excepcion) => {
                if (!controller.signal.aborted) {
                    setError(obtenerDetalleError(excepcion) ?? 'No se ha podido cargar el panel de clientes.')
                }
            })
            .finally(() => {
                if (!controller.signal.aborted) setCargando(false)
            })

        return () => controller.abort()
    }, [soloRequiereAtencion, ordenarPor, busqueda])

    function actualizarFiltros(actualizar: () => void) {
        setCargando(true)
        setError(null)
        actualizar()
    }

    function navegarDesdeAviso(cliente: PanelClienteFila, aviso: PanelAvisoOrigen) {
        if (aviso.tipo === 'CHECKLIST') {
            navigate(`/gestor/clientes/${cliente.autonomoId}/calendario`, {
                state: {
                    obligacionId: aviso.obligacionFiscalId ?? undefined,
                    ejercicio: aviso.ejercicio ?? undefined,
                },
            })
            return
        }
        navigate('/gestor/bandeja-validacion', {
            state: { documentoId: aviso.documentoId ?? undefined },
        })
    }

    const resumen = panel?.resumen ?? {
        requierenAtencion: 0,
        obligacionesProximas: 0,
        checklistsIncompletos: 0,
        documentosPorValidar: 0,
        anomaliasPendientes: 0,
    }

    return (
        <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
            <div className="mx-auto max-w-7xl space-y-6">
                <header className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
                    <div>
                        <h1 className="font-serif text-3xl font-bold tracking-tight text-[#152546] sm:text-4xl">
                            Panel de clientes
                        </h1>
                        {panel && (
                            <p className="mt-1.5 text-sm text-[#647083]">
                                {panel.nombreGestor}
                                {panel.nombreAsesoria ? ` · ${panel.nombreAsesoria}` : ''} ·{' '}
                                <strong>{panel.clientesActivosCount}</strong>{' '}
                                {panel.clientesActivosCount === 1
                                    ? 'cliente con vinculación ACTIVA'
                                    : 'clientes con vinculación ACTIVA'}
                                {panel.solicitudesPendientesCount > 0 &&
                                    ` · ${panel.solicitudesPendientesCount} ${panel.solicitudesPendientesCount === 1
                                        ? 'solicitud pendiente no incluida'
                                        : 'solicitudes pendientes no incluidas'
                                    }`}
                            </p>
                        )}
                    </div>
                </header>

                <section
                    className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5"
                    aria-label="Resumen de indicadores del panel"
                >
                    <article className="rounded-2xl border border-[#e3ded4] bg-white p-4 shadow-sm">
                        <AlertTriangle className="size-4 text-[#c86b1c]" aria-hidden="true" />
                        <p className="mt-2.5 text-2xl font-bold text-[#c86b1c]">{resumen.requierenAtencion}</p>
                        <h2 className="mt-1 text-xs font-medium text-[#647083]">Requieren atención</h2>
                    </article>

                    <article className="rounded-2xl border border-[#e3ded4] bg-white p-4 shadow-sm">
                        <CalendarClock className="size-4 text-[#1d2b4f]" aria-hidden="true" />
                        <p className="mt-2.5 text-2xl font-bold text-[#152546]">
                            {resumen.obligacionesProximas}
                        </p>
                        <h2 className="mt-1 text-xs font-medium text-[#647083]">
                            Obligaciones próximas (≤ {panel?.diasAntelacionObligaciones ?? 35} días)
                        </h2>
                    </article>

                    <article className="rounded-2xl border border-[#e3ded4] bg-white p-4 shadow-sm">
                        <ListChecks className="size-4 text-[#c86b1c]" aria-hidden="true" />
                        <p className="mt-2.5 text-2xl font-bold text-[#c86b1c]">
                            {resumen.checklistsIncompletos}
                        </p>
                        <h2 className="mt-1 text-xs font-medium text-[#647083]">Checklists incompletos</h2>
                    </article>

                    <article className="rounded-2xl border border-[#e3ded4] bg-white p-4 shadow-sm">
                        <FileCheck2 className="size-4 text-[#2563eb]" aria-hidden="true" />
                        <p className="mt-2.5 text-2xl font-bold text-[#2563eb]">
                            {resumen.documentosPorValidar}
                        </p>
                        <h2 className="mt-1 text-xs font-medium text-[#647083]">Documentos por validar</h2>
                    </article>

                    <article className="col-span-2 rounded-2xl border border-[#e3ded4] bg-white p-4 shadow-sm sm:col-span-1">
                        <AlertTriangle className="size-4 text-red-600" aria-hidden="true" />
                        <p className="mt-2.5 text-2xl font-bold text-red-600">
                            {resumen.anomaliasPendientes}
                        </p>
                        <h2 className="mt-1 text-xs font-medium text-[#647083]">Anomalías pendientes</h2>
                    </article>
                </section>

                <section className="overflow-hidden rounded-2xl border border-[#e3ded4] bg-white shadow-sm">
                    <div className="flex flex-col justify-between gap-3 border-b border-[#ece9e3] p-4 sm:flex-row sm:items-center sm:px-5">
                        <div className="flex flex-wrap items-center gap-2.5">
                            <button
                                type="button"
                                aria-pressed={soloRequiereAtencion}
                                onClick={() => actualizarFiltros(() => setSoloRequiereAtencion((prev) => !prev))}
                                className={`inline-flex items-center gap-2 rounded-full border px-4 py-2 text-xs font-bold transition ${soloRequiereAtencion
                                        ? 'border-[#c86b1c] bg-[#c86b1c] text-white'
                                        : 'border-[#ded9cf] bg-[#fbfaf7] text-[#3d3a30] hover:bg-[#f2efe8]'
                                    }`}
                            >
                                <Filter className="size-3.5" aria-hidden="true" />
                                Solo requieren atención
                            </button>

                            <label className="inline-flex items-center gap-2 text-xs font-medium text-[#647083]">
                                <span className="sr-only">Ordenar clientes por</span>
                                <select
                                    value={ordenarPor}
                                    onChange={(e) => actualizarFiltros(() => setOrdenarPor(e.target.value as 'proxima' | 'nombre'))}
                                    className="rounded-lg border border-[#ded9cf] bg-[#fbfaf7] px-3 py-2 text-xs font-semibold text-[#1b2a4a] outline-none focus:border-[#1d2b4f]"
                                >
                                    <option value="proxima">⇅ Obligación más próxima</option>
                                    <option value="nombre">⇅ Nombre</option>
                                </select>
                            </label>
                        </div>

                        <label className="relative block w-full sm:w-72">
                            <Search
                                className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[#857f70]"
                                aria-hidden="true"
                            />
                            <input
                                type="search"
                                value={busqueda}
                                onChange={(e) => actualizarFiltros(() => setBusqueda(e.target.value))}
                                placeholder="Buscar cliente o actividad"
                                aria-label="Buscar cliente o actividad"
                                className="w-full rounded-lg border border-[#ded9cf] bg-[#fbfaf7] py-2 pl-9 pr-3 text-sm text-[#1b2a4a] outline-none placeholder:text-[#8c877b] focus:border-[#1d2b4f]"
                            />
                        </label>
                    </div>

                    {avisoRecordatorio && (
                        <div
                            role="status"
                            className="flex items-center justify-between border-b border-amber-200 bg-amber-50 px-5 py-2.5 text-xs text-amber-900"
                        >
                            <span>{avisoRecordatorio}</span>
                            <button
                                type="button"
                                onClick={() => setAvisoRecordatorio(null)}
                                className="font-bold underline"
                            >
                                Cerrar
                            </button>
                        </div>
                    )}

                    {error && (
                        <div
                            role="alert"
                            className="m-4 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
                        >
                            {error}
                        </div>
                    )}

                    {cargando ? (
                        <div
                            className="flex items-center justify-center gap-2 py-16 text-sm text-[#647083]"
                            aria-busy="true"
                        >
                            <Loader2 className="size-5 animate-spin" aria-hidden="true" />
                            Cargando panel de clientes…
                        </div>
                    ) : !panel || panel.clientes.length === 0 ? (
                        <div className="px-6 py-14 text-center">
                            <p className="text-sm font-semibold text-[#1b2a4a]">
                                No hay clientes que coincidan con el filtro actual.
                            </p>
                            <p className="mt-1 text-xs text-[#647083]">
                                {panel?.clientesActivosCount === 0
                                    ? 'Aún no tienes clientes con vinculación activa en tu asesoría.'
                                    : 'Prueba a desactivar el filtro o limpiar la búsqueda.'}
                            </p>
                        </div>
                    ) : (
                        <>
                            <div className="hidden grid-cols-[minmax(280px,2.4fr)_repeat(4,minmax(76px,0.7fr))_minmax(130px,1fr)_auto] items-center gap-3 border-b border-[#ece9e3] px-5 py-3 text-[11px] font-bold uppercase tracking-wider text-[#7c8592] lg:grid">
                                <span>Cliente · Motivo</span>
                                <span className="text-center">Próximas</span>
                                <span className="text-center">Checklist inc.</span>
                                <span className="text-center">Por validar</span>
                                <span className="text-center">Anomalías</span>
                                <span className="text-right">Próxima obligación</span>
                                <span className="sr-only">Acción</span>
                            </div>

                            <ul className="divide-y divide-[#ece9e3]">
                                {panel.clientes.map((cliente) => {
                                    const avisosVisibles = cliente.avisos.slice(0, 3)
                                    const restantes = Math.max(0, cliente.avisos.length - avisosVisibles.length)

                                    return (
                                        <li
                                            key={cliente.autonomoId}
                                            className="grid gap-4 px-4 py-4 transition hover:bg-[#faf8f4] sm:px-5 lg:grid-cols-[minmax(280px,2.4fr)_repeat(4,minmax(76px,0.7fr))_minmax(130px,1fr)_auto] lg:items-center lg:gap-3"
                                        >
                                            <div className="min-w-0">
                                                <div className="flex flex-wrap items-center gap-2.5">
                                                    <span
                                                        className="grid size-9 shrink-0 place-items-center rounded-full bg-[#1d2b4f] text-xs font-bold text-white"
                                                        aria-hidden="true"
                                                    >
                                                        {cliente.iniciales}
                                                    </span>
                                                    <div className="min-w-0">
                                                        <div className="flex flex-wrap items-center gap-2">
                                                            <Link
                                                                to={`/gestor/clientes/${cliente.autonomoId}/calendario`}
                                                                className="truncate text-sm font-bold text-[#152546] hover:underline"
                                                            >
                                                                {cliente.nombreCompleto}
                                                            </Link>
                                                            {cliente.requiereAtencion ? (
                                                                <span className="rounded bg-[#c86b1c] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
                                                                    Requiere atención
                                                                </span>
                                                            ) : (
                                                                <span className="rounded bg-emerald-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-emerald-800">
                                                                    Sin incidencias
                                                                </span>
                                                            )}
                                                        </div>
                                                        <p className="text-xs text-[#647083]">
                                                            {cliente.actividad ?? `NIF ${cliente.nif}`}
                                                        </p>
                                                    </div>
                                                </div>

                                                {avisosVisibles.length > 0 && (
                                                    <div className="mt-2.5 flex flex-wrap items-center gap-1.5 pl-11">
                                                        {avisosVisibles.map((aviso, idx) => {
                                                            const esDocumento = aviso.tipo === 'DOCUMENTO'
                                                            const esAnomalia = aviso.tipo === 'ANOMALIA'
                                                            const clasesChip = esAnomalia
                                                                ? 'border-red-200 bg-red-50 text-red-800 hover:bg-red-100'
                                                                : esDocumento
                                                                    ? 'border-blue-200 bg-blue-50 text-blue-800 hover:bg-blue-100'
                                                                    : 'border-amber-200 bg-amber-50 text-amber-900 hover:bg-amber-100'

                                                            return (
                                                                <button
                                                                    key={`${aviso.tipo}-${aviso.obligacionFiscalId ?? aviso.documentoId ?? aviso.anomaliaId ?? idx}`}
                                                                    type="button"
                                                                    onClick={() => navegarDesdeAviso(cliente, aviso)}
                                                                    className={`inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[11px] font-semibold transition ${clasesChip}`}
                                                                >
                                                                    <span>{aviso.titulo}</span>
                                                                    <ChevronRight className="size-3" aria-hidden="true" />
                                                                </button>
                                                            )
                                                        })}
                                                        {restantes > 0 && (
                                                            <Link
                                                                to={`/gestor/clientes/${cliente.autonomoId}/calendario`}
                                                                className="px-1.5 text-[11px] font-medium text-[#647083] hover:underline"
                                                            >
                                                                +{restantes} más
                                                            </Link>
                                                        )}
                                                    </div>
                                                )}
                                            </div>

                                            <div className="flex items-center justify-between text-xs lg:justify-center">
                                                <span className="text-[#647083] lg:hidden">Próximas</span>
                                                <span className="font-bold text-[#152546]">
                                                    {cliente.obligacionesProximasCount > 0
                                                        ? cliente.obligacionesProximasCount
                                                        : '—'}
                                                </span>
                                            </div>

                                            <div className="flex items-center justify-between text-xs lg:justify-center">
                                                <span className="text-[#647083] lg:hidden">Checklists incompletos</span>
                                                <span
                                                    className={
                                                        cliente.checklistsIncompletosCount > 0
                                                            ? 'font-bold text-[#c86b1c]'
                                                            : 'text-[#9aa1ac]'
                                                    }
                                                >
                                                    {cliente.checklistsIncompletosCount > 0
                                                        ? cliente.checklistsIncompletosCount
                                                        : '—'}
                                                </span>
                                            </div>

                                            <div className="flex items-center justify-between text-xs lg:justify-center">
                                                <span className="text-[#647083] lg:hidden">Documentos por validar</span>
                                                <span
                                                    className={
                                                        cliente.documentosPorValidarCount > 0
                                                            ? 'font-bold text-[#2563eb]'
                                                            : 'text-[#9aa1ac]'
                                                    }
                                                >
                                                    {cliente.documentosPorValidarCount > 0
                                                        ? cliente.documentosPorValidarCount
                                                        : '—'}
                                                </span>
                                            </div>

                                            <div className="flex items-center justify-between text-xs lg:justify-center">
                                                <span className="text-[#647083] lg:hidden">Anomalías pendientes</span>
                                                <span
                                                    className={
                                                        cliente.anomaliasPendientesCount > 0
                                                            ? 'font-bold text-red-600'
                                                            : 'text-[#9aa1ac]'
                                                    }
                                                >
                                                    {cliente.anomaliasPendientesCount > 0
                                                        ? cliente.anomaliasPendientesCount
                                                        : '—'}
                                                </span>
                                            </div>

                                            <div className="flex items-center justify-between lg:block lg:text-right">
                                                <span className="text-xs text-[#647083] lg:hidden">Próxima obligación</span>
                                                <div>
                                                    <p className="text-xs font-bold text-[#152546]">
                                                        {formatearFechaCorta(cliente.proximaObligacionFecha)}
                                                    </p>
                                                    {cliente.proximaObligacionEtiqueta && (
                                                        <p className="text-[11px] font-semibold text-[#c86b1c]">
                                                            {cliente.proximaObligacionEtiqueta}
                                                        </p>
                                                    )}
                                                </div>
                                            </div>

                                            <div className="flex justify-end">
                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        setAvisoRecordatorio(
                                                            `Recordatorio preparado para ${cliente.nombreCompleto}.`,
                                                        )
                                                    }
                                                    aria-label={`Enviar recordatorio a ${cliente.nombreCompleto}`}
                                                    title="Recordatorio"
                                                    className="rounded-lg border border-[#e3ded4] bg-white p-2 text-[#647083] transition hover:bg-[#f2efe8] hover:text-[#152546]"
                                                >
                                                    <Bell className="size-4" aria-hidden="true" />
                                                </button>
                                            </div>
                                        </li>
                                    )
                                })}
                            </ul>
                        </>
                    )}
                </section>

                <footer className="flex items-start gap-2.5 rounded-xl border border-[#e3ded4] bg-[#fbfaf7] px-4 py-3 text-xs text-[#647083]">
                    <Info className="mt-0.5 size-4 shrink-0 text-[#2563eb]" aria-hidden="true" />
                    <p>
                        <strong>Reglas visibles del panel:</strong> una obligación es <em>próxima</em> si su
                        fecha configurada vence en{' '}
                        <strong>{panel?.diasAntelacionObligaciones ?? 35} días o menos</strong>. Un cliente{' '}
                        <em>requiere atención</em> si tiene una obligación próxima con checklist incompleto,
                        un documento pendiente de validación o una anomalía pendiente. No es una valoración
                        fiscal automática; los datos se calculan en tiempo real desde el origen.
                    </p>
                </footer>
            </div>
        </div>
    )
}