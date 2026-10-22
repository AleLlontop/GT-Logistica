import { render, screen } from '@testing-library/react'
import type { TooltipContentProps } from 'recharts'
import { describe, expect, it } from 'vitest'
import { ValorAlApuntar } from './GraficosRecharts'

describe('tooltip exacto', () => {
  it('lee la cadena original, aunque la coordenada aproximada haya perdido centavos visuales', () => {
    const contenido = { active: true, payload: [{ payload: { cobrado: 10, original: { etiqueta: 'Octubre', valores: { cobrado: '9999999999999999.99', neto: '-0.01' } } } }] } as unknown as TooltipContentProps
    render(<ValorAlApuntar contenido={contenido} series={[{ clave: 'cobrado', nombre: 'Cobrado' }, { clave: 'neto', nombre: 'Neto' }]} monetario />)
    expect(screen.getByText('$ 9.999.999.999.999.999,99')).toBeVisible()
    expect(screen.getByText('-$ 0,01')).toBeVisible()
  })
})
