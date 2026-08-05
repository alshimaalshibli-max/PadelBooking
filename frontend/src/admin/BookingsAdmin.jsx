import { useCallback, useEffect, useState } from 'react'
import { apiRequest, buildQuery } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import StatusBadge from '../components/StatusBadge'
import {
  formatArabicDate,
  formatCurrency,
  formatTime,
  paymentMethodLabels,
} from '../utils/format'

const initialFilters = {
  courtId: '',
  dateFrom: '',
  dateTo: '',
  bookingStatus: '',
  paymentStatus: '',
  paymentMethod: '',
  phone: '',
}

export default function BookingsAdmin({ token, onUnauthorized }) {
  const [filters, setFilters] = useState(initialFilters)
  const [appliedFilters, setAppliedFilters] = useState(initialFilters)
  const [page, setPage] = useState(1)
  const [data, setData] = useState({ items: [], totalCount: 0, totalPages: 0 })
  const [summary, setSummary] = useState(null)
  const [courts, setCourts] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const handleError = useCallback(
    (requestError) => {
      if (requestError.status === 401) onUnauthorized()
      setError(requestError.message)
    },
    [onUnauthorized],
  )

  const loadBookings = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const query = buildQuery({ ...appliedFilters, page, pageSize: 10 })
      const result = await apiRequest(`/bookings/search?${query}`, { token })
      setData(result)
    } catch (requestError) {
      handleError(requestError)
    } finally {
      setLoading(false)
    }
  }, [appliedFilters, handleError, page, token])

  const loadSummary = useCallback(async () => {
    try {
      const result = await apiRequest('/dashboard/summary', { token })
      setSummary(result)
    } catch (requestError) {
      handleError(requestError)
    }
  }, [handleError, token])

  useEffect(() => {
    apiRequest('/courts', { token })
      .then(setCourts)
      .catch(handleError)
  }, [handleError, token])

  useEffect(() => {
    loadBookings()
  }, [loadBookings])

  useEffect(() => {
    loadSummary()
  }, [loadSummary])

  const applyFilters = (event) => {
    event.preventDefault()
    setPage(1)
    setAppliedFilters(filters)
  }

  const clearFilters = () => {
    setFilters(initialFilters)
    setAppliedFilters(initialFilters)
    setPage(1)
  }

  const updateStatus = async (bookingId, action) => {
    setError('')
    try {
      await apiRequest(`/bookings/${bookingId}/${action}`, {
        method: 'PATCH',
        token,
      })
      await Promise.all([loadBookings(), loadSummary()])
    } catch (requestError) {
      handleError(requestError)
    }
  }

  const courtName = (courtId) =>
    courts.find((court) => court.id === courtId)?.name || `ملعب ${courtId}`

  return (
    <section className="admin-section">
      <div className="admin-section__heading">
        <div>
          <span className="eyebrow">إدارة الحجوزات</span>
          <h1>الحجوزات</h1>
          <p>ابحث، راجع حالة الدفع، ونفّذ إجراءات الحجز من شاشة واحدة.</p>
        </div>
        <div className="metric-card">
          <span>النتائج</span>
          <strong>{data.totalCount}</strong>
        </div>
      </div>

      <div className="dashboard-metrics" aria-label="ملخص الحجوزات">
        <article className="dashboard-stat card">
          <span>إجمالي الحجوزات</span>
          <strong>{summary?.totalBookings ?? '—'}</strong>
          <small>{summary ? `${summary.confirmedBookings} حجز مؤكد` : 'جارٍ التحديث'}</small>
        </article>
        <article className="dashboard-stat card">
          <span>حجوزات اليوم</span>
          <strong>{summary?.todayBookings ?? '—'}</strong>
          <small>{summary ? `${summary.completedBookings} حجز مكتمل` : 'جارٍ التحديث'}</small>
        </article>
        <article className="dashboard-stat card">
          <span>بانتظار الدفع</span>
          <strong>{summary?.pendingPayments ?? '—'}</strong>
          <small>{summary ? `${summary.cancelledBookings} حجز ملغي` : 'جارٍ التحديث'}</small>
        </article>
        <article className="dashboard-stat dashboard-stat--revenue card">
          <span>الإيراد المحصّل</span>
          <strong>{summary ? formatCurrency(summary.paidRevenue) : '—'}</strong>
          <small>{summary ? `${summary.paidBookings} حجز مدفوع` : 'جارٍ التحديث'}</small>
        </article>
      </div>

      <form className="filter-panel card" onSubmit={applyFilters}>
        <label className="field">
          <span>الملعب</span>
          <select
            value={filters.courtId}
            onChange={(event) => setFilters({ ...filters, courtId: event.target.value })}
          >
            <option value="">كل الملاعب</option>
            {courts.map((court) => (
              <option key={court.id} value={court.id}>{court.name}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>من تاريخ</span>
          <input
            type="date"
            value={filters.dateFrom}
            onChange={(event) => setFilters({ ...filters, dateFrom: event.target.value })}
          />
        </label>
        <label className="field">
          <span>إلى تاريخ</span>
          <input
            type="date"
            value={filters.dateTo}
            onChange={(event) => setFilters({ ...filters, dateTo: event.target.value })}
          />
        </label>
        <label className="field">
          <span>حالة الحجز</span>
          <select
            value={filters.bookingStatus}
            onChange={(event) => setFilters({ ...filters, bookingStatus: event.target.value })}
          >
            <option value="">الكل</option>
            <option value="Confirmed">مؤكد</option>
            <option value="Completed">مكتمل</option>
            <option value="Cancelled">ملغي</option>
          </select>
        </label>
        <label className="field">
          <span>حالة الدفع</span>
          <select
            value={filters.paymentStatus}
            onChange={(event) => setFilters({ ...filters, paymentStatus: event.target.value })}
          >
            <option value="">الكل</option>
            <option value="Pending">بانتظار الدفع</option>
            <option value="Paid">مدفوع</option>
            <option value="Failed">فشل</option>
          </select>
        </label>
        <label className="field">
          <span>طريقة الدفع</span>
          <select
            value={filters.paymentMethod}
            onChange={(event) => setFilters({ ...filters, paymentMethod: event.target.value })}
          >
            <option value="">الكل</option>
            <option value="Cash">عند الوصول</option>
            <option value="Thawani">ثواني</option>
            <option value="Card">بطاقة (قديم)</option>
          </select>
        </label>
        <label className="field">
          <span>رقم الهاتف</span>
          <input
            value={filters.phone}
            onChange={(event) => setFilters({ ...filters, phone: event.target.value })}
            inputMode="tel"
            placeholder="ابحث بالرقم"
          />
        </label>
        <div className="filter-panel__actions">
          <button className="button button--primary">تطبيق</button>
          <button type="button" className="button button--ghost" onClick={clearFilters}>مسح</button>
        </div>
      </form>

      <Feedback type="error">{error}</Feedback>
      {loading ? (
        <Loading label="جارٍ تحميل الحجوزات..." />
      ) : (
        <div className="table-card card">
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>الحجز</th>
                  <th>العميل</th>
                  <th>الموعد</th>
                  <th>الملعب</th>
                  <th>السعر</th>
                  <th>الدفع</th>
                  <th>الحالة</th>
                  <th>الإجراءات</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((booking) => (
                  <tr key={booking.id}>
                    <td><strong>#{booking.id}</strong></td>
                    <td>
                      <strong>{booking.customerName || 'بدون اسم'}</strong>
                      <small>{booking.phone}</small>
                    </td>
                    <td>
                      <strong>{formatArabicDate(booking.bookingDate)}</strong>
                      <small>{formatTime(booking.startTime)} · {booking.hours} س</small>
                    </td>
                    <td>{courtName(booking.courtId)}</td>
                    <td>{formatCurrency(booking.totalPrice)}</td>
                    <td>
                      <StatusBadge value={booking.paymentStatus} />
                      <small>{paymentMethodLabels[booking.paymentMethod] || booking.paymentMethod}</small>
                    </td>
                    <td><StatusBadge value={booking.bookingStatus} /></td>
                    <td>
                      <div className="row-actions">
                        {booking.paymentStatus !== 'Paid' && booking.bookingStatus !== 'Cancelled' && (
                          <button onClick={() => updateStatus(booking.id, 'pay')}>دفع</button>
                        )}
                        {booking.paymentStatus === 'Paid' && booking.bookingStatus === 'Confirmed' && (
                          <button onClick={() => updateStatus(booking.id, 'complete')}>إكمال</button>
                        )}
                        {booking.bookingStatus === 'Confirmed' && (
                          <button className="danger-link" onClick={() => updateStatus(booking.id, 'cancel')}>إلغاء</button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr><td colSpan="8"><div className="empty-state">لا توجد حجوزات مطابقة.</div></td></tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="pagination">
            <button
              className="button button--ghost button--small"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
            >
              السابق
            </button>
            <span>صفحة {page} من {data.totalPages || 1}</span>
            <button
              className="button button--ghost button--small"
              disabled={data.totalPages === 0 || page >= data.totalPages}
              onClick={() => setPage((current) => current + 1)}
            >
              التالي
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
