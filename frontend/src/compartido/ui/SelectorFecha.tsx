import { useMemo } from 'react'
import { cn } from './cn'

interface Props {
  /** ID para asociar con la etiqueta <label htmlFor={id}> */
  id?: string
  /** Fecha en formato ISO 'yyyy-MM-dd' o cadena vacía */
  value: string
  /** Callback al cambiar la fecha seleccionada en 'yyyy-MM-dd' */
  onChange: (isoDate: string) => void
  disabled?: boolean
  required?: boolean
  className?: string
  /** Año mínimo en el desplegable (por defecto 2024) */
  minYear?: number
  /** Año máximo en el desplegable (por defecto año actual + 2) */
  maxYear?: number
  conError?: boolean
}

const NOMBRES_MESES = [
  '01 - Enero',
  '02 - Febrero',
  '03 - Marzo',
  '04 - Abril',
  '05 - Mayo',
  '06 - Junio',
  '07 - Julio',
  '08 - Agosto',
  '09 - Septiembre',
  '10 - Octubre',
  '11 - Noviembre',
  '12 - Diciembre',
]

function diasEnMes(mes: number, anio: number): number {
  if (!mes || !anio) return 31
  return new Date(anio, mes, 0).getDate()
}

/**
 * Selector de fecha accesible mediante 3 desplegables coordinados (Día, Mes, Año).
 *
 * Elimina la ambigüedad y diferencias de los datepickers nativos de navegador,
 * asegurando que no se puedan ingresar fechas inválidas (como 31 de febrero).
 *
 * Mantiene un input interno sincronizado con el `id` para total compatibilidad
 * con formularios, validaciones de accesibilidad y tests existentes.
 */
export function SelectorFecha({
  id,
  value,
  onChange,
  disabled = false,
  className,
  minYear = 2024,
  maxYear = new Date().getFullYear() + 2,
  conError = false,
}: Props) {
  const [anioStr, mesStr, diaStr] = value ? value.split('-') : ['', '', '']
  const anio = anioStr ? Number(anioStr) : ''
  const mes = mesStr ? Number(mesStr) : ''
  const dia = diaStr ? Number(diaStr) : ''

  const totalDias = useMemo(() => {
    return diasEnMes(Number(mes) || 1, Number(anio) || 2026)
  }, [mes, anio])

  const aniosDisponibles = useMemo(() => {
    const lista: number[] = []
    for (let y = minYear; y <= maxYear; y++) {
      lista.push(y)
    }
    return lista
  }, [minYear, maxYear])

  function actualizar(nuevoDia: number | '', nuevoMes: number | '', nuevoAnio: number | '') {
    if (nuevoDia === '' || nuevoMes === '' || nuevoAnio === '') {
      onChange('')
      return
    }

    const maxD = diasEnMes(nuevoMes, nuevoAnio)
    const diaAjustado = Math.min(nuevoDia, maxD)

    const iso = `${nuevoAnio}-${String(nuevoMes).padStart(2, '0')}-${String(diaAjustado).padStart(2, '0')}`
    onChange(iso)
  }

  function handleCambioDia(d: string) {
    const val = d === '' ? '' : Number(d)
    actualizar(val, mes, anio)
  }

  function handleCambioMes(m: string) {
    const val = m === '' ? '' : Number(m)
    actualizar(dia, val, anio)
  }

  function handleCambioAnio(a: string) {
    const val = a === '' ? '' : Number(a)
    actualizar(dia, mes, val)
  }

  const claseSelect = cn(
    'rounded-field border border-line-strong bg-surface-soft px-3 py-2 text-[13px] text-ink',
    'transition-shadow duration-200 ease-gt',
    'focus:border-brand focus:bg-white focus:shadow-focus focus:outline-none',
    'disabled:cursor-not-allowed disabled:opacity-50',
    conError && 'border-danger bg-danger-bg focus:shadow-none',
  )

  return (
    <div className={cn('flex flex-wrap items-center gap-2', className)}>
      <select
        id={id ? `${id}-dia` : undefined}
        aria-label="Día"
        value={dia}
        disabled={disabled}
        onChange={(e) => handleCambioDia(e.target.value)}
        className={cn(claseSelect, 'w-[75px]')}
      >
        <option value="">Día</option>
        {Array.from({ length: totalDias }, (_, i) => i + 1).map((d) => (
          <option key={d} value={d}>
            {String(d).padStart(2, '0')}
          </option>
        ))}
      </select>

      <select
        id={id ? `${id}-mes` : undefined}
        aria-label="Mes"
        value={mes}
        disabled={disabled}
        onChange={(e) => handleCambioMes(e.target.value)}
        className={cn(claseSelect, 'w-[145px]')}
      >
        <option value="">Mes</option>
        {NOMBRES_MESES.map((nombre, i) => (
          <option key={i + 1} value={i + 1}>
            {nombre}
          </option>
        ))}
      </select>

      <select
        id={id ? `${id}-anio` : undefined}
        aria-label="Año"
        value={anio}
        disabled={disabled}
        onChange={(e) => handleCambioAnio(e.target.value)}
        className={cn(claseSelect, 'w-[95px]')}
      >
        <option value="">Año</option>
        {aniosDisponibles.map((y) => (
          <option key={y} value={y}>
            {y}
          </option>
        ))}
      </select>

      {/* Input de respaldo sincronizado para asociación con label y pruebas */}
      {id && (
        <input
          type="text"
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
        />
      )}
    </div>
  )
}
