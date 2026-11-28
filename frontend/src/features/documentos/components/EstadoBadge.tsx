import { Loader2 } from 'lucide-react'
import type { EstadoDocumento } from '../../../types/documentos'

const ESTADOS: Record<EstadoDocumento, { texto: string; clase: string }> = {
  SUBIDO: { texto: 'Subido', clase: 'bg-fondo text-texto-suave' },
  EXTRAYENDO_DATOS: { texto: 'Extrayendo datos…', clase: 'bg-info-fondo text-info' },
  PENDIENTE_VALIDACION: { texto: 'Pendiente de validar', clase: 'bg-aviso-fondo text-aviso' },
  ACLARACION_SOLICITADA: { texto: 'Aclaración pedida', clase: 'bg-acento-suave text-acento' },
  VALIDADO: { texto: 'Validado', clase: 'bg-ok-fondo text-ok' },
  ERROR: { texto: 'Error', clase: 'bg-error-fondo text-error' },
}

export function EstadoBadge({ estado }: { estado: EstadoDocumento }) {
  const { texto, clase } = ESTADOS[estado] ?? { texto: estado, clase: 'bg-fondo text-texto-suave' }

  return (
    <span role={estado === 'EXTRAYENDO_DATOS' ? 'status' : undefined} className={`inline-flex items-center gap-1 whitespace-nowrap rounded-pastilla px-2.5 py-1 text-[11px] font-semibold ${clase}`}>
      {estado === 'EXTRAYENDO_DATOS' && <Loader2 aria-hidden="true" className="size-3 animate-spin" />}
      {texto}
    </span>
  )
}
