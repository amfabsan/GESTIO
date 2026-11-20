import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Bell, Calculator, CheckCircle2, Clock, FileCheck2, ListChecks, MessageSquare, Users } from 'lucide-react'
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
    const [avisoGestor, setAvisoGestor] = useState('')
    const [revisado, setRevisado] = useState(false)

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
    const requiereAtencion = !revisado && ((estimacion?.elementosPorRevisar?.length ?? 0) > 0)

    const fechaFormateada = estimacion?.fechaCalculoUtc
        ? new Date(estimacion.fechaCalculoUtc)
            .toLocaleDateString('es-ES', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
            .replace('.', '')
        : '15 jun · 18:00'

    return (
        <main className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
            <Link to="/gestor/clientes" className="inline-block text-sm font-medium text-[#6b6656] hover:text-[#1b2a4a]">
                ← Volver al panel de clientes
            </Link>

            <div className="mt-5 flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
                <div className="flex items-center gap-3.5">
                    <span
                        className="grid size-11 shrink-0 place-items-center rounded-xl bg-[#1d2b4f] text-sm font-bold text-white"
                        aria-hidden="true"
                    >
                        {inicialesCliente}
                    </span>
                    <div>
                        <div className="flex flex-wrap items-center gap-2.5">
                            <h1 className="font-serif text-3xl font-bold text-[#152546] sm:text-4xl">
                                {clienteNombre}
                            </h1>
                            {requiereAtencion ? (
                                <span className="inline-flex items-center gap-1 rounded-full bg-[#fff0df] px-2.5 py-0.5 text-xs font-bold text-[#c86b1c]">
                                    ● Requiere atención
                                </span>
                            ) : (
                                <span className="inline-flex items-center gap-1 rounded-full bg-[#e3f5e9] px-2.5 py-0.5 text-xs font-bold text-[#247344]">
                                    ● Sin incidencias
                                </span>
                            )}
                        </div>
                    </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    <button
                        type="button"
                        onClick={() => setAvisoGestor(`Recordatorio preparado para ${clienteNombre}.`)}
                        className="inline-flex items-center gap-1.5 rounded-xl border border-[#d6d2c8] bg-white px-3.5 py-2 text-xs font-semibold text-[#152546] hover:bg-[#ece9e3]"
                    >
                        <Bell className="size-3.5 text-[#6b6656]" aria-hidden="true" />
                        Recordatorio
                    </button>
                    <button
                        type="button"
                        onClick={() => setAvisoGestor(`Mensajes con ${clienteNombre} no disponibles en esta vista.`)}
                        className="inline-flex items-center gap-1.5 rounded-xl border border-[#d6d2c8] bg-white px-3.5 py-2 text-xs font-semibold text-[#152546] hover:bg-[#ece9e3]"
                    >
                        <MessageSquare className="size-3.5 text-[#6b6656]" aria-hidden="true" />
                        Mensajes
                    </button>
                    <button
                        type="button"
                        onClick={() => {
                            setRevisado(true)
                            setAvisoGestor(`Estado de revisión actualizado para ${clienteNombre}.`)
                        }}
                        className="inline-flex items-center gap-1.5 rounded-xl bg-[#1d2b4f] px-4 py-2 text-xs font-semibold text-white hover:bg-[#2b3e66]"
                    >
                        <CheckCircle2 className="size-3.5" aria-hidden="true" />
                        Marcar revisado
                    </button>
                </div>
            </div>

            <nav
                aria-label="Secciones del cliente"
                className="mt-6 flex gap-6 overflow-x-auto border-b border-[#d6d2c8] text-sm font-semibold"
            >
                <span aria-disabled="true" className="flex cursor-not-allowed items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656]/55">
                    <Users className="size-4" aria-hidden="true" />
                    Resumen
                </span>
                <Link
                    to={`/gestor/clientes/${autonomoId}/perfil-fiscal`}
                    className="flex items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656] hover:text-[#1b2a4a]"
                >
                    <FileCheck2 className="size-4" aria-hidden="true" />
                    Perfil fiscal
                </Link>
                <Link
                    to={`/gestor/clientes/${autonomoId}/calendario`}
                    className="flex items-center gap-2 whitespace-nowrap pb-3 text-[#6b6656] hover:text-[#1b2a4a]"
                >
                    <ListChecks className="size-4" aria-hidden="true" />
                    Obligaciones y checklist
                </Link>
                <span className="flex items-center gap-2 whitespace-nowrap border-b-2 border-[#d96c13] pb-3 text-[#1b2a4a]">
                    <Calculator className="size-4" aria-hidden="true" />
                    Estimación
                </span>
            </nav>

            {avisoGestor && (
                <div
                    role="status"
                    className="mt-4 flex items-center justify-between rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-xs text-amber-900"
                >
                    <span>{avisoGestor}</span>
                    <button
                        type="button"
                        onClick={() => setAvisoGestor('')}
                        className="font-bold underline"
                    >
                        Cerrar
                    </button>
                </div>
            )}

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