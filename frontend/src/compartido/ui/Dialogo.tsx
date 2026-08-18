import * as Radix from '@radix-ui/react-dialog'
import { useRef, type ReactNode } from 'react'
import { cn } from './cn'

/**
 * El contenedor de todos los diálogos del sistema.
 *
 * Sobre `@radix-ui/react-dialog`, que aporta lo único que el diálogo del Módulo 2 no tenía: la
 * **retención del foco**, que la tabulación cicle adentro y no se escape al contenido de atrás
 * (FR-036). Lo que ya funcionaba —recibir el foco al abrir, cerrar con `Escape` y devolver el foco
 * al elemento de origen— Radix lo cubre con el mismo comportamiento.
 *
 * Los cuatro diálogos que llevan campos adentro —las dos confirmaciones de emisión, el registro de
 * cobro y la anulación de viaje con su motivo— usan este contenedor en vez de tener su propio
 * `role="dialog"`, que es lo que hacía que hubiera cuatro versiones de lo mismo.
 */

interface Props {
  titulo: string
  /** Cancelar. Nunca modifica nada: cerrar un diálogo es siempre no hacer la operación. */
  onCerrar: () => void
  children: ReactNode
  className?: string
}

export function Dialogo({ titulo, onCerrar, children, className }: Props) {
  const contenido = useRef<HTMLDivElement>(null)

  return (
    <Radix.Root
      open
      onOpenChange={(abierto) => {
        if (!abierto) onCerrar()
      }}
    >
      <Radix.Portal>
        <Radix.Overlay className="fixed inset-0 z-40 bg-texto/40" />

        <Radix.Content
          ref={contenido}
          /*
           * Radix, librado a sí mismo, enfoca el primer control del diálogo —*Cancelar*—. El
           * Módulo 2 fijó otra cosa: **el diálogo recibe el foco al abrirse**, para que un lector de
           * pantalla lea el título y el mensaje antes que los botones. Esa decisión es de la spec y
           * está cubierta por un test, así que manda sobre el valor por defecto de la biblioteca.
           */
          onOpenAutoFocus={(evento) => {
            evento.preventDefault()
            contenido.current?.focus()
          }}
          className={cn(
            'fixed top-1/2 left-1/2 z-50 w-[min(32rem,calc(100vw-2rem))]',
            '-translate-x-1/2 -translate-y-1/2',
            'max-h-[calc(100vh-2rem)] overflow-y-auto',
            'rounded-grande border border-borde bg-superficie p-6 shadow-dialogo',
            className,
          )}
        >
          <Radix.Title className="text-lg font-semibold text-texto">{titulo}</Radix.Title>

          {children}
        </Radix.Content>
      </Radix.Portal>
    </Radix.Root>
  )
}

/** El pie del diálogo: la acción que confirma y la que cancela, siempre en el mismo lugar. */
export function DialogoAcciones({ children }: { children: ReactNode }) {
  return <div className="mt-6 flex flex-wrap justify-end gap-2">{children}</div>
}
