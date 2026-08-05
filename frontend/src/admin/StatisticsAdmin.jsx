import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import { formatArabicDate, formatCurrency, formatTime } from '../utils/format'

const statusColors = {
  confirmed: '#1f8a64',
  completed: '#0b3f34',
  cancelled: '#c75562',
  pending: '#e8b949',
}

function shortDate(value) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('ar-OM', {
    day: 'numeric',
    month: 'short',
  }).format(new Date(`${value.slice(0, 10)}T12:00:00`))
}

function monthLabel(value) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('ar-OM', {
    month: 'short',
    year: '2-digit',
  }).format(new Date(`${value}-01T12:00:00`))
}

function formatPercentage(value) {
  return `${new Intl.NumberFormat('ar-OM', { maximumFractionDigits: 1 }).format(Number(value || 0))}٪`
}

function changeLabel(percentage) {
  if (percentage === null || percentage === undefined) return 'لا توجد بيانات للشهر الماضي'
  if (Number(percentage) === 0) return 'دون تغيير عن الشهر الماضي'
  return `${Number(percentage) > 0 ? '↑' : '↓'} ${formatPercentage(Math.abs(percentage))} عن الشهر الماضي`
}

function MetricCard({ label, value, detail, tone = '' }) {
  return (
    <article className={`statistics-metric card ${tone ? `statistics-metric--${tone}` : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  )
}

function BarChart({ items, valueKey, labelFormatter, valueFormatter, dense = false }) {
  const maxValue = Math.max(1, ...items.map((item) => Number(item[valueKey] || 0)))

  return (
    <div className="chart-scroll">
      <div className={`bar-chart ${dense ? 'bar-chart--dense' : ''}`}>
        {items.map((item) => {
          const value = Number(item[valueKey] || 0)
          const height = value === 0 ? 2 : Math.max(8, (value / maxValue) * 100)
          return (
            <div className="bar-chart__item" key={item.date || item.month} title={valueFormatter(value)}>
              <span className="bar-chart__value">{valueFormatter(value)}</span>
              <div className="bar-chart__track"><i style={{ height: `${height}%` }} /></div>
              <small>{labelFormatter(item.date || item.month)}</small>
            </div>
          )
        })}
      </div>
    </div>
  )
}

function StatusDonut({ statuses }) {
  const entries = [
    { key: 'confirmed', label: 'مؤكدة', value: statuses.confirmed },
    { key: 'completed', label: 'مكتملة', value: statuses.completed },
    { key: 'cancelled', label: 'ملغاة', value: statuses.cancelled },
    { key: 'pending', label: 'قيد الانتظار', value: statuses.pending },
  ]
  const total = entries.reduce((sum, item) => sum + Number(item.value || 0), 0)
  let cursor = 0
  const stops = entries.map((item) => {
    const start = cursor
    cursor += total === 0 ? 0 : (Number(item.value || 0) / total) * 100
    return `${statusColors[item.key]} ${start}% ${cursor}%`
  })
  const background = total === 0
    ? '#e5ebe8'
    : `conic-gradient(${stops.join(', ')})`

  return (
    <div className="donut-layout">
      <div className="donut-chart" style={{ background }} aria-label={`إجمالي الحالات ${total}`}>
        <div><strong>{total}</strong><span>إجمالي الحجوزات</span></div>
      </div>
      <div className="donut-legend">
        {entries.map((item) => (
          <div key={item.key}>
            <i style={{ background: statusColors[item.key] }} />
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </div>
    </div>
  )
}

export default function StatisticsAdmin({ token, onUnauthorized }) {
  const [rangeDays, setRangeDays] = useState(7)
  const [statistics, setStatistics] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadStatistics = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setStatistics(await apiRequest(`/dashboard/statistics?days=${rangeDays}`, { token }))
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      setError(requestError.message)
    } finally {
      setLoading(false)
    }
  }, [onUnauthorized, rangeDays, token])

  useEffect(() => {
    loadStatistics()
  }, [loadStatistics])

  const metrics = useMemo(() => {
    if (!statistics) return []
    return [
      {
        label: 'أكثر يوم ازدحامًا',
        value: formatArabicDate(statistics.busiestDate),
        detail: `${statistics.busiestDateBookings} حجز في اليوم`,
      },
      {
        label: 'أكثر وقت يتم اختياره',
        value: formatTime(statistics.mostPopularStartTime),
        detail: `${statistics.mostPopularStartTimeBookings} حجز يبدأ في هذا الوقت`,
      },
      {
        label: 'متوسط قيمة الحجز',
        value: formatCurrency(statistics.averageBookingValue),
        detail: 'للحجوزات غير الملغاة',
      },
      {
        label: 'أكثر ملعب تم حجزه',
        value: statistics.mostBookedCourtName || 'لا توجد بيانات',
        detail: `${statistics.mostBookedCourtBookings} حجز`,
      },
      {
        label: 'نسبة إشغال الملاعب',
        value: formatPercentage(statistics.occupancyRate),
        detail: 'من الساعات المتاحة هذا الشهر',
      },
      {
        label: 'حجوزات هذا الشهر',
        value: statistics.currentMonthBookings,
        detail: `${statistics.previousMonthBookings} الشهر الماضي · ${changeLabel(statistics.bookingsChangePercentage)}`,
      },
      {
        label: 'إجمالي الإيرادات',
        value: formatCurrency(statistics.totalRevenue),
        detail: 'إجمالي الحجوزات المدفوعة',
        tone: 'dark',
      },
      {
        label: 'إيرادات هذا الشهر',
        value: formatCurrency(statistics.currentMonthRevenue),
        detail: `${formatCurrency(statistics.previousMonthRevenue)} الشهر الماضي`,
        tone: 'accent',
      },
      {
        label: 'الإيرادات مقارنة بالشهر الماضي',
        value: formatCurrency(statistics.revenueDifference),
        detail: changeLabel(statistics.revenueChangePercentage),
      },
      {
        label: 'عدد الملاعب',
        value: statistics.totalCourts,
        detail: `${statistics.activeCourts} ملعب نشط`,
      },
      {
        label: 'عدد المستخدمين',
        value: statistics.uniqueCustomers,
        detail: 'بحسب أرقام الهاتف الفريدة',
      },
    ]
  }, [statistics])

  return (
    <section className="admin-section statistics-section">
      <div className="admin-section__heading statistics-heading">
        <div>
          <span className="eyebrow">تحليلات الأداء</span>
          <h1>الإحصائيات</h1>
          <p>نظرة موحدة على الحجوزات والإشغال والإيرادات واتجاهات العملاء.</p>
        </div>
        <button className="button button--ghost button--small" onClick={loadStatistics} disabled={loading}>
          تحديث البيانات
        </button>
      </div>

      <Feedback type="error">{error}</Feedback>
      {loading && !statistics ? (
        <Loading label="جارٍ تجهيز الإحصائيات..." />
      ) : statistics && (
        <>
          <div className="statistics-grid">
            {metrics.map((metric) => <MetricCard key={metric.label} {...metric} />)}
          </div>

          <div className="analytics-grid">
            <article className="chart-card chart-card--wide card">
              <div className="chart-card__heading">
                <div><span>اتجاه الحجوزات</span><h2>عدد الحجوزات اليومية</h2></div>
                <div className="range-toggle" aria-label="نطاق الرسم اليومي">
                  {[7, 30].map((days) => (
                    <button
                      key={days}
                      className={rangeDays === days ? 'active' : ''}
                      onClick={() => setRangeDays(days)}
                    >
                      {days} أيام
                    </button>
                  ))}
                </div>
              </div>
              <BarChart
                items={statistics.dailyBookings}
                valueKey="count"
                labelFormatter={shortDate}
                valueFormatter={(value) => `${value}`}
                dense={rangeDays === 30}
              />
            </article>

            <article className="chart-card card">
              <div className="chart-card__heading">
                <div><span>آخر 6 أشهر</span><h2>الإيرادات الشهرية</h2></div>
              </div>
              <BarChart
                items={statistics.monthlyRevenue}
                valueKey="revenue"
                labelFormatter={monthLabel}
                valueFormatter={formatCurrency}
              />
            </article>

            <article className="chart-card card">
              <div className="chart-card__heading">
                <div><span>توزيع الحالات</span><h2>حالات الحجوزات</h2></div>
              </div>
              <StatusDonut statuses={statistics.bookingStatuses} />
            </article>
          </div>
        </>
      )}
    </section>
  )
}
