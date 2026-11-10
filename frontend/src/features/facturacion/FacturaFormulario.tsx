import { useMemo, useState, type FormEvent } from 'react'
import { Plus, Save, Eye, Trash2 } from 'lucide-react'
import { btnSecundario, card } from '../../components/ui/estilos'
import type { Contacto } from '../contactos/contactosApi'
import type { Factura, GuardarFactura } from './facturacionApi'

type Props = {
  factura: Factura | null
  contactos: Contacto[]
  direccionFiscalEmisor: string
  onDireccionFiscalEmisorChange: (direccion: string) => void
  guardando: boolean
  error: string | null
  onGuardar: (datos: GuardarFactura) => Promise<Factura | null>
  onGuardarDireccion: (contactoId: string, direccion: string) => Promise<void>
  onRevisar: (factura: Factura) => void
}

type LineaCampo = { descripcion: string; cantidad: string; precioUnitario: string; ivaPorcentaje: string }

const lineaVacia = (): LineaCampo => ({ descripcion: '', cantidad: '', precioUnitario: '', ivaPorcentaje: '21' })
const redondear = (importe: number) => Math.sign(importe) * Math.floor(Math.abs(importe) * 100 + 0.5000001) / 100
const euro = (importe: number) => new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(importe)
const numero = (valor: string) => valor.trim() === '' || !Number.isFinite(Number(valor)) ? null : Number(valor)

export function FacturaFormulario({ factura, contactos, direccionFiscalEmisor, onDireccionFiscalEmisorChange, guardando, error, onGuardar, onGuardarDireccion, onRevisar }: Props) {
  const [contactoId, setContactoId] = useState(factura?.contactoId ?? '')
  const [fecha, setFecha] = useState(factura?.fecha ?? new Date().toISOString().slice(0, 10))
  const [retencion, setRetencion] = useState(factura?.retencionPorcentaje ? 'otra' : '0')
  const [otraRetencion, setOtraRetencion] = useState(factura?.retencionPorcentaje?.toString() ?? '')
  const [lineas, setLineas] = useState<LineaCampo[]>(factura?.lineas.map(linea => ({
    descripcion: linea.descripcion ?? '', cantidad: linea.cantidad?.toString() ?? '',
    precioUnitario: linea.precioUnitario?.toString() ?? '', ivaPorcentaje: linea.ivaPorcentaje?.toString() ?? '21',
  })) ?? [lineaVacia()])
  const [direccionEditada, setDireccionEditada] = useState<{ contactoId: string; valor: string } | null>(null)
  const [guardandoDireccion, setGuardandoDireccion] = useState(false)
  const [errorDireccion, setErrorDireccion] = useState('')
  const [mensajeDireccion, setMensajeDireccion] = useState('')
  const [errorRetencion, setErrorRetencion] = useState('')
  const cliente = contactos.find(contacto => contacto.id === contactoId)
  const direccion = direccionEditada && direccionEditada.contactoId === cliente?.id ? direccionEditada.valor : cliente?.direccion ?? ''
  const porcentajeRetencion = retencion === 'otra' ? numero(otraRetencion) : null

  const datos = useMemo<GuardarFactura>(() => ({
    contactoId: contactoId || null,
    fecha,
    retencionPorcentaje: porcentajeRetencion,
    lineas: lineas.map(linea => ({
      descripcion: linea.descripcion.trim() || null,
      cantidad: numero(linea.cantidad),
      precioUnitario: numero(linea.precioUnitario),
      ivaPorcentaje: numero(linea.ivaPorcentaje),
    })),
  }), [contactoId, fecha, lineas, porcentajeRetencion])

  const desglose = useMemo(() => {
    const bases = new Map<number, number>()
    for (const linea of datos.lineas) {
      if (!linea.descripcion || linea.cantidad === null || linea.cantidad <= 0 || linea.precioUnitario === null || linea.precioUnitario < 0 || linea.ivaPorcentaje === null) continue
      bases.set(linea.ivaPorcentaje, (bases.get(linea.ivaPorcentaje) ?? 0) + linea.cantidad * linea.precioUnitario)
    }
    const grupos = [...bases.entries()].sort(([a], [b]) => a - b).map(([tipo, base]) => {
      const baseRedondeada = redondear(base)
      return { tipo, base: baseRedondeada, cuota: redondear(baseRedondeada * tipo / 100) }
    })
    const base = redondear(grupos.reduce((suma, grupo) => suma + grupo.base, 0))
    const cuota = redondear(grupos.reduce((suma, grupo) => suma + grupo.cuota, 0))
    const retencionImporte = porcentajeRetencion && porcentajeRetencion > 0 ? redondear(base * porcentajeRetencion / 100) : 0
    return { grupos, base, cuota, retencion: retencionImporte, total: redondear(base + cuota - retencionImporte) }
  }, [datos.lineas, porcentajeRetencion])

  const cambiarLinea = (indice: number, campo: keyof LineaCampo, valor: string) => {
    setLineas(actuales => actuales.map((linea, posicion) => posicion === indice ? { ...linea, [campo]: valor } : linea))
  }

  const guardar = async (evento: FormEvent) => {
    evento.preventDefault()
    if (retencion === 'otra' && (porcentajeRetencion === null || porcentajeRetencion < 0 || porcentajeRetencion > 100)) {
      setErrorRetencion('Indica una retención entre 0 % y 100 %.')
      return
    }
    setErrorRetencion('')
    const guardada = await onGuardar(datos)
    if (guardada) onRevisar(guardada)
  }

  const guardarBorrador = () => {
    if (retencion === 'otra' && (porcentajeRetencion === null || porcentajeRetencion < 0 || porcentajeRetencion > 100)) {
      setErrorRetencion('Indica una retención entre 0 % y 100 %.')
      return
    }
    setErrorRetencion('')
    void onGuardar(datos)
  }

  const guardarDireccion = async () => {
    if (!cliente) return
    setGuardandoDireccion(true)
    setErrorDireccion('')
    setMensajeDireccion('')
    try {
      await onGuardarDireccion(cliente.id, direccion)
      setDireccionEditada({ contactoId: cliente.id, valor: direccion })
      setMensajeDireccion('Dirección fiscal guardada en el contacto.')
    } catch (error) {
      setErrorDireccion(error instanceof Error ? error.message : 'No se ha podido guardar la dirección.')
    } finally {
      setGuardandoDireccion(false)
    }
  }

  const claseCampo = 'w-full rounded-lg border border-[#d9d4c9] bg-[#f7f6f3] px-3 py-2.5 text-sm text-[#202b43] outline-none focus:border-[#435271] focus:ring-2 focus:ring-[#435271]/15 disabled:opacity-60'
  const etiqueta = 'mb-1 block text-xs font-medium text-[#555348]'

  return (
    <form className="space-y-4" onSubmit={guardar}>
      {error && <p role="alert" className="rounded-xl border border-red-300 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1.45fr)_minmax(340px,1fr)]">
        <section className={`${card} p-4 sm:p-5`} aria-labelledby="datos-factura">
          <h2 id="datos-factura" className="text-sm font-bold uppercase tracking-wide text-[#696354]">Datos de la factura</h2>
          <label className="mt-4 block"><span className={etiqueta}>Dirección fiscal del emisor</span><textarea className={claseCampo} maxLength={500} value={direccionFiscalEmisor} onChange={evento => onDireccionFiscalEmisorChange(evento.target.value)} placeholder="Calle, número, código postal y municipio" /><span className="mt-1 block text-xs text-[#777265]">Se guarda en tu perfil fiscal y se copia al emitir. Es obligatoria para emitir.</span></label>
          <div className="mt-4 grid gap-3 sm:grid-cols-3">
            <label><span className={etiqueta}>Serie</span><input readOnly className={claseCampo} value={`F-${fecha ? fecha.slice(0, 4) : new Date().getFullYear()}`} /></label>
            <label><span className={etiqueta}>Número</span><input readOnly className={claseCampo} value="Al emitir" /></label>
            <label><span className={etiqueta}>Fecha</span><input type="date" className={claseCampo} value={fecha} onChange={evento => setFecha(evento.target.value)} /></label>
          </div>
          <label className="mt-4 block"><span className={etiqueta}>Cliente (desde tu agenda)</span>
            <select className={claseCampo} value={contactoId} onChange={evento => { setContactoId(evento.target.value); setDireccionEditada(null); setErrorDireccion(''); setMensajeDireccion('') }}>
              <option value="">Selecciona un cliente</option>
              {contactos.filter(contacto => contacto.activo && (contacto.clasificacion === 'CLIENTE' || contacto.clasificacion === 'AMBOS')).map(contacto => <option key={contacto.id} value={contacto.id}>{contacto.nombre} · {contacto.nif}</option>)}
            </select>
          </label>
          {cliente && <div className="mt-3 rounded-xl bg-[#f5f3ef] p-3 text-sm">
            <p className="font-semibold text-[#202b43]">{cliente.nombre} · NIF {cliente.nif}</p>
            {cliente.email && <p className="mt-0.5 text-xs text-[#6b6656]">{cliente.email}</p>}
            {cliente.direccion ? <p className="mt-1 text-xs text-[#6b6656]">{cliente.direccion}</p> : <>
              <p className="mt-1 text-xs font-medium text-[#b45309]">Falta la dirección fiscal; es necesaria para emitir. Puedes guardar el borrador mientras tanto.</p>
              <div className="mt-2 flex flex-col gap-2 sm:flex-row">
                <input className={`${claseCampo} bg-white`} maxLength={500} placeholder="Calle, número, CP y municipio" value={direccion} onChange={evento => { setDireccionEditada({ contactoId: cliente.id, valor: evento.target.value }); setErrorDireccion(''); setMensajeDireccion('') }} />
                <button className={`${btnSecundario} shrink-0`} type="button" disabled={guardandoDireccion || !direccion.trim()} onClick={() => void guardarDireccion()}>{guardandoDireccion ? 'Guardando…' : 'Guardar en el contacto'}</button>
              </div>
              {errorDireccion && <p role="alert" className="mt-2 text-xs text-red-700">{errorDireccion}</p>}
              {mensajeDireccion && <p role="status" className="mt-2 text-xs text-green-700">{mensajeDireccion}</p>}
            </>}
          </div>}
        </section>

        <aside className={`${card} p-4 sm:p-5`} aria-labelledby="desglose-factura">
          <div className="flex items-center justify-between"><h2 id="desglose-factura" className="text-sm font-bold uppercase tracking-wide text-[#696354]">Desglose</h2><span className="text-xs text-[#777265]">estimación</span></div>
          <div className="mt-3 space-y-2 text-sm">
            {desglose.grupos.map(grupo => <div key={grupo.tipo} className="flex justify-between gap-3 text-[#555348]"><span>Base al {grupo.tipo} % {euro(grupo.base)}</span><span>IVA {grupo.tipo} % {euro(grupo.cuota)}</span></div>)}
            <div className="flex justify-between border-t border-[#eeeae2] pt-2"><span>Base imponible</span><span>{euro(desglose.base)}</span></div>
            <div className="flex justify-between"><span>Cuotas de IVA</span><span>{euro(desglose.cuota)}</span></div>
            {desglose.retencion > 0 && <div className="flex justify-between text-[#9a3412]"><span>Retención IRPF ({porcentajeRetencion} %)</span><span>− {euro(desglose.retencion)}</span></div>}
            <div className="flex justify-between border-t border-[#eeeae2] pt-3 text-lg font-bold text-[#1d2b4f]"><span>Total</span><span>{euro(desglose.total)}</span></div>
          </div>
          <p className="mt-3 text-xs leading-relaxed text-[#777265]">El cálculo definitivo se valida en el servidor al revisar la factura.</p>
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            <button type="button" className={btnSecundario} disabled={guardando} onClick={guardarBorrador}><Save size={16} aria-hidden="true" />{guardando ? 'Guardando…' : 'Guardar borrador'}</button>
            <button type="submit" className="inline-flex items-center justify-center gap-2 rounded-xl bg-[#c96016] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#ab4e10] disabled:opacity-50" disabled={guardando}><Eye size={16} aria-hidden="true" />Revisar y emitir</button>
          </div>
        </aside>
      </div>

      <section className={`${card} p-4 sm:p-5`} aria-labelledby="lineas-factura">
        <h2 id="lineas-factura" className="text-sm font-bold uppercase tracking-wide text-[#696354]">Líneas</h2>
        <div className="mt-4 space-y-3">
          {lineas.map((linea, indice) => <div key={indice} className="grid gap-2 rounded-xl border border-[#eeeae2] p-3 sm:grid-cols-[minmax(180px,1fr)_90px_110px_110px_36px] sm:items-end sm:border-0 sm:p-0">
            <label><span className={etiqueta}>Descripción</span><input className={claseCampo} maxLength={500} value={linea.descripcion} onChange={evento => cambiarLinea(indice, 'descripcion', evento.target.value)} /></label>
            <label><span className={etiqueta}>Cantidad</span><input className={claseCampo} type="number" min="0" step="0.0001" value={linea.cantidad} onChange={evento => cambiarLinea(indice, 'cantidad', evento.target.value)} /></label>
            <label><span className={etiqueta}>Precio unitario</span><input className={claseCampo} type="number" min="0" step="0.0001" value={linea.precioUnitario} onChange={evento => cambiarLinea(indice, 'precioUnitario', evento.target.value)} /></label>
            <label><span className={etiqueta}>IVA</span><select className={claseCampo} value={linea.ivaPorcentaje} onChange={evento => cambiarLinea(indice, 'ivaPorcentaje', evento.target.value)}><option value="">Selecciona IVA</option><option value="0">0 %</option><option value="4">4 %</option><option value="10">10 %</option><option value="21">21 %</option></select></label>
            <button type="button" aria-label={`Eliminar línea ${indice + 1}`} className="grid size-10 place-items-center rounded-lg text-[#756f60] hover:bg-red-50 hover:text-red-700" onClick={() => setLineas(actuales => actuales.filter((_, posicion) => posicion !== indice))}><Trash2 size={17} /></button>
          </div>)}
        </div>
        <button type="button" className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-[#c96016] hover:underline" onClick={() => setLineas(actuales => [...actuales, lineaVacia()])}><Plus size={16} />Añadir línea</button>
        <div className="mt-4 flex flex-wrap items-end gap-3 border-t border-[#eeeae2] pt-4">
          <label className="text-sm font-medium">Retención IRPF
            <select className={`${claseCampo} mt-1 min-w-48`} value={retencion} onChange={evento => { setRetencion(evento.target.value); setErrorRetencion('') }}><option value="0">Sin retención</option><option value="otra">Otra retención</option></select>
          </label>
          {retencion === 'otra' && <label className="text-sm font-medium">Porcentaje
            <div className="mt-1 flex items-center gap-2"><input className={`${claseCampo} w-28`} type="number" min="0" max="100" step="0.01" value={otraRetencion} onChange={evento => { setOtraRetencion(evento.target.value); setErrorRetencion('') }} /><span>%</span></div>
          </label>}
          {errorRetencion && <p role="alert" className="text-xs text-red-700">{errorRetencion}</p>}
        </div>
      </section>
    </form>
  )
}
