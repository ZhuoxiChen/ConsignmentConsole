import type { FormEvent } from 'react'
import {
  CONSIGNMENT_STATUSES,
  type Consignment,
  type ConsignmentStatus,
} from '../api'

interface Props {
  items: Consignment[]
  total: number
  page: number
  pageSize: number
  status: ConsignmentStatus | ''
  searchInput: string
  loading: boolean
  error: string
  selectedId: string | null
  onStatusChange: (status: ConsignmentStatus | '') => void
  onSearchInputChange: (search: string) => void
  onSearchChange: (search: string) => void
  onPageChange: (page: number) => void
  onSelect: (id: string) => void
}

function statusLabel(status: string): string {
  return status.replaceAll('_', ' ')
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString()
}

export function ConsignmentList({
  items,
  total,
  page,
  pageSize,
  status,
  searchInput,
  loading,
  error,
  selectedId,
  onStatusChange,
  onSearchInputChange,
  onSearchChange,
  onPageChange,
  onSelect,
}: Props) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSearchChange(searchInput.trim())
  }

  return (
    <section className="panel list-panel" aria-labelledby="list-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Operations</p>
          <h2 id="list-heading">Consignments</h2>
        </div>
        <span className="result-count">{loading ? 'Loading...' : `${total} total`}</span>
      </div>

      <div className="list-controls">
        <form className="search-form" onSubmit={submitSearch}>
          <label htmlFor="consignment-search">Search number or receiver</label>
          <div className="search-row">
            <input
              id="consignment-search"
              type="search"
              value={searchInput}
              onChange={(event) => onSearchInputChange(event.target.value)}
              placeholder="e.g. CN100020 or Henry"
            />
            <button type="submit">Search</button>
          </div>
        </form>
        <label className="filter-control" htmlFor="status-filter">
          Status
          <select
            id="status-filter"
            value={status}
            onChange={(event) =>
              onStatusChange(event.target.value as ConsignmentStatus | '')
            }
          >
            <option value="">All statuses</option>
            {CONSIGNMENT_STATUSES.map((value) => (
              <option key={value} value={value}>
                {statusLabel(value)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && (
        <p className="notice error-notice" role="alert">
          Could not load consignments: {error}
        </p>
      )}
      {!error && loading && <p className="state-message">Loading consignments...</p>}
      {!error && !loading && items.length === 0 && (
        <p className="state-message">No consignments match these filters.</p>
      )}
      {!error && !loading && items.length > 0 && (
        <>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Consignment</th>
                  <th scope="col">Receiver</th>
                  <th scope="col">Status</th>
                  <th scope="col">Service</th>
                  <th scope="col">Items / weight</th>
                  <th scope="col">Created</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={item.id}
                    className={selectedId === item.id ? 'selected-row' : undefined}
                  >
                    <td>
                      <button
                        type="button"
                        className="text-button consignment-link"
                        aria-label={`Open ${item.consignmentNumber}`}
                        onClick={() => onSelect(item.id)}
                      >
                        {item.consignmentNumber}
                      </button>
                      <span className="secondary-line">
                        {item.receiverSuburb}, {item.receiverState}
                      </span>
                    </td>
                    <td>{item.receiverName}</td>
                    <td>
                      <span className={`status-pill status-${item.status}`}>
                        {statusLabel(item.status)}
                      </span>
                    </td>
                    <td>{item.serviceType}</td>
                    <td>
                      {item.itemCount} / {item.weightKg} kg
                    </td>
                    <td>{formatDate(item.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label="Consignment pages">
            <span>
              Page {page} of {pageCount}
            </span>
            <div>
              <button
                type="button"
                className="secondary-button"
                disabled={page <= 1}
                onClick={() => onPageChange(page - 1)}
              >
                Previous
              </button>
              <button
                type="button"
                className="secondary-button"
                disabled={page >= pageCount}
                onClick={() => onPageChange(page + 1)}
              >
                Next
              </button>
            </div>
          </nav>
        </>
      )}
    </section>
  )
}
