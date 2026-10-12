import { Loader2 } from 'lucide-react'
import type { EstadoDocumento } from '../../../types/documentos'

const ESTADOS: Record<EstadoDocumento, { texto: string; clase: string }> = {
  SUBIDO: { texto: 'Subido', clase: 'bg-slate-100 text-slate-700' },
  PROCESANDO: { texto: 'Procesando', clase: 'bg-blue-50 text-blue-700' },
  PENDIENTE_VALIDACION: { texto: 'Pendiente de validar', clase: 'bg-amber-100 text-amber-800' },
  ACLARACION_SOLICITADA: { texto: 'Aclaración pedida', clase: 'bg-purple-100 text-purple-800' },
  VALIDADO: { texto: 'Validado', clase: 'bg-green-100 text-green-800' },
  ERROR: { texto: 'Error', clase: 'bg-red-100 text-red-800' },
}

export function EstadoBadge({ estado }: { estado: EstadoDocumento }) {
  const { texto, clase } = ESTADOS[estado] ?? { texto: estado, clase: 'bg-slate-100 text-slate-700' }

  return (
    <span className={`inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${clase}`}>
      {estado === 'PROCESANDO' && <Loader2 aria-hidden="true" className="size-3 animate-spin" />}
      {texto}
    </span>
  )
}