import { type ChangeEvent, type InputHTMLAttributes } from 'react'
import { cn } from './cn'

interface Props extends Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange'> {
  value: string
  onChange: (valor: string) => void
}

/**
 * Campo de texto que formatea un importe con separador de miles (punto)
 * y hasta dos decimales (coma) a medida que el usuario escribe.
 */
export function InputImporte({ value, onChange, className, ...props }: Props) {
  function handleChange(evento: ChangeEvent<HTMLInputElement>) {
    let texto = evento.target.value

    // Eliminar todo excepto dígitos y coma
    texto = texto.replace(/[^\d,]/g, '')

    const partes = texto.split(',')

    // Dejar sólo la primera coma
    if (partes.length > 2) {
      texto = partes[0] + ',' + partes.slice(1).join('')
    }

    const nuevasPartes = texto.split(',')

    // Limitar a 2 decimales
    if (nuevasPartes.length === 2 && nuevasPartes[1].length > 2) {
      nuevasPartes[1] = nuevasPartes[1].substring(0, 2)
    }

    // Agregar puntos de miles al entero
    if (nuevasPartes[0]) {
      // Eliminar ceros a la izquierda, excepto si es solo un cero
      nuevasPartes[0] = nuevasPartes[0].replace(/^0+(?=\d)/, '')
      nuevasPartes[0] = nuevasPartes[0].replace(/\B(?=(\d{3})+(?!\d))/g, '.')
    }

    const formateado =
      nuevasPartes.length > 1 ? nuevasPartes[0] + ',' + nuevasPartes[1] : nuevasPartes[0]

    onChange(formateado)
  }

  return (
    <input
      type="text"
      inputMode="decimal"
      className={cn('text-right font-semibold text-ink', className)}
      value={value}
      onChange={handleChange}
      {...props}
    />
  )
}
