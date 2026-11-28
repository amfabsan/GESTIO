import { useEffect, useRef, useState, type ComponentType } from 'react'
import { Link, NavLink } from 'react-router-dom'
import {
    Calculator,
    CalendarDays,
    ClipboardCheck,
    FileText,
    Handshake,
    House,
    Inbox,
    Menu,
    MessagesSquare,
    UserPlus,
    Users,
    UsersRound,
    X,
} from 'lucide-react'
import { useAuth } from '../../context/useAuth'
import { useConversaciones } from '../../features/documentos/hooks/useConversaciones'
import { obtenerEtiquetaRol, obtenerIniciales, obtenerNombreCompleto } from '../../utils/usuarios'

type Icono = ComponentType<{ className?: string; 'aria-hidden'?: boolean }>
type Enlace = { ruta: string; texto: string; icono: Icono }

// Solo rutas que existen. Lo que el Figma trae sin backend (Ayuda, Cerrar sesión…) no se muestra.
const ENLACES_AUTONOMO: Enlace[] = [
    { ruta: '/autonomo/inicio', texto: 'Inicio', icono: House },
    { ruta: '/autonomo/buzon', texto: 'Buzón', icono: Inbox },
    { ruta: '/autonomo/calendario', texto: 'Calendario', icono: CalendarDays },
    { ruta: '/autonomo/facturas', texto: 'Facturas', icono: FileText },
    { ruta: '/autonomo/estimacion', texto: 'Estimación', icono: Calculator },
    { ruta: '/autonomo/contactos', texto: 'Contactos', icono: Users },
    { ruta: '/autonomo/mi-asesoria', texto: 'Mi asesoría', icono: Handshake },
]

const ENLACES_GESTOR: Enlace[] = [
    { ruta: '/gestor/clientes', texto: 'Clientes', icono: UsersRound },
    { ruta: '/gestor/solicitudes', texto: 'Solicitudes', icono: UserPlus },
    { ruta: '/gestor/bandeja-validacion', texto: 'Bandeja', icono: ClipboardCheck },
]

const claseEnlace = ({ isActive }: { isActive: boolean }) =>
    `flex h-[42px] items-center gap-3 rounded-control px-3 text-[17px] font-medium transition-colors ${
        isActive ? 'bg-acento text-white' : 'text-white/85 hover:bg-white/10 hover:text-white'
    }`

function Separador() {
    return <hr className="mx-[-2px] my-5 border-white/15" />
}

export function BarraLateral() {
    const { perfilActivo, esGestor } = useAuth()
    const [abierta, setAbierta] = useState(false)
    const refNav = useRef<HTMLElement>(null)
    const refBoton = useRef<HTMLButtonElement>(null)

    const conversaciones = useConversaciones(perfilActivo?.id)
    const sinLeer = conversaciones?.datos.reduce((total, item) => total + item.sinLeer, 0) ?? 0

    // En móvil el cajón se cierra al pulsar un enlace (onClick del <nav>) y con Escape. Al abrirlo el foco entra en
    // el primer enlace; al cerrarlo con Escape vuelve al botón ☰.
    useEffect(() => {
        if (!abierta) return
        refNav.current?.querySelector<HTMLElement>('a')?.focus()
        const cerrarConEscape = (evento: KeyboardEvent) => {
            if (evento.key !== 'Escape') return
            setAbierta(false)
            refBoton.current?.focus()
        }
        window.addEventListener('keydown', cerrarConEscape)
        return () => window.removeEventListener('keydown', cerrarConEscape)
    }, [abierta])

    if (!perfilActivo) return null

    const nombreCompleto = obtenerNombreCompleto(perfilActivo.nombre, perfilActivo.primerApellido, perfilActivo.segundoApellido)
    const enlaces = esGestor ? ENLACES_GESTOR : ENLACES_AUTONOMO

    return (
        <>
            {/* Barra superior solo en móvil (el Figma no trae marcos móviles: decisión nuestra). */}
            <header className="sticky top-0 z-30 flex h-14 items-center justify-between bg-tinta px-4 text-white md:hidden">
                <Link className="text-lg font-bold" to={esGestor ? '/gestor/clientes' : '/autonomo/inicio'}>
                    Gestio
                </Link>
                <button
                    ref={refBoton}
                    aria-controls="barra-lateral"
                    aria-expanded={abierta}
                    aria-label={abierta ? 'Cerrar menú' : 'Abrir menú'}
                    className="grid size-10 place-items-center rounded-control hover:bg-white/10"
                    type="button"
                    onClick={() => setAbierta((valor) => !valor)}
                >
                    {abierta ? <X className="size-5" aria-hidden /> : <Menu className="size-5" aria-hidden />}
                </button>
            </header>

            {abierta && (
                <div aria-hidden="true" className="fixed inset-0 z-30 bg-tinta/50 md:hidden" onClick={() => setAbierta(false)} />
            )}

            <nav
                id="barra-lateral"
                ref={refNav}
                aria-label="Navegación principal"
                onClick={(evento) => (evento.target as HTMLElement).closest('a') && setAbierta(false)}
                className={`fixed inset-y-0 left-0 z-40 flex w-[205px] shrink-0 flex-col overflow-y-auto px-3 pb-6 pt-16 text-white transition-transform md:sticky md:top-0 md:h-screen md:translate-x-0 ${
                    abierta ? 'translate-x-0' : 'invisible -translate-x-full md:visible'
                } ${esGestor ? 'bg-degradado-gestio' : 'bg-tinta'}`}
            >
                <ul className="flex flex-col gap-[25px]">
                    {enlaces.map(({ ruta, texto, icono: IconoEnlace }) => (
                        <li key={ruta}>
                            <NavLink className={claseEnlace} to={ruta}>
                                <IconoEnlace className="size-4 shrink-0" aria-hidden />
                                {texto}
                            </NavLink>
                        </li>
                    ))}
                </ul>

                <Separador />

                <NavLink className={claseEnlace} to="/mensajes">
                    <MessagesSquare className="size-4 shrink-0" aria-hidden />
                    Mensajes
                    {sinLeer > 0 && (
                        <span
                            aria-label={`${sinLeer} ${sinLeer === 1 ? 'mensaje' : 'mensajes'} sin leer`}
                            className="ml-auto grid min-w-[19px] place-items-center rounded-pastilla bg-acento px-1.5 text-xs font-semibold text-white"
                        >
                            {sinLeer}
                        </span>
                    )}
                </NavLink>

                <Separador />

                {/* Tarjeta de cuenta del Figma. Lleva a la elección de perfil (Sprint 1 sin login), como el navbar anterior. */}
                <Link
                    className="flex items-center gap-3 rounded-control px-2 py-1.5 hover:bg-white/10"
                    to="/users"
                    aria-label={`Cambiar usuario. Perfil actual: ${nombreCompleto}`}
                >
                    <span className="grid size-[42px] shrink-0 place-items-center rounded-full bg-acento-suave text-sm font-bold text-acento">
                        {obtenerIniciales(nombreCompleto)}
                    </span>
                    <span className="min-w-0">
                        <span className="block truncate text-sm font-semibold">{nombreCompleto}</span>
                        <span className="mt-1 inline-block rounded-pastilla bg-white/15 px-2 py-0.5 text-[10px] font-semibold">
                            {obtenerEtiquetaRol(perfilActivo.rol)}
                        </span>
                    </span>
                </Link>
            </nav>
        </>
    )
}
