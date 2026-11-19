import { useState, type FormEvent } from 'react'
import {
  FAILURE_REASONS,
  type ConsignmentDetail,
  type ConsignmentStatus,
  type FailureReason,
  type StatusUpdate,
  ApiRequestError,
} from '../api'

interface Props {
  detail: ConsignmentDetail | null
  loading: boolean
  error: string
  updateError: ApiRequestError | null
  conflict: boolean
  saving: boolean
  formResetKey: number
  onSubmit: (update: Omit<StatusUpdate, 'version'>) => Promise<void>
}

const ALLOWED_NEXT: Partial<Record<ConsignmentStatus, readonly ConsignmentStatus[]>> = {
  booked: ['picked_up', 'cancelled'],
  picked_up: ['in_transit'],
  in_transit: ['out_for_delivery'],
  out_for_delivery: ['delivered', 'failed_delivery'],
  failed_delivery: ['out_for_delivery', 'returned'],
}

function statusLabel(status: string): string {
  return status.replaceAll('_', ' ')
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString()
}

function DetailFields({ detail }: { detail: ConsignmentDetail }) {
  return (
    <dl className="detail-grid">
      <div>
        <dt>Receiver</dt>
        <dd>{detail.receiverName}</dd>
      </div>
      <div>
        <dt>Delivery address</dt>
        <dd>
          {detail.receiverSuburb}, {detail.receiverState} {detail.receiverPostcode}
        </dd>
      </div>
      <div>
        <dt>Customer</dt>
        <dd>{detail.customerName}</dd>
      </div>
      <div>
        <dt>Service</dt>
        <dd>{detail.serviceType}</dd>
      </div>
      <div>
        <dt>Items</dt>
        <dd>{detail.itemCount}</dd>
      </div>
      <div>
        <dt>Weight</dt>
        <dd>{detail.weightKg} kg</dd>
      </div>
      <div>
        <dt>Created</dt>
        <dd>{formatDateTime(detail.createdAt)}</dd>
      </div>
    </dl>
  )
}

function StatusUpdateForm({
  detail,
  updateError,
  conflict,
  saving,
  onSubmit,
}: {
  detail: ConsignmentDetail
  updateError: ApiRequestError | null
  conflict: boolean
  saving: boolean
  onSubmit: Props['onSubmit']
}) {
  const [status, setStatus] = useState<ConsignmentStatus | ''>('')
  const [signedBy, setSignedBy] = useState('')
  const [deliveryTime, setDeliveryTime] = useState('')
  const [failureReason, setFailureReason] = useState<FailureReason | ''>('')

  const allowed = ALLOWED_NEXT[detail.status] ?? []
  const options = status && !allowed.includes(status) ? [...allowed, status] : allowed

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!status) {
      return
    }

    await onSubmit({
      status,
      signedBy: status === 'delivered' ? signedBy : null,
      deliveryTime: status === 'delivered' ? deliveryTime : null,
      failureReason: status === 'failed_delivery' ? failureReason || null : null,
    })
  }

  return (
    <form className="status-form" onSubmit={(event) => void handleSubmit(event)}>
      <h3>Update status</h3>
      {updateError && (
        <div className="notice error-notice" role="alert">
          <strong>{updateError.message}</strong>
          {Object.entries(updateError.fieldErrors).map(([field, messages]) => (
            <div key={field}>
              {messages.map((message) => (
                <p key={message}>
                  <span className="field-name">{field}:</span> {message}
                </p>
              ))}
            </div>
          ))}
        </div>
      )}
      <label htmlFor="target-status">New status</label>
      <select
        id="target-status"
        required
        value={status}
        onChange={(event) => setStatus(event.target.value as ConsignmentStatus | '')}
      >
        <option value="">Choose an allowed next status</option>
        {options.map((value) => (
          <option key={value} value={value}>
            {statusLabel(value)}
            {!allowed.includes(value) ? ' (draft; review latest status)' : ''}
          </option>
        ))}
      </select>

      {status === 'delivered' && (
        <div className="outcome-fields">
          <label htmlFor="signed-by">Signed by</label>
          <input
            id="signed-by"
            value={signedBy}
            onChange={(event) => setSignedBy(event.target.value)}
            autoComplete="name"
            required
          />
          <label htmlFor="delivery-time">Delivery time</label>
          <input
            id="delivery-time"
            type="datetime-local"
            value={deliveryTime}
            onChange={(event) => setDeliveryTime(event.target.value)}
            required
          />
        </div>
      )}

      {status === 'failed_delivery' && (
        <div className="outcome-fields">
          <label htmlFor="failure-reason">Failure reason</label>
          <select
            id="failure-reason"
            required
            value={failureReason}
            onChange={(event) => setFailureReason(event.target.value as FailureReason | '')}
          >
            <option value="">Choose a reason</option>
            {FAILURE_REASONS.map((reason) => (
              <option key={reason} value={reason}>
                {reason.replaceAll('_', ' ')}
              </option>
            ))}
          </select>
        </div>
      )}

      <button type="submit" disabled={saving || !status}>
        {saving
          ? 'Saving...'
          : conflict
            ? 'Submit draft with latest version'
            : 'Update status'}
      </button>
    </form>
  )
}

export function ConsignmentDetailPanel({
  detail,
  loading,
  error,
  updateError,
  conflict,
  saving,
  formResetKey,
  onSubmit,
}: Props) {
  return (
    <section className="panel detail-panel" aria-labelledby="detail-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Selected consignment</p>
          <h2 id="detail-heading">Details &amp; history</h2>
        </div>
      </div>

      {loading && <p className="state-message">Loading consignment...</p>}
      {error && (
        <p className="notice error-notice" role="alert">
          Could not load consignment: {error}
        </p>
      )}
      {!loading && !error && !detail && (
        <p className="state-message">Select a consignment to view its details.</p>
      )}
      {!loading && detail && (
        <>
          <div className="detail-title">
            <div>
              <h3>{detail.consignmentNumber}</h3>
              <span className={`status-pill status-${detail.status}`}>
                {statusLabel(detail.status)}
              </span>
            </div>
            <span className="version-label">Version {detail.version}</span>
          </div>

          {conflict && (
            <div className="notice conflict-notice" role="alert">
              <strong>Update conflict</strong>
              <p>
                {`The server has the latest status '${statusLabel(detail.status)}' at Version ${detail.version}.`}
              </p>
              <p>
                Your draft is preserved. Review the latest state before choosing to submit it
                with this version.
              </p>
            </div>
          )}

          <DetailFields detail={detail} />

          <section className="history-section" aria-labelledby="history-heading">
            <div className="subsection-heading">
              <h3 id="history-heading">Status history</h3>
              <span>Oldest first</span>
            </div>
            {detail.history.length === 0 ? (
              <p className="muted-copy">No status changes recorded.</p>
            ) : (
              <ol className="history-list">
                {detail.history.map((entry) => (
                  <li key={entry.id}>
                    <div className="history-transition">
                      <strong>{statusLabel(entry.toStatus)}</strong>
                      <span>from {statusLabel(entry.fromStatus)}</span>
                    </div>
                    <time dateTime={entry.changedAt}>{formatDateTime(entry.changedAt)}</time>
                    {entry.signedBy && <p>Signed by {entry.signedBy}</p>}
                    {entry.deliveryTime && (
                      <p>Delivered {formatDateTime(entry.deliveryTime)}</p>
                    )}
                    {entry.failureReason && (
                      <p>Reason: {entry.failureReason.replaceAll('_', ' ')}</p>
                    )}
                  </li>
                ))}
              </ol>
            )}
          </section>

          <StatusUpdateForm
            key={`${detail.id}-${formResetKey}`}
            detail={detail}
            updateError={updateError}
            conflict={conflict}
            saving={saving}
            onSubmit={onSubmit}
          />
        </>
      )}
    </section>
  )
}
