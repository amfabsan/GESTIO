export function formatearTamano(bytes: number) {
  const megas = bytes / (1024 * 1024)
  return `${new Intl.NumberFormat('es-ES', { maximumFractionDigits: 1 }).format(megas)} MB`
}

// Muestra un archivo descargado en una pestaña nueva y libera la memoria cuando ya se ha cargado.
export function abrirEnPestanaNueva(archivo: Blob) {
  const url = URL.createObjectURL(archivo)
  window.open(url, '_blank', 'noopener')
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}