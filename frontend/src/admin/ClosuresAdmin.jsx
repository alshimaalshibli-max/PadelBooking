import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import { formatArabicDate, toInputDate } from '../utils/format'

const weekdays = [
  { value: 6, label: 'السبت' },
  { value: 0, label: 'الأحد' },
  { value: 1, label: 'الاثنين' },
  { value: 2, label: 'الثلاثاء' },
  { value: 3, label: 'الأربعاء' },
  { value: 4, label: 'الخميس' },
  { value: 5, label: 'الجمعة' },
]

export default function ClosuresAdmin({ token, onUnauthorized }) {
  const today = useMemo(() => toInputDate(), [])
  const [closures, setClosures] = useState([])
  const [courts, setCourts] = useState([])
  const [form, setForm] = useState({
    isGeneral: true,
    courtIds: [],
    startDate: today,
    endDate: today,
    daysOfWeek: [],
    reason: '',
  })
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const handleError = useCallback((requestError) => {
    if (requestError.status === 401) onUnauthorized()
    setError(requestError.message)
  }, [onUnauthorized])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [closureResult, courtResult] = await Promise.all([
        apiRequest('/closures', { token }),
        apiRequest('/courts', { token }),
      ])
      setClosures(closureResult)
      setCourts(courtResult)
    } catch (requestError) {
      handleError(requestError)
    } finally {
      setLoading(false)
    }
  }, [handleError, token])

  useEffect(() => { load() }, [load])

  const toggleCourt = (courtId) => {
    setForm((current) => ({
      ...current,
      courtIds: current.courtIds.includes(courtId)
        ? current.courtIds.filter((id) => id !== courtId)
        : [...current.courtIds, courtId],
    }))
  }

  const toggleDay = (day) => {
    setForm((current) => ({
      ...current,
      daysOfWeek: current.daysOfWeek.includes(day)
        ? current.daysOfWeek.filter((value) => value !== day)
        : [...current.daysOfWeek, day],
    }))
  }

  const submit = async (event) => {
    event.preventDefault(); setError(''); setSuccess('')
    if (!form.isGeneral && form.courtIds.length === 0) {
      setError('اختر ملعبًا واحدًا على الأقل أو فعّل الإغلاق العام.')
      return
    }
    setSubmitting(true)
    try {
      const result = await apiRequest('/closures/batch', {
        method: 'POST', token,
        body: {
          courtIds: form.isGeneral ? null : form.courtIds,
          startDate: form.startDate,
          endDate: form.endDate,
          daysOfWeek: form.daysOfWeek.length ? form.daysOfWeek : null,
          reason: form.reason,
        },
      })
      setSuccess(`تم إنشاء ${result.count} إغلاق بنجاح.`)
      setForm((current) => ({ ...current, reason: '', daysOfWeek: [] }))
      await load()
    } catch (requestError) { handleError(requestError) }
    finally { setSubmitting(false) }
  }

  const remove = async (closure) => {
    if (!window.confirm('هل تريد حذف هذا الإغلاق؟')) return
    try { await apiRequest(`/closures/${closure.id}`, { method: 'DELETE', token }); await load() }
    catch (requestError) { handleError(requestError) }
  }

  return (
    <section className="admin-section">
      <div className="admin-section__heading"><div><span className="eyebrow">إدارة التوفر</span><h1>الإغلاقات</h1><p>أغلق ملعبًا أو عدة ملاعب أو جميعها عبر نطاق زمني.</p></div><div className="metric-card"><span>الإغلاقات المسجلة</span><strong>{closures.length}</strong></div></div>
      <div className="admin-split admin-split--wide-form">
        <form className="resource-form card" onSubmit={submit}>
          <h2>إغلاق جديد</h2>
          <div className="segmented">
            <button type="button" className={form.isGeneral ? 'active' : ''} onClick={() => setForm({ ...form, isGeneral: true, courtIds: [] })}>كل الملاعب</button>
            <button type="button" className={!form.isGeneral ? 'active' : ''} onClick={() => setForm({ ...form, isGeneral: false })}>ملاعب محددة</button>
          </div>
          {!form.isGeneral && (
            <div className="check-grid">
              {courts.map((court) => (
                <label key={court.id}><input type="checkbox" checked={form.courtIds.includes(court.id)} onChange={() => toggleCourt(court.id)} /><span>{court.name}</span></label>
              ))}
            </div>
          )}
          <div className="field-grid">
            <label className="field"><span>من تاريخ</span><input type="date" min={today} value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value, endDate: event.target.value > form.endDate ? event.target.value : form.endDate })} required /></label>
            <label className="field"><span>إلى تاريخ</span><input type="date" min={form.startDate} value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} required /></label>
          </div>
          <div className="field"><span>أيام محددة <small>اتركها فارغة لتطبيق كل الأيام</small></span><div className="weekday-grid">{weekdays.map((day) => <button type="button" key={day.value} className={form.daysOfWeek.includes(day.value) ? 'active' : ''} onClick={() => toggleDay(day.value)}>{day.label}</button>)}</div></div>
          <label className="field"><span>سبب الإغلاق</span><textarea rows="3" minLength="2" maxLength="500" value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} required /></label>
          <Feedback type="error">{error}</Feedback><Feedback type="success">{success}</Feedback>
          <button className="button button--primary" disabled={submitting}>{submitting ? 'جارٍ الإنشاء...' : 'إنشاء الإغلاقات'}</button>
        </form>

        <div className="resource-list closure-list">
          {loading ? <Loading /> : closures.map((closure) => (
            <article className="resource-card card" key={closure.id}>
              <div className="resource-card__top"><div><span className="closure-icon">×</span><strong>{closure.courtName || 'إغلاق عام'}</strong></div><button type="button" className="danger-link closure-delete" onClick={() => remove(closure)}>حذف</button></div>
              <p>{formatArabicDate(closure.date)}</p><small>{closure.reason}</small>
            </article>
          ))}
          {!loading && closures.length === 0 && <div className="empty-state card">لا توجد إغلاقات مسجلة.</div>}
        </div>
      </div>
    </section>
  )
}
