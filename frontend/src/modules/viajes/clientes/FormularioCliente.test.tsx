import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConfirmacionBajaCliente } from '../componentes/ConfirmacionBajaCliente'
import { FormularioCliente } from './FormularioCliente'

const crearCliente = vi.fn()

vi.mock('./servicioClientes', async () => {
  const real = await vi.importActual<typeof import('./servicioClientes')>('./servicioClientes')
  return { ...real, crearCliente: (...args: unknown[]) => crearCliente(...args) }
})

function renderizarFormulario() {
  return render(
    <MemoryRouter>
      <FormularioCliente />
    </MemoryRouter>,
  )
}

async function completarFormulario(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.type(screen.getByLabelText('Razón social'), 'Distribuidora del Litoral')
  await usuario.type(screen.getByLabelText('CUIT (con o sin guiones)'), '30-71234567-8')
  await usuario.type(screen.getByLabelText('Teléfono'), '0341-555-5555')
  await usuario.type(screen.getByLabelText('Email'), 'compras@litoral.com.ar')
}

describe('FormularioCliente', () => {
  beforeEach(() => {
    crearCliente.mockReset()
    crearCliente.mockResolvedValue({ id: 1 })
  })

  /** La dirección es opcional: el módulo no la usa para operar (FR-002, Principio V). */
  it('deja guardar sin dirección', async () => {
    const usuario = userEvent.setup()
    renderizarFormulario()

    await completarFormulario(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar cliente' }))

    expect(crearCliente).toHaveBeenCalledWith(expect.objectContaining({ direccion: null }))
  })

  /** El CUIT viaja como se tipeó: normalizarlo es responsabilidad del backend (FR-004). */
  it('manda el CUIT tal como se escribió, con guiones incluidos', async () => {
    const usuario = userEvent.setup()
    renderizarFormulario()

    await completarFormulario(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar cliente' }))

    expect(crearCliente).toHaveBeenCalledWith(
      expect.objectContaining({ cuit: '30-71234567-8' }),
    )
  })

  /** FR-004: cada rechazo marca el campo puntual, no el formulario entero. */
  it('marca el campo que el backend señala', async () => {
    crearCliente.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'cuit_invalido',
        mensaje: 'El CUIT tiene que tener once dígitos y un dígito verificador válido.',
        campo: 'cuit',
      }),
    )

    const usuario = userEvent.setup()
    renderizarFormulario()

    await completarFormulario(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar cliente' }))

    expect(
      await screen.findByText(
        'El CUIT tiene que tener once dígitos y un dígito verificador válido.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('CUIT (con o sin guiones)')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  /**
   * FR-007: el CUIT de un cliente dado de baja no es un duplicado cualquiera. Sin el mensaje propio,
   * quien lo intenta sale a buscarlo a un listado donde no aparece.
   */
  it('explica que el CUIT pertenece a un cliente dado de baja', async () => {
    crearCliente.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'cuit_de_cliente_dado_de_baja',
        mensaje:
          'Ese CUIT pertenece a un cliente dado de baja. Dalo de alta de nuevo desde el listado en ' +
          'vez de registrarlo otra vez.',
        campo: 'cuit',
      }),
    )

    const usuario = userEvent.setup()
    renderizarFormulario()

    await completarFormulario(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar cliente' }))

    expect(await screen.findByText(/pertenece a un cliente dado de baja/)).toBeInTheDocument()
    expect(screen.getByText('Pertenece a un cliente dado de baja.')).toBeInTheDocument()
  })
})

describe('ConfirmacionBajaCliente', () => {
  /** US1 esc. 7: cancelar no modifica nada, y eso empieza por no llamar al backend. */
  it('cancelar no dispara ninguna petición', async () => {
    const onConfirmar = vi.fn()
    const onCancelar = vi.fn()
    const usuario = userEvent.setup()

    render(
      <ConfirmacionBajaCliente
        razonSocial="Distribuidora del Litoral"
        onConfirmar={onConfirmar}
        onCancelar={onCancelar}
      />,
    )

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onCancelar).toHaveBeenCalledOnce()
    expect(onConfirmar).not.toHaveBeenCalled()

    // El botón dice qué va a pasar, con el verbo de `contracts/README.md`.
    expect(screen.getByRole('button', { name: 'Dar de baja' })).toBeInTheDocument()
  })

  it('dice qué deja de pasar y que se puede deshacer', () => {
    render(
      <ConfirmacionBajaCliente
        razonSocial="Distribuidora del Litoral"
        onConfirmar={vi.fn()}
        onCancelar={vi.fn()}
      />,
    )

    expect(
      screen.getByRole('heading', { name: '¿Dar de baja a Distribuidora del Litoral?' }),
    ).toBeInTheDocument()

    expect(screen.getByText(/Sus viajes históricos se conservan/)).toBeInTheDocument()
  })
})
