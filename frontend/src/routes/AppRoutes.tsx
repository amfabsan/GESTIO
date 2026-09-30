import { Link, Route, Routes } from 'react-router-dom'
import { DocumentosPage } from '../pages/DocumentosPage'

function Inicio() {
  return <main><h1>Gestio</h1><p>Plataforma de gestión fiscal.</p></main>
}

export function AppRoutes() {
  return (
    <>
      <nav className="flex gap-4"><Link to="/">Inicio</Link><Link to="/documentos">Documentos</Link></nav>
      <Routes>
        <Route path="/" element={<Inicio />} />
        <Route path="/documentos" element={<DocumentosPage />} />
      </Routes>
    </>
  )
}
