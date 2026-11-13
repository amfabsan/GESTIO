import { useState } from 'react'
import { ArrowDown, CheckCircle2, ChevronDown, ChevronUp, ShieldAlert, ShieldCheck } from 'lucide-react'
import { btnPrimario, card } from '../../components/ui/estilos'
import { obtenerDetalleError } from '../../services/apiClient'
import { formatearEuros } from '../../utils/formato'
import { listarRegistros, verificarCadena, type RegistroFacturacion, type VerificacionCadena } from './registrosApi'

type Props = { autonomoId: string; registros: RegistroFacturacion[] }

// UT22 (mockups 1–4). Sin controles para alterar o restaurar datos: el requisito 7 impide editar registros desde la
// aplicación. La verificación la hace el backend; aquí solo se muestra su resultado.
export function RegistroVerifactuVista({ autonomoId, registros }: Props) {
  // `base` es la lista del padre cuando se verificó; `lista` son los datos releídos junto con la verificación.
  const [verificado, setVerificado] = useState<{ base: RegistroFacturacion[]; lista: RegistroFacturacion[]; resultado: VerificacionCadena } | null>(null)
  const [verificando, setVerificando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [abierto, setAbierto] = useState<string | null>(null)

  // Al verificar se muestran los datos releídos en ese momento (lo que de verdad se ha verificado). Si el padre
  // recarga la lista (p. ej. tras emitir), ese resultado deja de valer y se vuelve a la lista del padre.
  const vigente = verificado?.base === registros ? verificado : null
  const resultado = vigente?.resultado ?? null
  const mostrados = vigente?.lista ?? registros

  const verificar = async () => {
    setVerificando(true)
    setError(null)
    try {
      const [lista, verificacion] = await Promise.all([listarRegistros(autonomoId), verificarCadena(autonomoId)])
      setVerificado({ base: registros, lista, resultado: verificacion })
    } catch (excepcion) {
      setError(obtenerDetalleError(excepcion) ?? 'No se ha podido verificar la cadena.')
    } finally {
      setVerificando(false)
    }
  }

  return (
    <div className="mt-5 space-y-4">
      <div className="flex gap-2 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-xs text-blue-900">
        <ShieldCheck className="mt-px h-4 w-4 flex-shrink-0" aria-hidden="true" />
        <p>Cada factura emitida genera un registro de ALTA con huella SHA-256 calculada sobre una representación canónica y enlazada con el registro anterior del mismo emisor.
          <span className="font-semibold"> Simulación académica:</span> no es un sistema certificado ni está conectado a la AEAT. Los registros no se pueden editar ni eliminar.</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => void verificar()} disabled={verificando} className={btnPrimario}>
          <ShieldCheck className="h-4 w-4" aria-hidden="true" />{verificando ? 'Verificando…' : 'Verificar cadena'}
        </button>
        <span className="ml-auto text-xs text-[#6b6656]">
          {mostrados.length} registros{mostrados.length > 0 ? ` · emisor ${mostrados[0].idEmisorFactura}` : ''}
        </span>
      </div>

      {error && <p role="alert" className="rounded-xl border border-red-300 bg-red-50 p-3 text-sm text-red-800">{error}</p>}

      {resultado && (resultado.coherente ? (
        <div role="status" className="flex gap-3 rounded-xl border border-green-200 bg-green-50 p-4 text-green-900">
          <CheckCircle2 className="h-5 w-5 flex-shrink-0" aria-hidden="true" />
          <div><p className="font-semibold">Cadena coherente</p><p className="mt-0.5 text-xs">{resultado.revisados} registros revisados: todas las huellas recalculadas y los enlaces con el registro anterior coinciden.</p></div>
        </div>
      ) : (
        <div role="alert" className="flex gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-red-900">
          <ShieldAlert className="h-5 w-5 flex-shrink-0" aria-hidden="true" />
          <div><p className="font-semibold">Incoherencia detectada en el registro #{resultado.secuenciaIncoherente ?? '—'}</p><p className="mt-0.5 text-xs">{resultado.motivo ?? 'La cadena no es coherente.'} Es el primer registro donde se rompe la cadena.</p></div>
        </div>
      ))}

      {mostrados.length === 0
        ? <div className={`${card} grid place-items-center p-10 text-center text-sm text-[#6b6656]`}>Todavía no hay registros: se crean al emitir una factura.</div>
        : <ol className="space-y-0" aria-label="Cadena de registros de facturación">
          {mostrados.map((r, i) => {
            const roto = resultado !== null && !resultado.coherente && resultado.registroIncoherenteId === r.id
            return (
              <li key={r.id}>
                {i > 0 && <div className="flex justify-center py-1 text-[#b5b0a2]" aria-hidden="true"><ArrowDown className="h-4 w-4" /></div>}
                <article className={`${card} p-4 ${roto ? 'border-red-400 ring-2 ring-red-200' : ''}`} aria-label={`Registro ${r.secuencia}`}>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <span className={`flex h-10 w-10 items-center justify-center rounded-xl text-sm font-bold ${roto ? 'bg-red-100 text-red-700' : 'bg-[#1d2b4f] text-white'}`}>#{r.secuencia}</span>
                      <div>
                        <p className="text-sm font-semibold text-[#1d2b4f]">{r.numSerieFactura} <span className="ml-1 rounded bg-green-100 px-1.5 py-0.5 text-[10px] font-bold text-green-800">{r.tipo}</span></p>
                        <p className="text-xs text-[#6b6656]">{r.clienteNombre ?? '—'} · {formatearEuros(r.importeTotal)} · generado {r.fechaHoraHusoGenRegistro.replace('T', ' ').slice(0, 19)}</p>
                      </div>
                    </div>
                    <span className="text-xs text-[#6b6656]">{r.secuenciaAnterior !== null ? `↳ enlaza con #${r.secuenciaAnterior}` : 'Inicio de cadena'}</span>
                  </div>
                  <div className="mt-3 grid grid-cols-1 gap-3 text-[11px] md:grid-cols-2">
                    <div className="rounded-lg bg-[#f5f4f0] p-2.5">
                      <p className="font-medium text-[#6b6656]">Huella anterior</p>
                      <p className="mt-0.5 break-all font-mono text-[#3d3a30]">{r.huellaAnterior ?? '— (primer registro)'}</p>
                    </div>
                    <div className="rounded-lg bg-[#f5f4f0] p-2.5">
                      <p className="font-medium text-[#6b6656]">Huella propia (SHA-256)</p>
                      <p className="mt-0.5 break-all font-mono font-semibold text-[#1d2b4f]">{r.huella}</p>
                    </div>
                  </div>
                  <button type="button" onClick={() => setAbierto(abierto === r.id ? null : r.id)} aria-expanded={abierto === r.id}
                    className="mt-2 flex items-center gap-1 text-[11px] text-[#6b6656] hover:text-[#1d2b4f]">
                    Representación canónica {abierto === r.id ? <ChevronUp className="h-3 w-3" aria-hidden="true" /> : <ChevronDown className="h-3 w-3" aria-hidden="true" />}
                  </button>
                  {abierto === r.id && <p className="mt-1 break-all rounded-lg bg-[#1a1916] p-2.5 font-mono text-[10px] text-green-300">{r.representacionCanonica}</p>}
                </article>
              </li>
            )
          })}
        </ol>}
    </div>
  )
}
