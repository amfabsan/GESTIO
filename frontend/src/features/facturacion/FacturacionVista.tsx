import { useCallback, useEffect, useMemo, useState } from 'react'
import { Eye, FilePlus2, Pencil, Trash2 } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import { btnPrimario, btnSecundario, card } from '../../components/ui/estilos'
import { listarContactos, type Contacto } from '../contactos/contactosApi'
import { obtenerDetalleError } from '../../services/apiClient'
import { formatearEuros, formatearFecha } from '../../utils/formato'
import { FacturaFormulario } from './FacturaFormulario'
import { FacturaDetalleModal } from './FacturaDetalleModal'
import { comprobarFactura, eliminarBorrador, emitirFactura, guardarBorrador, guardarDireccionFiscal, guardarDireccionFiscalEmisor, listarFacturas, obtenerDireccionFiscalEmisor, obtenerFactura, type Factura, type FacturaLista, type GuardarFactura } from './facturacionApi'
import { listarRegistros, type RegistroFacturacion } from './registrosApi'
import { RegistroVerifactuVista } from './RegistroVerifactuVista'

type Props = { autonomoId: string; soloLectura?: boolean }
type Pestaña = 'FORMULARIO' | 'BORRADORES' | 'EMITIDAS' | 'REGISTRO'
type Modal = { factura: Factura; modo: 'revision' | 'emitida' | 'consulta'; error?: string | null }

export function FacturacionVista({ autonomoId, soloLectura = false }: Props) {
  const [facturas, setFacturas] = useState<FacturaLista[]>([])
  const [registros, setRegistros] = useState<RegistroFacturacion[]>([])
  const [errorRegistros, setErrorRegistros] = useState<string | null>(null)
  const [contactos, setContactos] = useState<Contacto[]>([])
  const [direccionFiscalEmisor, setDireccionFiscalEmisor] = useState('')
  const [facturaEditada, setFacturaEditada] = useState<Factura | null>(null)
  const [reinicioFormulario, setReinicioFormulario] = useState(0)
  const [pestaña, setPestaña] = useState<Pestaña>(soloLectura ? 'EMITIDAS' : 'FORMULARIO')
  const [modal, setModal] = useState<Modal | null>(null)
  const [eliminar, setEliminar] = useState<FacturaLista | null>(null)
  const [cargando, setCargando] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorEliminar, setErrorEliminar] = useState<string | null>(null)

  const cargar = useCallback(async () => {
    setCargando(true)
    setError(null)
    try {
      // Los registros se piden aparte: si su endpoint falla, las pestañas de facturas deben seguir funcionando.
      const cadena = listarRegistros(autonomoId).then(
        datos => { setRegistros(datos); setErrorRegistros(null) },
        excepcion => { setRegistros([]); setErrorRegistros(obtenerDetalleError(excepcion) ?? 'No se han podido cargar los registros de facturación.') })
      const [lista, agenda, direccion] = await Promise.all([listarFacturas(autonomoId), listarContactos(autonomoId), soloLectura ? Promise.resolve('') : obtenerDireccionFiscalEmisor(autonomoId)])
      await cadena
      setFacturas(lista)
      setContactos(agenda)
      setDireccionFiscalEmisor(direccion)
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido cargar la facturación.')
    } finally {
      setCargando(false)
    }
  }, [autonomoId, soloLectura])

  useEffect(() => { void cargar() }, [cargar])

  const borradores = useMemo(() => facturas.filter(factura => factura.estado === 'BORRADOR'), [facturas])
  const emitidas = useMemo(() => facturas.filter(factura => factura.estado === 'EMITIDA'), [facturas])

  const nuevaFactura = () => {
    setFacturaEditada(null)
    setReinicioFormulario(actual => actual + 1)
    setError(null)
    setPestaña('FORMULARIO')
  }

  const guardar = async (datos: GuardarFactura): Promise<Factura | null> => {
    setGuardando(true)
    setError(null)
    try {
      if (direccionFiscalEmisor.trim()) {
        const guardada = await guardarDireccionFiscalEmisor(autonomoId, direccionFiscalEmisor)
        setDireccionFiscalEmisor(guardada)
      }
      const guardada = await guardarBorrador(autonomoId, datos, facturaEditada?.id)
      setFacturaEditada(guardada)
      await cargar()
      return guardada
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido guardar el borrador.')
      return null
    } finally {
      setGuardando(false)
    }
  }

  const revisar = async (borrador: Factura) => {
    setGuardando(true)
    setError(null)
    try {
      const comprobada = await comprobarFactura(autonomoId, borrador.id)
      setModal({ factura: comprobada, modo: 'revision' })
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'Corrige los errores antes de emitir.')
    } finally {
      setGuardando(false)
    }
  }

  const confirmarEmision = async () => {
    if (!modal) return
    setGuardando(true)
    setModal(actual => actual ? { ...actual, error: null } : actual)
    try {
      const emitida = await emitirFactura(autonomoId, modal.factura.id)
      setModal({ factura: emitida, modo: 'emitida' })
      await cargar()
    } catch (excepcion) {
      setModal(actual => actual ? { ...actual, error: obtenerDetalleError(excepcion) ?? 'No se ha podido emitir la factura.' } : actual)
    } finally {
      setGuardando(false)
    }
  }

  const consultar = async (facturaId: string) => {
    setGuardando(true)
    try {
      const detalle = await obtenerFactura(autonomoId, facturaId)
      setModal({ factura: detalle, modo: 'consulta' })
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido consultar la factura.')
    } finally {
      setGuardando(false)
    }
  }

  const editarBorrador = async (facturaId: string) => {
    setGuardando(true)
    setError(null)
    try {
      setFacturaEditada(await obtenerFactura(autonomoId, facturaId))
      setModal(null)
      setPestaña('FORMULARIO')
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido abrir el borrador.')
    } finally {
      setGuardando(false)
    }
  }

  const guardarDireccion = async (contactoId: string, direccion: string) => {
    try {
      await guardarDireccionFiscal(autonomoId, contactoId, direccion)
      setContactos(actuales => actuales.map(contacto => contacto.id === contactoId ? { ...contacto, direccion } : contacto))
      setError(null)
    } catch (excepcion) {
      throw new Error(obtenerDetalleError(excepcion) ?? 'No se ha podido guardar la dirección fiscal.', { cause: excepcion })
    }
  }

  const confirmarEliminacion = async () => {
    if (!eliminar) return
    setErrorEliminar(null)
    try {
      await eliminarBorrador(autonomoId, eliminar.id)
      setEliminar(null)
      await cargar()
    } catch (excepcion) {
      setErrorEliminar(obtenerDetalleError(excepcion) ?? 'No se ha podido eliminar el borrador.')
    }
  }

  const irAEdicionDesdeModal = () => {
    if (modal?.factura.estado === 'BORRADOR') void editarBorrador(modal.factura.id)
  }

  const pestañas: { clave: Pestaña; titulo: string; contador?: number }[] = [
    { clave: 'FORMULARIO', titulo: facturaEditada ? 'Editar borrador' : 'Nueva factura' },
    { clave: 'BORRADORES', titulo: 'Borradores', contador: borradores.length },
    { clave: 'EMITIDAS', titulo: 'Emitidas', contador: emitidas.length },
    { clave: 'REGISTRO', titulo: 'Registro Verifactu', contador: registros.length },
  ]
  const pestañasLectura: { clave: Pestaña; titulo: string; contador: number }[] = [
    { clave: 'BORRADORES', titulo: 'Borradores', contador: borradores.length },
    { clave: 'EMITIDAS', titulo: 'Emitidas', contador: emitidas.length },
    { clave: 'REGISTRO', titulo: 'Registro Verifactu', contador: registros.length },
  ]

  return (
    <section className="min-h-screen bg-fondo px-4 py-6 text-tinta sm:px-8 lg:px-9" aria-labelledby="titulo-facturacion">
      <PageHeader titulo="Facturación" subtitulo={soloLectura ? 'Consulta de facturas del autónomo vinculado.' : 'Crea, revisa y guarda tus facturas.'} derecha={!soloLectura ? <button className={btnPrimario} type="button" onClick={nuevaFactura}><FilePlus2 size={16} />Nueva factura</button> : undefined} />
      {!soloLectura && <nav className="mt-5 flex gap-5 border-b border-borde" aria-label="Secciones de facturación">
        {pestañas.map(item => <button key={item.clave} type="button" onClick={() => setPestaña(item.clave)} aria-current={pestaña === item.clave ? 'page' : undefined} className={`-mb-px border-b-2 px-1 pb-3 text-sm ${pestaña === item.clave ? 'border-acento font-semibold text-tinta' : 'border-transparent text-texto-suave hover:text-tinta'}`}>{item.titulo}{item.contador !== undefined && <span className="ml-1">({item.contador})</span>}</button>)}
      </nav>}
      {soloLectura && <nav className="mt-5 flex gap-5 border-b border-borde" aria-label="Facturas consultables">
        {pestañasLectura.map(item => <button key={item.clave} type="button" onClick={() => setPestaña(item.clave)} aria-current={pestaña === item.clave ? 'page' : undefined} className={`-mb-px border-b-2 px-1 pb-3 text-sm ${pestaña === item.clave ? 'border-acento font-semibold text-tinta' : 'border-transparent text-texto-suave'}`}>{item.titulo} ({item.contador})</button>)}
      </nav>}
      {error && <p role="alert" className="mt-4 rounded-xl border border-red-300 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
      {cargando ? <p className="py-10 text-sm text-texto-suave" role="status">Cargando facturas…</p> : <>
        {!soloLectura && pestaña === 'FORMULARIO' && <div className="mt-4"><FacturaFormulario key={`${facturaEditada?.id ?? 'nueva'}-${reinicioFormulario}`} factura={facturaEditada} contactos={contactos} direccionFiscalEmisor={direccionFiscalEmisor} onDireccionFiscalEmisorChange={setDireccionFiscalEmisor} guardando={guardando} error={null} onGuardar={guardar} onGuardarDireccion={guardarDireccion} onRevisar={borrador => void revisar(borrador)} /></div>}
        {pestaña === 'BORRADORES' && <div className="mt-5">{borradores.length === 0 ? <Vacio texto="No hay borradores guardados." /> : <div className="space-y-3">{borradores.map(factura => <article key={factura.id} className={`${card} grid gap-3 p-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center`}>
          <div><p className="font-semibold">{factura.clienteNombre || 'Cliente sin seleccionar'}</p><p className="mt-1 text-sm text-texto-suave">{formatearFecha(factura.fecha)} · {factura.numeroLineas} líneas · {factura.total === null ? 'Importe pendiente' : formatearEuros(factura.total)}</p><p className="mt-1 text-xs font-medium text-aviso">Sin número asignado</p></div>
          <div className="flex flex-wrap gap-2"><button className={btnSecundario} type="button" onClick={() => void consultar(factura.id)}><Eye size={15} />Vista previa</button>{!soloLectura && <><button className={btnSecundario} type="button" onClick={() => void editarBorrador(factura.id)}><Pencil size={15} />Editar</button><button className="inline-flex items-center gap-2 rounded-xl border border-red-200 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-50" type="button" onClick={() => { setEliminar(factura); setErrorEliminar(null) }}><Trash2 size={15} />Eliminar</button></>}</div>
        </article>)}</div>}</div>}
        {pestaña === 'EMITIDAS' && <div className="mt-5">{emitidas.length === 0 ? <Vacio texto="No hay facturas emitidas." /> : <div className={`${card} overflow-hidden`}><div className="overflow-x-auto"><table className="w-full min-w-[700px] text-left text-sm"><thead className="bg-fondo text-xs uppercase text-texto-suave"><tr><th className="px-4 py-3">Número</th><th className="px-4 py-3">Fecha</th><th className="px-4 py-3">Cliente</th><th className="px-4 py-3 text-right">Base</th><th className="px-4 py-3 text-right">Total</th><th className="px-4 py-3">Acción</th></tr></thead><tbody>{emitidas.map(factura => <tr key={factura.id} className="border-t border-fondo"><td className="px-4 py-3 font-semibold">{factura.numeroVisible}</td><td className="px-4 py-3">{formatearFecha(factura.fecha)}</td><td className="px-4 py-3"><p>{factura.clienteNombre || '—'}</p><p className="text-xs text-texto-suave">NIF {factura.clienteNif || '—'}{factura.clienteDireccion ? ` · ${factura.clienteDireccion}` : ''}</p></td><td className="px-4 py-3 text-right">{factura.baseImponible === null ? '—' : formatearEuros(factura.baseImponible)}</td><td className="px-4 py-3 text-right font-semibold">{factura.total === null ? '—' : formatearEuros(factura.total)}</td><td className="px-4 py-3"><button className={btnSecundario} type="button" onClick={() => void consultar(factura.id)}><Eye size={15} />Consultar</button></td></tr>)}</tbody></table></div></div>}</div>}
        {pestaña === 'REGISTRO' && (errorRegistros
          ? <p role="alert" className="mt-5 rounded-xl border border-red-300 bg-red-50 p-3 text-sm text-red-800">{errorRegistros}</p>
          : <RegistroVerifactuVista autonomoId={autonomoId} registros={registros} />)}
      </>}
      {modal && <FacturaDetalleModal factura={modal.factura} modo={modal.modo} cargando={guardando} error={modal.error} onVolver={() => setModal(null)} onConfirmar={() => void confirmarEmision()} onCerrar={() => setModal(null)} onEditar={irAEdicionDesdeModal} onConsultar={() => setModal(actual => actual ? { ...actual, modo: 'consulta' } : actual)} />}
      {eliminar && <div className="fixed inset-0 z-50 flex items-center justify-center bg-tinta/55 p-4"><section role="alertdialog" aria-modal="true" aria-labelledby="titulo-eliminar-factura" className="w-full max-w-md rounded-2xl bg-fondo p-6 shadow-2xl"><h2 id="titulo-eliminar-factura" className="text-xl font-bold">Eliminar borrador</h2><p className="mt-2 text-sm text-tinta">¿Quieres eliminar este borrador? Esta acción no se puede deshacer.</p>{errorEliminar && <p role="alert" className="mt-3 text-sm text-red-700">{errorEliminar}</p>}<div className="mt-5 flex justify-end gap-2"><button className={btnSecundario} type="button" onClick={() => setEliminar(null)}>Cancelar</button><button className="rounded-xl bg-red-700 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-800" type="button" onClick={() => void confirmarEliminacion()}>Eliminar</button></div></section></div>}
    </section>
  )
}

function Vacio({ texto }: { texto: string }) {
  return <div className={`${card} grid place-items-center p-10 text-center text-sm text-texto-suave`}>{texto}</div>
}
