import { describe, it, expect } from 'vitest'

import {
  type Property,
  type Unit,
  type RentRollRow,
  OccupancyStatus as _OccupancyStatus,
  RentStatus as _RentStatus,
} from '../property.js'

import {
  type PLSummary,
  type LedgerEntry,
  type BankTransaction,
  type OutstandingInvoice,
  ReconciliationStatus as _ReconciliationStatus,
} from '../accounting.js'

import {
  type Tenant,
  type Lease,
  type PaymentRecord,
  type MessageThread,
} from '../tenant.js'

import {
  type SyncStatus,
  type OfflineQueueEntry,
  type ConflictRecord,
} from '../sync.js'

import {
  IntegrationCategory,
  CredentialFieldKind,
  ProviderValidationStatus,
  type IntegrationAtlasView,
} from '../integrations.js'

import {
  OverallVerdict,
  DimensionChangeKind,
  parseSystemRequirementsResult,
  type SystemRequirementsResult,
} from '../system-requirements.js'

describe('@sunfish/contracts — property namespace', () => {
  it('OccupancyStatus values are correct string literals', () => {
    const statuses = ['Active', 'Vacant', 'Maintenance', 'Sold'] as const
    statuses.forEach(s => {
      const prop: Property = {
        name: 'PROP-0001',
        propertyName: 'Test',
        addressLine1: '123 Main St',
        city: 'Seattle',
        state: 'WA',
        postalCode: '98101',
        units: 1,
        status: s,
        company: 'Acme',
      }
      expect(prop.status).toBe(s)
    })
  })

  it('RentRollRow shape satisfies required fields', () => {
    const row: RentRollRow = {
      propertyId: 'PROP-0001',
      propertyName: 'Test',
      monthlyRent: 1500,
      balanceDue: 0,
      status: 'Current',
    }
    expect(row.propertyId).toBe('PROP-0001')
    expect(row.balanceDue).toBe(0)
  })
})

describe('@sunfish/contracts — accounting namespace', () => {
  it('PLSummary net equals income minus expenses', () => {
    const summary: PLSummary = {
      period: '2025-01',
      income: 5000,
      expenses: 2000,
      net: 3000,
    }
    expect(summary.net).toBe(summary.income - summary.expenses)
  })

  it('BankTransaction has reconciliationStatus field', () => {
    const tx: BankTransaction = {
      name: 'BT-0001',
      date: '2025-01-15',
      description: 'Rent payment',
      amount: 1500,
      currency: 'USD',
      reconciliationStatus: 'Unreconciled',
    }
    expect(tx.reconciliationStatus).toBe('Unreconciled')
  })
})

describe('@sunfish/contracts — tenant namespace', () => {
  it('Lease status is one of the four expected values', () => {
    const statuses: Lease['status'][] = ['Active', 'Expired', 'Terminated', 'Pending']
    statuses.forEach(status => {
      expect(['Active', 'Expired', 'Terminated', 'Pending']).toContain(status)
    })
  })

  it('MessageThread requires threadId and subject', () => {
    const thread: MessageThread = {
      threadId: 'thread-001',
      subject: 'Maintenance request',
      participants: ['tenant@example.com', 'owner@example.com'],
    }
    expect(thread.threadId).toBe('thread-001')
    expect(thread.participants).toHaveLength(2)
  })
})

describe('@sunfish/contracts — sync namespace', () => {
  it('OfflineQueueEntry opType is create | update | delete', () => {
    const entry: OfflineQueueEntry = {
      id: 'q-001',
      doctype: 'Lease',
      opType: 'create',
      payloadJson: '{}',
      createdAt: '2025-01-01T00:00:00Z',
    }
    expect(['create', 'update', 'delete']).toContain(entry.opType)
  })

  it('SyncStatus values are valid literals', () => {
    const statuses: SyncStatus[] = ['online', 'offline', 'syncing']
    statuses.forEach(s => expect(typeof s).toBe('string'))
  })
})

describe('@sunfish/contracts — integrations namespace (ADR 0067)', () => {
  it('IntegrationCategory enum values match PascalCase wire format', () => {
    expect(IntegrationCategory.Payments).toBe('Payments')
    expect(IntegrationCategory.MeshVpn).toBe('MeshVpn')
    expect(IntegrationCategory.TransactionalEmail).toBe('TransactionalEmail')
  })

  it('IntegrationAtlasView shape is structurally valid', () => {
    const view: IntegrationAtlasView = {
      activeByCategory: {
        Payments: { providerId: 'stripe', activatedAt: '2025-01-01T00:00:00Z' },
      },
      statusByCategory: {
        Payments: ProviderValidationStatus.Valid,
      },
    }
    expect(view.activeByCategory['Payments']?.providerId).toBe('stripe')
  })
})

describe('@sunfish/contracts — system-requirements namespace (ADR 0063)', () => {
  it('parseSystemRequirementsResult accepts a valid object', () => {
    const raw = {
      overall: OverallVerdict.Pass,
      dimensions: [],
      evaluatedAt: '2025-01-01T00:00:00+00:00',
    }
    const result: SystemRequirementsResult = parseSystemRequirementsResult(raw)
    expect(result.overall).toBe('Pass')
  })

  it('parseSystemRequirementsResult throws on missing overall', () => {
    expect(() => parseSystemRequirementsResult({ dimensions: [], evaluatedAt: '2025-01-01' })).toThrow(
      TypeError,
    )
  })

  it('DimensionChangeKind has all 10 mission-envelope dimensions', () => {
    const expected = [
      'Hardware', 'User', 'Regulatory', 'Runtime', 'FormFactor',
      'Edition', 'Network', 'TrustAnchor', 'SyncState', 'VersionVector',
    ]
    const actual = Object.values(DimensionChangeKind)
    expect(actual).toEqual(expect.arrayContaining(expected))
    expect(actual).toHaveLength(10)
  })
})
