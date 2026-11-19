import type { ImportReport } from '../api'

interface Props {
  report: ImportReport | null
  error: string
}

export function ImportReportPanel({ report, error }: Props) {
  return (
    <details className="import-report">
      <summary>
        {report
          ? `Startup import: ${report.imported} imported - ${report.skipped} skipped - ${report.rejected} rejected`
          : 'Startup import report'}
      </summary>
      {error && <p className="notice error-notice">{error}</p>}
      {report && (
        <div className="import-report-content">
          <p>
            {report.totalRowsProcessed} processed - {report.imported} imported - {report.skipped}{' '}
            skipped - {report.rejected} rejected
          </p>
          {report.issues.length > 0 && (
            <ul>
              {report.issues.map((issue, index) => (
                <li key={`${issue.row}-${issue.consignmentNumber ?? 'unknown'}-${index}`}>
                  Row {issue.row}
                  {issue.tenantCode ? ` - ${issue.tenantCode}` : ''}
                  {issue.consignmentNumber ? ` - ${issue.consignmentNumber}` : ''}: {issue.reason}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {!report && !error && <p>Loading import report...</p>}
    </details>
  )
}
