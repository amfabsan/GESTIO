import axios from 'axios'

// UT23 (PB-23): datos que muestra la página a la que lleva el QR. Son los únicos que devuelve la API pública.
export type FacturaPublica = {
  emisorNombre: string
  serieNumero: string
  fecha: string
  total: number
  aviso: string
}

// Cliente propio, SIN el interceptor de apiClient: la consulta pública no envía X-Usuario-Id aunque haya un
// perfil elegido en este navegador (req. 6 y prueba 1605: la página funciona igual sin sesión).
const clientePublico = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api' })

export async function consultarFacturaPublica(codigo: string) {
  const { data } = await clientePublico.get<FacturaPublica>(`/publico/facturas/${encodeURIComponent(codigo)}`)
  return data
}
