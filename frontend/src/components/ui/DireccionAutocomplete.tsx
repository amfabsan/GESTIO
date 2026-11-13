import { useEffect, useId, useRef, useState, type ChangeEvent, type KeyboardEvent } from 'react'

type PrediccionDireccion = {
  text: { text: string }
  toPlace: () => {
    formattedAddress?: string
    fetchFields: (options: { fields: string[] }) => Promise<void>
  }
}

type LibreriaPlaces = {
  AutocompleteSuggestion: {
    fetchAutocompleteSuggestions: (request: {
      input: string
      includedRegionCodes: string[]
      language: string
      region: string
      sessionToken: object
    }) => Promise<{ suggestions: { placePrediction?: PrediccionDireccion }[] }>
  }
  AutocompleteSessionToken: new () => object
}

declare global {
  interface Window {
    google?: {
      maps: {
        importLibrary: (libraryName: 'places') => Promise<unknown>
      }
    }
  }
}

const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim()
let libreriaPlaces: Promise<LibreriaPlaces> | null = null

function cargarPlaces(): Promise<LibreriaPlaces> {
  if (!apiKey) return Promise.reject(new Error('Google Places no está configurado.'))
  if (window.google?.maps.importLibrary) return window.google.maps.importLibrary('places') as Promise<LibreriaPlaces>

  if (!libreriaPlaces) {
    libreriaPlaces = new Promise<void>((resolve, reject) => {
      const existente = document.querySelector<HTMLScriptElement>('script[data-google-maps-places]')
      if (existente) {
        existente.addEventListener('load', () => resolve(), { once: true })
        existente.addEventListener('error', () => reject(new Error('No se pudo cargar Google Places.')), { once: true })
        return
      }

      const script = document.createElement('script')
      script.dataset.googleMapsPlaces = 'true'
      script.async = true
      script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&v=weekly&libraries=places&language=es&region=ES&loading=async`
      script.onload = () => resolve()
      script.onerror = () => reject(new Error('No se pudo cargar Google Places.'))
      document.head.appendChild(script)
    }).then(async () => {
      if (!window.google?.maps.importLibrary) throw new Error('Google Places no está disponible.')
      return window.google.maps.importLibrary('places') as Promise<LibreriaPlaces>
    }).catch(error => {
      libreriaPlaces = null
      throw error
    })
  }

  return libreriaPlaces
}

type Props = {
  value: string
  onChange: (direccion: string) => void
  className: string
  placeholder?: string
  maxLength?: number
  multiline?: boolean
  ariaInvalid?: boolean
}

export function DireccionAutocomplete({ value, onChange, className, placeholder, maxLength = 500, multiline = false, ariaInvalid }: Props) {
  const [enfocado, setEnfocado] = useState(false)
  const [consultaCargando, setConsultaCargando] = useState('')
  const [resultado, setResultado] = useState<{ consulta: string; predicciones: PrediccionDireccion[] } | null>(null)
  const [errorConsulta, setErrorConsulta] = useState(false)
  const [seleccionada, setSeleccionada] = useState(-1)
  const tokenSesion = useRef<object | null>(null)
  const peticion = useRef(0)
  const idLista = useId()
  const texto = value.trim()
  const predicciones = enfocado && resultado?.consulta === texto ? resultado.predicciones : []
  const cargando = enfocado && consultaCargando === texto

  useEffect(() => {
    if (!enfocado || texto.length < 3 || !apiKey) return

    let vigente = true
    const idPeticion = ++peticion.current
    const temporizador = window.setTimeout(async () => {
      setConsultaCargando(texto)
      setErrorConsulta(false)
      try {
        const places = await cargarPlaces()
        tokenSesion.current ??= new places.AutocompleteSessionToken()
        const respuesta = await places.AutocompleteSuggestion.fetchAutocompleteSuggestions({
          input: texto,
          includedRegionCodes: ['es'],
          language: 'es',
          region: 'ES',
          sessionToken: tokenSesion.current,
        })
        if (vigente && idPeticion === peticion.current) {
          setResultado({ consulta: texto, predicciones: respuesta.suggestions.flatMap(sugerencia => sugerencia.placePrediction ? [sugerencia.placePrediction] : []).slice(0, 4) })
          setSeleccionada(-1)
        }
      } catch {
        if (vigente && idPeticion === peticion.current) {
          setResultado({ consulta: texto, predicciones: [] })
          setErrorConsulta(true)
        }
      } finally {
        if (vigente && idPeticion === peticion.current) setConsultaCargando(actual => actual === texto ? '' : actual)
      }
    }, 300)

    return () => {
      vigente = false
      window.clearTimeout(temporizador)
    }
  }, [enfocado, texto])

  const elegirPrediccion = async (prediccion: PrediccionDireccion) => {
    const lugar = prediccion.toPlace()
    let direccion = prediccion.text.text
    try {
      await lugar.fetchFields({ fields: ['formattedAddress'] })
      direccion = lugar.formattedAddress || direccion
    } catch {
      direccion = prediccion.text.text
    }
    onChange(direccion)
    tokenSesion.current = null
    setResultado({ consulta: texto, predicciones: [] })
    setEnfocado(false)
  }

  const cambiarValor = (evento: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setResultado(null)
    setErrorConsulta(false)
    onChange(evento.target.value)
  }

  const manejarTeclado = (evento: KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    if (!predicciones.length) return
    if (evento.key === 'ArrowDown') {
      evento.preventDefault()
      setSeleccionada(actual => (actual + 1) % predicciones.length)
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault()
      setSeleccionada(actual => actual <= 0 ? predicciones.length - 1 : actual - 1)
    } else if (evento.key === 'Enter' && seleccionada >= 0) {
      evento.preventDefault()
      void elegirPrediccion(predicciones[seleccionada])
    } else if (evento.key === 'Escape') {
      setResultado({ consulta: texto, predicciones: [] })
    }
  }

  const propsCampo = {
    className,
    maxLength,
    placeholder,
    value,
    onChange: cambiarValor,
    onFocus: () => setEnfocado(true),
    onBlur: () => setEnfocado(false),
    onKeyDown: manejarTeclado,
    'aria-invalid': ariaInvalid,
    'aria-autocomplete': 'list' as const,
    'aria-expanded': enfocado && predicciones.length > 0,
    'aria-controls': idLista,
    'aria-activedescendant': seleccionada >= 0 ? `${idLista}-opcion-${seleccionada}` : undefined,
  }
  const faltaClave = enfocado && texto.length >= 3 && !apiKey

  return (
    <div className="relative">
      {multiline ? <textarea {...propsCampo} rows={2} /> : <input {...propsCampo} type="text" />}
      {enfocado && (faltaClave || cargando || errorConsulta || predicciones.length > 0) && (
        <div className="absolute z-30 mt-1 w-full overflow-hidden rounded-lg border border-[#d9d4c9] bg-white shadow-lg">
          {faltaClave && <p role="status" className="px-3 py-2 text-xs text-[#6b6656]">Configura la clave de Google Places para activar las sugerencias. Puedes escribir la dirección manualmente.</p>}
          {cargando && <p role="status" className="px-3 py-2 text-xs text-[#6b6656]">Buscando direcciones…</p>}
          {errorConsulta && <p role="status" className="px-3 py-2 text-xs text-[#6b6656]">No se pudieron cargar las sugerencias. Comprueba la clave y que Places API (New) esté habilitada.</p>}
          {predicciones.length > 0 && <>
            <ul id={idLista} role="listbox" aria-label="Sugerencias de dirección" className="max-h-64 overflow-y-auto py-1">
              {predicciones.map((prediccion, indice) => <li
                key={`${prediccion.text.text}-${indice}`}
                id={`${idLista}-opcion-${indice}`}
                role="option"
                aria-selected={seleccionada === indice}
                onMouseDown={evento => evento.preventDefault()}
                onClick={() => void elegirPrediccion(prediccion)}
                className={`cursor-pointer px-3 py-2 text-sm text-[#202b43] ${seleccionada === indice ? 'bg-[#f0ede5]' : 'hover:bg-[#f5f3ef]'}`}
              >{prediccion.text.text}</li>)}
            </ul>
            <div className="flex justify-end border-t border-[#eeeae2] px-3 py-1.5">
              <img src="https://maps.gstatic.com/mapfiles/api-3/images/powered-by-google-on-white3.png" alt="Powered by Google" className="h-4 w-auto" />
            </div>
          </>}
        </div>
      )}
    </div>
  )
}
