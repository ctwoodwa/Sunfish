import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getMaintenanceTickets,
  createMaintenanceTicket,
  updateMaintenanceTicket,
  type MaintenanceTicket,
  type CreateMaintenanceInput,
} from '@/api/erpnext'
import { AuthRoleGate } from '@/components/AuthRoleGate'

const PRIORITY_COLORS: Record<string, string> = {
  Critical: 'bg-priority-urgent text-priority-urgent-fg',
  High:     'bg-priority-high text-priority-high-fg',
  Medium:   'bg-priority-medium text-priority-medium-fg',
  Low:      'bg-priority-low text-priority-low-fg',
}

const STATUS_COLORS: Record<string, string> = {
  Open:          'bg-status-open text-status-open-fg',
  'In Progress': 'bg-status-in-progress text-status-in-progress-fg',
  Resolved:      'bg-status-resolved text-status-resolved-fg',
  Closed:        'bg-status-closed text-status-closed-fg',
}

function TicketRow({ ticket, onStatusChange }: { ticket: MaintenanceTicket; onStatusChange: (name: string, status: string) => void }) {
  return (
    <tr className="border-t border-border hover:bg-muted">
      <td className="py-3 px-4 text-sm font-mono text-muted-foreground">{ticket.name}</td>
      <td className="py-3 px-4 text-sm text-foreground">{ticket.subject}</td>
      <td className="py-3 px-4 text-sm text-muted-foreground">{ticket.property}</td>
      <td className="py-3 px-4">
        <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${PRIORITY_COLORS[ticket.priority] ?? 'bg-muted text-muted-foreground'}`}>
          {ticket.priority}
        </span>
      </td>
      <td className="py-3 px-4">
        <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[ticket.status] ?? 'bg-muted text-muted-foreground'}`}>
          {ticket.status}
        </span>
      </td>
      <td className="py-3 px-4 text-sm text-muted-foreground">{ticket.assigned_to ?? '—'}</td>
      <td className="py-3 px-4 text-sm text-muted-foreground">
        {ticket.cost != null ? `$${ticket.cost.toFixed(2)}` : '—'}
      </td>
      <td className="py-3 px-4">
        <AuthRoleGate allow={['owner', 'manager']}>
          <select
            className="text-xs rounded border border-border bg-background py-1 px-2 text-foreground"
            value={ticket.status}
            onChange={(e) => onStatusChange(ticket.name, e.target.value)}
          >
            <option>Open</option>
            <option>In Progress</option>
            <option>Resolved</option>
            <option>Closed</option>
          </select>
        </AuthRoleGate>
      </td>
    </tr>
  )
}

function CreateTicketForm({ onSuccess }: { onSuccess: () => void }) {
  const [subject, setSubject] = useState('')
  const [property, setProperty] = useState('')
  const [priority, setPriority] = useState('Medium')

  const mutation = useMutation({
    mutationFn: (payload: CreateMaintenanceInput) => createMaintenanceTicket(payload),
    onSuccess: () => {
      setSubject('')
      setProperty('')
      setPriority('Medium')
      onSuccess()
    },
  })

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        mutation.mutate({ Subject: subject, Property: property, Priority: priority })
      }}
      className="rounded-lg border border-border bg-muted p-4 space-y-3"
    >
      <h3 className="text-sm font-semibold text-foreground">New Ticket</h3>
      <div className="grid grid-cols-3 gap-3">
        <input
          required
          placeholder="Subject"
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
          className="col-span-1 rounded border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <input
          required
          placeholder="Property (ERPNext name)"
          value={property}
          onChange={(e) => setProperty(e.target.value)}
          className="col-span-1 rounded border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <select
          value={priority}
          onChange={(e) => setPriority(e.target.value)}
          className="col-span-1 rounded border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        >
          <option>Low</option>
          <option>Medium</option>
          <option>High</option>
          <option>Critical</option>
        </select>
      </div>
      <button
        type="submit"
        disabled={mutation.isPending}
        className="rounded bg-primary px-4 py-1.5 text-sm text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
      >
        {mutation.isPending ? 'Submitting…' : 'Submit'}
      </button>
      {mutation.isError && (
        <p className="text-xs text-destructive">{(mutation.error as Error).message}</p>
      )}
    </form>
  )
}

export function MaintenancePage() {
  const qc = useQueryClient()

  const { data: tickets = [], isLoading, isError, error } = useQuery({
    queryKey: ['maintenance'],
    queryFn: getMaintenanceTickets,
    staleTime: 2 * 60 * 1000,
  })

  const statusMutation = useMutation({
    mutationFn: ({ name, status }: { name: string; status: string }) =>
      updateMaintenanceTicket(name, { Status: status }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['maintenance'] }),
  })

  const handleStatusChange = (name: string, status: string) =>
    statusMutation.mutate({ name, status })

  const openCount = tickets.filter((t) => t.status === 'Open' || t.status === 'In Progress').length

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Maintenance</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {openCount} open ticket{openCount !== 1 ? 's' : ''}
        </p>
      </div>

      <AuthRoleGate allow={['owner', 'manager']}>
        <CreateTicketForm onSuccess={() => void qc.invalidateQueries({ queryKey: ['maintenance'] })} />
      </AuthRoleGate>

      {isLoading && <p className="text-sm text-muted-foreground">Loading maintenance tickets…</p>}
      {isError && (
        <p className="text-sm text-destructive">Error: {(error as Error).message}</p>
      )}

      {!isLoading && !isError && tickets.length === 0 && (
        <p className="text-sm text-muted-foreground">No maintenance tickets found.</p>
      )}

      {tickets.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full text-left">
            <thead className="bg-muted text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="py-3 px-4">ID</th>
                <th className="py-3 px-4">Subject</th>
                <th className="py-3 px-4">Property</th>
                <th className="py-3 px-4">Priority</th>
                <th className="py-3 px-4">Status</th>
                <th className="py-3 px-4">Assigned To</th>
                <th className="py-3 px-4">Cost</th>
                <th className="py-3 px-4">Action</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((ticket) => (
                <TicketRow
                  key={ticket.name}
                  ticket={ticket}
                  onStatusChange={handleStatusChange}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
