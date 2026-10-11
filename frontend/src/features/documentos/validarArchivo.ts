import type { ConfiguracionSubida } from '../../types/documentos'
import { formatearTamano } from '../../utils/archivos'

export function describirTiposPermitidos(configuracion: ConfiguracionSubida) {
  return configuracion.tiposPermitidos.map((tipo) => tipo.extension.replace('.', '').toUpperCase()).join(', ')
}

// Aviso previo para no esperar a la subida. Las reglas las publica la API, que es quien valida de verdad.
export async function validarArchivo(archivo: File, configuracion: ConfiguracionSubida): Promise<string | null> {
  if (archivo.size === 0) return 'El archivo está vacío.'
  if (archivo.size > configuracion.tamanoMaximoBytes) {
    return `El archivo supera el tamaño máximo de ${formatearTamano(configuracion.tamanoMaximoBytes)}.`
  }

  const extension = `.${archivo.name.split('.').pop()?.toLowerCase() ?? ''}`
  const tipo = configuracion.tiposPermitidos.find((permitido) => permitido.extension === extension)
  if (!tipo || !(await coincideFirma(archivo, tipo.firmaHex))) {
    return `Solo se admiten archivos ${describirTiposPermitidos(configuracion)}.`
  }

  return null
}

async function coincideFirma(archivo: File, firmaHex: string) {
  const firma = (firmaHex.match(/../g) ?? []).map((byte) => parseInt(byte, 16))
  const cabecera = new Uint8Array(await archivo.slice(0, firma.length).arrayBuffer())
  return firma.every((byte, indice) => cabecera[indice] === byte)
}