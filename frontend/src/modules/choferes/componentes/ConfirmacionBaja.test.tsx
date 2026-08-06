import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmacionBaja } from './ConfirmacionBaja'

describe('ConfirmacionBaja', () => {
  /**
   * SC-008: cancelar una confirmación no modifica nada. El test mira lo único que puede probarlo
   * desde acá — que no se dispara la acción— porque si `onConfirmar` no corre, no hay llamada al
   * backend posible.
   */
  it('cancelar no dispara ninguna acción (FR-026, SC-008)', async () => {
    const onConfirmar = vi.fn()
    const onCancelar = vi.fn()

    const usuario = userEvent.setup()

    render(
      <ConfirmacionBaja
        que={{ tipo: 'chofer', apellido: 'Gómez', nombre: 'Ramona' }}
        onConfirmar={onConfirmar}
        onCancelar={onCancelar}
      />,
    )

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onConfirmar).not.toHaveBeenCalled()
    expect(onCancelar).toHaveBeenCalledOnce()
  })

  it('cerrar con Escape equivale a cancelar', async () => {
    const onConfirmar = vi.fn()
    const onCancelar = vi.fn()

    const usuario = userEvent.setup()

    render(
      <ConfirmacionBaja
        que={{ tipo: 'transportista', nombre: 'G&T Logística S.A.' }}
        onConfirmar={onConfirmar}
        onCancelar={onCancelar}
      />,
    )

    await usuario.keyboard('{Escape}')

    expect(onConfirmar).not.toHaveBeenCalled()
    expect(onCancelar).toHaveBeenCalledOnce()
  })

  it('usa el texto del contrato para la baja de un chofer', () => {
    render(
      <ConfirmacionBaja
        que={{ tipo: 'chofer', apellido: 'Gómez', nombre: 'Ramona' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(
      screen.getByText(
        '¿Confirmás la baja de Gómez, Ramona? Va a quedar inactivo y no va a poder asignarse a un ' +
          'viaje. Su documentación se conserva.',
      ),
    ).toBeInTheDocument()
  })

  /**
   * La de eliminar un documento es la única que habla de borrar y no de dar de baja: es la única
   * operación del módulo que no se puede revertir (FR-015d), y el texto tiene que decirlo.
   */
  it('advierte que eliminar un documento no se puede deshacer (FR-015c)', () => {
    render(
      <ConfirmacionBaja
        que={{ tipo: 'documento', tipoDocumento: 'Licencia de conducir', numero: 'LIC-999' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(
      screen.getByText(
        '¿Confirmás que querés eliminar el Licencia de conducir N° LIC-999? Se borra junto con su ' +
          'archivo adjunto y no se puede deshacer.',
      ),
    ).toBeInTheDocument()
  })

  it('ofrece reactivar con su propio texto cuando el chofer está inactivo (FR-005b)', () => {
    render(
      <ConfirmacionBaja
        que={{ tipo: 'reactivarChofer', apellido: 'Gómez', nombre: 'Ramona' }}
        onConfirmar={() => {}}
        onCancelar={() => {}}
      />,
    )

    expect(
      screen.getByText(
        '¿Confirmás la reactivación de Gómez, Ramona? Va a volver al listado y su documentación va ' +
          'a contar de nuevo.',
      ),
    ).toBeInTheDocument()
  })
})
