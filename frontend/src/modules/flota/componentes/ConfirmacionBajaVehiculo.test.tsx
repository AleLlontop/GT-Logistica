import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmacionBajaVehiculo } from './ConfirmacionBajaVehiculo'
import { ConfirmacionEliminarDocumento } from './ConfirmacionEliminarDocumento'

/**
 * FR-007, FR-008e, FR-027 y SC-009: las tres operaciones piden confirmación explícita, con los textos
 * de `contracts/README.md`, y **cancelar no cambia nada**.
 */
describe('Confirmaciones del módulo de flota', () => {
  it('la baja usa el texto del contrato y aclara que se puede reactivar (FR-007)', () => {
    render(
      <ConfirmacionBajaVehiculo
        que={{ tipo: 'baja', patente: 'AB123CD' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(
      screen.getByText(
        '¿Dar de baja la unidad AB123CD? Va a dejar de figurar en el listado y en el panel de ' +
          'vencimientos. Su documentación se conserva y podés reactivarla más adelante.',
      ),
    ).toBeInTheDocument()
  })

  it('la reactivación usa el texto del contrato (FR-008e)', () => {
    render(
      <ConfirmacionBajaVehiculo
        que={{ tipo: 'reactivacion', patente: 'AB123CD' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(
      screen.getByText(
        '¿Reactivar la unidad AB123CD? Vuelve al listado y al panel de vencimientos con toda su ' +
          'documentación.',
      ),
    ).toBeInTheDocument()
  })

  /**
   * La eliminación de un documento es la única que advierte que **no se puede deshacer**, porque es
   * la única operación del módulo que borra de verdad (FR-027, FR-028).
   */
  it('sólo la eliminación de un documento advierte que no se puede deshacer (FR-027)', () => {
    const { unmount } = render(
      <ConfirmacionEliminarDocumento onConfirmar={() => {}} onCancelar={() => {}} />,
    )

    expect(
      screen.getByText(
        '¿Eliminar este documento? Se borra junto con su archivo adjunto y esta acción no se puede ' +
          'deshacer.',
      ),
    ).toBeInTheDocument()

    unmount()

    render(
      <ConfirmacionBajaVehiculo
        que={{ tipo: 'baja', patente: 'AB123CD' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(screen.queryByText(/no se puede deshacer/i)).not.toBeInTheDocument()
  })

  /** SC-009 y US6 esc. 6: cancelar no dispara ninguna petición. */
  it('cancelar no confirma nada (SC-009)', async () => {
    const alConfirmar = vi.fn()
    const alCancelar = vi.fn()

    const usuario = userEvent.setup()

    render(
      <ConfirmacionBajaVehiculo
        que={{ tipo: 'baja', patente: 'AB123CD' }}
        onConfirmar={alConfirmar}
        onCancelar={alCancelar}
      />,
    )

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(alConfirmar).not.toHaveBeenCalled()
    expect(alCancelar).toHaveBeenCalledOnce()
  })

  it('cancelar la eliminación de un documento tampoco confirma nada (US3 esc. 11)', async () => {
    const alConfirmar = vi.fn()
    const alCancelar = vi.fn()

    const usuario = userEvent.setup()

    render(
      <ConfirmacionEliminarDocumento onConfirmar={alConfirmar} onCancelar={alCancelar} />,
    )

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(alConfirmar).not.toHaveBeenCalled()
    expect(alCancelar).toHaveBeenCalledOnce()
  })

  /** Accesibilidad: el diálogo recibe el foco al abrirse (contracts §Accesibilidad). */
  it('el diálogo recibe el foco al abrirse', () => {
    render(
      <ConfirmacionBajaVehiculo
        que={{ tipo: 'baja', patente: 'AB123CD' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(screen.getByRole('dialog')).toHaveFocus()
  })
})
