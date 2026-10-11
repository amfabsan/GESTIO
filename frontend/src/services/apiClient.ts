import axios, { isAxiosError } from 'axios'

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Sprint 1 sin login: envía a la API el perfil elegido en la pantalla de usuarios.
apiClient.interceptors.request.use((config) => {
  const usuarioId = localStorage.getItem('gestio-usuario-id')
  if (usuarioId) config.headers.set('X-Usuario-Id', usuarioId)
  return config
})

// Mensaje que la API devuelve en sus errores (ProblemDetails), si lo hay.
export function obtenerDetalleError(excepcion: unknown) {
  return isAxiosError<{ detail?: string }>(excepcion) ? excepcion.response?.data?.detail : undefined
}