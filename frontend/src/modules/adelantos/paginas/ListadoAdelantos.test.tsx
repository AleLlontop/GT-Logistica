import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ListadoAdelantos } from './ListadoAdelantos'
import type { AdelantoListado, PaginaDeAdelantos } from '../servicios/servicioAdelantos'

const listarAdelantos = vi.fn()
const listarPersonasConAdelantos = vi.fn()

vi.mock('../servicios/servicioAdelantos', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioAdelantos')>(
    '../servicios/servicioAdelantos',
  )

  return {
    ...real,
    listarAdelantos: (...args: unknown[]) => listarAdelantos(...args),
    listarPersonasConAdelantos: () => listarPersonasConAdelantos(),
  }
})

const JUAN = { id: 5, apellido: 'Pérez', nombre: 'Juan', dni: '30123456' }

function adelanto(parcial: Partial<AdelantoListado>): AdelantoListado {
  return {
    id: 1,
    fecha: '2026-09-14',
    persona: JUAN,
    tipo: 'chofer',
    motivo: 'Gastos médicos',
    importe: 150_000,
    estado: 'pendiente',
    ...parcial,
  }
}

function pagina(items: AdelantoListado[], totalAdelantado = 0): PaginaDeAdelantos {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20, totalAdelantado }
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <ListadoAdelantos puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoAdelantos', () => {
  beforeEach(() => {
    listarPersonasConAdelantos.mockReset().mockResolvedValue([JUAN, { id: 9, apellido: 'Torres', nombre: 'Ana', dni: '33222111' }])
    listarAdelantos.mockReset().mockResolvedValue(
      pagina(
        [
          adelanto({ id: 1 }),
          adelanto({ id: 2, fecha: '2026-09-13', motivo: 'Error de carga', importe: 20_000, estado: 'rechazado' }),
          adelanto({ id: 3, fecha: '2026-09-12', motivo: 'Anticipo de aguinaldo', importe: 40_000, estado: 'anulado' }),
        ],
        200_000,
      ),
    )
  })

  /** FR-013: fecha, persona con DNI, motivo, importe y estado. */
  it('muestra cada fila con sus columnas', async () => {
    renderizar()

    const tabla = await screen.findByRole('table')
    const fila = within(tabla).getAllByRole('row').find((candidata) => within(candidata).queryByText('Gastos médicos'))!

    expect(within(fila).getByText('14/09/2026')).toBeInTheDocument()
    expect(within(fila).getByText('30123456')).toBeInTheDocument()
    expect(within(fila).getByText('$ 150.000,00')).toBeInTheDocument()
    expect(within(fila).getByText('Pendiente')).toBeInTheDocument()
  })

  /** FR-039, US2 esc. 8: rechazado y anulado atenuados y con su palabra. */
  it('atenúa el rechazado y el anulado y los nombra', async () => {
    renderizar()

    const filas = within(await screen.findByRole('table')).getAllByRole('row')
    const rechazado = filas.find((fila) => within(fila).queryByText('Error de carga'))!
    const anulado = filas.find((fila) => within(fila).queryByText('Anticipo de aguinaldo'))!

    expect(rechazado).toHaveClass('atenuada')
    expect(within(rechazado).getByText('Rechazado')).toBeInTheDocument()
    expect(anulado).toHaveClass('atenuada')
    expect(within(anulado).getByText('Anulado')).toBeInTheDocument()
  })

  /** Una persona tiene varias filas: el nombre accesible del enlace suma la fecha. */
  it('nombra el enlace de la persona con la fecha', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Ver adelanto de Pérez, Juan del 14/09/2026' })).toHaveAttribute(
      'href',
      '/adelantos/1',
    )
  })

  it('declara qué está mostrando con y sin filtro de estado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    expect(
      await screen.findByText('Mostrando todos los adelantos, incluidos los rechazados y los anulados.'),
    ).toHaveAttribute('role', 'status')

    await usuario.selectOptions(screen.getByLabelText('Estado'), 'aprobado')

    expect(await screen.findByText('Mostrando sólo los adelantos aprobados.')).toBeInTheDocument()
    await waitFor(() =>
      expect(listarAdelantos).toHaveBeenLastCalledWith(expect.objectContaining({ estado: 'aprobado' }), 1),
    )
  })

  /** FR-016. */
  it('muestra el total adelantado en el resumen', async () => {
    renderizar()

    expect(await screen.findByText('$ 200.000,00')).toBeInTheDocument()
    expect(screen.getByText(/Total adelantado/)).toBeInTheDocument()
    expect(screen.getByText('Suma sólo los aprobados')).toBeInTheDocument()
    expect(screen.getByText('3 adelantos')).toBeInTheDocument()
  })

  /** FR-015, US2 esc. 4. */
  it('con el rango invertido marca los dos campos y no consulta', async () => {
    renderizar()
    await screen.findByRole('table')

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-09-30' } })
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-09-01' } })

    expect(screen.getByText('La fecha desde es posterior a la hasta. Corregí una de las dos.')).toBeInTheDocument()
    expect(screen.getByLabelText('Desde')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Hasta')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Corregí el rango de fechas para ver los adelantos.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(listarAdelantos).not.toHaveBeenCalledWith(
      expect.objectContaining({ desde: '2026-09-30', hasta: '2026-09-01' }),
      expect.anything(),
    )
  })

  /** FR-014: el filtro de persona sale del servicio. */
  it('ofrece en el filtro de persona las que devuelve el servicio', async () => {
    renderizar()

    expect(await screen.findByRole('option', { name: 'Pérez, Juan — 30123456' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Torres, Ana — 33222111' })).toBeInTheDocument()
  })

  it('distingue la lista vacía de la que no coincide con los filtros', async () => {
    listarAdelantos.mockResolvedValue(pagina([]))
    const usuario = userEvent.setup()
    renderizar()

    expect(
      await screen.findByText(
        'Todavía no se registró ningún adelanto. Registrá el primero eligiendo a la persona y el importe.',
      ),
    ).toBeInTheDocument()

    await usuario.selectOptions(screen.getByLabelText('Estado'), 'anulado')

    expect(
      await screen.findByText('Ningún adelanto coincide con los filtros aplicados. Probá limpiarlos.'),
    ).toBeInTheDocument()
  })

  /** FR-043, US6 esc. 5. */
  it('un 403 muestra el aviso sin permiso, sin filtros ni tabla ni error de carga', async () => {
    listarAdelantos.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))

    renderizar()

    expect(
      await screen.findByText(
        'No tenés permiso para ver los adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.',
      ),
    ).toHaveAttribute('role', 'alert')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Estado')).not.toBeInTheDocument()
    expect(screen.queryByText(/No pudimos traer los adelantos/)).not.toBeInTheDocument()
  })

  /** US2 esc. 11: quien sólo consulta no ve Registrar adelanto, ni la invitación a registrar. */
  it('sin permiso de gestión no ofrece registrar', async () => {
    listarAdelantos.mockResolvedValue(pagina([]))
    renderizar(false)

    expect(await screen.findByText('Todavía no se registró ningún adelanto.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Registrar adelanto' })).not.toBeInTheDocument()
  })

  it('con permiso de gestión ofrece registrar', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Registrar adelanto' })).toHaveAttribute('href', '/adelantos/nuevo')
  })
})
