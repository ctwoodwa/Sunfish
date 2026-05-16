import { BrowserRouter, Routes, Route, Navigate, NavLink } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ErrorBoundary } from 'react-error-boundary'
import { lazy, Suspense, useEffect, useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import { PropertiesPage } from '@/pages/PropertiesPage'
import { LeasesPage } from '@/pages/LeasesPage'
import { LeaseDetailPage } from '@/pages/LeaseDetailPage'
import { RentCollectionPage } from '@/pages/RentCollectionPage'
import { AccountingPage } from '@/pages/AccountingPage'
import { CrewCommsPage } from '@/pages/CrewCommsPage'
import { MaintenancePage } from '@/pages/MaintenancePage'
import { LoginPage } from '@/pages/LoginPage'
import { SyncStateBadge } from '@sunfish/ui-react'

// Dev-only PDF preview route. import.meta.env.DEV is a build-time
// literal so the lazy import — and the react-pdf renderer it pulls
// in — tree-shake completely out of production bundles.
const InternalReportsPreviewPage = import.meta.env.DEV
  ? lazy(() =>
      import('@/pages/InternalReportsPreviewPage').then((m) => ({
        default: m.InternalReportsPreviewPage,
      })),
    )
  : null
import { OfflineBanner } from '@/components/OfflineBanner'
import { CompanySwitcher } from '@/components/CompanySwitcher'
import { useCompanyStore } from '@/stores/companyStore'
import { useAuthStore } from '@/stores/authStore'
import { useSyncStore } from '@/stores/syncStore'
import { getToken as loadStoredToken, clearToken as clearStoredToken } from '@/services/credentialStore'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 2,
      retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
    },
  },
})

function AppErrorFallback({ error, resetErrorBoundary }: { error: Error; resetErrorBoundary: () => void }) {
  useEffect(() => {
    fetch('/api/v1/telemetry/error', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: error.message,
        stack: error.stack,
        route: window.location.pathname,
        userAgent: navigator.userAgent,
        timestamp: new Date().toISOString(),
      }),
    }).catch(() => {/* best-effort */})
  }, [error])

  return (
    <div className="flex min-h-screen items-center justify-center p-6 bg-background text-foreground">
      <div className="rounded-lg border border-destructive/20 bg-destructive/10 p-8 max-w-md w-full">
        <h2 className="text-xl font-bold text-destructive">Something went wrong</h2>
        <p className="mt-2 text-sm text-muted-foreground">{error.message}</p>
        <button
          onClick={() => {
            queryClient.clear()
            resetErrorBoundary()
          }}
          className="mt-4 rounded bg-primary px-4 py-2 text-sm text-primary-foreground hover:bg-primary/90"
        >
          Retry
        </button>
      </div>
    </div>
  )
}

function AppLayout() {
  const setActiveCompany = useCompanyStore((s) => s.setActiveCompany)
  const setAvailableCompanies = useCompanyStore((s) => s.setAvailableCompanies)
  const setAuth = useAuthStore((s) => s.setAuth)
  const setToken = useAuthStore((s) => s.setToken)
  const syncState = useSyncStore((s) => s.syncState)

  async function onLogout() {
    try {
      await clearStoredToken()
      await invoke('set_bridge_token', { token: '' }).catch(() => {})
    } finally {
      setToken(null)
    }
  }

  useEffect(() => {
    fetch('/api/v1/whoami', { credentials: 'include' })
      .then((r) => r.json())
      .then((data: { user?: string; role?: string; defaultCompany?: string; availableCompanies?: string[] }) => {
        if (data.defaultCompany) setActiveCompany(data.defaultCompany)
        if (data.availableCompanies) setAvailableCompanies(data.availableCompanies)
        setAuth(data.user ?? 'dev-user', data.role ?? 'owner')
      })
      .catch(() => {
        setAuth('dev-user', 'owner')
      })
  }, [setActiveCompany, setAvailableCompanies, setAuth])

  return (
    <div className="min-h-screen bg-background text-foreground">
      <OfflineBanner />
      <header className="border-b border-border">
        <div className="mx-auto flex h-14 max-w-7xl items-center justify-between px-4">
          <nav className="flex items-center gap-6 text-sm">
            <span className="font-semibold text-foreground">Sunfish</span>
            <NavLink
              to="/properties"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Properties
            </NavLink>
            <NavLink
              to="/leases"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Leases
            </NavLink>
            <NavLink
              to="/rent"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Rent
            </NavLink>
            <NavLink
              to="/accounting"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Accounting
            </NavLink>
            <NavLink
              to="/comms"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Comms
            </NavLink>
            <NavLink
              to="/maintenance"
              className={({ isActive }) =>
                isActive ? 'text-foreground font-medium' : 'text-muted-foreground hover:text-foreground'
              }
            >
              Maintenance
            </NavLink>
          </nav>
          <div className="flex items-center gap-3">
            <SyncStateBadge state={syncState} />
            <CompanySwitcher />
            <button
              type="button"
              onClick={onLogout}
              className="rounded border border-border bg-background px-3 py-1.5 text-sm text-foreground hover:bg-muted"
            >
              Logout
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-8">
        <Routes>
          <Route path="/" element={<Navigate to="/properties" replace />} />
          <Route path="/properties" element={<PropertiesPage />} />
          <Route path="/leases" element={<LeasesPage />} />
          <Route path="/leases/:name" element={<LeaseDetailPage />} />
          <Route path="/rent" element={<RentCollectionPage />} />
          <Route path="/accounting" element={<AccountingPage />} />
          <Route path="/comms" element={<CrewCommsPage />} />
          <Route path="/maintenance" element={<MaintenancePage />} />
          {import.meta.env.DEV && InternalReportsPreviewPage && (
            <Route
              path="/internal/reports-preview"
              element={
                <Suspense fallback={<div className="p-4 text-sm text-muted-foreground">Loading…</div>}>
                  <InternalReportsPreviewPage />
                </Suspense>
              }
            />
          )}
        </Routes>
      </main>
    </div>
  )
}

/**
 * W#60 P4 PR 1 — auth boot gate.
 *
 * Two-stage probe on mount:
 *   1. `keychain_status` (Rust command, council A1.4) — surfaces OS-keychain
 *      derivation failures from setup time. On error: render a precise banner
 *      instead of falling through to LoginPage, which would otherwise look
 *      like a generic "please log in" even when the underlying problem is a
 *      GPO-locked Credential Manager or denied Keychain Access prompt.
 *   2. `credentialStore.getToken()` — pulls the persisted Bridge token from
 *      Stronghold. If present: seeds authStore + informs the Rust state via
 *      `set_bridge_token`. If absent: renders the LoginPage for manual entry.
 *
 * Subscribes to authStore.token so a successful LoginPage submit (which sets
 * the token) re-renders this component and reveals the app.
 */
function AuthGate() {
  const token = useAuthStore((s) => s.token)
  const setToken = useAuthStore((s) => s.setToken)
  const [loaded, setLoaded] = useState(false)
  const [keychainError, setKeychainError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      // Step 1 — keychain probe. If derivation failed at setup, don't even
      // attempt to open Stronghold; the closure would return the sentinel key
      // and Stronghold would surface an opaque decryption error.
      try {
        const status = await invoke<string | null>('keychain_status')
        if (cancelled) return
        if (status) {
          setKeychainError(status)
          setLoaded(true)
          return
        }
      } catch {
        // Command not registered or IPC denied — treat as keychain unavailable
        // for safety; user sees the banner rather than a confusing login loop.
        if (cancelled) return
        setKeychainError('Anchor could not reach the operating-system credential store.')
        setLoaded(true)
        return
      }
      // Step 2 — load any stored token from Stronghold.
      try {
        const stored = await loadStoredToken()
        if (cancelled) return
        if (stored) {
          await invoke('set_bridge_token', { token: stored }).catch(() => {})
          setToken(stored)
        }
      } catch {
        // Stronghold init failed or token absent — fall through to LoginPage.
      } finally {
        if (!cancelled) setLoaded(true)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [setToken])

  if (!loaded) {
    // Brief splash while we probe; avoids the LoginPage flashing on every cold
    // start before we know whether there's a stored token.
    return <div className="min-h-screen bg-background" />
  }
  if (keychainError) {
    return (
      <div className="flex min-h-screen items-center justify-center p-6 bg-background text-foreground">
        <div className="w-full max-w-md space-y-3 rounded-lg border border-destructive/20 bg-destructive/10 p-8">
          <h2 className="text-lg font-semibold text-destructive">Keychain unavailable</h2>
          <p className="text-sm text-muted-foreground">
            Anchor could not access the operating-system credential store needed to
            secure your Bridge auth token. The application cannot sign you in until
            this is resolved.
          </p>
          <p className="text-xs font-mono text-muted-foreground break-words">
            {keychainError}
          </p>
          <p className="text-sm text-muted-foreground">
            Common causes: Windows Group Policy disabling Credential Manager;
            macOS Keychain Access denial (try System Settings → Privacy &amp;
            Security → Keychain Access); a Linux session without an active
            Secret Service daemon (gnome-keyring / KWallet).
          </p>
        </div>
      </div>
    )
  }
  if (!token) return <LoginPage />
  return <AppLayout />
}

export function App() {
  return (
    <ErrorBoundary FallbackComponent={AppErrorFallback}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthGate />
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  )
}
