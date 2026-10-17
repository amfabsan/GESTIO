import { Navigate, Route, Routes } from 'react-router-dom'
import { BuzonPage } from '../pages/autonomo/BuzonPage'
import { InicioPage } from '../pages/autonomo/InicioPage'
import { MiAsesoriaPage } from '../pages/autonomo/MiAsesoriaPage'
import { ClientesPage } from '../pages/gestor/ClientesPage'
import { UsersPage } from '../pages/UsersPage'
import { useAuth } from '../context/useAuth'
import { AppLayout } from '../components/layout/AppLayout'
import { RoleGuard } from '../components/guards/RoleGuard'

function HomeRedirect() {
  const { esGestor } = useAuth()
  return <Navigate to={esGestor ? '/gestor/clientes' : '/autonomo/inicio'} replace />
}

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<HomeRedirect />} />
        <Route path="users" element={<UsersPage />} />
        <Route element={<RoleGuard rolPermitido="AUTONOMO" />}>
          <Route path="autonomo/inicio" element={<InicioPage />} />
          <Route path="autonomo/buzon" element={<BuzonPage />} />
          <Route path="autonomo/mi-asesoria" element={<MiAsesoriaPage />} />
        </Route>
        <Route element={<RoleGuard rolPermitido="GESTOR" />}>
          <Route path="gestor/clientes" element={<ClientesPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}