import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { Loader2, X } from 'lucide-react'
import { useNumeroWhatsapp } from '../hooks/useNumeroWhatsapp'

// UT27 · «Sube desde WhatsApp» (Figma: Overlay_WhatsApp, nodo 1056:9408).
// El diseño muestra el estado final (número vinculado). Los estados previos (sin número, código pendiente, canal no
// disponible) reutilizan la misma cabecera, la misma tarjeta gris y el mismo botón.
const estilos = {
    fondo: 'fixed inset-0 z-50 flex items-end justify-center bg-[rgba(20,30,56,0.45)] p-0 sm:items-center sm:p-4',
    ventana: 'flex w-full max-w-[520px] flex-col gap-[18px] rounded-t-[24px] border border-[#dfdfdc] bg-white p-[30px] shadow-[0_24px_24px_rgba(15,23,42,0.18)] sm:rounded-[24px]',
    titulo: 'text-[22px] font-bold tracking-[-0.22px] text-[#141e38]',
    subtitulo: 'text-[14px] leading-5 text-[#64748b]',
    cerrar: 'flex size-9 shrink-0 items-center justify-center rounded-[10px] bg-[#eff2f4] text-[#141e38] hover:bg-[#e2e6ea]',
    tarjeta: 'flex w-full items-center gap-[18px] rounded-[16px] bg-[#eff2f4] p-[18px]',
    destacado: 'text-[20px] font-bold text-[#141e38]',
    texto: 'text-[13px] leading-[19px] text-[#64748b]',
    primario: 'inline-flex h-12 w-full items-center justify-center gap-2 rounded-[24px] bg-[#141e38] px-5 text-[14px] font-semibold text-white hover:bg-[#1f2c50] disabled:cursor-not-allowed disabled:opacity-50 sm:w-[243px]',
    enlace: 'font-semibold text-[#141e38] underline underline-offset-2 hover:text-[#2a3d6b] disabled:opacity-50',
    campo: 'h-11 w-full rounded-[12px] border border-[#dfdfdc] bg-white px-3 text-[15px] text-[#141e38] outline-none focus:border-[#141e38]',
}

type Props = { onCerrar: () => void }

export function WhatsappModal({ onCerrar }: Props) {
    const { estado, cargando, enviando, error, vincular, desvincular } = useNumeroWhatsapp()
    const [telefono, setTelefono] = useState('')
    const [cambiando, setCambiando] = useState(false)

    useEffect(() => {
        const alPulsar = (evento: KeyboardEvent) => { if (evento.key === 'Escape') onCerrar() }
        window.addEventListener('keydown', alPulsar)
        return () => window.removeEventListener('keydown', alPulsar)
    }, [onCerrar])

    async function enviar(evento: FormEvent) {
        evento.preventDefault()
        if (await vincular(telefono)) setCambiando(false)
    }

    const pendiente = estado?.verificacionPendiente
    const pedirNumero = estado?.canalHabilitado && (cambiando || (!estado.verificado && !pendiente))

    return (
        <div className={estilos.fondo} onMouseDown={(evento) => { if (evento.target === evento.currentTarget) onCerrar() }}>
            <section role="dialog" aria-modal="true" aria-labelledby="titulo-whatsapp" className={estilos.ventana}>
                <header className="flex w-full items-start gap-[18px]">
                    <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                        <h2 id="titulo-whatsapp" className={estilos.titulo}>Sube desde WhatsApp</h2>
                        <p className={estilos.subtitulo}>Envía fotos de tickets y facturas desde el móvil.</p>
                    </div>
                    <button type="button" className={estilos.cerrar} onClick={onCerrar} aria-label="Cerrar">
                        <X aria-hidden="true" className="size-[18px]" />
                    </button>
                </header>

                {cargando || !estado ? (
                    <div className={estilos.tarjeta}>
                        {cargando ? <Loader2 aria-hidden="true" className="size-5 animate-spin text-[#141e38]" /> : null}
                        <p className={estilos.texto}>{cargando ? 'Consultando tu número…' : 'No se ha podido cargar el canal de WhatsApp.'}</p>
                    </div>
                ) : !estado.canalHabilitado ? (
                    <div className={estilos.tarjeta}>
                        <p className={estilos.texto}>El canal de WhatsApp de GESTIO no está disponible ahora mismo. Puedes subir tus documentos desde esta página.</p>
                    </div>
                ) : pedirNumero ? (
                    <form className={`${estilos.tarjeta} flex-col items-stretch`} onSubmit={(evento) => void enviar(evento)}>
                        <div className="flex flex-col gap-1.5">
                            <p className={estilos.destacado}>{cambiando ? 'Cambia tu número' : 'Vincula tu móvil'}</p>
                            <p className={estilos.texto}>
                                Escribe tu número de WhatsApp con el prefijo del país. Te daremos un código para enviarlo desde ese móvil
                                y así comprobar que es tuyo.
                            </p>
                        </div>
                        <label className="flex flex-col gap-1.5">
                            <span className="text-[13px] font-semibold text-[#141e38]">Tu número de WhatsApp</span>
                            <input
                                className={estilos.campo}
                                type="tel"
                                inputMode="tel"
                                autoComplete="tel"
                                placeholder="+34 600 123 456"
                                autoFocus
                                value={telefono}
                                onChange={(evento) => setTelefono(evento.target.value)}
                                required
                            />
                        </label>
                        <div className="flex flex-wrap items-center gap-4">
                            <button type="submit" className={estilos.primario} disabled={enviando || telefono.trim() === ''}>
                                {enviando && <Loader2 aria-hidden="true" className="size-4 animate-spin" />} Obtener código
                            </button>
                            {cambiando && <button type="button" className={`${estilos.enlace} text-[13px]`} onClick={() => setCambiando(false)}>Cancelar</button>}
                        </div>
                    </form>
                ) : pendiente ? (
                    <>
                        <div className={estilos.tarjeta}>
                            <Qr base64={pendiente.qrCodigoPngBase64} alt="QR para enviar el código por WhatsApp" />
                            <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                                <p className={`${estilos.destacado} break-all font-mono tracking-wide`}>{pendiente.codigo}</p>
                                <p className={estilos.texto}>
                                    Escanea el código o envía este texto por WhatsApp desde <strong>{pendiente.telefono}</strong> al{' '}
                                    <strong>{estado.numeroCanal}</strong>. <CuentaAtras hasta={pendiente.caducaEn} />
                                </p>
                                <p className={`${estilos.texto} flex flex-wrap gap-x-3`}>
                                    {pendiente.enlaceCodigo && <a className={estilos.enlace} href={pendiente.enlaceCodigo} target="_blank" rel="noreferrer">Abrir WhatsApp</a>}
                                    <button type="button" className={estilos.enlace} onClick={() => setCambiando(true)}>Cambiar número</button>
                                </p>
                            </div>
                        </div>
                        <p role="status" className={`${estilos.texto} flex items-center gap-2`}>
                            <Loader2 aria-hidden="true" className="size-3.5 animate-spin" /> Esperando tu mensaje… Esta ventana se actualizará sola.
                        </p>
                    </>
                ) : (
                    <>
                        <div className={estilos.tarjeta}>
                            <Qr base64={estado.qrCanalPngBase64} alt="QR para abrir el chat de GESTIO en WhatsApp" />
                            <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                                <p className={estilos.destacado}>{estado.numeroCanal}</p>
                                <p className={estilos.texto}>Escanea el código o guarda el número como «Gestio». Cada foto que envíes llega a tu Buzón.</p>
                            </div>
                        </div>
                        <p className={estilos.texto}>
                            Vinculado con <strong>{estado.telefonoVerificado}</strong> ·{' '}
                            <button type="button" className={estilos.enlace} onClick={() => setCambiando(true)}>Cambiar</button> ·{' '}
                            <button type="button" className={estilos.enlace} disabled={enviando} onClick={() => void desvincular()}>Desvincular</button>
                        </p>
                    </>
                )}

                {error && <p role="alert" className="w-full rounded-[12px] border border-red-200 bg-red-50 p-3 text-[13px] text-red-700">{error}</p>}

                {!pedirNumero && <button type="button" className={estilos.primario} onClick={onCerrar}>Entendido</button>}
            </section>
        </div>
    )
}

// El QR lo genera el backend; si no hay (canal sin número), se deja el hueco para no mover la maqueta.
function Qr({ base64, alt }: { base64: string | null; alt: string }): ReactNode {
    return base64
        ? <img src={`data:image/png;base64,${base64}`} alt={alt} className="size-[88px] shrink-0 rounded-[6px] bg-white [image-rendering:pixelated]" />
        : <div aria-hidden="true" className="size-[88px] shrink-0 rounded-[6px] bg-white" />
}

function CuentaAtras({ hasta }: { hasta: string }) {
    const [ahora, setAhora] = useState(() => Date.now())
    useEffect(() => {
        const temporizador = window.setInterval(() => setAhora(Date.now()), 1000)
        return () => window.clearInterval(temporizador)
    }, [])

    const restante = Math.max(0, Math.floor((new Date(hasta).getTime() - ahora) / 1000))
    if (restante === 0) return <span>El código ha caducado: pide uno nuevo con «Cambiar número».</span>
    const minutos = Math.floor(restante / 60)
    const segundos = String(restante % 60).padStart(2, '0')
    return <span>Caduca en {minutos}:{segundos}.</span>
}
