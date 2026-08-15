import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { LEYENDA_SIN_REMITO, SelectorDeViajes } from './SelectorDeViajes'
import { ResumenDeImportes } from './ResumenDeImportes'
import type { ViajeFacturable } from '../servicios/servicioFacturas'

function viaje(parcial: Partial<ViajeFacturable> & { id: number }): ViajeFacturable {
  return {
    numero: parcial.id + 1000,
    fecha: '2026-08-05',
    numeroRemito: `R-${parcial.id}`,
    origen: 'Rosario',
    destino: 'Córdoba',
    importe: 50_000,
    puedeFacturarse: true,
    motivoNoFacturable: null,
    ...parcial,
  }
}

function renderizar(viajes: ViajeFacturable[], seleccionados = new Set<number>()) {
  const onCambiarSeleccion = vi.fn()

  render(
    <SelectorDeViajes
      viajes={viajes}
      seleccionados={seleccionados}
      cliente="Distribuidora del Litoral"
      mes="agosto"
      anio="2026"
      cargando={false}
      onCambiarSeleccion={onCambiarSeleccion}
    />,
  )

  return { onCambiarSeleccion }
}

describe('SelectorDeViajes', () => {
  /**
   * FR-019a: el viaje sin remito **aparece igual**, con la casilla deshabilitada y la palabra que lo
   * explica. Esconderlo dejaría a quien opera buscando un viaje que sabe que existe (convención [003]).
   */
  it('ofrece el viaje sin remito con la casilla deshabilitada y la leyenda al lado', () => {
    renderizar([
      viaje({ id: 1 }),
      viaje({ id: 2, numeroRemito: null, puedeFacturarse: false, motivoNoFacturable: 'sinRemito' }),
    ])

    expect(screen.getByLabelText('Incluir el viaje 1001')).toBeEnabled()

    const sinRemito = screen.getByLabelText('Incluir el viaje 1002')
    expect(sinRemito).toBeDisabled()

    // La palabra, no sólo la casilla apagada: un elemento atenuado lleva el texto que lo explica.
    expect(screen.getByText(LEYENDA_SIN_REMITO)).toBeInTheDocument()
    expect(screen.getByText('Sin remito — no se puede facturar')).toBeInTheDocument()
  })

  it('avisa la selección al marcar un viaje', async () => {
    const usuario = userEvent.setup()
    const { onCambiarSeleccion } = renderizar([viaje({ id: 1 })])

    await usuario.click(screen.getByLabelText('Incluir el viaje 1001'))

    expect(onCambiarSeleccion).toHaveBeenCalledWith(new Set([1]))
  })

  /** FR-021: sin facturables el mensaje nombra la combinación y explica el criterio. */
  it('sin viajes facturables nombra el cliente y el período', () => {
    renderizar([])

    expect(
      screen.getByText(
        'No hay viajes facturables de Distribuidora del Litoral en agosto de 2026. Se ofrecen sólo ' +
          'los viajes rendidos, sin facturar, cuya fecha cae en ese período.',
      ),
    ).toBeInTheDocument()
  })

  /** Los importes van con `formatearPesos`, nunca con `toFixed(2)` (convención [005]). */
  it('formatea los importes en pesos argentinos', () => {
    renderizar([viaje({ id: 1, importe: 1_240_000 })])

    expect(screen.getByText('$ 1.240.000,00')).toBeInTheDocument()
  })
})

describe('ResumenDeImportes', () => {
  /**
   * FR-020 y FR-025: los tres importes se recalculan en cada cambio de la selección y del tipo. Con el
   * ejemplo de la propia spec: `82.644,63` → IVA `17.355,37` → total `100.000,00`.
   */
  it('calcula el ejemplo de la spec con Factura A', () => {
    render(
      <ResumenDeImportes
        importes={[30_000, 30_000, 22_644.63]}
        tipoComprobante="facturaA"
      />,
    )

    expect(screen.getByText('$ 82.644,63')).toBeInTheDocument()
    expect(screen.getByText('IVA (21%)')).toBeInTheDocument()
    expect(screen.getByText('$ 17.355,37')).toBeInTheDocument()
    expect(screen.getByText('$ 100.000,00')).toBeInTheDocument()
  })

  /**
   * FR-023: con `Factura C` el IVA es `$ 0,00` y el total es igual al neto. **No es un error ni una
   * factura incompleta**, y la pantalla lo dice con palabras.
   */
  it('con Factura C muestra IVA cero y total igual al neto, y lo explica', () => {
    render(<ResumenDeImportes importes={[52_644.63]} tipoComprobante="facturaC" />)

    expect(screen.getByText('IVA (0%)')).toBeInTheDocument()
    expect(screen.getByText('$ 0,00')).toBeInTheDocument()
    expect(screen.getAllByText('$ 52.644,63')).toHaveLength(2)
    expect(
      screen.getByText('Una Factura C no lleva IVA: el total es igual al neto.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-024: **no hay ningún campo donde escribir los importes.** No están deshabilitados: no existen
   * como campos. Es la diferencia entre "no podés editarlo" y "no es algo que se edite".
   */
  it('no ofrece ningún campo editable para los importes', () => {
    render(<ResumenDeImportes importes={[100_000]} tipoComprobante="facturaA" />)

    expect(screen.queryAllByRole('textbox')).toHaveLength(0)
    expect(screen.queryAllByRole('spinbutton')).toHaveLength(0)
  })

  /** La cantidad seleccionada se anuncia: cambia sin que la pantalla cambie (convención [003]). */
  it('anuncia la cantidad de viajes seleccionados con role="status"', () => {
    render(<ResumenDeImportes importes={[10_000, 20_000]} tipoComprobante="facturaA" />)

    const anuncio = screen.getByText('2 viajes seleccionados')
    expect(anuncio.closest('[role="status"]')).not.toBeNull()
  })

  it('usa el singular con un solo viaje', () => {
    render(<ResumenDeImportes importes={[10_000]} tipoComprobante="facturaA" />)

    expect(screen.getByText('1 viaje seleccionado')).toBeInTheDocument()
  })
})
