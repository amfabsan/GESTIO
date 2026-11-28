import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertCircle, Check, Clock3, Loader2, UserRoundX } from 'lucide-react'
import { apiClient, obtenerDetalleError } from '../../services/apiClient'
import type { Vinculacion } from '../../types/vinculaciones'

function nombreCompleto(vinculacion: Vinculacion) {
  const autonomo = vinculacion.autonomo
  if (!autonomo) return 'Autónomo'
  return `${autonomo.nombre} ${autonomo.primerApellido} ${autonomo.segundoApellido}`
}

function iniciales(vinculacion: Vinculacion) {
  const autonomo = vinculacion.autonomo
  return autonomo ? `${autonomo.nombre[0] ?? ''}${autonomo.primerApellido[0] ?? ''}`.toUpperCase() : 'AU'
}

function formatearFecha(fecha: string) {
  return new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(fecha))
}

export function SolicitudesPage() {
  const [vinculaciones, setVinculaciones] = useState<Vinculacion[]>([])
  const [cargando, setCargando] = useState(true)
  const [procesandoId, setProcesandoId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  const cargarVinculaciones = useCallback(async (signal?: AbortSignal) => {
    setCargando(true)
    setError(null)

    try {
      const respuesta = await apiClient.get<Vinculacion[]>('/vinculaciones', { signal })
      setVinculaciones(respuesta.data)
    } catch (excepcion) {
      if (!signal?.aborted) {
        setError(obtenerDetalleError(excepcion) ?? 'No se han podido cargar las solicitudes.')
      }
    } finally {
      if (!signal?.aborted) setCargando(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void Promise.resolve().then(() => cargarVinculaciones(controller.signal))
    return () => controller.abort()
  }, [cargarVinculaciones])

  const pendientes = useMemo(
    () => vinculaciones
      .filter((vinculacion) => vinculacion.estado === 'PENDIENTE')
      .sort((a, b) => new Date(b.fechaSolicitud).getTime() - new Date(a.fechaSolicitud).getTime()),
    [vinculaciones],
  )

  async function gestionarSolicitud(vinculacion: Vinculacion, accion: 'aceptar' | 'rechazar') {
    setProcesandoId(vinculacion.id)
    setError(null)
    setMensaje(null)

    try {
      await apiClient.post(`/vinculaciones/${vinculacion.id}/${accion}`)
      setMensaje(accion === 'aceptar'
        ? `Solicitud de ${nombreCompleto(vinculacion)} aceptada.`
        : `Solicitud de ${nombreCompleto(vinculacion)} rechazada.`)
      await cargarVinculaciones()
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido actualizar la solicitud.')
    } finally {
      setProcesandoId(null)
    }
  }

  return (
    <div className="min-h-screen bg-fondo px-6 py-8 text-tinta sm:px-10 lg:px-9 lg:py-10">
      <section className="mx-auto max-w-5xl">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
          <div>
            <h1 className="text-3xl font-bold sm:text-[34px]">Solicitudes de vinculación</h1>
            <p className="mt-2 text-sm text-texto-suave">Autónomos que quieren que tu asesoría lleve su fiscalidad.</p>
          </div>
          <span className="inline-flex w-fit items-center rounded-full bg-acento px-3 py-1.5 text-xs font-bold text-white">
            {pendientes.length} {pendientes.length === 1 ? 'pendiente' : 'pendientes'}
          </span>
        </div>

        {error && (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-error-fondo bg-error-fondo p-4 text-sm text-error" role="alert">
            <AlertCircle className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}
        {mensaje && (
          <div className="mt-6 rounded-xl border border-ok-fondo bg-ok-fondo p-4 text-sm text-ok" role="status">{mensaje}</div>
        )}

        <section className="mt-6 overflow-hidden rounded-tarjeta bg-tarjeta shadow-tarjeta" aria-label="Solicitudes pendientes">
          {cargando ? (
            <div className="flex items-center justify-center gap-2 p-12 text-sm text-texto-suave" aria-busy="true">
              <Loader2 className="size-5 animate-spin" aria-hidden="true" />
              Cargando solicitudes...
            </div>
          ) : pendientes.length === 0 ? (
            <div className="flex flex-col items-center px-6 py-14 text-center">
              <span className="grid size-12 place-items-center rounded-full bg-fondo text-texto-suave" aria-hidden="true"><Check className="size-6" /></span>
              <h2 className="mt-4 font-semibold">No tienes solicitudes pendientes</h2>
              <p className="mt-1 max-w-md text-sm text-texto-suave">Cuando un autónomo solicite vincularse con tu asesoría, aparecerá aquí para que puedas revisarla.</p>
            </div>
          ) : (
            <ul className="divide-y divide-borde">
              {pendientes.map((vinculacion) => {
                const procesando = procesandoId === vinculacion.id
                return (
                  <li className="flex flex-col justify-between gap-5 p-4 sm:flex-row sm:items-center sm:p-5" key={vinculacion.id}>
                    <div className="flex min-w-0 items-center gap-3">
                      <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-fondo text-sm font-bold text-tinta" aria-hidden="true">
                        {iniciales(vinculacion)}
                      </span>
                      <div className="min-w-0">
                        <h2 className="truncate text-sm font-bold">{nombreCompleto(vinculacion)}</h2>
                        <p className="mt-0.5 text-xs text-texto-suave">
                          NIF {vinculacion.autonomo?.nif ?? 'No disponible'} · Código {vinculacion.codigoAsesoria}
                        </p>
                        <p className="mt-1 flex items-center gap-1 text-xs text-texto-suave">
                          <Clock3 className="size-3.5" aria-hidden="true" />
                          Solicitado {formatearFecha(vinculacion.fechaSolicitud)}
                        </p>
                      </div>
                    </div>
                    <div className="flex shrink-0 items-center gap-2 pl-14 sm:pl-0">
                      <button
                        className="inline-flex items-center justify-center gap-2 rounded-control bg-fondo px-3 py-2.5 text-xs font-bold text-tinta transition hover:bg-borde disabled:cursor-not-allowed disabled:opacity-60"
                        type="button"
                        onClick={() => void gestionarSolicitud(vinculacion, 'rechazar')}
                        disabled={procesandoId !== null}
                        aria-label={`Rechazar solicitud de ${nombreCompleto(vinculacion)}`}
                      >
                        {procesando && <Loader2 className="size-4 animate-spin" aria-hidden="true" />}
                        {!procesando && <UserRoundX className="size-4" aria-hidden="true" />}
                        Rechazar
                      </button>
                      <button
                        className="inline-flex items-center justify-center gap-2 rounded-control bg-tinta px-3.5 py-2.5 text-xs font-bold text-white transition hover:bg-tinta-hover disabled:cursor-not-allowed disabled:opacity-60"
                        type="button"
                        onClick={() => void gestionarSolicitud(vinculacion, 'aceptar')}
                        disabled={procesandoId !== null}
                        aria-label={`Aceptar solicitud de ${nombreCompleto(vinculacion)}`}
                      >
                        {procesando && <Loader2 className="size-4 animate-spin" aria-hidden="true" />}
                        {!procesando && <Check className="size-4" aria-hidden="true" />}
                        Aceptar
                      </button>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </section>
      </section>
    </div>
  )
}
