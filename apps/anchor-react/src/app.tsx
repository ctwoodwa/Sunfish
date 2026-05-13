import { BrowserRouter, Routes, Route, Navigate, NavLink } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ErrorBoundary } from 'react-error-boundary'
import { useEffect } from 'react'
import { PropertiesPage } from '@/pages/PropertiesPage'
import { LeasesPage } from '@/pages/LeasesPage'
import { LeaseDetailPage } from '@/pages/LeaseDetailPage'
import { RentCollectionPage } from '@/pages/RentCollectionPage'
import { OfflineBanner } from '@/components/OfflineBanner'
import { CompanySwitcher } from '@/components/CompanySwitcher'
import { useCompanyStore } from '@/stores/companyStore'

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
    <div className="flex min-h-screen items-center justify-center p-6">
      <div className="rounded-lg border border-red-200 bg-red-50 p-8 max-w-md w-full">
        <h2 className="text-xl font-bold text-red-700">Something went wrong</h2>
        <p className="mt-2 text-sm text-gray-600">{error.message}</p>
        <button
          onClick={() => {
            queryClient.clear()
            resetErrorBoundary()
          }}
          className="mt-4 rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
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

  useEffect(() => {
    fetch('/api/v1/whoami', { credentials: 'include' })
      .then((r) => r.json())
      .then((data: { defaultCompany?: string; availableCompanies?: string[] }) => {
        if (data.defaultCompany) setActiveCompany(data.defaultCompany)
        if (data.availableCompanies) setAvailableCompanies(data.availableCompanies)
      })
      .catch(() => {/* whoami unavailable in dev without Bridge running */})
  }, [setActiveCompany, setAvailableCompanies])

  return (
    <div className="min-h-screen bg-white">
      <OfflineBanner />
      <header className="border-b border-gray-200">
        <div className="mx-auto flex h-14 max-w-7xl items-center justify-between px-4">
          <nav className="flex items-center gap-6 text-sm">
            <span className="font-semibold text-gray-900">Sunfish</span>
            <NavLink
              to="/properties"
              className={({ isActive }) =>
                isActive ? 'text-gray-900 font-medium' : 'text-gray-500 hover:text-gray-900'
              }
            >
              Properties
            </NavLink>
            <NavLink
              to="/leases"
              className={({ isActive }) =>
                isActive ? 'text-gray-900 font-medium' : 'text-gray-500 hover:text-gray-900'
              }
            >
              Leases
            </NavLink>
            <NavLink
              to="/rent"
              className={({ isActive }) =>
                isActive ? 'text-gray-900 font-medium' : 'text-gray-500 hover:text-gray-900'
              }
            >
              Rent
            </NavLink>
          </nav>
          <CompanySwitcher />
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-8">
        <Routes>
          <Route path="/" element={<Navigate to="/properties" replace />} />
          <Route path="/properties" element={<PropertiesPage />} />
          <Route path="/leases" element={<LeasesPage />} />
          <Route path="/leases/:name" element={<LeaseDetailPage />} />
          <Route path="/rent" element={<RentCollectionPage />} />
        </Routes>
      </main>
    </div>
  )
}

export function App() {
  return (
    <ErrorBoundary FallbackComponent={AppErrorFallback}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AppLayout />
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  )
}
