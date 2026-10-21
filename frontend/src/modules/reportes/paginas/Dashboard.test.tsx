import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Dashboard } from './Dashboard'
import { consultarDashboard } from '../servicios/servicioAnalisis'
import type { DashboardAnalisis } from '../servicios/tiposAnalisis'
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarDashboard: vi.fn() }))
vi.mock('../componentes/GraficoAccesible', () => ({ GraficoAccesible: ({ titulo }: { titulo: string }) => <h3>{titulo}</h3> }))
const sin = { estado: 'sinPermiso', datos: null } as const
function datos(): DashboardAnalisis {
  return { contexto: { desde: '2026-10-01', hasta: '2026-10-31', consultadoEn: '2026-10-20T14:00:00Z', hoyArgentina: '2026-10-20', criterioTemporal: 'perspectivasSeparadas' }, secciones: { viajes: sin, facturacion: sin, liquidaciones: sin, caja: sin, flota: sin, vencimientosChoferes: sin, vencimientosFlota: sin, vencimientosFacturas: sin } }
}
function montar(permisos = ['reportes.consultar']) { return render(<MemoryRouter><Dashboard sesionClave="gerente" permisos={permisos} /></MemoryRouter>) }
afterEach(() => { vi.useRealTimers(); vi.clearAllMocks() })
describe('Dashboard', () => {
  it('base aislada distingue secciones denegadas sin cifras ni enlaces sensibles', async () => {
    vi.mocked(consultarDashboard).mockResolvedValue(datos()); montar()
    expect(await screen.findByText('Facturación y cobros: acceso no disponible')).toBeInTheDocument()
    expect(screen.getAllByText('Acceso no disponible')).toHaveLength(8)
    expect(screen.queryByRole('link', { name: 'Facturación y cobros' })).not.toBeInTheDocument()
    expect(screen.queryByText('$ 0,00')).not.toBeInTheDocument()
  })
  it('sin base no invoca consulta; fallo de foto no muestra KPI ni ceros', async () => {
    const vista = montar([])
    expect(screen.getByText(/No tenés permiso/)).toBeInTheDocument(); expect(consultarDashboard).not.toHaveBeenCalled()
    vista.unmount(); vi.mocked(consultarDashboard).mockRejectedValue(new Error('SQL')); montar()
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Indicadores del período' })).not.toBeInTheDocument()
  })
  it('mes argentino por defecto, borrador no consulta y aplicar preserva período en acceso', async () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-01T01:30:00Z'))
    const d = datos(); d.secciones.viajes = { estado: 'disponible', datos: { resumen: { cantidad: 0, realizados: 0, pendientesRendicion: 0, pendientes: 0, anulados: 0, importeTotal: '0.00' }, series: [], distribuciones: { estado: { top: [], otros: null, totalCategorias: 0 }, cliente: { top: [], otros: null, totalCategorias: 0 }, transportista: { top: [], otros: null, totalCategorias: 0 } } } }
    vi.mocked(consultarDashboard).mockResolvedValue(d); montar()
    await screen.findByRole('link', { name: 'Viajes' })
    expect(consultarDashboard).toHaveBeenLastCalledWith({ desde: '2026-10-01', hasta: '2026-10-31' })
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-09-15' } })
    expect(consultarDashboard).toHaveBeenCalledTimes(1)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar período' }))
    await waitFor(() => expect(consultarDashboard).toHaveBeenCalledTimes(2))
    expect(await screen.findByRole('link', { name: 'Viajes' })).toHaveAttribute('href', '/reportes/viajes?desde=2026-09-15&hasta=2026-10-31')
    const indicadores = screen.getByRole('region', { name: 'Indicadores del período' }), graficos = screen.getByRole('region', { name: 'Gráficos del período' })
    expect(indicadores.compareDocumentPosition(graficos) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
  it('dinero y perspectivas independientes, vencimientos sin filtro global', async () => {
    const d = datos()
    d.secciones.caja = { estado: 'disponible', datos: { resumen: { cantidad: 2, ingresos: '0.00', egresos: '9999999999999999.99', neto: '-9999999999999999.99' }, series: [] } }
    d.secciones.vencimientosChoferes = { estado: 'disponible', datos: { contexto: { ...d.contexto, desde: null, hasta: null, criterioTemporal: 'situacionActual' }, resumen: { cantidad: 3 } } }
    vi.mocked(consultarDashboard).mockResolvedValue(d); montar()
    expect(await screen.findByText('Movimiento neto de Caja')).toBeInTheDocument()
    expect(screen.getByText(/-.*9\.999\.999\.999\.999\.999,99/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Vencimientos' })).toHaveAttribute('href', '/reportes/vencimientos')
    expect(screen.getByText(/situación actual, sin corte por período/)).toBeInTheDocument()
  })
})
