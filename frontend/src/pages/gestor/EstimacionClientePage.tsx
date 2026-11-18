import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Bell, Calculator, CheckCircle2, Clock, FileCheck2, ListChecks, MessageSquare, Users } from 'lucide-react'
import { apiClient } from '../../services/apiClient'
import { btnPrimario, btnSecundario, card } from '../../components/ui/estilos'
import type { Vinculacion } from '../../types/vinculaciones'
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

    const fechaFormateada = estimacion?.fechaCalculoUtc
        ? new Date(estimacion.fechaCalculoUtc)
            .toLocaleDateString('es-ES', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
            .replace('.', '')
        : '15 jun · 18:00'

    return (
        <main className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
            <Link to="/gestor/clientes" className="text-sm text-[#6b6656]">
                ← Volver al panel de clientes
            </Link>

            <div className="mt-7 flex flex-wrap items-center justify-between gap-4">
                <h1 className="font-serif text-4xl font-bold">{clienteNombre}</h1>

                <div className="flex items-center gap-2">
                    <button
                        type="button"
                        className="inline-flex items-center gap-1.5 rounded-xl border border-[#d6d2c8] bg-white px-4 py-2 text-sm font-semibold text-[#152546] hover:bg-[#ece9e3]"
                    >
                        <Bell className="size-4 text-[#6b6656]" />
                        Recordatorio
                    </button>
                    <button
                        type="button"
                        className="inline-flex items-center gap-1.5 rounded-xl border border-[#d6d2c8] bg-white px-4 py-2 text-sm font-semibold text-[#152546] hover:bg-[#ece9e3]"
                    >
                        <MessageSquare className="size-4 text-[#6b6656]" />
                        Mensajes
                    </button>
                    <button
                        type="button"
                        className="inline-flex items-center gap-1.5 rounded-xl bg-[#8a94a6] px-4 py-2 text-sm font-semibold text-white"
                    >
                        <CheckCircle2 className="size-4" />
                        Marcar revisado
                    </button>
                </div>
            </div>

            <nav className="mt-6 flex gap-6 overflow-x-auto border-b border-[#d6d2c8] text-sm font-semibold">
                <span aria-disabled="true" className="flex cursor-not-allowed items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656]/55">
                    <Users className="size-4" />
                    Resumen
                </span>
                <Link
                    to={`/gestor/clientes/${autonomoId}/perfil-fiscal`}
                    className="flex items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656] hover:text-[#1b2a4a]"
                >
                    <FileCheck2 className="size-4" />
                    Perfil fiscal
                </Link>
                <span aria-disabled="true" className="flex cursor-not-allowed items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656]/55">
                    <ListChecks className="size-4" />
                    Obligaciones y checklist
                </span>
                <span className="flex items-center gap-2 whitespace-nowrap border-b-2 border-[#d96c13] pb-3 text-[#1b2a4a]">
                    <Calculator className="size-4" />
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

                <div className="flex items-center gap-1.5 text-xs text-[#6b6656]">
                    <Clock className="size-3.5" />
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