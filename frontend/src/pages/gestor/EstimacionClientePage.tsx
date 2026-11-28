import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Calculator, Clock, FileCheck2, ListChecks } from 'lucide-react'
import { apiClient } from '../../services/apiClient'
import { btnPrimario, btnSecundario, card } from '../../components/ui/estilos'
import type { Vinculacion } from '../../types/vinculaciones'
import { obtenerIniciales } from '../../utils/usuarios'
import { useEstimacion } from '../../features/estimacion/hooks/useEstimacion'
import { VistaEstimacion } from '../../features/estimacion/components/VistaEstimacion'

export function EstimacionClientePage() {
    const { autonomoId } = useParams()
    const [ejercicio] = useState(2026)
    const [trimestre, setTrimestre] = useState(2)
    const [clienteNombre, setClienteNombre] = useState('Cliente')

    const { estimacion, cargando, error, procesando, actualizarInclusion } = useEstimacion(
        autonomoId ?? '',
        ejercicio,
        trimestre
    )

    useEffect(() => {
        if (!autonomoId) return
        let vigente = true

        apiClient
            .get<Vinculacion[]>('/vinculaciones')
            .then(res => {
                if (!vigente) return
                const vinc = res.data.find(v => v.autonomoId === autonomoId && v.estado === 'ACTIVA')
                if (vinc?.autonomo) {
                    const a = vinc.autonomo
                    setClienteNombre(`${a.nombre} ${a.primerApellido} ${a.segundoApellido ?? ''}`.trim())
                }
            })
            .catch(() => { })

        return () => {
            vigente = false
        }
    }, [autonomoId])

    if (!autonomoId) return null

    const inicialesCliente = obtenerIniciales(clienteNombre) || 'CL'
    const requiereAtencion = (estimacion?.elementosPorRevisar?.length ?? 0) > 0

    const fechaFormateada = estimacion?.fechaCalculoUtc
        ? new Date(estimacion.fechaCalculoUtc)
            .toLocaleDateString('es-ES', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
            .replace('.', '')
        : 'todavía sin calcular'

    return (
        <main className="min-h-screen bg-fondo px-4 py-7 text-tinta sm:px-8 lg:px-9">
            <Link to="/gestor/clientes" className="inline-block text-sm font-medium text-texto-suave hover:text-tinta">
                ← Volver al panel de clientes
            </Link>

            <div className="mt-5 flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
                <div className="flex items-center gap-3.5">
                    <span
                        className="grid size-11 shrink-0 place-items-center rounded-xl bg-tinta text-sm font-bold text-white"
                        aria-hidden="true"
                    >
                        {inicialesCliente}
                    </span>
                    <div>
                        <div className="flex flex-wrap items-center gap-2.5">
                            <h1 className="text-3xl font-bold text-tinta sm:text-[34px]">
                                {clienteNombre}
                            </h1>
                            {requiereAtencion ? (
                                <span className="inline-flex items-center gap-1 rounded-full bg-aviso-fondo px-2.5 py-0.5 text-xs font-bold text-aviso">
                                    ● Requiere atención
                                </span>
                            ) : (
                                <span className="inline-flex items-center gap-1 rounded-full bg-ok-fondo px-2.5 py-0.5 text-xs font-bold text-ok">
                                    ● Sin incidencias
                                </span>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            <nav
                aria-label="Secciones del cliente"
                className="mt-6 flex gap-6 overflow-x-auto border-b border-borde text-sm font-semibold"
            >
                <Link
                    to={`/gestor/clientes/${autonomoId}/perfil-fiscal`}
                    className="flex items-center gap-2 whitespace-nowrap pb-3 text-texto-suave hover:text-tinta"
                >
                    <FileCheck2 className="size-4" aria-hidden="true" />
                    Perfil fiscal
                </Link>
                <Link
                    to={`/gestor/clientes/${autonomoId}/calendario`}
                    className="flex items-center gap-2 whitespace-nowrap pb-3 text-texto-suave hover:text-tinta"
                >
                    <ListChecks className="size-4" aria-hidden="true" />
                    Obligaciones y checklist
                </Link>
                <span className="flex items-center gap-2 whitespace-nowrap border-b-2 border-acento pb-3 text-tinta">
                    <Calculator className="size-4" aria-hidden="true" />
                    Estimación
                </span>
            </nav>

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
                    <Clock className="size-3.5" aria-hidden="true" />
                    <span>Última actualización del cálculo: {fechaFormateada}</span>
                </div>
            </div>

            <VistaEstimacion
                estimacion={estimacion}
                cargando={cargando}
                error={error}
                procesando={procesando}
                esGestor={true}
                onActualizarInclusion={actualizarInclusion}
            />
        </main>
    )
}