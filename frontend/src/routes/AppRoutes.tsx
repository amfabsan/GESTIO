import { PerfilFiscalPage } from '../pages/PerfilFiscalPage'
import { CalendarioFiscalPage } from '../pages/CalendarioFiscalPage'
import { Navigate, Route, Routes } from 'react-router-dom'
import { BuzonPage } from '../pages/autonomo/BuzonPage'
import { InicioPage } from '../pages/autonomo/InicioPage'
import { MiAsesoriaPage } from '../pages/autonomo/MiAsesoriaPage'
import { ContactosPage } from '../pages/autonomo/ContactosPage'
import { FacturacionPage } from '../pages/autonomo/FacturacionPage'
import { FacturasClientePage } from '../pages/gestor/FacturasClientePage'
import { ClientesPage } from '../pages/gestor/ClientesPage'
import { SolicitudesPage } from '../pages/gestor/SolicitudesPage'
import { BandejaValidacionPage } from '../pages/gestor/BandejaValidacionPage'
import { UsersPage } from '../pages/UsersPage'
import { QrPublicoPage } from '../pages/QrPublicoPage'
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
            {/* UT23 (PB-23): página del QR, pública. Fuera de AppLayout y de los guards: sin sesión ni navegación. */}
            <Route path="f/:codigo" element={<QrPublicoPage />} />

            <Route element={<AppLayout />}>
                <Route index element={<HomeRedirect />} />
                <Route path="users" element={<UsersPage />} />

                <Route element={<RoleGuard rolPermitido="AUTONOMO" />}>
                    <Route path="autonomo/calendario" element={<CalendarioFiscalPage />} />
                    <Route path="autonomo/perfil-fiscal" element={<PerfilFiscalPage />} />
                    <Route path="autonomo/inicio" element={<InicioPage />} />
                    <Route path="autonomo/buzon" element={<BuzonPage />} />
                    <Route path="autonomo/mi-asesoria" element={<MiAsesoriaPage />} />
                    <Route path="autonomo/contactos" element={<ContactosPage />} />
                    <Route path="autonomo/facturas" element={<FacturacionPage />} />
                </Route>

                <Route element={<RoleGuard rolPermitido="GESTOR" />}>
                    <Route path="gestor/clientes/:autonomoId/calendario" element={<CalendarioFiscalPage />} />
                    <Route path="gestor/clientes/:autonomoId/perfil-fiscal" element={<PerfilFiscalPage />} />
                    <Route path="gestor/clientes/:autonomoId/facturas" element={<FacturasClientePage />} />
                    <Route path="gestor/clientes" element={<ClientesPage />} />
                    <Route path="gestor/solicitudes" element={<SolicitudesPage />} />
                    <Route path="gestor/bandeja-validacion" element={<BandejaValidacionPage />} />
                </Route>

                <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
        </Routes>
    )
}
