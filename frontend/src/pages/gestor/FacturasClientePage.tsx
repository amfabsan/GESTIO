import { Link, useParams } from 'react-router-dom'
import { FacturacionVista } from '../../features/facturacion/FacturacionVista'

export function FacturasClientePage() {
  const { autonomoId } = useParams()
  if (!autonomoId) return null
  return <section className="min-h-screen bg-fondo px-4 py-6 sm:px-8 lg:px-9">
    <Link className="mb-4 inline-block text-sm text-texto-suave hover:underline" to="/gestor/clientes">← Volver a clientes</Link>
    <FacturacionVista autonomoId={autonomoId} soloLectura />
  </section>
}
