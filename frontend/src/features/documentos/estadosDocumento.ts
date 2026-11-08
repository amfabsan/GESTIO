// Los eventos anteriores se conservan tal como fueron registrados; su presentación usa el nombre actual.
export function textoEstadoExtraccion(texto: string) {
  return texto.replace(/\b(?:PROCESANDO|EXTRAYENDO_DATOS)\b/g, 'Extrayendo datos…')
}
