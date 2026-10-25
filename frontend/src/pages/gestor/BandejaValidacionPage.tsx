import { useEffect, useMemo, useState } from 'react'
import {
    AlertCircle,
    Check,
    FileText,
    HelpCircle,
    Loader2,
    Pencil,
    RefreshCw,
    Save,
    Send,
} from 'lucide-react'
import type { CategoriaDocumento, Documento, EstadoDocumento } from '../../types/documentos'
import { obtenerOriginal } from '../../features/documentos/documentosApi'
import { HistorialDocumento } from '../../features/documentos/components/HistorialDocumento'
import { etiquetaCampo } from '../../features/documentos/eventosDocumento'
import { useValidacionDocumentos } from '../../features/documentos/hooks/useValidacionDocumentos'
import { formatearFecha } from '../../utils/formato'

type Campo =
    | 'emisorNombre'
    | 'emisorNif'
    | 'numeroFactura'
    | 'fechaEmision'
    | 'baseImponible'
    | 'tipoIva'
    | 'cuotaIva'
    | 'total'

// La categoría propuesta por la IA no se corrige aquí: se conserva para evaluar la IA (PB-09)
// y el gestor elige la categoría vigente en el bloque «Categoría».
const CAMPOS: { clave: Campo; etiqueta: string }[] = [
    { clave: 'emisorNombre', etiqueta: 'Proveedor' },
    { clave: 'emisorNif', etiqueta: 'NIF' },
    { clave: 'numeroFactura', etiqueta: 'N.º factura' },
    { clave: 'fechaEmision', etiqueta: 'Fecha' },
    { clave: 'baseImponible', etiqueta: 'Base imponible' },
    { clave: 'tipoIva', etiqueta: 'IVA' },
    { clave: 'cuotaIva', etiqueta: 'Cuota de IVA' },
    { clave: 'total', etiqueta: 'Total' },
]

type Pestana = 'PENDIENTES' | 'ACLARACION' | 'APROBADOS' | 'TODOS'

const PESTANAS: { clave: Pestana; etiqueta: string; estado: EstadoDocumento | null }[] = [
    { clave: 'PENDIENTES', etiqueta: 'Pendientes', estado: 'PENDIENTE_VALIDACION' },
    { clave: 'ACLARACION', etiqueta: 'En aclaración', estado: 'ACLARACION_SOLICITADA' },
    { clave: 'APROBADOS', etiqueta: 'Aprobados', estado: 'VALIDADO' },
    { clave: 'TODOS', etiqueta: 'Todos', estado: null },
]

function documentosDePestana(documentos: Documento[], pestana: Pestana) {
    const estado = PESTANAS.find((p) => p.clave === pestana)?.estado ?? null

    return estado ? documentos.filter((d) => d.estado === estado) : documentos
}

const CATEGORIAS: CategoriaDocumento[] = ['INGRESO', 'GASTO', 'OTRO']

const CAMPOS_API: Record<Campo, string> = {
    emisorNombre: 'EmisorNombre',
    emisorNif: 'EmisorNif',
    numeroFactura: 'NumeroFactura',
    fechaEmision: 'FechaEmision',
    baseImponible: 'BaseImponible',
    tipoIva: 'TipoIva',
    cuotaIva: 'CuotaIva',
    total: 'Total',
}

function obtenerValor(documento: Documento, campo: Campo) {
    const extraccion = documento.extraccion

    if (!extraccion) return ''

    const valor = extraccion[campo]

    return valor == null ? '' : String(valor)
}

function formatearImporte(valor: string) {
    if (!valor) return '—'

    const numero = Number(valor.replace(',', '.'))

    if (Number.isNaN(numero)) return valor

    return `${numero.toLocaleString('es-ES', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })} €`
}

function esImporte(campo: Campo) {
    return ['baseImponible', 'cuotaIva', 'total'].includes(campo)
}

function obtenerTotal(documento: Documento) {
    const total = documento.extraccion?.total

    if (total == null) return '—'

    return `${total.toLocaleString('es-ES', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })} €`
}

function tieneAnomalia(documento: Documento) {
    return documento.anomalias.some((anomalia) => !anomalia.resuelta)
}

// Regla que originó cada aviso (PB-11). La explicación concreta llega en la descripción.
const REGLAS_ANOMALIA: Record<string, string> = {
    NIF_AUSENTE: 'NIF del emisor ausente',
    DESCUADRE_ARITMETICO: 'Importes que no cuadran',
    POSIBLE_DUPLICADO: 'Posible duplicado',
    BAJA_CONFIANZA: 'Lectura poco fiable',
}

function reglaAnomalia(tipo: string) {
    return REGLAS_ANOMALIA[tipo] ?? tipo
}

// Campo señalado por un aviso pendiente: se marca para que el gestor sepa qué revisar.
function campoARevisar(documento: Documento, campo: Campo) {
    return documento.anomalias.some(
        (anomalia) => !anomalia.resuelta && anomalia.campo === CAMPOS_API[campo],
    )
}

function estadoLegible(estado: Documento['estado']) {
    switch (estado) {
        case 'PENDIENTE_VALIDACION':
            return 'Pendiente de validar'
        case 'ACLARACION_SOLICITADA':
            return 'En aclaración'
        case 'VALIDADO':
            return 'Aprobado'
        case 'PROCESANDO':
            return 'Procesando'
        case 'ERROR':
            return 'Error'
        default:
            return estado
    }
}

const NIVELES_CONFIANZA: Record<string, { etiqueta: string; clases: string }> = {
    ALTO: { etiqueta: 'Confianza alta', clases: 'bg-[#e3f6ea] text-[#008b45]' },
    MEDIO: { etiqueta: 'Confianza media', clases: 'bg-[#fff0bf] text-[#a95500]' },
    DUDOSO: { etiqueta: 'Confianza dudosa', clases: 'bg-[#ffe0e0] text-[#c62828]' },
}

// Solo se muestra la confianza que calcula el backend; nunca se inventa en el cliente.
function confianzaCampo(documento: Documento, campo: Campo) {
    const nivel = documento.extraccion?.nivelConfianza

    if (!nivel || obtenerValor(documento, campo).trim() === '') return null

    return NIVELES_CONFIANZA[nivel] ?? null
}

function normalizarValorCampo(campo: Campo, valor: string) {
    const limpio = valor.trim()

    if (!limpio) {
        return null
    }

    if (campo === 'fechaEmision') {
        const partes = limpio.split('/')

        if (partes.length === 3) {
            const [dia, mes, anio] = partes

            if (
                dia.length === 2 &&
                mes.length === 2 &&
                anio.length === 4
            ) {
                return `${anio}-${mes}-${dia}`
            }
        }

        return limpio
    }

    if (
        campo === 'baseImponible' ||
        campo === 'tipoIva' ||
        campo === 'cuotaIva' ||
        campo === 'total'
    ) {
        const sinMoneda = limpio
            .replace(/€/g, '')
            .replace(/\s/g, '')

        if (sinMoneda.includes(',')) {
            return sinMoneda.replace(/\./g, '').replace(',', '.')
        }

        return sinMoneda
    }

    return limpio
}

export function BandejaValidacionPage() {
    const {
        documentos,
        cargando,
        error,
        procesando,
        cargar,
        corregir,
        aprobar,
        clasificar,
        pedirAclaracion,
    } = useValidacionDocumentos()

    const [pestana, setPestana] = useState<Pestana>('PENDIENTES')
    const [seleccionadoId, setSeleccionadoId] = useState<string | null>(null)
    const [modoCorreccion, setModoCorreccion] = useState(false)
    const [modoAclaracion, setModoAclaracion] = useState(false)
    const [pregunta, setPregunta] = useState('')
    const [cambios, setCambios] = useState<Record<string, string>>({})
    // Documento en el que falló la última acción: su error se muestra junto a sus datos.
    const [errorAccionDe, setErrorAccionDe] = useState<string | null>(null)

    const [originalUrl, setOriginalUrl] = useState<string | null>(null)
    const [cargandoOriginal, setCargandoOriginal] = useState(false)
    const [errorOriginal, setErrorOriginal] = useState<string | null>(null)

    const visibles = useMemo(
        () => documentosDePestana(documentos, pestana),
        [documentos, pestana],
    )

    const pendientes = useMemo(
        () => documentosDePestana(documentos, 'PENDIENTES').length,
        [documentos],
    )

    const documentoSeleccionado = useMemo(() => {
        if (!seleccionadoId) {
            return visibles[0] ?? null
        }

        return (
            visibles.find((documento) => documento.id === seleccionadoId) ??
            visibles[0] ??
            null
        )
    }, [visibles, seleccionadoId])

    const admiteValidacion =
        documentoSeleccionado?.estado === 'PENDIENTE_VALIDACION'

    const nivelPropuesta = documentoSeleccionado?.nivelConfianzaPropuesta
    const confianzaPropuesta = nivelPropuesta ? NIVELES_CONFIANZA[nivelPropuesta] ?? null : null

    const errorAccion =
        error && errorAccionDe !== null && errorAccionDe === documentoSeleccionado?.id
            ? error
            : null

    useEffect(() => {
        let urlCreada: string | null = null
        let cancelado = false

        async function cargarArchivoOriginal() {
            if (!documentoSeleccionado?.tieneOriginal) {
                setOriginalUrl(null)
                setErrorOriginal(null)
                setCargandoOriginal(false)
                return
            }

            setCargandoOriginal(true)
            setErrorOriginal(null)
            setOriginalUrl(null)

            try {
                const archivo = await obtenerOriginal(documentoSeleccionado.id)

                if (cancelado) return

                urlCreada = URL.createObjectURL(archivo)
                setOriginalUrl(urlCreada)
            } catch {
                if (!cancelado) {
                    setErrorOriginal(
                        'No se ha podido cargar el documento original.',
                    )
                }
            } finally {
                if (!cancelado) {
                    setCargandoOriginal(false)
                }
            }
        }

        void cargarArchivoOriginal()

        return () => {
            cancelado = true

            if (urlCreada) {
                URL.revokeObjectURL(urlCreada)
            }
        }
    }, [documentoSeleccionado?.id, documentoSeleccionado?.tieneOriginal])

    function seleccionarDocumento(id: string) {
        setSeleccionadoId(id)
        setModoCorreccion(false)
        setModoAclaracion(false)
        setCambios({})
        setPregunta('')
    }

    function cambiarPestana(nueva: Pestana) {
        setPestana(nueva)
        setSeleccionadoId(null)
        setModoCorreccion(false)
        setModoAclaracion(false)
        setCambios({})
        setPregunta('')
    }

    function abrirDocumentoRelacionado(id: string) {
        cambiarPestana('TODOS')
        setSeleccionadoId(id)
    }

    function cambiarCampo(campo: Campo, valor: string) {
        setCambios((actuales) => ({
            ...actuales,
            [campo]: valor,
        }))
    }

    async function guardarCorrecciones() {
        if (!documentoSeleccionado) return

        const campos = Object.entries(cambios).map(([campo, valor]) => {
            const campoFrontend = campo as Campo

            return {
                campo: CAMPOS_API[campoFrontend] ?? campo,
                valor: normalizarValorCampo(campoFrontend, valor),
            }
        })

        if (campos.length === 0) {
            setModoCorreccion(false)
            return
        }

        const actualizado = await corregir(
            documentoSeleccionado.id,
            campos,
        )

        if (actualizado) {
            setCambios({})
            setModoCorreccion(false)
            setErrorAccionDe(null)
        } else {
            setErrorAccionDe(documentoSeleccionado.id)
        }
    }

    async function aprobarDocumento() {
        if (!documentoSeleccionado) return

        const actualizado = await aprobar(documentoSeleccionado.id)

        if (actualizado) {
            setSeleccionadoId(null)
            setCambios({})
            setModoCorreccion(false)
            setErrorAccionDe(null)
        } else {
            setErrorAccionDe(documentoSeleccionado.id)
        }
    }

    async function guardarCategoria(categoria: CategoriaDocumento) {
        if (!documentoSeleccionado) return

        const actualizado = await clasificar(documentoSeleccionado.id, categoria)

        if (actualizado) {
            setErrorAccionDe(null)
        } else {
            setErrorAccionDe(documentoSeleccionado.id)
        }
    }

    async function enviarAclaracion() {
        if (!documentoSeleccionado || !pregunta.trim()) return

        const enviada = await pedirAclaracion(documentoSeleccionado.id, pregunta.trim())

        if (enviada) {
            setPregunta('')
            setModoAclaracion(false)
            setSeleccionadoId(null)
            setErrorAccionDe(null)
        } else {
            setErrorAccionDe(documentoSeleccionado.id)
        }
    }

    if (cargando) {
        return (
            <main className="flex min-h-[calc(100dvh-7rem)] items-center justify-center bg-[#f7f6f2]">
                <Loader2 className="size-7 animate-spin text-[#1d2b55]" />
            </main>
        )
    }

    return (
        <main className="min-h-[calc(100dvh-7rem)] bg-[#f7f6f2] px-5 py-6 lg:px-8">
            <div className="mx-auto max-w-[1450px]">
                <header className="flex items-start justify-between gap-6">
                    <div>
                        <h1 className="font-serif text-4xl font-bold tracking-tight text-[#1d2b55]">
                            Bandeja de validación
                        </h1>

                        <p className="mt-2 text-lg text-[#766e61]">
                            Revisa lo que ha leído la IA y apruébalo con un clic
                        </p>
                    </div>

                    <div className="shrink-0 rounded-full bg-[#d66b13] px-5 py-2.5 text-base font-bold text-white">
                        {pendientes} por validar
                    </div>
                </header>

                <div className="mt-6 flex flex-wrap items-center gap-2">
                    {PESTANAS.map(({ clave, etiqueta }) => {
                        const activa = clave === pestana

                        return (
                            <button
                                key={clave}
                                type="button"
                                aria-pressed={activa}
                                onClick={() => cambiarPestana(clave)}
                                className={
                                    activa
                                        ? 'rounded-full border-2 border-[#1d2b55] bg-[#1d2b55] px-5 py-2 text-base font-bold text-white'
                                        : 'rounded-full border-2 border-[#ddd8ce] bg-white px-5 py-2 text-base font-medium text-[#766e61] hover:bg-[#f0eee8]'
                                }
                            >
                                {etiqueta} ({documentosDePestana(documentos, clave).length})
                            </button>
                        )
                    })}

                    <button
                        type="button"
                        onClick={() => {
                            setErrorAccionDe(null)
                            void cargar()
                        }}
                        disabled={procesando}
                        className="ml-auto inline-flex items-center gap-2 rounded-full border-2 border-[#ddd8ce] bg-white px-4 py-2 text-sm font-bold text-[#1d2b55] transition hover:bg-[#f0eee8] disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        <RefreshCw className="size-4" />
                        Actualizar
                    </button>
                </div>

                {error && errorAccionDe === null && (
                    <div
                        role="alert"
                        className="mt-4 flex items-center gap-3 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"
                    >
                        <AlertCircle className="size-5 shrink-0" />
                        <span>{error}</span>
                    </div>
                )}

                {visibles.length === 0 ? (
                    <section className="mt-6 rounded-3xl border border-[#ddd8ce] bg-white p-16 text-center">
                        <Check className="mx-auto size-10 text-emerald-600" />

                        <h2 className="mt-4 text-xl font-bold text-[#1d2b55]">
                            {pestana === 'PENDIENTES'
                                ? 'No hay documentos pendientes'
                                : 'No hay documentos en esta pestaña'}
                        </h2>

                        {pestana === 'PENDIENTES' && (
                            <p className="mt-2 text-sm text-[#766e61]">
                                Todos los documentos han sido revisados.
                            </p>
                        )}
                    </section>
                ) : (
                    <section className="mt-6 grid min-h-[620px] grid-cols-[300px_minmax(0,1fr)] overflow-hidden rounded-3xl border border-[#d9d4ca] bg-white">
                        <aside className="overflow-y-auto border-r border-[#e6e1d8]">
                            {visibles.map((documento) => {
                                const seleccionado =
                                    documento.id === documentoSeleccionado?.id

                                return (
                                    <button
                                        key={documento.id}
                                        type="button"
                                        onClick={() =>
                                            seleccionarDocumento(documento.id)
                                        }
                                        className={`relative w-full border-b border-[#e6e1d8] px-5 py-4 text-left transition ${seleccionado
                                            ? 'bg-[#efede8]'
                                            : 'bg-white hover:bg-[#faf9f6]'
                                            }`}
                                    >
                                        {seleccionado && (
                                            <span className="absolute inset-y-0 left-0 w-1 bg-[#d66b13]" />
                                        )}

                                        <p className="truncate text-base font-bold text-[#151515]">
                                            {documento.nombreArchivo}
                                        </p>

                                        <p className="mt-1 text-sm text-[#766e61]">
                                            {documento.usuario} ·{' '}
                                            {formatearFecha(
                                                documento.fechaSubida,
                                            )}
                                        </p>

                                        <div className="mt-2 flex items-center gap-2">
                                            <span className="text-base font-bold text-[#008b45]">
                                                {obtenerTotal(documento)}
                                            </span>

                                            {tieneAnomalia(documento) && (
                                                <span className="rounded-full bg-[#ffe0e0] px-2 py-0.5 text-xs font-bold text-[#c62828]">
                                                    Anomalía
                                                </span>
                                            )}
                                        </div>
                                    </button>
                                )
                            })}
                        </aside>

                        {documentoSeleccionado && (
                            <section className="flex min-w-0 flex-col">
                                <header className="flex items-center justify-between gap-4 border-b border-[#e6e1d8] px-7 py-5">
                                    <div className="min-w-0">
                                        <h2 className="truncate text-2xl font-bold text-[#1d2b55]">
                                            {documentoSeleccionado.nombreArchivo}
                                        </h2>

                                        <p className="mt-1 text-sm text-[#766e61]">
                                            {documentoSeleccionado.usuario} ·{' '}
                                            {documentoSeleccionado.origen ||
                                                'Documento'}{' '}
                                            ·{' '}
                                            {formatearFecha(
                                                documentoSeleccionado.fechaSubida,
                                            )}
                                        </p>
                                    </div>

                                    <div className="flex shrink-0 items-center gap-3">
                                        <span className="rounded-full bg-[#fff0bf] px-3 py-1.5 text-sm font-bold text-[#a95500]">
                                            {estadoLegible(
                                                documentoSeleccionado.estado,
                                            )}
                                        </span>

                                        <strong className="text-xl text-[#1d2b55]">
                                            {obtenerTotal(documentoSeleccionado)}
                                        </strong>
                                    </div>
                                </header>

                                <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1fr)_390px]">
                                    <section className="min-w-0 overflow-y-auto border-r border-[#e6e1d8] p-6">
                                        <div className="flex items-center gap-2">
                                            <FileText className="size-5 text-[#1d2b55]" />

                                            <h3 className="text-sm font-bold uppercase tracking-wide text-[#766e61]">
                                                Documento original
                                            </h3>
                                        </div>

                                        <div
                                            id="documento-original"
                                            className="mt-4 flex min-h-[330px] items-center justify-center overflow-hidden rounded-2xl border border-[#d9d4ca] bg-[#eeeae1] p-5"
                                        >
                                            {cargandoOriginal && (
                                                <div className="text-center">
                                                    <Loader2 className="mx-auto size-7 animate-spin text-[#1d2b55]" />

                                                    <p className="mt-2 text-sm text-[#766e61]">
                                                        Cargando documento…
                                                    </p>
                                                </div>
                                            )}

                                            {!cargandoOriginal &&
                                                errorOriginal && (
                                                    <div className="text-center text-sm text-red-700">
                                                        <AlertCircle className="mx-auto size-7" />

                                                        <p className="mt-2 font-semibold">
                                                            {errorOriginal}
                                                        </p>
                                                    </div>
                                                )}

                                            {!cargandoOriginal &&
                                                !errorOriginal &&
                                                originalUrl &&
                                                (documentoSeleccionado.tipoMime.startsWith('image/') ? (
                                                    <a
                                                        href={originalUrl}
                                                        target="_blank"
                                                        rel="noreferrer"
                                                        title="Abrir a tamaño completo"
                                                    >
                                                        <img
                                                            src={originalUrl}
                                                            alt={`Documento original: ${documentoSeleccionado.nombreArchivo}`}
                                                            className="max-h-[390px] w-auto max-w-full rounded-lg bg-white object-contain shadow-md"
                                                        />
                                                    </a>
                                                ) : (
                                                    <iframe
                                                        title={`Documento original: ${documentoSeleccionado.nombreArchivo}`}
                                                        src={originalUrl}
                                                        className="h-[390px] w-full rounded-lg bg-white shadow-md"
                                                    />
                                                ))}

                                            {!cargandoOriginal &&
                                                !errorOriginal &&
                                                !originalUrl && (
                                                    <p className="text-sm text-[#766e61]">
                                                        No hay documento original disponible.
                                                    </p>
                                                )}
                                        </div>

                                        {tieneAnomalia(documentoSeleccionado) && (
                                            <div className="mt-5 rounded-2xl border border-red-200 bg-[#fff4f4] p-4">
                                                <div className="flex gap-3">
                                                    <AlertCircle className="mt-0.5 size-5 shrink-0 text-red-600" />

                                                    <div>
                                                        <p className="text-sm font-bold text-red-700">
                                                            Posible anomalía detectada
                                                        </p>

                                                        {documentoSeleccionado.anomalias
                                                            .filter(
                                                                (anomalia) =>
                                                                    !anomalia.resuelta,
                                                            )
                                                            .map((anomalia) => (
                                                                <div
                                                                    key={anomalia.id}
                                                                    className="mt-2"
                                                                >
                                                                    <p className="text-sm font-semibold text-red-700">
                                                                        {reglaAnomalia(anomalia.tipo)}
                                                                        {anomalia.campo &&
                                                                            ` · Revisa: ${etiquetaCampo(anomalia.campo)}`}
                                                                    </p>

                                                                    <p className="text-sm leading-5 text-red-700">
                                                                        {anomalia.descripcion}
                                                                    </p>

                                                                    {anomalia.documentoRelacionadoId && (
                                                                        <button
                                                                            type="button"
                                                                            onClick={() =>
                                                                                abrirDocumentoRelacionado(
                                                                                    anomalia.documentoRelacionadoId!,
                                                                                )
                                                                            }
                                                                            className="mt-2 mr-4 text-sm font-bold text-red-700 underline"
                                                                        >
                                                                            Ver documento anterior →
                                                                        </button>
                                                                    )}
                                                                </div>
                                                            ))}

                                                        <button
                                                            type="button"
                                                            onClick={() =>
                                                                document
                                                                    .getElementById(
                                                                        'documento-original',
                                                                    )
                                                                    ?.scrollIntoView({
                                                                        behavior:
                                                                            'smooth',
                                                                    })
                                                            }
                                                            className="mt-3 text-sm font-bold text-red-700 underline"
                                                        >
                                                            Ver documento original →
                                                        </button>
                                                    </div>
                                                </div>
                                            </div>
                                        )}

                                        {documentoSeleccionado.anomalias.some((anomalia) => anomalia.resuelta) && (
                                            <div className="mt-3 rounded-2xl border border-[#e6e1d8] bg-[#faf9f6] p-4">
                                                <p className="text-xs font-bold uppercase tracking-wide text-[#766e61]">
                                                    Avisos resueltos
                                                </p>

                                                {documentoSeleccionado.anomalias
                                                    .filter((anomalia) => anomalia.resuelta)
                                                    .map((anomalia) => (
                                                        <p
                                                            key={anomalia.id}
                                                            className="mt-1.5 flex items-center gap-1.5 text-sm text-[#766e61]"
                                                        >
                                                            <Check className="size-3.5 shrink-0 text-[#008b45]" />
                                                            {reglaAnomalia(anomalia.tipo)}
                                                            {anomalia.campo && ` (${etiquetaCampo(anomalia.campo)})`}
                                                            {' · detectado el '}
                                                            {formatearFecha(anomalia.fechaDeteccion)}
                                                        </p>
                                                    ))}
                                            </div>
                                        )}

                                        <div className="mt-6">
                                            <HistorialDocumento
                                                key={`${documentoSeleccionado.id}-${documentoSeleccionado.estado}`}
                                                documentoId={documentoSeleccionado.id}
                                            />
                                        </div>
                                    </section>

                                    <aside className="flex min-h-0 flex-col p-6">
                                        <div className="mt-3 rounded-2xl border border-[#d9d4ca] bg-[#f8f6f3] p-4">
                                            <div className="flex items-center justify-between gap-3">
                                                <div className="flex items-center gap-2">
                                                    <span className="flex size-7 items-center justify-center rounded-md bg-[#1d2b55] text-[10px] font-bold text-white">
                                                        IA
                                                    </span>
                                                    <span className="text-xs font-bold uppercase tracking-[0.12em] text-[#766e61]">
                                                        Categoría
                                                    </span>
                                                </div>

                                                {documentoSeleccionado.categoriaPropuesta && confianzaPropuesta && (
                                                    <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${confianzaPropuesta.clases}`}>
                                                        {confianzaPropuesta.etiqueta}
                                                    </span>
                                                )}
                                            </div>

                                            {documentoSeleccionado.categoriaPropuesta && (
                                                <p className="mt-3 text-sm font-medium text-[#1d2b55]">
                                                    La IA propone <span className="font-bold">{documentoSeleccionado.categoriaPropuesta}</span>
                                                </p>
                                            )}

                                            {documentoSeleccionado.categoria === 'SIN_CLASIFICAR' && (
                                                <p className="mt-2 text-sm text-[#766e61]">
                                                    Sin clasificar: elige una categoría para guardarla.
                                                </p>
                                            )}

                                            <div className="mt-3 grid grid-cols-3 gap-2">
                                                {CATEGORIAS.map((categoria) => {
                                                    const activa = documentoSeleccionado.categoria === categoria
                                                    const propuesta = documentoSeleccionado.categoriaPropuesta === categoria

                                                    return (
                                                        <button
                                                            key={categoria}
                                                            type="button"
                                                            onClick={() => void guardarCategoria(categoria)}
                                                            disabled={procesando}
                                                            aria-pressed={activa}
                                                            className={[
                                                                'rounded-xl border px-3 py-2.5 text-sm font-bold transition disabled:cursor-not-allowed disabled:opacity-60',
                                                                activa
                                                                    ? 'border-[#1d2b55] bg-[#1d2b55] text-white shadow-sm'
                                                                    : propuesta
                                                                        ? 'border-[#d66b13] bg-[#fff4ea] text-[#d66b13]'
                                                                        : 'border-[#d9d4ca] bg-white text-[#1d2b55] hover:bg-[#f5f3ee]',
                                                            ].join(' ')}
                                                        >
                                                            {categoria}
                                                        </button>
                                                    )
                                                })}
                                            </div>
                                        </div>

                                        <div className="mt-5 flex items-center gap-2">
                                            <span className="flex size-9 items-center justify-center rounded-lg bg-[#1d2b55] text-xs font-bold text-white">
                                                IA
                                            </span>

                                            <h3 className="text-sm font-bold uppercase tracking-wide text-[#766e61]">
                                                {modoCorreccion
                                                    ? 'Corrige los campos'
                                                    : 'Datos extraídos'}
                                            </h3>
                                        </div>

                                        {modoCorreccion ? (
                                            <div className="mt-5 min-h-0 flex-1 space-y-4 overflow-y-auto pr-1">
                                                {CAMPOS.map(
                                                    ({ clave, etiqueta }) => (
                                                        <label
                                                            key={clave}
                                                            className="block"
                                                        >
                                                            <span className="mb-1.5 flex items-center gap-1.5 text-sm font-semibold text-[#766e61]">
                                                                {etiqueta}
                                                                {campoARevisar(documentoSeleccionado, clave) && (
                                                                    <span className="rounded-full bg-[#ffe0e0] px-2 py-0.5 text-xs font-bold text-[#c62828]">
                                                                        Revisar
                                                                    </span>
                                                                )}
                                                            </span>

                                                            <input
                                                                type="text"
                                                                value={
                                                                    cambios[
                                                                    clave
                                                                    ] ??
                                                                    obtenerValor(
                                                                        documentoSeleccionado,
                                                                        clave,
                                                                    )
                                                                }
                                                                onChange={(event) =>
                                                                    cambiarCampo(
                                                                        clave,
                                                                        event.target
                                                                            .value,
                                                                    )
                                                                }
                                                                className="w-full rounded-lg border border-[#d9d4ca] bg-[#faf9f6] px-3 py-2 text-sm text-[#151515] outline-none focus:border-[#1d2b55] focus:ring-2 focus:ring-[#1d2b55]/10"
                                                            />
                                                        </label>
                                                    ),
                                                )}
                                            </div>
                                        ) : (
                                            <div className="mt-5 min-h-0 flex-1 space-y-5 overflow-y-auto pr-1">
                                                {CAMPOS.map(
                                                    ({ clave, etiqueta }) => {
                                                        const valor = obtenerValor(
                                                            documentoSeleccionado,
                                                            clave,
                                                        )
                                                        const confianza =
                                                            confianzaCampo(
                                                                documentoSeleccionado,
                                                                clave,
                                                            )

                                                        return (
                                                            <div key={clave}>
                                                                <div className="flex items-end justify-between gap-3">
                                                                    <span className="flex items-center gap-1.5 text-sm text-[#766e61]">
                                                                        {etiqueta}
                                                                        {campoARevisar(documentoSeleccionado, clave) && (
                                                                            <span className="rounded-full bg-[#ffe0e0] px-2 py-0.5 text-xs font-bold text-[#c62828]">
                                                                                Revisar
                                                                            </span>
                                                                        )}
                                                                    </span>

                                                                    <strong className="text-right text-sm text-[#151515]">
                                                                        {esImporte(
                                                                            clave,
                                                                        )
                                                                            ? formatearImporte(
                                                                                valor,
                                                                            )
                                                                            : valor ||
                                                                            'Sin detectar'}
                                                                    </strong>
                                                                </div>

                                                                {confianza && (
                                                                    <span
                                                                        className={`mt-1.5 inline-block rounded-full px-2 py-0.5 text-xs font-bold ${confianza.clases}`}
                                                                    >
                                                                        {confianza.etiqueta}
                                                                    </span>
                                                                )}
                                                            </div>
                                                        )
                                                    },
                                                )}
                                            </div>
                                        )}

                                        {modoAclaracion && (
                                            <div className="mt-4 rounded-2xl border border-[#d9d4ca] bg-[#faf9f6] p-3">
                                                <p className="mb-2 text-sm font-bold text-[#1d2b55]">
                                                    ¿Qué necesitas que aclare el
                                                    cliente?
                                                </p>

                                                <textarea
                                                    value={pregunta}
                                                    onChange={(event) =>
                                                        setPregunta(
                                                            event.target.value,
                                                        )
                                                    }
                                                    maxLength={500}
                                                    rows={3}
                                                    placeholder="Escribe tu pregunta..."
                                                    className="w-full resize-none rounded-lg border border-[#1d2b55] bg-white p-3 text-sm outline-none"
                                                />

                                                <div className="mt-2 grid grid-cols-2 gap-2">
                                                    <button
                                                        type="button"
                                                        onClick={() => {
                                                            setModoAclaracion(false)
                                                            setPregunta('')
                                                        }}
                                                        className="rounded-lg bg-[#eeeae1] px-3 py-2 text-sm font-bold text-[#1d2b55]"
                                                    >
                                                        Cancelar
                                                    </button>

                                                    <button
                                                        type="button"
                                                        onClick={() => void enviarAclaracion()}
                                                        disabled={
                                                            !pregunta.trim() || procesando
                                                        }
                                                        className="rounded-lg bg-[#d66b13] px-3 py-2 text-sm font-bold text-white disabled:opacity-50 flex items-center justify-center gap-1"
                                                    >
                                                        {procesando ? (
                                                            <Loader2 className="size-3.5 animate-spin" />
                                                        ) : (
                                                            <Send className="size-3.5" />
                                                        )}
                                                        Enviar
                                                    </button>
                                                </div>
                                            </div>
                                        )}

                                        {errorAccion && (
                                            <div
                                                role="alert"
                                                className="mt-4 flex gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2.5 text-sm leading-5 text-red-800"
                                            >
                                                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                                                <span>{errorAccion}</span>
                                            </div>
                                        )}

                                        {!admiteValidacion && (
                                            <p className="mt-4 border-t border-[#e6e1d8] pt-4 text-sm text-[#766e61]">
                                                Este documento está en estado «
                                                {estadoLegible(documentoSeleccionado.estado)}
                                                » y no admite correcciones ni aprobación.
                                            </p>
                                        )}

                                        {admiteValidacion && !modoAclaracion && !modoCorreccion && (
                                            <div className="mt-4 grid grid-cols-3 gap-2 border-t border-[#e6e1d8] pt-4">
                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        void aprobarDocumento()
                                                    }
                                                    disabled={procesando}
                                                    className="flex items-center justify-center gap-1.5 rounded-lg bg-[#1d2b55] px-2 py-2 text-sm font-bold text-white disabled:opacity-50"
                                                >
                                                    {procesando ? (
                                                        <Loader2 className="size-3.5 animate-spin" />
                                                    ) : (
                                                        <Check className="size-3.5" />
                                                    )}
                                                    Aprobar
                                                </button>

                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        setModoCorreccion(true)
                                                    }
                                                    className="flex items-center justify-center gap-1.5 rounded-lg bg-[#eeeae1] px-2 py-2 text-sm font-bold text-[#1d2b55]"
                                                >
                                                    <Pencil className="size-3.5" />
                                                    Corregir
                                                </button>

                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        setModoAclaracion(true)
                                                    }
                                                    className="flex items-center justify-center gap-1.5 rounded-lg bg-[#eeeae1] px-2 py-2 text-sm font-bold text-[#1d2b55]"
                                                >
                                                    <HelpCircle className="size-3.5" />
                                                    Aclaración
                                                </button>
                                            </div>
                                        )}

                                        {admiteValidacion && modoCorreccion && (
                                            <div className="mt-4 grid grid-cols-2 gap-2 border-t border-[#e6e1d8] pt-4">
                                                <button
                                                    type="button"
                                                    onClick={() => {
                                                        setModoCorreccion(false)
                                                        setCambios({})
                                                    }}
                                                    className="rounded-lg bg-[#eeeae1] px-3 py-2 text-sm font-bold text-[#1d2b55]"
                                                >
                                                    Cancelar
                                                </button>

                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        void guardarCorrecciones()
                                                    }
                                                    disabled={procesando}
                                                    className="inline-flex items-center justify-center gap-1.5 rounded-lg bg-[#1d2b55] px-3 py-2 text-sm font-bold text-white disabled:opacity-50"
                                                >
                                                    {procesando ? (
                                                        <Loader2 className="size-4 animate-spin" />
                                                    ) : (
                                                        <Save className="size-4" />
                                                    )}
                                                    Guardar
                                                </button>
                                            </div>
                                        )}

                                        <div className="mt-4 flex items-center gap-2 text-xs text-[#766e61]">
                                            <Check className="size-3.5 text-[#008b45]" />
                                            Revisión manual requerida.
                                        </div>
                                    </aside>
                                </div>
                            </section>
                        )}
                    </section>
                )}

                <footer className="mt-6 flex justify-between border-t border-[#d9d4ca] pt-4 text-xs text-[#766e61]">
                    <span>Gestio · Bandeja de validación</span>
                    <span>Verifactu-ready</span>
                </footer>
            </div>
        </main>
    )
}