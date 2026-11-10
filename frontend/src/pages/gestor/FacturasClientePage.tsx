import { Link, useParams } from 'react-router-dom'
import { FacturacionVista } from '../../features/facturacion/FacturacionVista'

export function FacturasClientePage() {
  const { autonomoId } = useParams()
  if (!autonomoId) return null
  return <section className="min-h-[calc(100vh-3.5rem)] bg-[#f5f3ef] px-4 py-6 sm:px-8 lg:px-14">
    <Link className="mb-4 inline-block text-sm text-[#6b6656] hover:underline" to="/gestor/clientes">← Volver a clientes</Link>
    <FacturacionVista autonomoId={autonomoId} soloLectura />
  </section>
}
