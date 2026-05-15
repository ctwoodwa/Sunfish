import { describe, it, expect, afterEach } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import { OfflineBanner } from './OfflineBanner'

describe('OfflineBanner', () => {
  const originalOnline = Object.getOwnPropertyDescriptor(navigator, 'onLine')

  function setOnline(value: boolean) {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value })
  }

  afterEach(() => {
    if (originalOnline) {
      Object.defineProperty(navigator, 'onLine', originalOnline)
    }
  })

  it('renders nothing when online', () => {
    setOnline(true)
    const { container } = render(<OfflineBanner />)
    expect(container.firstChild).toBeNull()
  })

  it('renders banner text when navigator.onLine is false at mount', () => {
    setOnline(false)
    render(<OfflineBanner />)
    expect(screen.getByText(/Offline — changes can't save yet/i)).toBeInTheDocument()
  })

  it('shows banner when window offline event fires', () => {
    setOnline(true)
    render(<OfflineBanner />)
    expect(screen.queryByText(/Offline/i)).not.toBeInTheDocument()

    act(() => {
      setOnline(false)
      window.dispatchEvent(new Event('offline'))
    })

    expect(screen.getByText(/Offline — changes can't save yet/i)).toBeInTheDocument()
  })

  it('hides banner when window online event fires after going offline', () => {
    setOnline(false)
    render(<OfflineBanner />)
    expect(screen.getByText(/Offline/i)).toBeInTheDocument()

    act(() => {
      setOnline(true)
      window.dispatchEvent(new Event('online'))
    })

    expect(screen.queryByText(/Offline/i)).not.toBeInTheDocument()
  })
})
