import { Link } from 'react-router-dom'
import type { ComponentType } from 'react'
import { ArrowRight, CalendarDays, FileText, Inbox, UserRound } from 'lucide-react'
import { useAuth } from '../../context/useAuth'

// Rediseño: se quitaron los datos inventados (gestor «Pedro Sánchez», «3 documentos», «Al día» y un botón sin
// acción). Inicio solo muestra datos reales del perfil y accesos a pantallas que existen.
const ACCESOS: { ruta: string; titulo: string; texto: string; icono: ComponentType<{ className?: string }> }[] = [
  { ruta: '/autonomo/buzon', titulo: 'Buzón', texto: 'Sube facturas y tickets para que tu asesoría los valide.', icono: Inbox },
  { ruta: '/autonomo/calendario', titulo: 'Calendario', texto: 'Consulta tus obligaciones y plazos fiscales.', icono: CalendarDays },
  { ruta: '/autonomo/facturas', titulo: 'Facturas', texto: 'Crea, revisa y emite tus facturas.', icono: FileText },
  { ruta: '/autonomo/perfil-fiscal', titulo: 'Perfil fiscal', texto: 'Revisa tus datos fiscales.', icono: UserRound },
]

export function InicioPage() {
  const { perfilActivo } = useAuth()

  return (
    <div className="min-h-screen bg-fondo px-4 py-7 text-tinta sm:px-8 lg:px-9 lg:py-[50px]">
      <section className="mb-8 max-w-3xl">
        <h1 className="text-[34px] font-bold leading-tight">Hola{perfilActivo?.nombre.trim() ? `, ${perfilActivo.nombre.trim()}` : ''}</h1>
        <p className="mt-1 text-base text-texto-gris">Accede a tu gestión y a la ayuda de tu asesoría.</p>
      </section>

      <ul className="grid max-w-5xl gap-5 sm:grid-cols-2">
        {ACCESOS.map(({ ruta, titulo, texto, icono: Icono }) => (
          <li key={ruta}>
            <Link
              to={ruta}
              className="group flex h-full items-start gap-4 rounded-tarjeta bg-tarjeta p-6 shadow-tarjeta transition-shadow hover:shadow-md"
            >
              <span className="grid size-[42px] shrink-0 place-items-center rounded-control bg-fondo text-tinta" aria-hidden="true">
                <Icono className="size-5" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-lg font-bold">{titulo}</span>
                <span className="mt-1 block text-sm text-texto-suave">{texto}</span>
              </span>
              <ArrowRight className="mt-1 size-4 shrink-0 text-texto-suave transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
