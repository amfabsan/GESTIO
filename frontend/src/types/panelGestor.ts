// src/types/panelGestor.ts
export type TipoAvisoOrigen = 'CHECKLIST' | 'DOCUMENTO' | 'ANOMALIA'

export interface PanelResumenGlobales {
    requierenAtencion: number
    obligacionesProximas: number
    checklistsIncompletos: number
    documentosPorValidar: number
    anomaliasPendientes: number
}

export interface PanelAvisoOrigen {
    tipo: TipoAvisoOrigen
    titulo: string
    detalle: string
    obligacionFiscalId: string | null
    documentoId: string | null
    anomaliaId: string | null
    ejercicio: number | null
    periodo: string | null
    modelo: string | null
}

export interface PanelClienteFila {
    autonomoId: string
    nombreCompleto: string
    iniciales: string
    actividad: string | null
    nif: string
    requiereAtencion: boolean
    motivoPrincipal: string | null
    obligacionesProximasCount: number
    checklistsIncompletosCount: number
    documentosPorValidarCount: number
    anomaliasPendientesCount: number
    proximaObligacionFecha: string | null
    diasHastaProximaObligacion: number | null
    proximaObligacionEtiqueta: string | null
    avisos: PanelAvisoOrigen[]
}

export interface PanelGestorResponse {
    nombreGestor: string
    nombreAsesoria: string | null
    codigoAsesoria: string | null
    clientesActivosCount: number
    solicitudesPendientesCount: number
    diasAntelacionObligaciones: number
    fechaReferencia: string
    resumen: PanelResumenGlobales
    clientes: PanelClienteFila[]
}

export interface PanelClienteDetalleResponse {
    autonomoId: string
    nombreCompleto: string
    actividad: string | null
    nif: string
    requiereAtencion: boolean
    obligacionesProximas: PanelObligacionProximaDetalle[]
    documentosPorValidar: PanelDocumentoPendienteDetalle[]
    anomaliasPendientes: PanelAnomaliaPendienteDetalle[]
}

export interface PanelObligacionProximaDetalle {
    obligacionFiscalId: string
    modelo: string
    nombre: string
    ejercicio: number
    periodo: string
    fechaPrevista: string
    diasRestantes: number
    estadoObligacion: string
    obligatoriosConfirmados: number
    obligatoriosTotales: number
    checklistIncompleto: boolean
}

export interface PanelDocumentoPendienteDetalle {
    documentoId: string
    nombreArchivo: string
    fechaSubida: string
    estado: string
}

export interface PanelAnomaliaPendienteDetalle {
    anomaliaId: string
    documentoId: string
    nombreArchivoDocumento: string
    tipo: string
    severidad: string
    campo: string | null
    descripcion: string
    fechaDeteccion: string
}
