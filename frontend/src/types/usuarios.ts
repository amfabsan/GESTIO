export type RolUsuario = 'AUTONOMO' | 'GESTOR'

export interface UsuarioPerfil {
  id: string
  nombre: string
  primerApellido: string
  segundoApellido: string
  rol: RolUsuario
}
