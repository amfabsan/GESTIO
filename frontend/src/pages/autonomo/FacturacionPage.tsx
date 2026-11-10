import { useAuth } from '../../context/useAuth'
import { FacturacionVista } from '../../features/facturacion/FacturacionVista'

export function FacturacionPage() {
  const { perfilActivo } = useAuth()
  if (!perfilActivo) return null
  return <FacturacionVista autonomoId={perfilActivo.id} />
}
