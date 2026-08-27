import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

interface Props {
  a: string
  /** Lo que se busca con la vista: el apellido, la razón social. Va subrayado. */
  children: ReactNode
  /** El identificador que lo acompaña: el DNI, el CUIT, la patente. En mono, menor jerarquía. */
  identificador?: ReactNode
}

/**
 * Cómo se entra a la ficha de una fila (FR-040).
 *
 * **El enlace es el dato que se busca con la vista** —el apellido y nombre en el padrón de choferes,
 * la razón social en el de clientes—, no una columna *Ver ficha* al final de la fila. Quien recorre
 * un listado ya está mirando ese dato: hacerlo clickeable es hacer clickeable lo que la vista ya
 * encontró.
 *
 * El identificador que lo acompaña va **debajo y en mono**, en menor jerarquía: es lo que confirma
 * que la fila es la correcta, no lo que se busca.
 *
 * Hay **exactamente uno por fila** en los 9 listados de índice (SC-006). Las 12 tablas sin ficha de
 * destino no llevan ninguno: inventarles un destino de solo lectura sería alcance fantasma.
 */
export function EnlaceDeFila({ a, children, identificador }: Props) {
  return (
    <span className="flex flex-col gap-0.5">
      <Link
        to={a}
        className="w-fit text-[13px] font-semibold tracking-tight text-ink underline decoration-line-strong underline-offset-[3px] transition-colors duration-200 ease-gt hover:decoration-brand hover:text-brand"
      >
        {children}
      </Link>

      {identificador !== undefined && (
        <span className="font-mono text-[11.5px] text-faint">{identificador}</span>
      )}
    </span>
  )
}
