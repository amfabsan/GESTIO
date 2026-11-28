import { Outlet } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import { BarraLateral } from './BarraLateral'

export function AppLayout() {
  const { cargando, error, recargarPerfiles } = useAuth()

  if (cargando) {
    return <main className="min-h-screen bg-fondo p-8 text-tinta" aria-busy="true">Cargando usuarios...</main>
  }

  if (error) {
    return (
      <main className="min-h-screen bg-fondo px-6 py-12 text-tinta sm:px-10 lg:px-24">
        <h1 className="text-3xl font-bold">No se pueden cargar los perfiles</h1>
        <p className="mt-3 max-w-xl text-texto-suave">{error}</p>
        <button
          className="mt-6 rounded-control bg-tinta px-4 py-3 text-sm font-semibold text-white hover:bg-tinta-hover"
          type="button"
          onClick={recargarPerfiles}
        >
          Reintentar
        </button>
      </main>
    )
  }

  return (
    <div className="min-h-screen md:flex">
      <BarraLateral />
      <main className="min-w-0 flex-1"><Outlet /></main>
    </div>
  )
}
