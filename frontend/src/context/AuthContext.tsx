import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiClient } from '../services/apiClient'
import type { UsuarioPerfil } from '../types/usuarios'
import { AuthContext, type AuthContextValue } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [perfiles, setPerfiles] = useState<UsuarioPerfil[]>([])
  const [perfilActivo, setPerfilActivo] = useState<UsuarioPerfil | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [intentoCarga, setIntentoCarga] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    apiClient.get<UsuarioPerfil[]>('/usuarios/perfiles', { signal: controller.signal })
      .then(({ data }) => {
        if (controller.signal.aborted) return

        setPerfiles(data)
        if (data.length === 0) {
          setPerfilActivo(null)
          setError('No hay usuarios disponibles en la base de datos.')
          return
        }

        const idGuardado = localStorage.getItem('gestio-usuario-id')
        const perfilGuardado = data.find((perfil) => perfil.id === idGuardado)
        const perfilInicial = perfilGuardado ?? data[0]
        setPerfilActivo(perfilInicial)
        localStorage.setItem('gestio-usuario-id', perfilInicial.id)
        setError(null)
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setError('No se han podido cargar los usuarios. Comprueba que la API y la base de datos estén disponibles.')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })

    return () => controller.abort()
  }, [intentoCarga])

  const recargarPerfiles = useCallback(() => {
    setCargando(true)
    setError(null)
    setIntentoCarga((intento) => intento + 1)
  }, [])

  const seleccionarPerfil = useCallback((perfil: UsuarioPerfil) => {
    setPerfilActivo(perfil)
    localStorage.setItem('gestio-usuario-id', perfil.id)
  }, [])

  const value = useMemo<AuthContextValue>(() => ({
    perfiles,
    perfilActivo,
    esGestor: perfilActivo?.rol === 'GESTOR',
    cargando,
    error,
    recargarPerfiles,
    seleccionarPerfil,
  }), [perfiles, perfilActivo, cargando, error, recargarPerfiles, seleccionarPerfil])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
