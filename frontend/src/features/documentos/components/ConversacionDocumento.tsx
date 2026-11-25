import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { Send } from 'lucide-react'
import { useAuth } from '../../../context/useAuth'
import { obtenerDetalleError } from '../../../services/apiClient'
import { formatearFechaHora } from '../../../utils/formato'
import { enviarMensaje, EVENTO_CONVERSACIONES, marcarMensajesLeidos, obtenerMensajes, type MensajeDocumento } from '../mensajesApi'

const LIMITE = 1000

export function AclaracionesDocumento({ documentoId, abrir = false }: { documentoId: string; abrir?: boolean }) {
    const [abierto, setAbierto] = useState(abrir)
    return (
        <details open={abierto} onToggle={evento => setAbierto(evento.currentTarget.open)} className="mt-3">
            <summary className="cursor-pointer text-sm font-semibold text-violet-700">Aclaraciones del documento</summary>
            {abierto && <div className="mt-3"><ConversacionDocumento documentoId={documentoId} /></div>}
        </details>
    )
}

export function ConversacionDocumento({ documentoId }: { documentoId: string }) {
    const { perfilActivo } = useAuth()
    return <Hilo key={`${perfilActivo?.id}/${documentoId}`} documentoId={documentoId} usuarioId={perfilActivo?.id} />
}

function Hilo({ documentoId, usuarioId }: { documentoId: string; usuarioId?: string }) {
    const [mensajes, setMensajes] = useState<MensajeDocumento[]>()
    const [texto, setTexto] = useState('')
    const [error, setError] = useState('')
    const [errorCarga, setErrorCarga] = useState('')
    const [enviando, setEnviando] = useState(false)
    const [version, setVersion] = useState(0)
    const lista = useRef<HTMLDivElement>(null)
    const campo = useId()
    const ultimo = mensajes?.at(-1)?.id

    useEffect(() => {
        const controller = new AbortController()
        const leidos = new Set<string>()
        let actualizando = false
        async function actualizar() {
            if (actualizando) return
            actualizando = true
            try {
                const datos = await obtenerMensajes(documentoId, controller.signal)
                if (controller.signal.aborted) return
                setMensajes(datos)
                setErrorCarga('')
                if (document.visibilityState !== 'visible') return
                const recibidos = datos.filter(m => m.autorId !== usuarioId && !leidos.has(m.id)).map(m => m.id)
                if (recibidos.length) {
                    // Si falla la confirmación de lectura, se reintenta en la siguiente consulta.
                    void marcarMensajesLeidos(documentoId, recibidos, controller.signal)
                        .then(() => recibidos.forEach(id => leidos.add(id)))
                        .catch(() => undefined)
                }
            } catch (excepcion) {
                if (!controller.signal.aborted) {
                    setMensajes(undefined)
                    setErrorCarga(obtenerDetalleError(excepcion) ?? 'No se pudo cargar la conversación.')
                }
            } finally {
                actualizando = false
            }
        }

        void actualizar()
        const intervalo = window.setInterval(actualizar, 5000)
        return () => {
            controller.abort()
            window.clearInterval(intervalo)
        }
    }, [documentoId, usuarioId, version])

    useEffect(() => {
        if (lista.current) lista.current.scrollTop = lista.current.scrollHeight
    }, [ultimo])

    async function enviar(evento: FormEvent<HTMLFormElement>) {
        evento.preventDefault()
        if (enviando) return
        if (!texto.trim() || texto.length > LIMITE) {
            setError('Escribe un mensaje de entre 1 y 1.000 caracteres.')
            return
        }

        setError('')
        setEnviando(true)
        try {
            const mensaje = await enviarMensaje(documentoId, texto)
            setMensajes(actuales => [...(actuales ?? []), mensaje])
            setTexto('')
            setVersion(actual => actual + 1)
            window.dispatchEvent(new Event(EVENTO_CONVERSACIONES))
        } catch (excepcion) {
            setError(obtenerDetalleError(excepcion) ?? 'No se pudo enviar el mensaje.')
        } finally {
            setEnviando(false)
        }
    }

    return (
        <section aria-label="Conversación del documento" className="min-w-0 space-y-4">
            <div ref={lista} role="log" aria-label="Mensajes" className="max-h-96 min-h-28 overflow-y-auto">
                {errorCarga && <p role="alert" className="text-sm text-red-700">{errorCarga}</p>}
                {!mensajes && !errorCarga && <p className="text-sm text-slate-500">Cargando mensajes…</p>}
                {mensajes?.length === 0 && <p className="text-sm text-slate-500">Todavía no hay mensajes sobre este documento.</p>}
                <ol className="flex flex-col gap-3">
                    {mensajes?.map(mensaje => {
                        const propio = mensaje.autorId === usuarioId
                        return (
                            <li key={mensaje.id} className={`max-w-[85%] ${propio ? 'self-end text-right' : 'self-start'}`}>
                                <p className={`whitespace-pre-wrap rounded-2xl px-3 py-2 text-left text-sm [overflow-wrap:anywhere] ${propio ? 'bg-violet-700 text-white' : 'bg-slate-100 text-slate-900'}`}>
                                    {mensaje.contenido}
                                </p>
                                <p className="mt-1 text-xs text-slate-600">
                                    {mensaje.autor} · <time dateTime={mensaje.fecha}>{formatearFechaHora(mensaje.fecha)}</time>
                                </p>
                            </li>
                        )
                    })}
                </ol>
            </div>
            {mensajes && <form onSubmit={enviar} className="space-y-1">
                <label htmlFor={campo} className="text-sm font-medium">Mensaje</label>
                <div className="flex flex-wrap items-end gap-2">
                    <textarea id={campo} rows={2} placeholder="Escribe un mensaje…" value={texto}
                        readOnly={enviando} aria-describedby={`${campo}-limite`} aria-invalid={Boolean(error)}
                        onChange={evento => { setTexto(evento.target.value); setError('') }}
                        className="min-w-40 flex-1 rounded-2xl border border-slate-300 bg-slate-50 px-3 py-2 text-sm focus:border-violet-600 focus:outline-2 focus:outline-violet-600" />
                    <button type="submit" disabled={enviando}
                        className="flex items-center gap-2 rounded-full bg-violet-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-60">
                        <Send aria-hidden="true" size={16} /> {enviando ? 'Enviando…' : 'Enviar'}
                    </button>
                </div>
                <p id={`${campo}-limite`} className={`text-xs ${texto.length > LIMITE ? 'text-red-700' : 'text-slate-600'}`}>
                    {texto.length} / 1.000 caracteres
                </p>
                {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
            </form>}
        </section>
    )
}
