import type { ReactNode } from 'react'

interface Props {
  /** El dato de más valor de la ficha. **Nunca vacío** (FR-050, FR-053). */
  destacado: ReactNode
  children?: ReactNode
}

/**
 * La columna fija de 330 px de una ficha (FR-050).
 *
 * Adelante, el **dato que se vino a buscar**: el importe en un viaje o una factura, el semáforo de
 * documentación en un chofer, el estado operativo en un vehículo, los roles en un usuario. Debajo, lo
 * que la ficha ya mostraba y que más se consulta, en la jerarquía que le corresponde.
 *
 * **`destacado` es obligatorio**, y por eso el aside no se dibuja vacío: una ficha que no sabe cuál
 * es su dato de más valor es una ficha en la que hay que leer las cuatro secciones para encontrarlo.
 *
 * **No trae ningún dato que la ficha no reciba hoy** (FR-053): el backend no se toca en esta feature,
 * así que lo destacado es siempre una reordenación de lo que ya llegaba.
 */
export function AsideDeFicha({ destacado, children }: Props) {
  return (
    <aside className="flex w-[330px] shrink-0 flex-col gap-[18px]">
      <div className="rounded-card border border-line bg-surface p-[22px] shadow-card">
        {destacado}
      </div>

      {children}
    </aside>
  )
}

/**
 * La cifra destacada del aside: 32 px ExtraBold, con su rótulo en eyebrow encima (FR-053).
 *
 * Es la forma que toman el importe del viaje y el total de la factura. Los importes llegan ya
 * formateados con `compartido/moneda`, con separador de miles y coma decimal, y van en **una sola
 * línea**: el símbolo y el número nunca se parten.
 */
export function CifraDestacada({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase">
        {rotulo}
      </span>
      <span className="text-[32px] leading-none font-extrabold tracking-[-0.04em] whitespace-nowrap text-ink">
        {valor}
      </span>
    </div>
  )
}

/** Un bloque del aside debajo del destacado: rótulo en eyebrow y contenido. */
export function BloqueDeAside({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section className="rounded-card border border-line bg-surface p-[22px] shadow-card">
      <h2 className="m-0 mb-3 text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase">
        {titulo}
      </h2>
      <div className="flex flex-col gap-2.5 text-[13px] text-ink">{children}</div>
    </section>
  )
}
