import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GenerarReporte } from './GenerarReporte'

const generar = vi.hoisted(() => vi.fn())
vi.mock('../servicios/servicioReportes', async () => ({ ...await vi.importActual('../servicios/servicioReportes'), generarReporte: generar }))
beforeEach(() => generar.mockReset().mockResolvedValue({ estado: 'entregado', entrega: 'abierto' }))

describe('diálogo común para perspectivas015', () => {
  it('foco en diálogo, cancelar sin request y retorno al disparador', async () => {
    const usuario = userEvent.setup()
    render(<GenerarReporte reporte="analisis-cobros" cantidadDeFilas={21} puedeEmitir />)
    const boton = screen.getByRole('button', { name: 'Generar reporte' })
    await usuario.click(boton)
    await waitFor(() => expect(screen.getByRole('dialog')).toHaveFocus())
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(generar).not.toHaveBeenCalled()
    await waitFor(() => expect(boton).toHaveFocus())
  })
  it('envía una intención de PDF con filtros aplicados sin modificar página ni formulario', async () => {
    const usuario = userEvent.setup()
    const filtros = Object.freeze({ desde: '2026-10-01', hasta: '2026-10-31', clienteId: 3, pagina: 2 })
    render(<><p>Página actual: 2</p><GenerarReporte reporte="analisis-cobros" filtros={filtros} cantidadDeFilas={21} puedeEmitir /></>)
    await usuario.click(screen.getByRole('button', { name: 'Generar reporte' }))
    await usuario.click(screen.getByRole('button', { name: 'PDF' }))
    await waitFor(() => expect(generar).toHaveBeenCalledExactlyOnceWith('analisis-cobros', 'pdf', filtros))
    expect(screen.getByText('Página actual: 2')).toBeVisible()
    expect(filtros.pagina).toBe(2)
  })
  it('sin emisión no hay acción y con cero filas se explica indisponibilidad', () => {
    const { rerender } = render(<GenerarReporte reporte="analisis-pagos" cantidadDeFilas={3} puedeEmitir={false} />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    rerender(<GenerarReporte reporte="analisis-pagos" cantidadDeFilas={0} puedeEmitir />)
    expect(screen.getByRole('button', { name: 'Generar reporte' })).toBeDisabled()
    expect(screen.getByText('No hay filas para reportar.')).toBeVisible()
  })
})
