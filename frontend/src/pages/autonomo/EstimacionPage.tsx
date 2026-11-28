import { useState } from 'react'
import { Clock } from 'lucide-react'
import { useAuth } from '../../context/useAuth'
import { btnPrimario, btnSecundario, card } from '../../components/ui/estilos'
import { useEstimacion } from '../../features/estimacion/hooks/useEstimacion'
import { VistaEstimacion } from '../../features/estimacion/components/VistaEstimacion'

export function EstimacionPage() {
    const { perfilActivo } = useAuth()
    const [ejercicio] = useState(2026)
    const [trimestre, setTrimestre] = useState(2)

    const { estimacion, cargando, error, procesando, actualizarInclusion } = useEstimacion(
        perfilActivo?.id,
        ejercicio,
        trimestre
    )

    if (!perfilActivo?.id) return null

    const fechaFormateada = estimacion?.fechaCalculoUtc
        ? new Date(estimacion.fechaCalculoUtc)
            .toLocaleDateString('es-ES', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
            .replace('.', '')
        : 'todavía sin calcular'

    return (
        <main className="min-h-screen bg-fondo px-4 py-7 text-tinta sm:px-8 lg:px-9">
            <h1 className="text-[34px] font-bold text-tinta">Estimación fiscal</h1>
            <p className="mt-1.5 text-sm text-texto-suave">
                Previsión orientativa de IVA del trimestre y documentos de los que procede
            </p>

            {/* Barra de trimestres y última actualización */}
            <div className="my-6 flex flex-wrap items-center justify-between gap-4">
                <div className={`${card} inline-flex gap-1 p-1`} aria-label="Trimestre fiscal">
                    {[1, 2, 3, 4].map(t => (
                        <button
                            key={t}
                            type="button"
                            disabled={cargando || procesando}
                            onClick={() => setTrimestre(t)}
                            aria-pressed={t === trimestre}
                            className={t === trimestre ? btnPrimario : btnSecundario}
                        >
                            {t}T {ejercicio}
                        </button>
                    ))}
                </div>

                <div className="flex items-center gap-1.5 text-xs text-texto-suave">
                    <Clock className="size-3.5" />
                    <span>Última actualización del cálculo: {fechaFormateada}</span>
                </div>
            </div>

            <VistaEstimacion
                estimacion={estimacion}
                cargando={cargando}
                error={error}
                procesando={procesando}
                esGestor={false}
                onActualizarInclusion={actualizarInclusion}
            />
        </main>
    )
}