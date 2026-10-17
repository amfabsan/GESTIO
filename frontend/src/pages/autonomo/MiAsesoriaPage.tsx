import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertCircle, Building2, CheckCircle2, Clock3, Link2, Loader2, MapPin, ShieldCheck, Users } from 'lucide-react'
import { apiClient, obtenerDetalleError } from '../../services/apiClient'
import type { EstadoVinculacion, Vinculacion } from '../../types/vinculaciones'

const estadoTexto: Record<EstadoVinculacion, string> = {
  PENDIENTE: 'Solicitud pendiente',
  ACTIVA: 'Vinculado',
  FINALIZADA: 'Finalizada',
  RECHAZADA: 'Rechazada',
}

function formatearFecha(fecha: string) {
  return new Intl.DateTimeFormat('es-ES', {
    dateStyle: 'medium',
  }).format(new Date(fecha))
}

function nombreAsesoria(codigo: string) {
  return `Asesoría ${codigo}`
}

export function MiAsesoriaPage() {
  const [vinculaciones, setVinculaciones] = useState<Vinculacion[]>([])
  const [codigo, setCodigo] = useState('')
  const [cargando, setCargando] = useState(true)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  const cargarVinculaciones = useCallback(async (signal?: AbortSignal) => {
    setCargando(true)
    setError(null)

    try {
      const respuesta = await apiClient.get<Vinculacion[]>('/vinculaciones', { signal })
      setVinculaciones(respuesta.data)
    } catch (excepcion) {
      if (signal?.aborted) return
      setError(obtenerDetalleError(excepcion) ?? 'No se han podido cargar tus vinculaciones.')
    } finally {
      if (!signal?.aborted) setCargando(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    // Ejecutar en una microtask para evitar setState síncrono dentro del efecto
    void Promise.resolve().then(() => cargarVinculaciones(controller.signal))
    return () => controller.abort()
  }, [cargarVinculaciones])

  const activa = useMemo(
    () => vinculaciones.find((vinculacion) => vinculacion.estado === 'ACTIVA'),
    [vinculaciones],
  )
  const pendientes = useMemo(
    () => vinculaciones.filter((vinculacion) => vinculacion.estado === 'PENDIENTE'),
    [vinculaciones],
  )
  const finalizadas = useMemo(
    () => vinculaciones.filter((vinculacion) => vinculacion.estado === 'FINALIZADA'),
    [vinculaciones],
  )

  async function solicitarVinculacion(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const codigoNormalizado = codigo.trim().toUpperCase()
    if (!codigoNormalizado) {
      setError('Introduce el código que te ha facilitado la asesoría.')
      return
    }

    setEnviando(true)
    setError(null)
    setMensaje(null)

    try {
      await apiClient.post('/vinculaciones', { codigoAsesoria: codigoNormalizado })
      setCodigo('')
      setMensaje('Solicitud enviada. La asesoría debe aceptarla para activar el acceso.')
      await cargarVinculaciones()
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido enviar la solicitud.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="min-h-[calc(100vh-3.5rem)] bg-[#f5f3ef] px-6 py-8 text-[#1b2a4a] sm:px-10 lg:px-24 lg:py-10">
      <section className="mx-auto max-w-5xl">
        <p className="mb-2 text-xs font-bold uppercase tracking-[0.08em] text-[#c86b1c]">Gestión de asesoría</p>
        <h1 className="font-serif text-3xl font-bold sm:text-4xl">Mi asesoría</h1>
        <p className="mt-2 text-sm text-[#647083]">Gestiona quién revisa y presenta tus impuestos.</p>

        {error && (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-[#f2c7bd] bg-[#fff5f2] p-4 text-sm text-[#9d3d2f]" role="alert">
            <AlertCircle className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}

        {mensaje && (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-[#b9e5c8] bg-[#f0fbf3] p-4 text-sm text-[#247344]" role="status">
            <CheckCircle2 className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
            <span>{mensaje}</span>
          </div>
        )}

        {cargando ? (
          <div className="mt-8 flex items-center justify-center rounded-2xl border border-[#dfdbd3] bg-white p-12 text-sm text-[#647083]" aria-busy="true">
            <Loader2 className="mr-2 size-5 animate-spin" aria-hidden="true" />
            Cargando tus vinculaciones...
          </div>
        ) : (
          <>
            <section className="mt-8 rounded-2xl border border-[#dfdbd3] bg-white p-5 shadow-sm sm:p-6" aria-label="Asesoría actual">
              {activa ? (
                <>
                  <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-start">
                    <div className="flex items-start gap-4">
                      <span className="grid size-12 shrink-0 place-items-center rounded-xl bg-[#1b2a4a] text-white" aria-hidden="true">
                        <Building2 className="size-6" />
                      </span>
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <h2 className="text-lg font-bold">{nombreAsesoria(activa.codigoAsesoria)}</h2>
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#e3f5e9] px-2.5 py-1 text-xs font-bold text-[#247344]">
                            <CheckCircle2 className="size-3.5" aria-hidden="true" />
                            {estadoTexto.ACTIVA}
                          </span>
                        </div>
                        <p className="mt-1 text-sm text-[#647083]">Vinculación activa desde {formatearFecha(activa.fechaAceptacion ?? activa.fechaSolicitud)}</p>
                      </div>
                    </div>
                    <div className="text-left sm:text-right">
                      <p className="text-xs text-[#647083]">Código de vinculación</p>
                      <p className="mt-1 font-mono text-sm font-bold">{activa.codigoAsesoria}</p>
                    </div>
                  </div>

                  <div className="mt-6 grid gap-4 border-t border-[#ece9e3] pt-5 text-sm text-[#647083] sm:grid-cols-3">
                    <p className="flex items-center gap-2"><ShieldCheck className="size-4 text-[#c86b1c]" aria-hidden="true" /> Tus documentos solo los ve tu gestor</p>
                    <p className="flex items-center gap-2"><Users className="size-4 text-[#c86b1c]" aria-hidden="true" /> Acceso autorizado por vinculación</p>
                    <p className="flex items-center gap-2"><MapPin className="size-4 text-[#c86b1c]" aria-hidden="true" /> Asesoría identificada por código</p>
                  </div>
                </>
              ) : (
                <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
                  <span className="grid size-12 shrink-0 place-items-center rounded-xl bg-[#fff0df] text-[#c86b1c]" aria-hidden="true"><Building2 className="size-6" /></span>
                  <div>
                    <h2 className="font-bold">No tienes una asesoría activa</h2>
                    <p className="mt-1 text-sm text-[#647083]">Solicita una vinculación con el código que te facilite tu asesoría.</p>
                  </div>
                </div>
              )}
            </section>

            <section className="mt-8 max-w-3xl" aria-labelledby="nueva-vinculacion">
              <h2 id="nueva-vinculacion" className="text-xs font-bold uppercase tracking-[0.08em] text-[#647083]">¿Vas a cambiar de asesoría?</h2>
              <p className="mt-2 text-sm text-[#647083]">Introduce el código de una asesoría dada de alta en Gestio y envíale una solicitud de vinculación.</p>
              <form className="mt-4 flex flex-col gap-3 sm:flex-row" onSubmit={solicitarVinculacion}>
                <label className="sr-only" htmlFor="codigo-asesoria">Código de asesoría</label>
                <div className="relative flex-1">
                  <Link2 className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[#7d8796]" aria-hidden="true" />
                  <input
                    id="codigo-asesoria"
                    className="w-full rounded-lg border border-[#d7d2c9] bg-white py-3 pl-10 pr-3 text-sm uppercase outline-none transition placeholder:normal-case placeholder:text-[#9aa1ac] focus:border-[#1b2a4a] focus:ring-2 focus:ring-[#dbe3f2]"
                    placeholder="Código de asesoría, por ejemplo ASE-000123"
                    value={codigo}
                    onChange={(evento) => setCodigo(evento.target.value)}
                    disabled={enviando}
                    maxLength={10}
                  />
                </div>
                <button className="inline-flex items-center justify-center gap-2 rounded-lg bg-[#1b2a4a] px-5 py-3 text-sm font-bold text-white transition hover:bg-[#2b3e66] disabled:cursor-not-allowed disabled:opacity-60" type="submit" disabled={enviando}>
                  {enviando && <Loader2 className="size-4 animate-spin" aria-hidden="true" />}
                  {enviando ? 'Enviando...' : 'Solicitar vinculación'}
                </button>
              </form>
            </section>

            {pendientes.length > 0 && (
              <section className="mt-6 space-y-3" aria-label="Solicitudes pendientes">
                {pendientes.map((vinculacion) => (
                  <article className="flex flex-col justify-between gap-4 rounded-2xl border border-[#eadcae] bg-[#fffdf2] p-5 sm:flex-row sm:items-center" key={vinculacion.id}>
                    <div>
                      <h3 className="font-bold">{nombreAsesoria(vinculacion.codigoAsesoria)}</h3>
                      <p className="mt-1 text-sm text-[#647083]">Solicitud enviada el {formatearFecha(vinculacion.fechaSolicitud)}</p>
                    </div>
                    <span className="inline-flex items-center gap-2 self-start rounded-full bg-[#fff0c2] px-3 py-2 text-xs font-bold text-[#986515] sm:self-auto">
                      <Clock3 className="size-4" aria-hidden="true" />
                      {estadoTexto.PENDIENTE}
                    </span>
                  </article>
                ))}
              </section>
            )}

            {finalizadas.length > 0 && (
              <section className="mt-8" aria-label="Historial de vinculaciones">
                <h2 className="text-xs font-bold uppercase tracking-[0.08em] text-[#647083]">Historial</h2>
                <div className="mt-3 space-y-3">
                  {finalizadas.map((vinculacion) => (
                    <article className="flex flex-col justify-between gap-2 rounded-xl border border-[#dfdbd3] bg-white p-4 text-sm sm:flex-row" key={vinculacion.id}>
                      <span className="font-semibold">{nombreAsesoria(vinculacion.codigoAsesoria)}</span>
                      <span className="text-[#647083]">Finalizada{vinculacion.fechaFinalizacion ? ` el ${formatearFecha(vinculacion.fechaFinalizacion)}` : ''}</span>
                    </article>
                  ))}
                </div>
              </section>
            )}
          </>
        )}
      </section>
    </div>
  )
}
