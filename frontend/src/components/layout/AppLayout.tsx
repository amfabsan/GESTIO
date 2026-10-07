import { Outlet } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import { Navbar } from './Navbar'

export function AppLayout() {
  const { cargando, error, recargarPerfiles } = useAuth()

  if (cargando) {
    return <main className="min-h-screen bg-[#f5f3ef] p-8 text-[#1b2a4a]" aria-busy="true">Cargando usuarios...</main>
  }

  if (error) {
    return (
      <main className="min-h-screen bg-[#f5f3ef] px-6 py-12 text-[#1b2a4a] sm:px-10 lg:px-24">
        <h1 className="font-serif text-3xl font-bold">No se pueden cargar los perfiles</h1>
        <p className="mt-3 max-w-xl text-[#647083]">{error}</p>
        <button
          className="mt-6 rounded-lg bg-[#1b2a4a] px-4 py-3 text-sm font-bold text-white hover:bg-[#2b3e66]"
          type="button"
          onClick={recargarPerfiles}
        >
          Reintentar
        </button>
      </main>
    )
  }

  return (
    <>
      <Navbar />
      <main><Outlet /></main>
    </>
  )
}
