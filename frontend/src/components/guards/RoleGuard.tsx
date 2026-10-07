import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../../context/useAuth'
import type { RolUsuario } from '../../types/usuarios'

export function RoleGuard({ rolPermitido }: { rolPermitido: RolUsuario }) {
  const { perfilActivo } = useAuth()

  if (perfilActivo?.rol !== rolPermitido) return <Navigate to="/" replace />
  return <Outlet />
}
