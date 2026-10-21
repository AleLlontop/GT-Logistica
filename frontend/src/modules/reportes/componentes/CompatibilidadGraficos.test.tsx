import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Line, LineChart, XAxis } from 'recharts'

/** Integración real de la versión estable con React19/TS6; sin mock de la biblioteca. */
describe('compatibilidad de gráficos', () => {
  it('renderiza SVG accesible con el runtime React instalado', () => {
    const { container } = render(
      <LineChart width={400} height={240} data={[{ mes: '2026-09', cantidad: 1 }, { mes: '2026-10', cantidad: 2 }]}
        accessibilityLayer aria-label="Actividad por mes">
        <XAxis dataKey="mes" />
        <Line dataKey="cantidad" isAnimationActive={false} />
      </LineChart>,
    )
    expect(screen.getByRole('application', { name: 'Actividad por mes' })).toBeInTheDocument()
    expect(container.querySelector('svg')).not.toBeNull()
    expect(screen.getByText('2026-10')).toBeInTheDocument()
  })
})
