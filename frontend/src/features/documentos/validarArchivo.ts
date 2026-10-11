const FIRMA_POR_MIME: Record<string, number[]> = {
  'application/pdf': [0x25, 0x50, 0x44, 0x46, 0x2d],
  'image/jpeg': [0xff, 0xd8, 0xff],
  'image/png': [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
}

const MIME_POR_EXTENSION: Record<string, string> = {
  pdf: 'application/pdf',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  png: 'image/png',
}

export function formatearTamano(bytes: number) {
  const megas = bytes / (1024 * 1024)
  return `${new Intl.NumberFormat('es-ES', { maximumFractionDigits: 1 }).format(megas)} MB`
}

// Misma regla que la API: la extensión debe estar permitida y el contenido real coincidir con ella.
export async function validarArchivo(archivo: File, tamanoMaximoBytes: number): Promise<string | null> {
  if (archivo.size === 0) return 'El archivo está vacío.'
  if (archivo.size > tamanoMaximoBytes) return `El archivo supera el tamaño máximo de ${formatearTamano(tamanoMaximoBytes)}.`

  const extension = archivo.name.split('.').pop()?.toLowerCase() ?? ''
  const tipoMime = MIME_POR_EXTENSION[extension]
  const firma = tipoMime ? FIRMA_POR_MIME[tipoMime] : undefined
  const cabecera = new Uint8Array(await archivo.slice(0, 8).arrayBuffer())

  if (!firma || !firma.every((byte, indice) => cabecera[indice] === byte)) {
    return 'Solo se admiten archivos PDF, JPG o PNG.'
  }

  return null
}