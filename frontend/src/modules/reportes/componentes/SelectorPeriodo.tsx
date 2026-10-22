import { useId, useState } from 'react'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { erroresPeriodo } from '../servicios/periodoAnalisis'
import type { PeriodoAnalisis } from '../servicios/tiposAnalisis'

export function SelectorPeriodo({ valor, onCambiar, mostrarErrores = false }: {
  valor: PeriodoAnalisis; onCambiar: (periodo: PeriodoAnalisis) => void; mostrarErrores?: boolean
}) {
  const id = useId()
  const [tocados, setTocados] = useState({ desde: false, hasta: false })
  const errores = erroresPeriodo(valor)
  return <div className="flex flex-wrap items-start gap-3.5">
    {(['desde', 'hasta'] as const).map(campo => {
      const nombre = `${id}-${campo}`
      const error = mostrarErrores || tocados[campo] ? errores[campo] : undefined
      return <Campo key={campo} id={nombre} etiqueta={campo === 'desde' ? 'Desde' : 'Hasta'} obligatorio ancho="corto" error={error} ayuda="Día completo de Argentina, inclusive.">
        <input id={nombre} type="date" required value={valor[campo]} className={clasesDeControl(Boolean(error))}
          aria-invalid={Boolean(error)} aria-describedby={`${nombre}-ayuda${error ? ` ${nombre}-error` : ''}`}
          onBlur={() => setTocados(actual => ({ ...actual, [campo]: true }))}
          onChange={evento => onCambiar({ ...valor, [campo]: evento.target.value })} />
      </Campo>
    })}
  </div>
}
