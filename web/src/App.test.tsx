import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const consignment = {
  id: 'consignment-1',
  consignmentNumber: 'CN100020',
  customerName: 'Redgum Electrical',
  receiverName: 'Henry Harris',
  receiverSuburb: 'Penrith',
  receiverState: 'NSW',
  receiverPostcode: '2750',
  itemCount: 2,
  weightKg: 26.9,
  serviceType: 'standard',
  status: 'out_for_delivery',
  createdAt: '2026-11-10T00:37:00Z',
  version: 1,
}

const detail = { ...consignment, history: [] }

const latestDetail = {
  ...detail,
  status: 'delivered',
  version: 2,
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('dispatcher status updates', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('preserves a failed-delivery draft and shows the latest state after a conflict', async () => {
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
        const url = String(input)
        if (url.startsWith('/api/imports/latest')) {
          return jsonResponse({
            totalRowsProcessed: 68,
            imported: 63,
            skipped: 1,
            rejected: 4,
            issues: [],
          })
        }
        if (url.startsWith('/api/consignments?')) {
          return jsonResponse({
            items: [consignment],
            total: 1,
            page: 1,
            pageSize: 20,
          })
        }
        if (url === '/api/consignments/consignment-1') {
          return jsonResponse(detail)
        }
        if (url.endsWith('/status') && init?.method === 'PATCH') {
          return jsonResponse(
            {
              code: 'CONFLICT',
              message: 'This consignment changed after it was opened.',
              latest: latestDetail,
            },
            409,
          )
        }
        throw new Error(`Unexpected request: ${url}`)
      },
    )
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Open CN100020' }))
    fireEvent.change(await screen.findByLabelText('New status'), {
      target: { value: 'failed_delivery' },
    })
    fireEvent.change(await screen.findByLabelText('Failure reason'), {
      target: { value: 'no_one_home' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Update status' }))

    expect(await screen.findByText(/latest status 'delivered' at Version 2/i)).toBeTruthy()
    expect((screen.getByLabelText('New status') as HTMLSelectElement).value).toBe(
      'failed_delivery',
    )
    expect((screen.getByLabelText('Failure reason') as HTMLSelectElement).value).toBe(
      'no_one_home',
    )
    expect(
      screen.getByRole('button', { name: 'Submit draft with latest version' }),
    ).toBeTruthy()

    const updateCalls = fetchMock.mock.calls.filter(
      ([url, options]) => String(url).endsWith('/status') && options?.method === 'PATCH',
    )
    expect(updateCalls).toHaveLength(1)
    expect(new Headers(updateCalls[0][1]?.headers).get('X-Tenant')).toBe(
      'swiftline-freight',
    )
  })
})
