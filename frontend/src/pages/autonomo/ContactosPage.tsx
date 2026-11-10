import { useCallback, useEffect, useMemo, useState } from 'react'
import { Pencil, Plus, Power, Search, Trash2, UserRound, X } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import { useAuth } from '../../context/useAuth'
import { cambiarEstadoContacto, crearContacto, editarContacto, eliminarContacto, listarContactos, type Contacto, type GuardarContacto } from '../../features/contactos/contactosApi'
import { FormularioContacto } from '../../features/contactos/components/FormularioContacto'
import { obtenerDetalleError } from '../../services/apiClient'

type Filtro = 'TODOS' | 'CLIENTE' | 'PROVEEDOR' | 'INACTIVOS'
type Confirmacion = { contacto: Contacto; accion: 'activar' | 'desactivar' | 'eliminar' }

const etiquetaTipo = (clasificacion: Contacto['clasificacion']) => ({
  CLIENTE: 'Cliente',
  PROVEEDOR: 'Proveedor',
  AMBOS: 'Cliente y proveedor',
})[clasificacion]

const claseTipo = (clasificacion: Contacto['clasificacion']) => ({
  CLIENTE: 'bg-blue-100 text-blue-800',
  PROVEEDOR: 'bg-purple-100 text-purple-800',
  AMBOS: 'bg-teal-100 text-teal-800',
})[clasificacion]

const normalizarBusqueda = (valor: string) => valor.toLowerCase().replace(/[\s-]/g, '')

export function ContactosPage() {
  const { perfilActivo } = useAuth()
  const autonomoId = perfilActivo?.id
  const [contactos, setContactos] = useState<Contacto[]>([])
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<string | null>(null)
  const [termino, setTermino] = useState('')
  const [filtro, setFiltro] = useState<Filtro>('TODOS')
  const [formulario, setFormulario] = useState<Contacto | 'nuevo' | null>(null)
  const [confirmacion, setConfirmacion] = useState<Confirmacion | null>(null)
  const [errorFormulario, setErrorFormulario] = useState<string | null>(null)
  const [errorConfirmacion, setErrorConfirmacion] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)
  const [accionando, setAccionando] = useState(false)

  const cargar = useCallback(async (mostrarCarga = true) => {
    if (!autonomoId) return
    if (mostrarCarga) setCargando(true)
    setErrorCarga(null)
    try {
      setContactos(await listarContactos(autonomoId))
    } catch (error) {
      setErrorCarga(obtenerDetalleError(error) ?? 'No se ha podido cargar la agenda. Inténtalo de nuevo.')
    } finally {
      setCargando(false)
    }
  }, [autonomoId])

  useEffect(() => {
    let vigente = true
    if (autonomoId) {
      listarContactos(autonomoId)
        .then(datos => { if (vigente) setContactos(datos) })
        .catch(error => { if (vigente) setErrorCarga(obtenerDetalleError(error) ?? 'No se ha podido cargar la agenda. Inténtalo de nuevo.') })
        .finally(() => { if (vigente) setCargando(false) })
    }
    return () => { vigente = false }
  }, [autonomoId])

  const visibles = useMemo(() => {
    const busqueda = normalizarBusqueda(termino.trim())
    return contactos.filter(contacto => {
      const coincideTexto = !busqueda || contacto.nombre.toLowerCase().includes(termino.trim().toLowerCase()) || normalizarBusqueda(contacto.nif).includes(busqueda)
      const coincideFiltro = filtro === 'TODOS'
        || (filtro === 'INACTIVOS' && !contacto.activo)
        || (filtro === 'CLIENTE' && contacto.activo && (contacto.clasificacion === 'CLIENTE' || contacto.clasificacion === 'AMBOS'))
        || (filtro === 'PROVEEDOR' && contacto.activo && (contacto.clasificacion === 'PROVEEDOR' || contacto.clasificacion === 'AMBOS'))
      return coincideTexto && coincideFiltro
    })
  }, [contactos, filtro, termino])

  const clientes = contactos.filter(c => c.activo && (c.clasificacion === 'CLIENTE' || c.clasificacion === 'AMBOS')).length
  const proveedores = contactos.filter(c => c.activo && (c.clasificacion === 'PROVEEDOR' || c.clasificacion === 'AMBOS')).length
  const inactivos = contactos.filter(c => !c.activo).length

  const guardar = async (datos: GuardarContacto) => {
    if (!autonomoId) return
    setGuardando(true)
    setErrorFormulario(null)
    try {
      if (formulario && formulario !== 'nuevo') await editarContacto(autonomoId, formulario.id, datos)
      else await crearContacto(autonomoId, datos)
      setFormulario(null)
      await cargar(false)
    } catch (error) {
      setErrorFormulario(obtenerDetalleError(error) ?? 'No se han podido guardar los cambios. Inténtalo de nuevo.')
    } finally {
      setGuardando(false)
    }
  }

  const ejecutarConfirmacion = async () => {
    if (!autonomoId || !confirmacion) return
    setAccionando(true)
    setErrorConfirmacion(null)
    try {
      if (confirmacion.accion === 'eliminar') await eliminarContacto(autonomoId, confirmacion.contacto.id)
      else await cambiarEstadoContacto(autonomoId, confirmacion.contacto.id, confirmacion.accion === 'activar')
      setConfirmacion(null)
      await cargar(false)
    } catch (error) {
      setErrorConfirmacion(obtenerDetalleError(error) ?? 'No se ha podido completar la operación. Inténtalo de nuevo.')
    } finally {
      setAccionando(false)
    }
  }

  const abrirFormulario = (contacto: Contacto | 'nuevo') => {
    setErrorFormulario(null)
    setFormulario(contacto)
  }

  const abrirConfirmacion = (contacto: Contacto, accion: Confirmacion['accion']) => {
    setErrorConfirmacion(null)
    setConfirmacion({ contacto, accion })
  }

  const filtroBoton = (valor: Filtro, titulo: string, cantidad: number) => (
    <button type="button" aria-pressed={filtro === valor} onClick={() => setFiltro(valor)} className={`whitespace-nowrap rounded-full border px-4 py-2 text-sm transition ${filtro === valor ? 'border-[#1d2b4f] bg-[#1d2b4f] font-semibold text-white' : 'border-[#ded9cf] bg-white text-[#4c4a43] hover:bg-[#f5f3ef]'}`}>
      {titulo} ({cantidad})
    </button>
  )

  return (
    <section className="space-y-5">
      <PageHeader
        titulo="Contactos"
        subtitulo="Tu agenda de clientes y proveedores para reutilizarla al crear facturas"
        derecha={<button type="button" onClick={() => abrirFormulario('nuevo')} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#1d2b4f] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#2b3b62]"><Plus size={17} aria-hidden="true" />Nuevo contacto</button>}
      />

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <label className="relative block w-full sm:max-w-[340px]">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[#857f70]" aria-hidden="true" />
          <input className="min-h-11 w-full rounded-lg border border-[#ded9cf] bg-white py-2 pl-10 pr-3 text-sm outline-none focus:border-[#435271] focus:ring-2 focus:ring-[#435271]/15" placeholder="Buscar por nombre o NIF" value={termino} onChange={evento => setTermino(evento.target.value)} aria-label="Buscar por nombre o NIF" />
        </label>
        <div className="flex gap-2 overflow-x-auto pb-1" aria-label="Filtrar contactos">
          {filtroBoton('TODOS', 'Todos', contactos.length)}
          {filtroBoton('CLIENTE', 'Clientes', clientes)}
          {filtroBoton('PROVEEDOR', 'Proveedores', proveedores)}
          {filtroBoton('INACTIVOS', 'Inactivos', inactivos)}
        </div>
      </div>

      {errorCarga && <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"><span>{errorCarga}</span><button type="button" onClick={() => void cargar()} className="font-semibold underline">Reintentar</button></div>}
      {cargando ? (
        <div className="rounded-xl border border-[#e1ddd4] bg-white py-16 text-center text-sm text-[#6b6656]" role="status">Cargando contactos…</div>
      ) : !errorCarga && contactos.length === 0 ? (
        <div className="rounded-xl border border-[#e1ddd4] bg-white px-5 py-14 text-center">
          <UserRound className="mx-auto size-9 text-[#9a9588]" aria-hidden="true" />
          <h2 className="mt-3 font-semibold text-[#1d2b4f]">Aún no tienes contactos</h2>
          <p className="mt-1 text-sm text-[#6b6656]">Añade tu primer cliente o proveedor para tener sus datos a mano.</p>
          <button type="button" className="mt-4 rounded-lg bg-[#1d2b4f] px-4 py-2 text-sm font-semibold text-white" onClick={() => abrirFormulario('nuevo')}>Crear contacto</button>
        </div>
      ) : !errorCarga && visibles.length === 0 ? (
        <div className="rounded-xl border border-[#e1ddd4] bg-white px-5 py-14 text-center text-sm text-[#6b6656]">No hay contactos que coincidan con la búsqueda o el filtro.</div>
      ) : !errorCarga && (
        <div className="overflow-hidden rounded-xl border border-[#e1ddd4] bg-white">
          <div className="hidden grid-cols-[minmax(240px,1fr)_100px_160px_auto] gap-4 border-b border-[#eeeae2] px-5 py-3 text-xs font-medium uppercase tracking-wide text-[#6b6656] md:grid">
            <span>Contacto</span><span>NIF</span><span>Tipo</span><span className="text-right">Acciones</span>
          </div>
          <ul className="divide-y divide-[#eeeae2]">
            {visibles.map(contacto => (
              <li key={contacto.id} className={`flex flex-col gap-3 px-4 py-4 sm:px-5 md:grid md:grid-cols-[minmax(240px,1fr)_100px_160px_auto] md:items-center md:gap-4 ${contacto.activo ? '' : 'bg-[#f7f6f3]'}`}>
                <div className="flex min-w-0 items-center gap-3">
                  <span className={`grid size-10 shrink-0 place-items-center rounded-xl ${contacto.activo ? 'bg-[#f0ede5] text-[#344362]' : 'bg-[#ebe8df] text-[#aaa597]'}`}><UserRound size={18} aria-hidden="true" /></span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold text-[#202b43]">{contacto.nombre}{!contacto.activo && <span className="ml-2 rounded bg-[#e5e9ef] px-1.5 py-0.5 align-middle text-[10px] font-bold text-[#58647b]">INACTIVO</span>}</p>
                    <p className="mt-0.5 truncate text-xs text-[#6b6656]">{[contacto.email, contacto.telefono].filter(Boolean).join(' · ') || 'Sin datos de contacto'}{contacto.direccion ? ` · ${contacto.direccion}` : ''}</p>
                  </div>
                </div>
                <span className="pl-[52px] text-sm text-[#343b49] md:pl-0">{contacto.nif}</span>
                <span className={`ml-[52px] w-fit rounded-full px-2.5 py-1 text-xs font-medium md:ml-0 ${claseTipo(contacto.clasificacion)}`}>{etiquetaTipo(contacto.clasificacion)}</span>
                <div className="flex items-center justify-end gap-1 border-t border-[#eeeae2] pt-2 md:border-0 md:pt-0">
                  <button type="button" aria-label={`Editar ${contacto.nombre}`} title="Editar" disabled={accionando || guardando} onClick={() => abrirFormulario(contacto)} className="rounded-lg p-2 text-[#555348] hover:bg-[#f0ede5] disabled:opacity-40"><Pencil size={17} /></button>
                  <button type="button" aria-label={`${contacto.activo ? 'Desactivar' : 'Reactivar'} ${contacto.nombre}`} title={contacto.activo ? 'Desactivar' : 'Reactivar'} disabled={accionando || guardando} onClick={() => abrirConfirmacion(contacto, contacto.activo ? 'desactivar' : 'activar')} className={`rounded-lg p-2 hover:bg-[#f0ede5] disabled:opacity-40 ${contacto.activo ? 'text-[#555348]' : 'text-green-700'}`}><Power size={18} /></button>
                  <button type="button" aria-label={`Eliminar ${contacto.nombre}`} title="Eliminar" disabled={accionando || guardando} onClick={() => abrirConfirmacion(contacto, 'eliminar')} className="rounded-lg p-2 text-red-600 hover:bg-red-50 disabled:opacity-40"><Trash2 size={17} /></button>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="text-xs leading-relaxed text-[#6b6656]">ⓘ Solo tú accedes a tu agenda. Los contactos inactivos no estarán disponibles para nuevas operaciones. La selección de contactos al emitir facturas queda pendiente de la implementación de PB-21.</p>

      {formulario && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-[#101827]/50 p-0 sm:items-center sm:p-4" onMouseDown={evento => { if (evento.target === evento.currentTarget && !guardando) setFormulario(null) }}>
          <section role="dialog" aria-modal="true" aria-labelledby="titulo-formulario-contacto" className="max-h-[95vh] w-full overflow-y-auto rounded-t-2xl bg-[#fbfaf7] p-5 shadow-2xl sm:max-w-xl sm:rounded-2xl sm:p-7">
            <div className="mb-5 flex items-start justify-between gap-4">
              <div><h2 id="titulo-formulario-contacto" className="font-serif text-2xl text-[#1d2b4f]">{formulario === 'nuevo' ? 'Nuevo contacto' : 'Editar contacto'}</h2><p className="mt-1 text-sm text-[#6b6656]">Los datos marcados con * son obligatorios.</p></div>
              <button type="button" className="rounded-lg p-2 text-[#6b6656] hover:bg-[#efede6]" aria-label="Cerrar" disabled={guardando} onClick={() => setFormulario(null)}><X size={20} /></button>
            </div>
            <FormularioContacto key={formulario === 'nuevo' ? 'nuevo' : formulario.id} contacto={formulario === 'nuevo' ? undefined : formulario} enviando={guardando} error={errorFormulario} onCancelar={() => setFormulario(null)} onGuardar={datos => void guardar(datos)} />
          </section>
        </div>
      )}

      {confirmacion && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-[#101827]/50 p-0 sm:items-center sm:p-4">
          <section role="alertdialog" aria-modal="true" aria-labelledby="titulo-confirmacion-contacto" className="w-full rounded-t-2xl bg-[#fbfaf7] p-5 shadow-2xl sm:max-w-md sm:rounded-2xl sm:p-6">
            <h2 id="titulo-confirmacion-contacto" className="font-serif text-xl text-[#1d2b4f]">{confirmacion.accion === 'eliminar' ? 'Eliminar contacto' : confirmacion.accion === 'activar' ? 'Reactivar contacto' : 'Desactivar contacto'}</h2>
            <p className="mt-2 text-sm leading-relaxed text-[#555348]">{confirmacion.accion === 'eliminar'
              ? `¿Quieres eliminar definitivamente a ${confirmacion.contacto.nombre}? Si se utiliza en una factura, no podrá eliminarse; podrás desactivarlo en su lugar.`
              : confirmacion.accion === 'activar'
                ? `¿Quieres reactivar a ${confirmacion.contacto.nombre} para permitir su uso en nuevas operaciones?`
                : `¿Quieres desactivar a ${confirmacion.contacto.nombre}? Se conservará en la agenda, pero no estará disponible para nuevas operaciones.`}</p>
            {errorConfirmacion && <p role="alert" className="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-800">{errorConfirmacion}</p>}
            <div className="mt-5 flex flex-col-reverse justify-end gap-2 sm:flex-row">
              <button type="button" disabled={accionando} onClick={() => setConfirmacion(null)} className="rounded-lg border border-[#d9d4c9] px-4 py-2.5 text-sm font-medium disabled:opacity-60">Cancelar</button>
              <button type="button" disabled={accionando} onClick={() => void ejecutarConfirmacion()} className={`rounded-lg px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-60 ${confirmacion.accion === 'eliminar' ? 'bg-red-700 hover:bg-red-800' : 'bg-[#1d2b4f] hover:bg-[#2b3b62]'}`}>{accionando ? 'Procesando…' : confirmacion.accion === 'eliminar' ? 'Eliminar' : confirmacion.accion === 'activar' ? 'Reactivar' : 'Desactivar'}</button>
            </div>
          </section>
        </div>
      )}
    </section>
  )
}
