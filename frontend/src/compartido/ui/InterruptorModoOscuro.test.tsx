import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, beforeEach } from 'vitest'
import { InterruptorModoOscuro } from './InterruptorModoOscuro'

describe('InterruptorModoOscuro', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.classList.remove('dark')
  })

  it('alterna entre modo claro y oscuro al hacer clic', () => {
    render(<InterruptorModoOscuro />)

    const switchBtn = screen.getByRole('switch')
    expect(switchBtn).toBeInTheDocument()

    // Inicialmente modo claro (o según sistema)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(switchBtn).toHaveAttribute('aria-checked', 'false')

    // Click activa modo oscuro
    fireEvent.click(switchBtn)

    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(switchBtn).toHaveAttribute('aria-checked', 'true')
    expect(localStorage.getItem('gt-tema')).toBe('dark')

    // Click desactiva modo oscuro
    fireEvent.click(switchBtn)

    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(switchBtn).toHaveAttribute('aria-checked', 'false')
    expect(localStorage.getItem('gt-tema')).toBe('light')
  })
})
