const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic']

// Mismo formato que el prototipo: "16 jun 2026".
export function formatearFecha(iso: string) {
  const fecha = new Date(iso)
  return `${String(fecha.getDate()).padStart(2, '0')} ${MESES[fecha.getMonth()]} ${fecha.getFullYear()}`
}

// Mismo formato que el prototipo: "1.234,56 €". Intl con es-ES no agrupa los miles en números de 4 cifras.
export function formatearEuros(importe: number, decimales = 2) {
  const [entero, decimal] = Math.abs(importe).toFixed(decimales).split('.')
  const miles = entero.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  return `${importe < 0 ? '−' : ''}${miles}${decimal ? `,${decimal}` : ''} €`
}

// utils/formato.ts (al final)
export function formatearFechaHora(iso: string) {
    const fecha = new Date(iso)
          const hora = `${String(fecha.getHours()).padStart(2, '0')}:${String(fecha.getMinutes()).padStart(2, '0')}`
              const esHoy = fecha.toDateString() === new Date().toDateString()
                  const dia = esHoy
                        ? 'hoy'
                    : `${String(fecha.getDate()).padStart(2, '0')} ${MESES[fecha.getMonth()]}`
                  return `${dia} · ${hora}`
                    }