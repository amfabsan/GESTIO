import { Route, Routes } from 'react-router-dom'
import { AppRoutes } from './routes/AppRoutes'
import { AuthProvider } from './context/AuthContext'
import { QrPublicoPage } from './pages/QrPublicoPage'

export default function App() {
  return (
    <Routes>
      {/* UT23 (PB-23): la página del QR queda FUERA de AuthProvider. Dentro, al abrirla se pedía la lista de
          perfiles (/usuarios/perfiles) y se guardaba un perfil en localStorage del visitante (req. 6, prueba 1607). */}
      <Route path="f/:codigo" element={<QrPublicoPage />} />
      <Route path="*" element={<AuthProvider><AppRoutes /></AuthProvider>} />
    </Routes>
  )
}
