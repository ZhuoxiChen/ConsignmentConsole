export const CONSIGNMENT_STATUSES = [
  'booked',
  'picked_up',
  'in_transit',
  'out_for_delivery',
  'delivered',
  'failed_delivery',
  'returned',
  'cancelled',
] as const

export type ConsignmentStatus = (typeof CONSIGNMENT_STATUSES)[number]

export const DEMO_TENANTS = [
  { code: 'swiftline-freight', name: 'Swiftline Freight' },
  { code: 'coastal-couriers', name: 'Coastal Couriers' },
  { code: 'northern-haulage', name: 'Northern Haulage' },
] as const

export const FAILURE_REASONS = [
  'no_one_home',
  'address_incorrect',
  'refused',
  'access_issue',
] as const

export type FailureReason = (typeof FAILURE_REASONS)[number]

export interface Consignment {
  id: string
  consignmentNumber: string
  customerName: string
  receiverName: string
  receiverSuburb: string
  receiverState: string
  receiverPostcode: string
  itemCount: number
  weightKg: number | string
  serviceType: string
  status: ConsignmentStatus
  createdAt: string
  version: number
}

export interface StatusHistoryEntry {
  id: string
  fromStatus: ConsignmentStatus
  toStatus: ConsignmentStatus
  changedAt: string
  signedBy: string | null
  deliveryTime: string | null
  failureReason: FailureReason | null
}

export interface ConsignmentDetail extends Consignment {
  history: StatusHistoryEntry[]
}

export interface PagedResult {
  items: Consignment[]
  total: number
  page: number
  pageSize: number
}

export interface StatusUpdate {
  status: ConsignmentStatus
  version: number
  signedBy: string | null
  deliveryTime: string | null
  failureReason: FailureReason | null
}

export interface ImportIssue {
  row: number
  tenantCode: string | null
  consignmentNumber: string | null
  reason: string
}

export interface ImportReport {
  totalRowsProcessed: number
  imported: number
  skipped: number
  rejected: number
  issues: ImportIssue[]
}

export class ApiRequestError extends Error {
  readonly status: number
  readonly code: string
  readonly fieldErrors: Record<string, string[]>
  readonly latest?: ConsignmentDetail

  constructor(
    message: string,
    options: {
      status: number
      code: string
      fieldErrors?: Record<string, string[]>
      latest?: ConsignmentDetail
    },
  ) {
    super(message)
    this.name = 'ApiRequestError'
    this.status = options.status
    this.code = options.code
    this.fieldErrors = options.fieldErrors ?? {}
    this.latest = options.latest
  }
}

interface RequestOptions {
  tenant?: string
  signal?: AbortSignal
  method?: 'GET' | 'PATCH'
  body?: unknown
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isConsignmentDetail(value: unknown): value is ConsignmentDetail {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.status === 'string' &&
    typeof value.version === 'number' &&
    Array.isArray(value.history)
  )
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {}
  }

  return Object.fromEntries(
    Object.entries(value).flatMap(([key, messages]) =>
      Array.isArray(messages) && messages.every((message) => typeof message === 'string')
        ? [[key, messages]]
        : [],
    ),
  )
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers({ Accept: 'application/json' })
  if (options.tenant) {
    headers.set('X-Tenant', options.tenant)
  }
  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })

  const responseText = await response.text()
  let payload: unknown
  if (responseText) {
    try {
      payload = JSON.parse(responseText)
    } catch {
      if (response.ok) {
        throw new Error('The API returned an invalid JSON response.')
      }
    }
  }

  if (!response.ok) {
    const body = isRecord(payload) ? payload : {}
    const latest = isConsignmentDetail(body.latest) ? body.latest : undefined
    throw new ApiRequestError(
      typeof body.message === 'string'
        ? body.message
        : `The request failed with HTTP ${response.status}.`,
      {
        status: response.status,
        code: typeof body.code === 'string' ? body.code : 'REQUEST_FAILED',
        fieldErrors: readFieldErrors(body.errors),
        latest,
      },
    )
  }

  if (payload === undefined) {
    throw new Error('The API returned an empty response.')
  }

  return payload as T
}

export function getConsignments(
  tenant: string,
  query: {
    status?: ConsignmentStatus
    search?: string
    page: number
    pageSize: number
    signal?: AbortSignal
  },
): Promise<PagedResult> {
  const parameters = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })
  if (query.status) {
    parameters.set('status', query.status)
  }
  if (query.search) {
    parameters.set('search', query.search)
  }

  return request(`/api/consignments?${parameters.toString()}`, {
    tenant,
    signal: query.signal,
  })
}

export function getConsignment(
  tenant: string,
  id: string,
  signal?: AbortSignal,
): Promise<ConsignmentDetail> {
  return request(`/api/consignments/${encodeURIComponent(id)}`, { tenant, signal })
}

export function updateConsignmentStatus(
  tenant: string,
  id: string,
  update: StatusUpdate,
): Promise<ConsignmentDetail> {
  return request(`/api/consignments/${encodeURIComponent(id)}/status`, {
    tenant,
    method: 'PATCH',
    body: update,
  })
}

export function getImportReport(signal?: AbortSignal): Promise<ImportReport> {
  return request('/api/imports/latest', { signal })
}
