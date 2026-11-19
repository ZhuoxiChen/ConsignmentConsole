import { useEffect, useRef, useState } from 'react'
import {
  ApiRequestError,
  DEMO_TENANTS,
  getConsignment,
  getConsignments,
  getImportReport,
  updateConsignmentStatus,
  type ConsignmentDetail,
  type ConsignmentStatus,
  type ImportReport,
  type PagedResult,
  type StatusUpdate,
} from './api'
import { ConsignmentDetailPanel } from './components/ConsignmentDetailPanel'
import { ConsignmentList } from './components/ConsignmentList'
import { ImportReportPanel } from './components/ImportReportPanel'
import './App.css'

const PAGE_SIZE = 20

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'An unexpected error occurred.'
}

function App() {
  const [tenant, setTenant] = useState<string>(DEMO_TENANTS[0].code)
  const [statusFilter, setStatusFilter] = useState<ConsignmentStatus | ''>('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [page, setPage] = useState(1)
  const [list, setList] = useState<PagedResult | null>(null)
  const [listLoading, setListLoading] = useState(true)
  const [listError, setListError] = useState('')
  const [listRevision, setListRevision] = useState(0)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<ConsignmentDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState('')
  const [updateError, setUpdateError] = useState<ApiRequestError | null>(null)
  const [conflict, setConflict] = useState(false)
  const [saving, setSaving] = useState(false)
  const [formResetKey, setFormResetKey] = useState(0)
  const updateGeneration = useRef(0)

  const [importReport, setImportReport] = useState<ImportReport | null>(null)
  const [importError, setImportError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    void getConsignments(tenant, {
      status: statusFilter || undefined,
      search: search || undefined,
      page,
      pageSize: PAGE_SIZE,
      signal: controller.signal,
    })
      .then(setList)
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setListError(errorMessage(error))
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setListLoading(false)
        }
      })

    return () => controller.abort()
  }, [tenant, statusFilter, search, page, listRevision])

  useEffect(() => {
    if (!selectedId) {
      return
    }

    const controller = new AbortController()

    void getConsignment(tenant, selectedId, controller.signal)
      .then(setDetail)
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setDetailError(errorMessage(error))
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setDetailLoading(false)
        }
      })

    return () => controller.abort()
  }, [tenant, selectedId])

  useEffect(() => {
    const controller = new AbortController()

    void getImportReport(controller.signal)
      .then(setImportReport)
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setImportError(errorMessage(error))
        }
      })

    return () => controller.abort()
  }, [])

  function changeTenant(nextTenant: string) {
    updateGeneration.current += 1
    setSaving(false)
    setList(null)
    setListLoading(true)
    setListError('')
    setTenant(nextTenant)
    setStatusFilter('')
    setSearch('')
    setSearchInput('')
    setPage(1)
    setSelectedId(null)
    setDetail(null)
    setDetailLoading(false)
    setDetailError('')
    setUpdateError(null)
    setConflict(false)
    setFormResetKey((current) => current + 1)
  }

  function prepareListReload() {
    setList(null)
    setListLoading(true)
    setListError('')
  }

  function selectConsignment(id: string) {
    if (selectedId === id) {
      if (saving) {
        return
      }
      setUpdateError(null)
      setConflict(false)
      setFormResetKey((current) => current + 1)
      return
    }

    updateGeneration.current += 1
    setSaving(false)
    setSelectedId(id)
    setDetail(null)
    setDetailLoading(true)
    setDetailError('')
    setUpdateError(null)
    setConflict(false)
    setFormResetKey((current) => current + 1)
  }

  async function submitStatusUpdate(update: Omit<StatusUpdate, 'version'>) {
    if (!detail) {
      return
    }

    const generation = ++updateGeneration.current
    setSaving(true)
    setUpdateError(null)

    try {
      const request: StatusUpdate = {
        ...update,
        version: detail.version,
      }
      if (update.status === 'delivered' && update.deliveryTime) {
        request.deliveryTime = new Date(update.deliveryTime).toISOString()
      }
      const updated = await updateConsignmentStatus(tenant, detail.id, request)
      if (generation !== updateGeneration.current) {
        return
      }
      setDetail(updated)
      setConflict(false)
      setFormResetKey((current) => current + 1)
      prepareListReload()
      setListRevision((current) => current + 1)
    } catch (error) {
      if (generation !== updateGeneration.current) {
        return
      }
      if (error instanceof ApiRequestError) {
        if (error.status === 409 && error.latest) {
          setDetail(error.latest)
          setConflict(true)
          prepareListReload()
          setListRevision((current) => current + 1)
        } else {
          setUpdateError(error)
        }
      } else {
        setUpdateError(
          new ApiRequestError(errorMessage(error), {
            status: 0,
            code: 'REQUEST_FAILED',
          }),
        )
      }
    } finally {
      if (generation === updateGeneration.current) {
        setSaving(false)
      }
    }
  }

  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Consignment operations</p>
          <h1>Dispatcher console</h1>
        </div>
        <label className="tenant-control">
          <span>Carrier</span>
          <select
            aria-label="Carrier"
            value={tenant}
            onChange={(event) => changeTenant(event.target.value)}
          >
            {DEMO_TENANTS.map((option) => (
              <option key={option.code} value={option.code}>
                {option.name}
              </option>
            ))}
          </select>
        </label>
      </header>

      <div className="workspace">
        <ConsignmentList
          items={list?.items ?? []}
          total={list?.total ?? 0}
          page={page}
          pageSize={PAGE_SIZE}
          status={statusFilter}
          searchInput={searchInput}
          loading={listLoading}
          error={listError}
          selectedId={selectedId}
          onStatusChange={(value) => {
            if (value === statusFilter && page === 1) {
              return
            }
            prepareListReload()
            setStatusFilter(value)
            setPage(1)
          }}
          onSearchInputChange={setSearchInput}
          onSearchChange={(value) => {
            const normalized = value.trim()
            if (normalized === search && page === 1) {
              return
            }
            prepareListReload()
            setSearch(normalized)
            setPage(1)
          }}
          onPageChange={(nextPage) => {
            prepareListReload()
            setPage(nextPage)
          }}
          onSelect={selectConsignment}
        />
        <ConsignmentDetailPanel
          detail={detail}
          loading={detailLoading}
          error={detailError}
          updateError={updateError}
          conflict={conflict}
          saving={saving}
          formResetKey={formResetKey}
          onSubmit={submitStatusUpdate}
        />
      </div>

      <ImportReportPanel report={importReport} error={importError} />
    </main>
  )
}

export default App
