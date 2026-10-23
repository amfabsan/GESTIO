import { useMemo, useState } from 'react'
import { AlertCircle, Check, FileText, Loader2 } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import type { Documento } from '../../types/documentos'
import { useValidacionDocumentos } from '../../features/documentos/hooks/useValidacionDocumentos'
import { formatearEuros, formatearFecha } from '../../utils/formato'

const CAMPOS_EDITABLES = [
    'NumeroFactura',
    'EmisorNombre',
    'EmisorNif',
    'FechaEmision',
    'BaseImponible',
    'TipoIva',
    'CuotaIva',
    'Total',
]

function convertirCampo(campo: string, valor: string | null) {
    if (valor == null || valor === '') return null
    return valor
}

export function BandejaValidacionPage() {
    const { documentos, cargando, error, procesando, corregir, aprobar } = useValidacionDocumentos()
    const [seleccionadoId, setSeleccionadoId] = useState<string | null>(null)
    const [cambios, setCambios] = useState<Record<string, string>>({})

    const documentoSeleccionado = useMemo(() => {
        if (!seleccionadoId) return documentos[0] ?? null
        return documentos.find(d => d.id === seleccionadoId) ?? documentos[0] ?? null
    }, [documentos, seleccionadoId])

    const actualizarCambio = (campo: string, valor: string) => {
        setCambios((actual) => ({ ...actual, [campo]: valor }))
    }

    const guardarCambios = async () => {
        if (!documentoSeleccionado) return
        const payload = Object.entries(cambios)
            .filter(([_, valor]) => valor != null && valor.trim() !== '')
            .map(([campo, valor]) => ({ campo, valor: convertirCampo(campo, valor) }))
        if (payload.length === 0) return

        await corregir(documentoSeleccionado.id, payload)
    }

    const aprobarActual = async () => {
        if (!documentoSeleccionado) return
        await aprobar(documentoSeleccionado.id)
        setSeleccionadoId(null)
        setCambios({})
    }

    if (cargando) {
        return (
            <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f4f0] p-6">
                <div className="mx-auto max-w-6xl">
                    <PageHeader titulo="Bandeja de validación" subtitulo="Documentos de clientes pendientes de revisión" />
                    <div className="mt-6 rounded-xl border bg-white p-8 text-center text-sm text-slate-600">
                        <Loader2 className="mx-auto size-5 animate-spin" />
                        Cargando documentos…
                    </div>
                </div>
            </div>
        )
    }

    if (documentos.length === 0) {
        return (
            <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f4f0] p-6">
                <div className="mx-auto max-w-6xl">
                    <PageHeader titulo="Bandeja de validación" subtitulo="Documentos de clientes pendientes de revisión" />
                    <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white p-12 text-center">
                        <p className="text-lg font-semibold text-slate-700">No hay documentos pendientes de validación.</p>
                    </div>
                </div>
            </div>
        )
    }

    return (
        <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f4f0] p-6">
            <div className="mx-auto max-w-7xl">
                <PageHeader titulo="Bandeja de validación" subtitulo="Revisa el documento original, corrige los datos y aprueba la información." />

                {error && (
                    <div className="mt-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700" role="alert">
                        <AlertCircle className="mt-0.5 size-4" />
                        <span>{error}</span>
                    </div>
                )}

                <div className="mt-6 grid gap-5 lg:grid-cols-[350px_minmax(0,1fr)]">
                    <aside className="rounded-2xl border bg-white p-4 shadow-sm">
                        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Documentos pendientes</h2>
                        <ul className="mt-4 space-y-3">
                            {documentos.map((doc: Documento) => (
                                <li key={doc.id}>
                                    <button
                                        type="button"
                                        onClick={() => setSeleccionadoId(doc.id)}
                                        className={`w-full rounded-xl border p-3 text-left transition ${doc.id === documentoSeleccionado?.id ? 'border-[#1b2a4a] bg-[#eef3ff]' : 'border-slate-200 bg-slate-50 hover:border-slate-300'}`}
                                    >
                                        <div className="flex items-center gap-2 text-sm font-semibold">
                                            <FileText className="size-4" />
                                            {doc.nombreArchivo}
                                        </div>
                                        <div className="mt-1 text-xs text-slate-500">
                                            {doc.usuario} · {formatearFecha(doc.fechaSubida)}
                                        </div>
                                    </button>
                                </li>
                            ))}
                        </ul>
                    </aside>

                    <section className="rounded-2xl border bg-white p-4 shadow-sm">
                        {documentoSeleccionado && (
                            <>
                                <div className="flex flex-col gap-3 border-b pb-4 sm:flex-row sm:items-center sm:justify-between">
                                    <div>
                                        <h2 className="text-xl font-semibold">{documentoSeleccionado.nombreArchivo}</h2>
                                        <p className="text-sm text-slate-500">Cliente: {documentoSeleccionado.usuario}</p>
                                    </div>
                                    <div className="flex gap-2">
                                        <button
                                            type="button"
                                            onClick={() => void guardarCambios()}
                                            disabled={procesando}
                                            className="rounded-lg border border-[#1b2a4a] px-3 py-2 text-sm font-medium text-[#1b2a4a] disabled:opacity-60"
                                        >
                                            Guardar correcciones
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => void aprobarActual()}
                                            disabled={procesando}
                                            className="rounded-lg bg-[#1b2a4a] px-3 py-2 text-sm font-semibold text-white disabled:opacity-60"
                                        >
                                            Aprobar
                                        </button>
                                    </div>
                                </div>

                                <div className="mt-5 grid gap-5 xl:grid-cols-2">
                                    <div>
                                        <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Documento original</h3>
                                        <iframe
                                            title="original"
                                            src={documentoSeleccionado.tieneOriginal ? `/api/documentos/${documentoSeleccionado.id}/archivo` : undefined}
                                            className="h-[420px] w-full rounded-xl border bg-slate-100"
                                        />
                                    </div>

                                    <div>
                                        <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Datos propuestos / confianza</h3>
                                        <div className="space-y-3">
                                            {CAMPOS_EDITABLES.map((campo) => {
                                                const valorActual =
                                                    documentoSeleccionado.extraccion?.[
                                                    campo as keyof NonNullable<typeof documentoSeleccionado.extraccion>
                                                    ] as unknown as string | number | null

                                                const valorTexto = valorActual == null ? '' : String(valorActual)

                                                return (
                                                    <label key={campo} className="block">
                                                        <span className="mb-1 block text-sm font-medium text-slate-700">{campo}</span>
                                                        <input
                                                            type="text"
                                                            value={cambios[campo] ?? valorTexto}
                                                            onChange={(event) => actualizarCambio(campo, event.target.value)}
                                                            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none ring-0 focus:border-[#1b2a4a]"
                                                        />
                                                    </label>
                                                )
                                            })}
                                        </div>

                                        <div className="mt-5 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">
                                            <div className="font-medium text-slate-700">Confianza por campo</div>
                                            <div className="mt-2">No disponible en esta versión del backend; se puede añadir al next sprint.</div>
                                        </div>

                                        {documentoSeleccionado.extraccion?.advertencias?.length ? (
                                            <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 p-3">
                                                <p className="text-sm font-medium text-amber-800">Advertencias IA</p>
                                                <ul className="mt-2 list-disc pl-5 text-sm text-amber-700">
                                                    {documentoSeleccionado.extraccion.advertencias.map((aviso) => (
                                                        <li key={aviso}>{aviso}</li>
                                                    ))}
                                                </ul>
                                            </div>
                                        ) : null}
                                    </div>
                                </div>

                                <div className="mt-6 flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 p-3">
                                    <div className="text-sm text-slate-600">
                                        <span className="font-medium text-slate-700">Estado:</span> {documentoSeleccionado.estado}
                                    </div>
                                    <div className="inline-flex items-center gap-2 text-sm text-emerald-700">
                                        <Check className="size-4" />
                                        Se valida antes de aprobar
                                    </div>
                                </div>
                            </>
                        )}
                    </section>
                </div>
            </div>
        </div>
    )
}