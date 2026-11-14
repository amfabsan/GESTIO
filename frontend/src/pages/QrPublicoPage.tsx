import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { isAxiosError } from 'axios'
import { FileCheck2, GraduationCap, OctagonAlert } from 'lucide-react'
import { consultarFacturaPublica, type FacturaPublica } from '../features/facturacion/facturaPublicaApi'
import { formatearEuros, formatearFecha } from '../utils/formato'

type Estado =
  | { tipo: 'cargando' }
  | { tipo: 'encontrada'; factura: FacturaPublica }
  | { tipo: 'no-valido' }
  | { tipo: 'sin-conexion' }

// Mismo texto que el aviso que fija el backend; se muestra también mientras carga y si el enlace no es válido.
const AVISO = 'Demostración académica: factura no cotejable en la AEAT'

/**
 * UT23 (PB-23): página a la que lleva el QR. Fuera de AppLayout y de los guards: no necesita sesión,
 * no muestra navegación de la app y no ofrece ninguna acción de edición (req. 3 y 6).
 */
export function QrPublicoPage() {
  const { codigo = '' } = useParams()
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })

  useEffect(() => {
    let activo = true
    consultarFacturaPublica(codigo)
      .then(factura => { if (activo) setEstado({ tipo: 'encontrada', factura }) })
      .catch(error => {
        if (!activo) return
        // Req. 7: cualquier 404 es «enlace no válido», sin distinguir el motivo.
        setEstado(isAxiosError(error) && error.response?.status === 404 ? { tipo: 'no-valido' } : { tipo: 'sin-conexion' })
      })
    return () => { activo = false }
  }, [codigo])

  return (
    <div className="flex min-h-screen flex-col bg-[#f5f4f0] text-[#1a1916]">
      <header className="bg-[#1d2b4f] text-white">
        <div className="mx-auto flex h-14 max-w-xl items-center gap-2 px-4">
          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-[#c8651a]"><span className="text-sm font-bold">G</span></div>
          <span className="font-serif text-lg tracking-tight">Gestio</span>
          <span className="ml-2 text-xs text-blue-200">Identificación de factura</span>
        </div>
      </header>

      <main className="mx-auto w-full max-w-xl flex-1 space-y-4 px-4 py-8">
        <div role="note" className="flex gap-3 rounded-2xl border-2 border-amber-400 bg-amber-100 px-4 py-3 text-amber-900">
          <GraduationCap className="h-6 w-6 flex-shrink-0" aria-hidden="true" />
          <div>
            <p className="font-bold">{estado.tipo === 'encontrada' ? estado.factura.aviso : AVISO}</p>
            <p className="mt-0.5 text-xs">Este QR solo identifica la factura dentro de la demostración de GESTIO. No prueba que la factura se haya remitido a la Agencia Tributaria.</p>
          </div>
        </div>

        {estado.tipo === 'cargando' && <p className="rounded-2xl border border-[#d6d2c8] bg-white p-8 text-center text-sm text-[#6b6656]">Consultando la factura…</p>}

        {estado.tipo === 'encontrada' && (
          <div className="overflow-hidden rounded-2xl border border-[#d6d2c8] bg-white">
            <div className="flex items-center gap-3 border-b border-[#ede9e0] px-6 py-5">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-green-100 text-green-700"><FileCheck2 className="h-6 w-6" aria-hidden="true" /></span>
              <div>
                <p className="text-xs font-medium uppercase tracking-wider text-[#6b6656]">Factura emitida con GESTIO</p>
                <h1 className="font-serif text-xl text-[#1d2b4f]">{estado.factura.serieNumero}</h1>
              </div>
            </div>
            <dl className="divide-y divide-[#ede9e0] text-sm">
              <div className="flex justify-between gap-4 px-6 py-3"><dt className="text-[#6b6656]">Emisor</dt><dd className="text-right font-semibold">{estado.factura.emisorNombre}</dd></div>
              <div className="flex justify-between gap-4 px-6 py-3"><dt className="text-[#6b6656]">Serie y número</dt><dd className="font-semibold">{estado.factura.serieNumero}</dd></div>
              <div className="flex justify-between gap-4 px-6 py-3"><dt className="text-[#6b6656]">Fecha de expedición</dt><dd className="font-semibold">{formatearFecha(estado.factura.fecha)}</dd></div>
              <div className="flex justify-between gap-4 px-6 py-3"><dt className="text-[#6b6656]">Importe total</dt><dd className="text-lg font-bold text-[#1d2b4f]">{formatearEuros(estado.factura.total)}</dd></div>
            </dl>
            <p className="bg-[#f8f7f4] px-6 py-3 text-[11px] text-[#6b6656]">Consulta pública de solo lectura. No se muestran datos del cliente, documentos, contactos ni mensajes.</p>
          </div>
        )}

        {estado.tipo === 'no-valido' && (
          <div className="rounded-2xl border border-[#d6d2c8] bg-white p-8 text-center">
            <OctagonAlert className="mx-auto h-10 w-10 text-red-500" aria-hidden="true" />
            <h1 className="mt-3 font-semibold text-[#1d2b4f]">Enlace no válido</h1>
            <p className="mt-1 text-sm text-[#6b6656]">No hay ninguna factura asociada a este código.</p>
          </div>
        )}

        {estado.tipo === 'sin-conexion' && (
          <p role="alert" className="rounded-2xl border border-red-300 bg-red-50 p-6 text-center text-sm text-red-800">No se ha podido consultar la factura. Comprueba la conexión e inténtalo de nuevo.</p>
        )}
        {/* Sin enlace «Volver a la demo» (rechazo del desarrollador, 2026-11-14): el cliente que escanea no debe
            quedar a un clic de la app privada. */}
      </main>
    </div>
  )
}
