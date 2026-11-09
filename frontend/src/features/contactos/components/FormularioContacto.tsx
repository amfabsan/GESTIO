import { useState, type FormEvent } from 'react'
import type { ClasificacionContacto, Contacto, GuardarContacto } from '../contactosApi'

type Props = {
  contacto?: Contacto
  enviando: boolean
  error: string | null
  onCancelar: () => void
  onGuardar: (datos: GuardarContacto) => void
}

type Campos = Omit<GuardarContacto, 'email' | 'telefono' | 'direccion'> & {
  email: string
  telefono: string
  direccion: string
}

const normalizarNif = (nif: string) => nif.replace(/[\s-]/g, '').toUpperCase()
const letrasControlDni = 'TRWAGMYFPDXBNJZSQVHLCKE'
const letrasControlCif = 'JABCDEFGHI'

const nifValido = (nif: string) => {
  if (/^[0-9]{8}[A-Z]$|^[XYZ][0-9]{7}[A-Z]$/.test(nif)) {
    const numero = nif[0] === 'X' ? `0${nif.slice(1, 8)}`
      : nif[0] === 'Y' ? `1${nif.slice(1, 8)}`
        : nif[0] === 'Z' ? `2${nif.slice(1, 8)}`
          : nif.slice(0, 8)
    return nif.at(-1) === letrasControlDni[Number(numero) % 23]
  }

  if (!/^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$/.test(nif)) return false

  let suma = 0
  for (let i = 0; i < 7; i++) {
    const digito = Number(nif[i + 1])
    if (i % 2 === 0) {
      const doble = digito * 2
      suma += Math.floor(doble / 10) + doble % 10
    } else {
      suma += digito
    }
  }

  const control = (10 - suma % 10) % 10
  const prefijo = nif[0]
  if ('ABEH'.includes(prefijo)) return nif.at(-1) === String(control)
  if ('PQS'.includes(prefijo)) return nif.at(-1) === letrasControlCif[control]
  return nif.at(-1) === String(control) || nif.at(-1) === letrasControlCif[control]
}

export function FormularioContacto({ contacto, enviando, error, onCancelar, onGuardar }: Props) {
  const [campos, setCampos] = useState<Campos>({
    clasificacion: contacto?.clasificacion ?? 'CLIENTE',
    nombre: contacto?.nombre ?? '',
    nif: contacto?.nif ?? '',
    email: contacto?.email ?? '',
    telefono: contacto?.telefono ?? '',
    direccion: contacto?.direccion ?? '',
  })
  const [errores, setErrores] = useState<Record<string, string>>({})

  const actualizar = (campo: keyof Campos, valor: string) => {
    setCampos(actuales => ({ ...actuales, [campo]: valor }))
    setErrores(actuales => ({ ...actuales, [campo]: '' }))
  }

  const guardar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault()
    const nif = normalizarNif(campos.nif)
    const nuevosErrores: Record<string, string> = {}
    if (!campos.nombre.trim()) nuevosErrores.nombre = 'El nombre o razón social es obligatorio.'
    else if (campos.nombre.trim().length > 200) nuevosErrores.nombre = 'No puede superar los 200 caracteres.'
    if (!nif) nuevosErrores.nif = 'El NIF es obligatorio.'
    else if (!nifValido(nif)) nuevosErrores.nif = 'Introduce un DNI, NIE o NIF de empresa válido.'
    if (campos.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(campos.email.trim())) nuevosErrores.email = 'Introduce un correo electrónico válido.'
    if (campos.telefono.trim() && !/^\+?[0-9\s().-]{6,30}$/.test(campos.telefono.trim())) nuevosErrores.telefono = 'Introduce un teléfono válido.'
    if (campos.direccion.length > 500) nuevosErrores.direccion = 'No puede superar los 500 caracteres.'
    setErrores(nuevosErrores)
    if (Object.keys(nuevosErrores).length > 0) return

    onGuardar({
      clasificacion: campos.clasificacion as ClasificacionContacto,
      nombre: campos.nombre.trim(),
      nif,
      email: campos.email.trim() || null,
      telefono: campos.telefono.trim() || null,
      direccion: campos.direccion.trim() || null,
    })
  }

  const claseCampo = 'mt-1 w-full rounded-lg border border-[#d9d4c9] bg-white px-3 py-2.5 text-sm text-[#202b43] outline-none focus:border-[#435271] focus:ring-2 focus:ring-[#435271]/15'
  const etiqueta = 'block text-sm font-medium text-[#343b49]'

  return (
    <form className="space-y-4" noValidate onSubmit={guardar}>
      <label className={etiqueta}>
        Clasificación <span className="text-red-700" aria-hidden="true">*</span>
        <select className={claseCampo} value={campos.clasificacion} onChange={evento => actualizar('clasificacion', evento.target.value)}>
          <option value="CLIENTE">Cliente</option>
          <option value="PROVEEDOR">Proveedor</option>
          <option value="AMBOS">Cliente y proveedor</option>
        </select>
      </label>
      <label className={etiqueta}>
        Nombre o razón social <span className="text-red-700" aria-hidden="true">*</span>
        <input autoFocus className={claseCampo} maxLength={200} required value={campos.nombre} onChange={evento => actualizar('nombre', evento.target.value)} aria-invalid={Boolean(errores.nombre)} />
        {errores.nombre && <span className="mt-1 block text-xs text-red-700">{errores.nombre}</span>}
      </label>
      <label className={etiqueta}>
        NIF <span className="text-red-700" aria-hidden="true">*</span>
        <input className={claseCampo} maxLength={40} required value={campos.nif} onChange={evento => actualizar('nif', evento.target.value)} aria-invalid={Boolean(errores.nif)} />
        {errores.nif && <span className="mt-1 block text-xs text-red-700">{errores.nif}</span>}
      </label>
      <div className="grid gap-4 sm:grid-cols-2">
        <label className={etiqueta}>
          Correo electrónico <span className="text-xs font-normal text-[#777265]">(opcional)</span>
          <input className={claseCampo} maxLength={320} type="email" value={campos.email} onChange={evento => actualizar('email', evento.target.value)} aria-invalid={Boolean(errores.email)} />
          {errores.email && <span className="mt-1 block text-xs text-red-700">{errores.email}</span>}
        </label>
        <label className={etiqueta}>
          Teléfono <span className="text-xs font-normal text-[#777265]">(opcional)</span>
          <input className={claseCampo} maxLength={30} type="tel" value={campos.telefono} onChange={evento => actualizar('telefono', evento.target.value)} aria-invalid={Boolean(errores.telefono)} />
          {errores.telefono && <span className="mt-1 block text-xs text-red-700">{errores.telefono}</span>}
        </label>
      </div>
      <label className={etiqueta}>
        Dirección fiscal <span className="text-xs font-normal text-[#777265]">(opcional; se completará al facturar)</span>
        <textarea className={`${claseCampo} min-h-20 resize-y`} maxLength={500} value={campos.direccion} onChange={evento => actualizar('direccion', evento.target.value)} aria-invalid={Boolean(errores.direccion)} />
        {errores.direccion && <span className="mt-1 block text-xs text-red-700">{errores.direccion}</span>}
      </label>
      {error && <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">{error}</p>}
      <div className="flex flex-col-reverse justify-end gap-2 border-t border-[#eeeae2] pt-4 sm:flex-row">
        <button className="rounded-lg border border-[#d9d4c9] px-4 py-2.5 text-sm font-medium text-[#343b49] hover:bg-[#f5f3ef] disabled:opacity-60" type="button" onClick={onCancelar} disabled={enviando}>Cancelar</button>
        <button className="rounded-lg bg-[#1d2b4f] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#2b3b62] disabled:cursor-wait disabled:opacity-60" type="submit" disabled={enviando}>{enviando ? 'Guardando…' : contacto ? 'Guardar cambios' : 'Crear contacto'}</button>
      </div>
    </form>
  )
}
