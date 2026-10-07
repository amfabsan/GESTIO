import { createContext } from 'react'
import type { UsuarioPerfil } from '../types/usuarios'

export interface AuthContextValue {
  perfiles: UsuarioPerfil[]
  perfilActivo: UsuarioPerfil | null
  esGestor: boolean
  cargando: boolean
  error: string | null
  recargarPerfiles: () => void
  seleccionarPerfil: (perfil: UsuarioPerfil) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
