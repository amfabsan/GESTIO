import { useEffect, useState } from 'react'
import { Save } from 'lucide-react'
import { btnPrimario, btnSecundario, card } from '../../components/ui/estilos'
import { apiClient, obtenerDetalleError } from '../../services/apiClient'

type Props = { autonomoId: string }

export function DireccionFiscalEmisor({ autonomoId }: Props) {
  const [direccion, setDireccion] = useState('')
  const [original, setOriginal] = useState('')
  const [cargando, setCargando] = useState(true)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')

  useEffect(() => {
    let vigente = true
    apiClient.get<{ direccionFiscal: string | null }>(`/autonomos/${autonomoId}/direccion-fiscal`)
      .then(({ data }) => {
        if (!vigente) return
        const valor = data.direccionFiscal ?? ''
        setDireccion(valor)
        setOriginal(valor)
      })
      .catch(excepcion => { if (vigente) setError(obtenerDetalleError(excepcion) ?? 'No se ha podido cargar la dirección fiscal.') })
      .finally(() => { if (vigente) setCargando(false) })
    return () => { vigente = false }
  }, [autonomoId])

  const guardar = async () => {
    setGuardando(true)
    setError('')
    setMensaje('')
    try {
      const { data } = await apiClient.put<{ direccionFiscal: string }>(`/autonomos/${autonomoId}/direccion-fiscal`, { direccionFiscal: direccion })
      setDireccion(data.direccionFiscal)
      setOriginal(data.direccionFiscal)
      setMensaje('Dirección fiscal guardada.')
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido guardar la dirección fiscal.')
    } finally {
      setGuardando(false)
    }
  }

  return <section className={`${card} mb-5 p-5`} aria-labelledby="direccion-fiscal-emisor">
    <h2 id="direccion-fiscal-emisor" className="font-semibold">Dirección fiscal del emisor</h2>
    <p className="mt-1 text-sm text-[#6b6656]">Se copiará a las facturas nuevas al emitirlas. Las emitidas conservarán estos datos aunque actualices el perfil.</p>
    {cargando ? <p className="mt-3 text-sm text-[#6b6656]" role="status">Cargando dirección fiscal…</p> : <>
      <label className="mt-3 block text-sm font-medium">Dirección fiscal
        <textarea className="mt-1 min-h-20 w-full rounded-xl border border-[#d6d2c8] bg-[#f5f3ef] p-3 text-sm outline-none focus:border-[#435271] focus:ring-2 focus:ring-[#435271]/15" maxLength={500} value={direccion} onChange={evento => { setDireccion(evento.target.value); setError(''); setMensaje('') }} placeholder="Calle, número, código postal y municipio" />
      </label>
      {error && <p role="alert" className="mt-2 text-sm text-red-700">{error}</p>}
      {mensaje && <p role="status" className="mt-2 text-sm text-green-700">{mensaje}</p>}
      <div className="mt-3 flex justify-end gap-2">
        <button type="button" className={btnSecundario} disabled={guardando || direccion === original} onClick={() => { setDireccion(original); setError(''); setMensaje('') }}>Descartar</button>
        <button type="button" className={btnPrimario} disabled={guardando || direccion.trim() === original} onClick={() => void guardar()}><Save size={16} />{guardando ? 'Guardando…' : 'Guardar dirección'}</button>
      </div>
    </>}
  </section>
}
