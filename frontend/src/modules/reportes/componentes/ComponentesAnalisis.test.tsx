import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { Boton } from '../../../compartido/ui/Boton'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { erroresPeriodo, fechaAnalisisValida, mesActualArgentina } from '../servicios/periodoAnalisis'
import { SelectorPeriodo } from './SelectorPeriodo'
import { KpiReporte } from './KpiReporte'
import { ResumenReporte } from './ResumenReporte'
import { EstadoConsultaAnalisis, SinDatosReporte, SinPermisoReporte } from './EstadoConsultaAnalisis'

function FormularioPeriodo({ consultar }: { consultar: (periodo: { desde: string; hasta: string }) => void }) {
  const [valor, setValor] = useState(mesActualArgentina)
  const [intento, setIntento] = useState(false)
  return <form noValidate onSubmit={evento => { evento.preventDefault(); setIntento(true); if (!Object.keys(erroresPeriodo(valor)).length) consultar(valor) }}>
    <SelectorPeriodo valor={valor} onCambiar={setValor} mostrarErrores={intento} />
    <Boton variante="secundario" type="submit">Aplicar período</Boton>
  </form>
}
afterEach(() => vi.useRealTimers())

describe('período argentino', () => {
  it('propone octubre aunque UTC ya sea noviembre; no muestra errores al abrir', () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-01T01:30:00Z'))
    const consultar = vi.fn(); render(<FormularioPeriodo consultar={consultar} />)
    expect(screen.getByLabelText('Desde')).toHaveValue('2026-10-01')
    expect(screen.getByLabelText('Hasta')).toHaveValue('2026-10-31')
    expect(screen.getByLabelText('Desde')).toHaveAttribute('aria-invalid', 'false')
    expect(screen.queryByText('Elegí la fecha desde.')).not.toBeInTheDocument()
    expect(consultar).not.toHaveBeenCalled()
  })

  it('requerido aparece después de blur y rango invertido al aplicar; ninguno consulta', async () => {
    const consultar = vi.fn(), usuario = userEvent.setup()
    render(<FormularioPeriodo consultar={consultar} />)
    const desde = screen.getByLabelText('Desde')
    fireEvent.change(desde, { target: { value: '' } })
    expect(screen.queryByText('Elegí la fecha desde.')).not.toBeInTheDocument()
    fireEvent.blur(desde)
    expect(desde).toHaveAccessibleDescription(expect.stringContaining('Elegí la fecha desde.'))
    expect(desde).toHaveAttribute('aria-invalid', 'true')
    fireEvent.change(desde, { target: { value: '2026-10-01' } })
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-09-30' } })
    await usuario.click(screen.getByRole('button', { name: 'Aplicar período' }))
    expect(screen.getByLabelText('Hasta')).toHaveAccessibleDescription(expect.stringContaining('La fecha hasta no puede ser anterior a desde.'))
    expect(consultar).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-10-31' } })
    await usuario.click(screen.getByRole('button', { name: 'Aplicar período' }))
    expect(consultar).toHaveBeenCalledExactlyOnceWith({ desde: '2026-10-01', hasta: '2026-10-31' })
  })

  it('admite bisiesto y extremos DateOnly, sin normalizar fechas inválidas', () => {
    expect(mesActualArgentina(new Date('2024-02-29T15:00:00Z'))).toEqual({ desde: '2024-02-01', hasta: '2024-02-29' })
    expect(fechaAnalisisValida('2024-02-29')).toBe(true)
    expect(fechaAnalisisValida('2026-02-29')).toBe(false)
    expect(fechaAnalisisValida('2026-02-30')).toBe(false)
    expect(fechaAnalisisValida('0001-01-01')).toBe(true)
    expect(fechaAnalisisValida('9999-12-31')).toBe(true)
    expect(fechaAnalisisValida('0000-01-01')).toBe(false)
    expect(erroresPeriodo({ desde: '', hasta: '' })).toEqual({ desde: 'Elegí la fecha desde.', hasta: 'Elegí la fecha hasta.' })
  })
})

describe('KPI y contexto', () => {
  it('dinero conserva todos sus centavos y falta de permiso no se presenta como cero', () => {
    render(<dl><KpiReporte titulo="Importe" importe="9999999999999999.99" /><KpiReporte titulo="Movimiento neto" importe="-0.01" />
      <KpiReporte titulo="Cantidad" cantidad={0} /><KpiReporte titulo="Sin acceso" sinPermiso /></dl>)
    expect(screen.getByText('$ 9.999.999.999.999.999,99')).toBeVisible()
    expect(screen.getByText('-$ 0,01')).toBeVisible()
    expect(screen.getByText('0')).toBeVisible()
    expect(screen.getByText('Acceso no disponible')).toBeVisible()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('declara meses completos, vigencia actual e instante en Argentina', () => {
    render(<ResumenReporte contexto={{ desde: '2026-09-15', hasta: '2026-10-10', consultadoEn: '2026-10-21T01:30:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'periodoLiquidado', periodoLiquidadoDesde: '2026-09', periodoLiquidadoHasta: '2026-10' }} />)
    expect(screen.getByText('Desde 15/09/2026 hasta 10/10/2026 inclusive.')).toBeVisible()
    expect(screen.getByText(/Se incluyen completos los meses 2026-09 a 2026-10/)).toBeVisible()
    expect(screen.getByText(/20\/10\/2026, 22:30/)).toBeVisible()
  })

  it('vencimientos no simula un corte por período', () => {
    render(<ResumenReporte contexto={{ desde: null, hasta: null, consultadoEn: '2026-10-20T12:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'situacionActual' }} />)
    expect(screen.getByText(/Situación actual al 20\/10\/2026. No es un corte histórico/)).toBeVisible()
    expect(screen.queryByText(/^Desde/)).not.toBeInTheDocument()
  })
})

describe('estados de consulta', () => {
  it('la opción nueva no cambia la región viva de los consumidores legacy', () => {
    const { rerender } = render(<EstadoVacio caso="error">Mensaje anterior</EstadoVacio>)
    expect(screen.getByRole('alert').tagName).toBe('DIV')
    expect(screen.getByRole('alert')).toHaveTextContent('Mensaje anterior')
    rerender(<EstadoVacio caso="error" anuncioEnMensaje>Mensaje nuevo</EstadoVacio>)
    expect(screen.getByRole('alert').tagName).toBe('P')
  })
  it('carga y vacío son mensajes, sin datos ficticios', () => {
    const { rerender } = render(<EstadoConsultaAnalisis solicitud={{ estado: 'cargando', datos: null, error: null, reintentar: vi.fn() }} />)
    expect(screen.getByRole('status')).toHaveTextContent('Cargando análisis…')
    expect(screen.getByRole('status').tagName).toBe('P')
    rerender(<SinDatosReporte />)
    expect(screen.getByRole('status')).toHaveTextContent('Sin datos para el período.')
  })
  it('403 indica permiso y no ofrece reintentar; anuncio está en el mensaje', () => {
    render(<SinPermisoReporte />)
    expect(screen.getByRole('alert')).toHaveTextContent('Pedíselo a quien administra el sistema.')
    expect(screen.getByRole('alert').tagName).toBe('P')
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
  })
  it('error transitorio conserva mensaje del servidor y permite reintentar', async () => {
    const reintentar = vi.fn(), usuario = userEvent.setup()
    render(<EstadoConsultaAnalisis solicitud={{ estado: 'error', datos: null, error: new ErrorHttp(503, { codigo: 'analisis_no_disponible', mensaje: 'La foto no está disponible.' }), reintentar }} />)
    expect(screen.getByRole('alert')).toHaveTextContent('La foto no está disponible.')
    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(reintentar).toHaveBeenCalledOnce()
  })
})
