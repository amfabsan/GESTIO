import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import { obtenerEtiquetaRol, obtenerIniciales, obtenerNombreCompleto } from '../../utils/usuarios'

export function Navbar() {
  const { perfilActivo, esGestor } = useAuth()
  const location = useLocation()

  if (!perfilActivo) return null

  const nombreCompleto = obtenerNombreCompleto(perfilActivo.nombre, perfilActivo.primerApellido, perfilActivo.segundoApellido)
  const claseEnlace = (ruta: string) => `whitespace-nowrap rounded-lg px-3 py-2 text-sm ${location.pathname === ruta ? 'bg-[#435271] font-semibold text-white' : 'text-[#dbe3f2] hover:bg-[#344362]'}`

  return (
    <nav className="flex min-h-14 flex-wrap items-center gap-2 bg-[#1b2a4a] px-4 py-2 text-white sm:px-8">
      <Link className="flex shrink-0 items-center gap-2 font-serif text-lg font-bold" to={esGestor ? '/gestor/clientes' : '/autonomo/inicio'}>
        <span className="grid size-7 place-items-center rounded-lg bg-[#d96c13] font-sans text-sm">G</span>Gestio
      </Link>
      <span className="hidden rounded bg-[#344362] px-2 py-1 text-[10px] font-bold tracking-wide text-[#c9d2e6] sm:inline">{obtenerEtiquetaRol(perfilActivo.rol).toUpperCase()}</span>

      <div className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto sm:ml-3">
        {esGestor ? (
          <>
            <Link className={claseEnlace('/gestor/clientes')} to="/gestor/clientes">♧ Clientes</Link>
            <Link className={claseEnlace('/gestor/solicitudes')} to="/gestor/solicitudes">♧ Solicitudes</Link>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">▣ Bandeja <span className="ml-1 rounded-full bg-[#d96c13] px-1.5 text-xs">6</span></button>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">▦ Calendario</button>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">◯ Mensajes <span className="ml-1 rounded-full bg-[#d96c13] px-1.5 text-xs">1</span></button>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">♧ Equipo</button>
          </>
        ) : (
          <>
            <Link className={claseEnlace('/autonomo/inicio')} to="/autonomo/inicio">⌂ Inicio</Link>
            <Link className={claseEnlace('/autonomo/buzon')} to="/autonomo/buzon">▱ Buzón</Link>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">▦ Calendario</button>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">▣ Facturas</button>
            <button className="whitespace-nowrap rounded-lg px-3 py-2 text-sm text-[#dbe3f2] hover:bg-[#344362]" type="button">◯ Mensajes <span className="ml-1 rounded-full bg-[#d96c13] px-1.5 text-xs">1</span></button>
            <Link className={claseEnlace('/autonomo/mi-asesoria')} to="/autonomo/mi-asesoria">↔ Mi asesoría</Link>
          </>
        )}
      </div>

      <Link className="ml-auto flex shrink-0 items-center gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-[#344362]" to="/users" aria-label={`Cambiar usuario. Perfil actual: ${nombreCompleto}`}>
        <svg aria-hidden="true" className="size-4" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
          <circle cx="12" cy="8" r="4" />
          <path d="M5 21a7 7 0 0 1 14 0" />
        </svg>
        <span className="grid size-7 place-items-center rounded-full bg-[#d96c13] text-xs font-bold text-white">{obtenerIniciales(nombreCompleto)}</span>
        <span className="hidden sm:inline">{nombreCompleto}</span>
      </Link>
    </nav>
  )
}