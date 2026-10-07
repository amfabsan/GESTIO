import type { RolUsuario } from '../types/usuarios'

export function obtenerIniciales(nombre: string): string {
  return nombre
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte[0]?.toLocaleUpperCase('es-ES') ?? '')
    .join('')
}

export function obtenerNombreCompleto(nombre: string, primerApellido: string, segundoApellido: string): string {
  return [nombre, primerApellido, segundoApellido].filter(Boolean).join(' ')
}

export function obtenerEtiquetaRol(rol: RolUsuario): string {
  return rol === 'GESTOR' ? 'Gestor' : 'Autónomo'
}
