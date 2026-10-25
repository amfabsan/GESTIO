import { useState } from 'react'
import { Bot, User } from 'lucide-react'
import { Vacio } from '../../../components/ui/Vacio'
import { btnSecundario, etiqueta } from '../../../components/ui/estilos'
import { formatearFechaHora } from '../../../utils/formato'
import { esEventoDelSistema, etiquetaCampo, nombreActor, textoEvento, valorCorregido } from '../eventosDocumento'
import { useHistorialDocumento } from '../hooks/useHistorialDocumento'

// Solo lectura: muestra la línea temporal y no ofrece ninguna acción sobre los eventos.
// version (p. ej. el número de eventos del documento) recarga la línea temporal sin cerrarla.
export function HistorialDocumento({ documentoId, version }: { documentoId: string; version?: number }) {
  const [abierto, setAbierto] = useState(false)

  return (
    <details className="mt-2 text-sm" onToggle={(evento) => setAbierto(evento.currentTarget.open)}>
      <summary className="cursor-pointer">Ver historial</summary>
      {/* Se monta al abrir para no pedir el historial de cada fila del listado. */}
      {abierto && <LineaTemporal key={documentoId} documentoId={documentoId} version={version} />}
    </details>
  )
}

function LineaTemporal({ documentoId, version }: { documentoId: string; version?: number }) {
  const { historial, error, hayAnteriores, haySiguientes, anteriores, siguientes } = useHistorialDocumento(documentoId, version)

  if (error) return <p role="alert" className="mt-2 text-red-700">{error}</p>
  if (!historial) return <Vacio texto="Cargando historial…" />
  if (historial.total === 0) return <Vacio texto="Este documento aún no tiene eventos" />

  return (
    <div className="mt-3">
      <ol className="space-y-3 border-l border-[#d6d2c8] pl-4">
        {historial.elementos.map((evento) => (
          <li key={evento.id}>
            <div className="flex items-center gap-2">
              {esEventoDelSistema(evento)
                ? <Bot aria-hidden="true" className="size-3.5 text-[#1d2b4f]" />
                : <User aria-hidden="true" className="size-3.5 text-[#6b6656]" />}
              <p className="font-medium">{textoEvento(evento)}</p>
            </div>
            <p className="text-xs text-[#6b6656]">
              <time dateTime={evento.fecha}>{formatearFechaHora(evento.fecha)}</time> · {nombreActor(evento)}
            </p>
            {evento.cambio ? (
              <p className="mt-1">
                {etiquetaCampo(evento.cambio.campo)}: <del>{valorCorregido(evento.cambio.campo, evento.cambio.valorAnterior)}</del> → <span className="font-medium">{valorCorregido(evento.cambio.campo, evento.cambio.valorNuevo)}</span>
              </p>
            ) : (
              evento.detalle && <p className="mt-1 text-[#3d3a30]">{evento.detalle}</p>
            )}
          </li>
        ))}
      </ol>

      {historial.totalPaginas > 1 && (
        <nav aria-label="Páginas del historial" className="mt-3 flex items-center justify-between">
          <button type="button" className={btnSecundario} disabled={!hayAnteriores} onClick={anteriores}>
            Anteriores
          </button>
          <span className={etiqueta}>Página {historial.pagina} de {historial.totalPaginas}</span>
          <button type="button" className={btnSecundario} disabled={!haySiguientes} onClick={siguientes}>
            Siguientes
          </button>
        </nav>
      )}
    </div>
  )
}
