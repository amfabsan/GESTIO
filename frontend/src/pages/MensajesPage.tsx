import { useEffect, useState } from 'react'
import { FileText, MessageCircle } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { ConversacionDocumento } from '../features/documentos/components/ConversacionDocumento'
import { EstadoBadge } from '../features/documentos/components/EstadoBadge'
import { useConversaciones } from '../features/documentos/hooks/useConversaciones'
import { rutaConversacion } from '../features/documentos/mensajesApi'
import { apiClient } from '../services/apiClient'
import type { Documento } from '../types/documentos'
import { formatearFechaHora } from '../utils/formato'

export function MensajesPage() {
    const { perfilActivo, esGestor } = useAuth()
    const { documentoId } = useParams()
    const consulta = useConversaciones(perfilActivo?.id)
    const [soloSinLeer, setSoloSinLeer] = useState(false)
    const conversaciones = consulta?.datos ?? []
    const visibles = soloSinLeer ? conversaciones.filter(c => c.sinLeer > 0) : conversaciones
    const seleccionado = documentoId

    return (
        <div className="min-h-screen bg-fondo text-tinta">
            <div className="mx-auto max-w-7xl px-4 py-8 sm:px-8">
                <h1 className="text-[34px] font-bold">Mensajes</h1>
                <p className="mb-6 mt-1 text-slate-600">Consulta y responde aclaraciones sobre documentos.</p>
                <div className="grid items-stretch gap-5 lg:grid-cols-[minmax(16rem,1fr)_minmax(0,2fr)]">
                    <section aria-label="Conversaciones" className="min-w-0 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                        <h2 className="flex items-center gap-2 font-semibold"><MessageCircle aria-hidden="true" size={20} /> Conversaciones</h2>
                        <div className="my-4 flex flex-wrap gap-2 border-b border-slate-100 pb-4">
                            {[false, true].map(filtro => (
                                <button type="button" key={String(filtro)} aria-pressed={soloSinLeer === filtro}
                                    onClick={() => setSoloSinLeer(filtro)}
                                    className="rounded-full border border-slate-200 px-3 py-1.5 text-xs font-semibold aria-pressed:bg-tinta aria-pressed:text-white">
                                    {filtro ? `Sin leer (${conversaciones.filter(c => c.sinLeer > 0).length})` : `Todas (${conversaciones.length})`}
                                </button>
                            ))}
                        </div>
                        {!consulta && <p className="text-sm text-slate-600">Cargando conversaciones…</p>}
                        {consulta?.error && <p role="alert" className="text-sm text-red-700">{consulta.error}</p>}
                        {consulta && !consulta.error && !visibles.length && (
                            <p className="text-sm text-slate-600">{soloSinLeer ? 'No hay conversaciones sin leer.' : 'Abre un documento para iniciar una conversación.'}</p>
                        )}
                        <ul className="space-y-1">
                            {visibles.map(c => (
                                <li key={c.documentoId}>
                                    <Link to={rutaConversacion(c.documentoId)} aria-current={seleccionado === c.documentoId ? 'page' : undefined}
                                        className="block rounded-xl px-3 py-3 hover:bg-slate-50 aria-[current=page]:bg-violet-50">
                                        <div className="flex items-center gap-2">
                                            <span className="truncate text-sm font-semibold">{c.nombreArchivo}</span>
                                            {c.sinLeer > 0 && <span className="rounded-full bg-violet-700 px-2 text-xs text-white">{c.sinLeer} sin leer</span>}
                                        </div>
                                        <p className="mt-1 truncate text-xs text-slate-600">{c.ultimoMensaje.autorId === perfilActivo?.id ? 'Tú' : c.ultimoMensaje.autor}: {c.ultimoMensaje.contenido}</p>
                                        <time className="text-xs text-slate-500" dateTime={c.ultimoMensaje.fecha}>{formatearFechaHora(c.ultimoMensaje.fecha)}</time>
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    </section>
                    <div className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                        {seleccionado
                            ? <DetalleConversacion key={`${perfilActivo?.id}/${seleccionado}`} id={seleccionado} esGestor={esGestor} />
                            : <p className="text-sm text-slate-600">Selecciona una conversación.</p>}
                    </div>
                </div>
            </div>
        </div>
    )
}

function DetalleConversacion({ id, esGestor }: { id: string; esGestor: boolean }) {
    const [documento, setDocumento] = useState<Documento>()
    const [error, setError] = useState(false)

    useEffect(() => {
        const controller = new AbortController()
        apiClient.get<Documento>(`/documentos/${id}`, { signal: controller.signal })
            .then(({ data }) => setDocumento(data))
            .catch(() => { if (!controller.signal.aborted) setError(true) })
        return () => controller.abort()
    }, [id])

    if (error) return <p role="alert" className="text-sm text-red-700">No se pudo abrir el documento.</p>
    if (!documento) return <p className="text-sm text-slate-600">Cargando documento…</p>

    const bandeja = esGestor ? '/gestor/bandeja-validacion' : '/autonomo/buzon'
    return (
        <>
            <header className="mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-4">
                <div className="min-w-0">
                    <h2 className="flex items-center gap-2 font-semibold"><FileText aria-hidden="true" size={18} className="shrink-0" /><span className="truncate">{documento.nombreArchivo}</span></h2>
                    <p className="mt-1 text-xs text-slate-600">{documento.usuario}</p>
                </div>
                <EstadoBadge estado={documento.estado} />
                <Link to={`${bandeja}?documento=${id}`} className="text-sm font-semibold text-violet-700 underline">Ver documento</Link>
            </header>
            <ConversacionDocumento documentoId={id} />
        </>
    )
}
