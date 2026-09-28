import { Link, Route, Routes } from 'react-router-dom'

function Inicio() {
  return <main><h1>Gestio</h1><p>Plataforma de gestión fiscal.</p></main>
}

export function AppRoutes() {
  return (
    <>
      <nav><Link to="/">Inicio</Link></nav>
      <Routes><Route path="/" element={<Inicio />} /></Routes>
    </>
  )
}
