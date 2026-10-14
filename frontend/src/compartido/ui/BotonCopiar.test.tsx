import { fireEvent, render, screen, act } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { BotonCopiar } from './BotonCopiar'

describe('BotonCopiar', () => {
  let writeTextMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    writeTextMock = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(window.navigator, 'clipboard', {
      value: {
        writeText: writeTextMock,
      },
      writable: true,
      configurable: true,
    })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('copia el texto al hacer clic y muestra feedback ¡Copiado!', async () => {
    render(<BotonCopiar texto="30712345671" etiqueta="Copiar CUIT" />)

    const boton = screen.getByRole('button', { name: 'Copiar CUIT' })
    expect(boton).toBeInTheDocument()

    fireEvent.click(boton)

    expect(writeTextMock).toHaveBeenCalledWith('30712345671')
    expect(await screen.findByText('¡Copiado!')).toBeInTheDocument()
  })

  it('vuelve al estado original después del timeout', async () => {
    vi.useFakeTimers()

    render(<BotonCopiar texto="75123456789012" etiqueta="Copiar CAE" />)

    const boton = screen.getByRole('button', { name: 'Copiar CAE' })

    await act(async () => {
      fireEvent.click(boton)
    })

    expect(screen.getByText('¡Copiado!')).toBeInTheDocument()

    await act(async () => {
      vi.advanceTimersByTime(2100)
    })

    expect(screen.queryByText('¡Copiado!')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Copiar CAE' })).toBeInTheDocument()
  })
})
