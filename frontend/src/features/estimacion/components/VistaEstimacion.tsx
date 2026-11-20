import { useState } from 'react'
import { AlertTriangle, ArrowDownRight, ArrowUpRight, Ban, Calculator, Check, FileText, Info, Minus, Plus } from 'lucide-react'
import type { EstimacionResponse, ElementoPorRevisar } from '../../../types/estimacion'
import { formatearEuros } from '../../../utils/formato'

const card = 'rounded-2xl border border-[#e2dfd7] bg-white shadow-[0_1px_2px_rgba(0,0,0,0.03)]'

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
    const [toastMensaje, setToastMensaje] = useState<string | null>(null)

    if (cargando) {
        return <p role="status" className="py-12 text-center text-sm text-[#6b6656]">Calculando estimación fiscal…</p>
    }

    if (estimacion && !estimacion.disponible) {
        const motivo =
            estimacion.motivoNoDisponible ||
            'No hay estimación disponible para el perfil fiscal de este ejercicio.'

        return (
            <div role="alert" className={`${card} flex flex-col items-center justify-center px-6 py-16 text-center`}>
                <div className="grid size-12 place-items-center rounded-full bg-[#eef0f3] text-[#647083]">
                    <Ban className="size-6" strokeWidth={1.8} />
                </div>
                <h2 className="mt-4 text-base font-bold text-[#152546]">Estimación no disponible para este perfil</h2>
                <p className="mt-2 max-w-[520px] text-sm leading-relaxed text-[#6b6656]">
                    {motivo}
                </p>
            </div>
        )
    }

    if (error && !estimacion) {
        return (
            <div role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-sm text-red-800">
                {error}
            </div>
        )
    }

    if (!estimacion) return null

    const { ivaIngresos, ivaGastos, diferencia, documentosIncluidos, elementosPorRevisar, trimestre } = estimacion

    const ingresos = documentosIncluidos.filter(d => d.tipo === 'INGRESO')
    const gastos = documentosIncluidos.filter(d => d.tipo === 'GASTO')
    const sinDatos = ingresos.length === 0 && gastos.length === 0

    async function manejarToggleGasto(documentoId: string, etiqueta: string, incluir: boolean) {
        const ok = await onActualizarInclusion(documentoId, incluir)
        if (ok) {
            setToastMensaje(
                incluir
                    ? `IVA de ${etiqueta} incluido en la estimación`
                    : `IVA de ${etiqueta} excluido de la estimación`
            )
            setTimeout(() => setToastMensaje(null), 4000)
        }
    }

    return (
        <div className="space-y-5">
            {error && (
                <div role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-sm text-red-800">
                    {error}
                </div>
            )}

            {/* Fila 1: 3 Tarjetas KPI superiores */}
            <div className="grid gap-5 md:grid-cols-3">
                {/* KPI 1: IVA de ingresos incluidos */}
                <div className={`${card} flex flex-col justify-between p-6`}>
                    <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-[#6b6656]">
                        <ArrowUpRight className="size-3.5 text-[#1f7a5c]" strokeWidth={2.4} />
                        IVA DE INGRESOS INCLUIDOS
                    </p>
                    <p className="mt-3 text-[32px] font-extrabold leading-none tracking-tight text-[#152546]">
                        {formatearEuros(ivaIngresos)}
                    </p>
                    <p className="mt-2.5 text-xs text-[#6b6656]">
                        {ingresos.length} facturas emitidas validadas
                    </p>
                </div>

                {/* KPI 2: IVA de gastos incluidos */}
                <div className={`${card} flex flex-col justify-between p-6`}>
                    <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-[#6b6656]">
                        <ArrowDownRight className="size-3.5 text-[#6b6656]" strokeWidth={2.4} />
                        IVA DE GASTOS INCLUIDOS
                    </p>
                    <p className="mt-3 text-[32px] font-extrabold leading-none tracking-tight text-[#152546]">
                        {formatearEuros(ivaGastos)}
                    </p>
                    <p className="mt-2.5 text-xs text-[#6b6656]">
                        {gastos.length} gastos marcados por el gestor
                    </p>
                </div>

                {/* KPI 3: Diferencia orientativa (Tarjeta Azul Marino) */}
                <div className="flex flex-col justify-between rounded-2xl bg-[#152546] p-6 text-white shadow-sm">
                    <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-[#9aa8be]">
                        <Calculator className="size-3.5" strokeWidth={2} />
                        DIFERENCIA ORIENTATIVA · {trimestre}T
                    </p>
                    {sinDatos ? (
                        <>
                            <p className="mt-3 text-2xl font-bold tracking-tight text-white">Datos insuficientes</p>
                            <p className="mt-2 text-xs leading-relaxed text-[#9aa8be]">
                                No hay documentos validados de este trimestre que permitan estimar una cifra.
                            </p>
                        </>
                    ) : (
                        <>
                            <p className="mt-3 text-[32px] font-extrabold leading-none tracking-tight text-[#f5a623]">
                                {formatearEuros(diferencia)}
                            </p>
                            <p className="mt-2.5 text-xs text-[#9aa8be]">
                                {formatearEuros(ivaIngresos)} – {formatearEuros(ivaGastos)} · importe a reservar orientativo
                            </p>
                        </>
                    )}
                </div>
            </div>

            {/* Fila 2: Banner informativo amarillo */}
            <div className="flex items-start gap-3 rounded-xl border border-[#ebd899] bg-[#fdf8e6] px-4 py-3 text-xs leading-relaxed text-[#7c6218]">
                <Info className="mt-0.5 size-4 shrink-0 text-[#b78a14]" />
                <p>
                    <strong className="font-bold text-[#5c4810]">Previsión orientativa, no definitiva.</strong> No es el resultado de una declaración tributaria. Solo incluye documentos VALIDADOS del trimestre; el IVA de un gasto solo se resta cuando el gestor lo marca para la estimación (clasificarlo como gasto no implica que sea deducible).
                </p>
            </div>

            {/* Fila 3: Desglose en 2 columnas */}
            <div className="grid items-start gap-5 lg:grid-cols-2">
                {/* Columna Izquierda: Ingresos utilizados */}
                <section className={`${card} overflow-hidden`}>
                    <header className="flex items-center justify-between border-b border-[#ece9e3] px-6 py-4">
                        <h3 className="flex items-center gap-2 text-sm font-bold text-[#152546]">
                            <ArrowUpRight className="size-4 text-[#1f7a5c]" strokeWidth={2.4} />
                            Ingresos utilizados
                        </h3>
                        <span className="text-xs text-[#6b6656]">{ingresos.length} documentos</span>
                    </header>

                    {ingresos.length === 0 ? (
                        <p className="px-6 py-5 text-sm text-[#6b6656]">Ningún documento incluido.</p>
                    ) : (
                        <ul className="divide-y divide-[#f2efe9]">
                            {ingresos.map(doc => (
                                <li key={doc.documentoId} className="flex items-center justify-between gap-4 px-6 py-3.5 text-sm">
                                    <div className="min-w-0">
                                        <p className="truncate font-bold text-[#152546]">
                                            {doc.numeroFactura || doc.emisor || 'Factura emitida'}
                                        </p>
                                        <p className="mt-0.5 truncate text-xs text-[#6b6656]">
                                            {doc.emisor ? `${doc.emisor}${doc.nif ? ` (${doc.nif})` : ''} · validado` : 'Documento validado'}
                                        </p>
                                    </div>
                                    <span className="shrink-0 font-bold text-[#152546]">
                                        {formatearEuros(doc.ivaIncluido)}
                                    </span>
                                </li>
                            ))}
                        </ul>
                    )}

                    <footer className="flex items-center justify-between border-t border-[#ece9e3] px-6 py-3.5 text-sm font-bold text-[#152546]">
                        <span>Suma de cuotas de IVA</span>
                        <span>{formatearEuros(ivaIngresos)}</span>
                    </footer>
                </section>

                {/* Columna Derecha: Gastos incluidos por el gestor */}
                <section className={`${card} overflow-hidden`}>
                    <header className="flex items-center justify-between border-b border-[#ece9e3] px-6 py-4">
                        <h3 className="flex items-center gap-2 text-sm font-bold text-[#152546]">
                            <ArrowDownRight className="size-4 text-[#6b6656]" strokeWidth={2.4} />
                            Gastos incluidos por el gestor
                        </h3>
                        <span className="text-xs text-[#6b6656]">{gastos.length} documentos</span>
                    </header>

                    {gastos.length === 0 ? (
                        <p className="px-6 py-5 text-sm text-[#6b6656]">Ningún documento incluido.</p>
                    ) : (
                        <ul className="divide-y divide-[#f2efe9]">
                            {gastos.map(doc => {
                                const nombreVisible = doc.numeroFactura || doc.emisor || 'Gasto deducible'
                                return (
                                    <li key={doc.documentoId} className="flex items-center justify-between gap-4 px-6 py-3.5 text-sm">
                                        <div className="min-w-0">
                                            <p className="truncate font-bold text-[#152546]">{nombreVisible}</p>
                                            <p className="mt-0.5 truncate text-xs text-[#6b6656]">
                                                {doc.emisor ? `${doc.emisor}${doc.nif ? ` (${doc.nif})` : ''} · validado` : 'Documento validado'}
                                            </p>
                                        </div>
                                        <div className="flex shrink-0 items-center gap-3">
                                            <span className="font-bold text-[#152546]">
                                                {formatearEuros(doc.ivaIncluido)}
                                            </span>
                                            {esGestor && (
                                                <button
                                                    type="button"
                                                    disabled={procesando}
                                                    onClick={() => void manejarToggleGasto(doc.documentoId, nombreVisible, false)}
                                                    title="Excluir gasto de la estimación"
                                                    className="grid size-6 place-items-center rounded-full bg-[#f0ede6] text-[#6b6656] transition hover:bg-red-100 hover:text-red-700 disabled:opacity-50"
                                                >
                                                    <Minus className="size-3.5" />
                                                </button>
                                            )}
                                        </div>
                                    </li>
                                )
                            })}
                        </ul>
                    )}

                    <footer className="flex items-center justify-between border-t border-[#ece9e3] px-6 py-3.5 text-sm font-bold text-[#152546]">
                        <span>Suma de cuotas de IVA</span>
                        <span>{formatearEuros(ivaGastos)}</span>
                    </footer>
                </section>
            </div>

            {/* Fila 4: Elementos por revisar · excluidos del cálculo */}
            <section className={`${card} overflow-hidden`}>
                <header className="flex items-center justify-between border-b border-[#ece9e3] px-6 py-4">
                    <h3 className="flex items-center gap-2 text-sm font-bold text-[#152546]">
                        <AlertTriangle className="size-4 text-[#d97706]" />
                        Elementos por revisar · excluidos del cálculo
                    </h3>
                    <span className="text-xs font-semibold text-[#6b6656]">{elementosPorRevisar.length}</span>
                </header>

                {elementosPorRevisar.length === 0 ? (
                    <p className="px-6 py-5 text-sm text-[#6b6656]">No hay documentos excluidos en este trimestre.</p>
                ) : (
                    <ul className="divide-y divide-[#f2efe9]">
                        {elementosPorRevisar.map((elemento: ElementoPorRevisar) => {
                            const esAnomaliaOPendiente =
                                elemento.severidad === 'ANOMALIA' ||
                                elemento.motivo.toLowerCase().includes('anomalía') ||
                                elemento.motivo.toLowerCase().includes('pendiente de validación')

                            const puedeIncluirse = esGestor && !esAnomaliaOPendiente

                            return (
                                <li key={elemento.documentoId} className="flex flex-col justify-between gap-3 px-6 py-4 text-sm sm:flex-row sm:items-center">
                                    <div className="flex items-start gap-3">
                                        <FileText className="mt-0.5 size-4 shrink-0 text-[#8a94a6]" />
                                        <div>
                                            <p className="font-bold text-[#152546]">
                                                Documento {elemento.documentoId.slice(0, 8)}
                                            </p>
                                            <p className={`mt-0.5 text-xs ${puedeIncluirse ? 'text-[#6b6656]' : 'text-[#b45309]'}`}>
                                                {elemento.motivo}
                                            </p>
                                        </div>
                                    </div>

                                    {puedeIncluirse && (
                                        <button
                                            type="button"
                                            disabled={procesando}
                                            onClick={() => void manejarToggleGasto(elemento.documentoId, elemento.documentoId.slice(0, 8), true)}
                                            className="inline-flex shrink-0 items-center gap-1.5 self-start rounded-xl bg-[#152546] px-3.5 py-2 text-xs font-semibold text-white transition hover:bg-[#243b6b] disabled:opacity-50 sm:self-auto"
                                        >
                                            <Plus className="size-3.5" />
                                            Incluir IVA
                                        </button>
                                    )}
                                </li>
                            )
                        })}
                    </ul>
                )}
            </section>

            {/* Toast oscuro inferior al incluir/excluir (solo gestor) */}
            {toastMensaje && (
                <div
                    role="status"
                    className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2 rounded-xl bg-[#1c1b18] px-4 py-3 text-xs font-semibold text-white shadow-lg"
                >
                    <Check className="size-4 text-[#34d399]" />
                    <span>{toastMensaje}</span>
                </div>
            )}
        </div>
    )
}