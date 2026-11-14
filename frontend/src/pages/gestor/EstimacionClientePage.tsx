import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useEstimacion } from '../../features/estimacion/hooks/useEstimacion'
import { VistaEstimacion } from '../../features/estimacion/components/VistaEstimacion'

export function EstimacionClientePage() {
  const { autonomoId } = useParams()
  const [ejercicio, setEjercicio] = useState(2026)
  const [trimestre, setTrimestre] = useState(1)

  const { estimacion, cargando, error, procesando, actualizarInclusion } = useEstimacion(
    autonomoId,
    ejercicio,
    trimestre
  )

  if (!autonomoId) return null

  return (
    <main className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
      <Link to="/gestor/clientes" className="text-sm text-[#6b6656] hover:underline">
        ← Volver al panel de clientes
      </Link>

      <h1 className="mt-7 font-serif text-4xl font-bold">Estimación de IVA del cliente</h1>
      <p className="mt-2 text-[#647083]">Previsión trimestral orientativa de IVA para validar gastos deducibles.</p>

      {/* Selector de ejercicio y trimestre */}
      <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:gap-6">
        <div>
          <label className="block text-sm font-semibold mb-2">Ejercicio fiscal</label>
          <select
            value={ejercicio}
            onChange={(e) => setEjercicio(Number(e.target.value))}
            disabled={cargando || procesando}
            className="rounded-lg border border-[#d6d2c8] bg-white px-4 py-2 text-[#1b2a4a] focus:outline-none focus:ring-2 focus:ring-[#d96c13]"
          >
            <option value={2026}>2026</option>
            <option value={2027}>2027</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-semibold mb-2">Trimestre</label>
          <div className="flex gap-2">
            {[1, 2, 3, 4].map((t) => (
              <button
                key={t}
                type="button"
                onClick={() => setTrimestre(t)}
                disabled={cargando || procesando}
                className={`rounded-lg px-4 py-2 font-semibold transition ${
                  trimestre === t
                    ? 'bg-[#d96c13] text-white'
                    : 'border border-[#d6d2c8] bg-white text-[#1b2a4a] hover:border-[#d96c13]'
                }`}
              >
                T{t}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Vista de estimación */}
      <div className="mt-8">
        <VistaEstimacion
          estimacion={estimacion}
          cargando={cargando}
          error={error}
          procesando={procesando}
          esGestor={true}
          onActualizarInclusion={actualizarInclusion}
        />
      </div>
    </main>
  )
}
