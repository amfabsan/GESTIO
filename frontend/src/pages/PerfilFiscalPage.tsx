import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { apiClient, obtenerDetalleError } from '../services/apiClient'
import { btnPrimario, btnSecundario, card } from '../components/ui/estilos'
import type { Vinculacion } from '../types/vinculaciones'

interface Datos { regimenIva: string; regimenIrpf: string; trabajadoresConRetencion: boolean | null; localAlquiladoConRetencion: boolean | null }
interface Cambio { id: string; gestor: string | null; usuarioId: string; fechaCambio: string; campo: string; valorAnterior: string | null; valorNuevo: string }
interface Perfil extends Datos { id: string; historial: Cambio[] }
const vacio: Datos = { regimenIva: '', regimenIrpf: '', trabajadoresConRetencion: null, localAlquiladoConRetencion: null }
const etiquetas: Record<keyof Datos, string> = { regimenIva: 'Régimen de IVA', regimenIrpf: 'Régimen de IRPF', trabajadoresConRetencion: 'Trabajadores con retención', localAlquiladoConRetencion: 'Local alquilado con retención' }

export function PerfilFiscalPage() {
  const { autonomoId } = useParams()
  const { perfilActivo, esGestor } = useAuth()
  const id = esGestor ? autonomoId : perfilActivo?.id
  const [ejercicio, setEjercicio] = useState(2026)
  return <Contenido key={`${perfilActivo?.id}-${id}-${ejercicio}`} id={id} ejercicio={ejercicio} seleccionar={setEjercicio} esGestor={esGestor} />
}

function Contenido({ id, ejercicio, seleccionar, esGestor }: { id?: string; ejercicio: number; seleccionar: (valor: number) => void; esGestor: boolean }) {
  const { perfilActivo } = useAuth()
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [datos, setDatos] = useState<Datos>(vacio)
  const [catalogo, setCatalogo] = useState<{ iva: Record<string, string>; irpf: Record<string, string> }>({ iva: {}, irpf: {} })
  const [nombre, setNombre] = useState(esGestor ? '' : `${perfilActivo?.nombre ?? ''} ${perfilActivo?.primerApellido ?? ''}`)
  const [cargando, setCargando] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const url = `/autonomos/${id}/perfiles-fiscales`
  useEffect(() => {
    let vigente = true
    Promise.all([apiClient.get<{ perfil: Perfil | null }>(`${url}/${ejercicio}`), apiClient.get<typeof catalogo>(`${url}/catalogo`), esGestor ? apiClient.get<Vinculacion[]>('/vinculaciones') : Promise.resolve(null)])
      .then(([respuesta, opciones, vinculaciones]) => {
        if (!vigente) return
        setPerfil(respuesta.data.perfil)
        setDatos(respuesta.data.perfil ?? vacio)
        setCatalogo(opciones.data)
        const cliente = vinculaciones?.data.find(v => v.autonomoId === id && v.estado === 'ACTIVA')?.autonomo
        if (cliente) setNombre(`${cliente.nombre} ${cliente.primerApellido} ${cliente.segundoApellido}`.trim())
      }).catch(e => { if (vigente) setError(obtenerDetalleError(e) ?? 'No se ha podido consultar el perfil fiscal.') })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [url, ejercicio, esGestor, id])
  const pendientes = (Object.keys(etiquetas) as (keyof Datos)[]).filter(k => datos[k] === '' || datos[k] === null)
  const cambiado = (Object.keys(etiquetas) as (keyof Datos)[]).some(k => datos[k] !== (perfil ?? vacio)[k])
  async function guardar() {
    setMensaje('')
    if (pendientes.length) { setError(`Campos pendientes: ${pendientes.map(k => etiquetas[k]).join(' · ')}`); return }
    setGuardando(true); setError('')
    try {
      await apiClient.put(`${url}/${ejercicio}`, datos)
      const respuesta = await apiClient.get<{ perfil: Perfil }>(`${url}/${ejercicio}`)
      setPerfil(respuesta.data.perfil); setDatos(respuesta.data.perfil)
      setMensaje('Perfil fiscal guardado correctamente.')
    } catch (e) { setError(obtenerDetalleError(e) ?? 'No se ha podido guardar el perfil fiscal.') }
    finally { setGuardando(false) }
  }
  function cambiar(k: keyof Datos, valor: string | boolean) { setDatos(anterior => ({ ...anterior, [k]: valor })); setMensaje(''); setError('') }
  const valorVisible = (valor: string | null) => valor === null ? 'Sin indicar' : catalogo.iva[valor] ?? catalogo.irpf[valor] ?? valor
  return <main className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
    {esGestor && <Link to="/gestor/clientes" className="text-sm text-[#6b6656]">← Volver al panel de clientes</Link>}
    <h1 className="mt-7 font-serif text-4xl font-bold">{esGestor ? nombre || 'Perfil fiscal del cliente' : 'Perfil fiscal'}</h1>
    <div className="mt-6 border-b border-[#d6d2c8] pb-3"><span className="border-b-2 border-[#d96c13] px-3 pb-3 font-semibold">Perfil fiscal</span></div>
    <div className={`${card} my-6 inline-flex gap-1 p-1`} aria-label="Ejercicio fiscal">{[2026, 2027].map(a => <button key={a} disabled={guardando} onClick={() => seleccionar(a)} aria-pressed={a === ejercicio} className={a === ejercicio ? btnPrimario : btnSecundario}>Ejercicio {a}</button>)}</div>
    {cargando ? <p role="status">Cargando perfil fiscal…</p> : <>
    {error && <p role="alert" className="mb-4 rounded-xl border border-red-300 bg-red-50 p-4 text-red-800">{error}</p>}
    {mensaje && <p role="status" className="mb-4 rounded-xl bg-green-50 p-4 text-green-800">{mensaje}</p>}
    {Object.keys(catalogo.iva).length > 0 && <div className="grid items-start gap-5 lg:grid-cols-[3fr_2fr]">
      <section className={card}>
        <header className="flex items-center justify-between gap-3 border-b border-[#ece9e3] p-6"><div><h2 className="text-lg font-bold">Perfil fiscal {ejercicio} · {nombre}</h2><p className="mt-1 text-sm text-[#6b6656]">Un único perfil por ejercicio: guardar actualiza este mismo perfil.</p></div><span className={`rounded-full px-3 py-1 text-xs font-bold ${perfil ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-600'}`}>{perfil ? 'Configurado' : 'Sin perfil'}</span></header>
        <div className="px-6 pt-5"><Link className="text-sm font-semibold underline" to={esGestor ? `/gestor/clientes/${id}/calendario` : '/autonomo/calendario'}>Consultar calendario fiscal</Link></div>
        <form className="space-y-7 p-6" onSubmit={e => { e.preventDefault(); void guardar() }}>
          {(['regimenIva', 'regimenIrpf'] as const).map(k => <label key={k} className="block font-semibold">{etiquetas[k]}<select disabled={!esGestor || guardando} aria-invalid={!datos[k]} value={datos[k]} onChange={e => cambiar(k, e.target.value)} className={`mt-2 block w-full rounded-xl border p-3 font-normal ${datos[k] ? 'border-[#d6d2c8] bg-[#f5f3ef]' : 'border-amber-400 bg-amber-50'}`}><option value="">— Selecciona un régimen —</option>{Object.entries(k === 'regimenIva' ? catalogo.iva : catalogo.irpf).map(([codigo, texto]) => <option value={codigo} key={codigo}>{texto}</option>)}</select></label>)}
          <div className="grid gap-5 sm:grid-cols-2">{(['trabajadoresConRetencion', 'localAlquiladoConRetencion'] as const).map(k => <fieldset key={k} disabled={!esGestor || guardando}><legend className="mb-2 font-semibold">{k === 'trabajadoresConRetencion' ? '¿Tiene trabajadores con retención?' : '¿Alquila un local con retención?'}</legend><div className={`inline-flex rounded-xl border p-1 ${datos[k] === null ? 'border-amber-400 bg-amber-50' : 'border-[#d6d2c8] bg-[#f5f3ef]'}`}>{[true, false].map(v => <button type="button" aria-pressed={datos[k] === v} key={String(v)} onClick={() => cambiar(k, v)} className={datos[k] === v ? btnPrimario : 'rounded-xl px-4 py-2 text-[#6b6656]'}>{v ? 'Sí' : 'No'}</button>)}</div></fieldset>)}</div>
          {esGestor && <><p className="text-xs text-[#6b6656]">Solo se admiten los valores del catálogo del prototipo; la API rechaza cualquier otro valor.</p>{pendientes.length > 0 && <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-amber-800"><strong>Campos pendientes para guardarlo como configurado:</strong><p className="mt-1 text-sm">{pendientes.map(k => etiquetas[k]).join(' · ')}</p></div>}<div className="flex justify-end gap-3"><button type="button" className={btnSecundario} disabled={!cambiado || guardando} onClick={() => { setDatos(perfil ?? vacio); setError(''); setMensaje('') }}>↶ Descartar</button><button className={btnPrimario} disabled={guardando || (!!perfil && !cambiado)}>{guardando ? 'Guardando…' : perfil ? 'Guardar cambios' : `Crear perfil ${ejercicio}`}</button></div></>}
          {!esGestor && <p className="text-sm text-[#6b6656]">Solo lectura. {perfil ? 'Configurado y revisado por tu gestor.' : 'Tu gestor todavía no ha configurado este ejercicio.'}</p>}
        </form>
      </section>
      <aside className="space-y-5"><section className={`${card} p-6`}><h2 className="mb-4 text-sm font-medium text-[#6b6656]">HISTORIAL DE CAMBIOS</h2>{!perfil?.historial.length ? <p className="text-[#6b6656]">Sin cambios registrados.</p> : <ul className="space-y-4">{perfil.historial.map(h => <li key={h.id} className="text-sm"><p><strong>{h.gestor ?? h.usuarioId}</strong> · {etiquetas[(h.campo[0].toLowerCase() + h.campo.slice(1)) as keyof Datos]}</p><p>{h.valorAnterior !== null && <><del className="text-[#6b6656]">{valorVisible(h.valorAnterior)}</del> → </>}<strong>{valorVisible(h.valorNuevo)}</strong> · {new Date(h.fechaCambio).toLocaleString('es-ES')}</p></li>)}</ul>}</section><p className="text-xs leading-relaxed text-[#6b6656]">ⓘ Datos introducidos y revisados por el gestor. GESTIO no verifica por sí mismo la situación tributaria real del cliente. El autónomo puede consultarlos pero no modificarlos.</p></aside>
    </div>}</>}
  </main>
}

export function ClientesPerfilFiscal() {
  const { perfilActivo } = useAuth()
  const [clientes, setClientes] = useState<Vinculacion[]>([])
  const [error, setError] = useState('')
  useEffect(() => {
    let vigente = true
    apiClient.get<Vinculacion[]>('/vinculaciones').then(r => { if (vigente) setClientes(r.data.filter(v => v.estado === 'ACTIVA')) }).catch(e => { if (vigente) setError(obtenerDetalleError(e) ?? 'No se han podido cargar los perfiles fiscales.') })
    return () => { vigente = false }
  }, [perfilActivo?.id])
  return <section className={`${card} mt-5 p-5`}><h2 className="mb-3 font-semibold">Perfiles fiscales de clientes vinculados</h2>{error && <p role="alert">{error}</p>}<ul className="space-y-3">{clientes.map(v => <li key={v.id}><Link className="font-semibold underline" to={`/gestor/clientes/${v.autonomoId}/perfil-fiscal`}>{v.autonomo ? `${v.autonomo.nombre} ${v.autonomo.primerApellido} ${v.autonomo.segundoApellido}` : v.autonomoId} · Perfil fiscal</Link><Link className="ml-4 text-sm font-semibold underline" to={`/gestor/clientes/${v.autonomoId}/calendario`}>Calendario fiscal</Link></li>)}</ul></section>
}
