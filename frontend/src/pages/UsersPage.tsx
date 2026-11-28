import type { UsuarioPerfil } from '../types/usuarios'
import { useAuth } from '../context/useAuth'
import { obtenerEtiquetaRol, obtenerIniciales, obtenerNombreCompleto } from '../utils/usuarios'
import { useNavigate } from 'react-router-dom'

export function UsersPage() {
  const { perfiles, perfilActivo, seleccionarPerfil } = useAuth()
  const navigate = useNavigate()

  function onSeleccionar(perfil: UsuarioPerfil) {
    seleccionarPerfil(perfil)
    navigate(perfil.rol === 'GESTOR' ? '/gestor/clientes' : '/autonomo/inicio')
  }

  return (
    <div className="min-h-screen bg-fondo px-6 py-12 text-tinta sm:px-10 lg:px-9">
      <header className="mb-8">
        <p className="mb-2 text-xs font-bold uppercase tracking-[0.08em] text-acento">Gestio</p>
        <h1 className="text-[34px] font-bold">Elegir perfil</h1>
        <p className="mt-2 text-texto-suave">Selecciona el espacio de trabajo que quieres utilizar.</p>
      </header>

      <section className="grid max-w-3xl gap-4 md:grid-cols-2" aria-label="Perfiles disponibles">
        {perfiles.map((perfil) => {
          const seleccionado = perfilActivo?.id === perfil.id
          return (
            <button
              className={`flex items-center gap-4 rounded-2xl border bg-white p-5 text-left shadow-sm transition hover:-translate-y-0.5 hover:shadow-md ${seleccionado ? 'border-acento ring-2 ring-acento/20' : 'border-borde'}`}
              type="button"
              key={perfil.id}
              aria-pressed={seleccionado}
              onClick={() => onSeleccionar(perfil)}
            >
              <span className="grid size-14 shrink-0 place-items-center rounded-full bg-tinta font-bold text-white">{obtenerIniciales(obtenerNombreCompleto(perfil.nombre, perfil.primerApellido, perfil.segundoApellido))}</span>
              <span className="min-w-0 flex-1">
                <span className="block font-semibold text-tinta">{obtenerNombreCompleto(perfil.nombre, perfil.primerApellido, perfil.segundoApellido)}</span>
                <span className="mt-1 block text-sm font-medium text-acento">{obtenerEtiquetaRol(perfil.rol)}</span>
              </span>
              {seleccionado && <span className="shrink-0 rounded-full bg-ok-fondo px-3 py-1 text-xs font-bold text-ok">Activo</span>}
            </button>
          )
        })}
      </section>
    </div>
  )
}
