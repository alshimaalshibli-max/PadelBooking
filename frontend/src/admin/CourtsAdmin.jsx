import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import { formatCurrency, formatTime } from '../utils/format'

const emptyForm = {
  name: '',
  pricePerHour: '10',
  openingTime: '08:00',
  closingTime: '23:00',
  isActive: true,
}

export default function CourtsAdmin({ token, onUnauthorized }) {
  const [courts, setCourts] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const handleError = useCallback((requestError) => {
    if (requestError.status === 401) onUnauthorized()
    setError(requestError.message)
  }, [onUnauthorized])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setCourts(await apiRequest('/courts', { token }))
    } catch (requestError) {
      handleError(requestError)
    } finally {
      setLoading(false)
    }
  }, [handleError, token])

  useEffect(() => { load() }, [load])

  const submit = async (event) => {
    event.preventDefault()
    setError('')
    setSuccess('')
    const body = {
      ...form,
      pricePerHour: Number(form.pricePerHour),
      openingTime: `${form.openingTime}:00`,
      closingTime: `${form.closingTime}:00`,
    }
    try {
      await apiRequest(editingId ? `/courts/${editingId}` : '/courts', {
        method: editingId ? 'PUT' : 'POST',
        token,
        body,
      })
      setSuccess(editingId ? 'تم تحديث الملعب.' : 'تمت إضافة الملعب.')
      setEditingId(null)
      setForm(emptyForm)
      await load()
    } catch (requestError) {
      handleError(requestError)
    }
  }

  const edit = (court) => {
    setEditingId(court.id)
    setForm({
      name: court.name,
      pricePerHour: String(court.pricePerHour),
      openingTime: formatTime(court.openingTime),
      closingTime: formatTime(court.closingTime),
      isActive: court.isActive,
    })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const remove = async (court) => {
    if (!window.confirm(`هل تريد حذف ${court.name}؟`)) return
    setError('')
    try {
      await apiRequest(`/courts/${court.id}`, { method: 'DELETE', token })
      await load()
    } catch (requestError) {
      handleError(requestError)
    }
  }

  return (
    <section className="admin-section">
      <div className="admin-section__heading">
        <div><span className="eyebrow">إدارة الموارد</span><h1>الملاعب</h1><p>حدد ساعات العمل والسعر وحالة كل ملعب.</p></div>
        <div className="metric-card"><span>إجمالي الملاعب</span><strong>{courts.length}</strong></div>
      </div>

      <div className="admin-split">
        <form className="resource-form card" onSubmit={submit}>
          <h2>{editingId ? 'تعديل الملعب' : 'إضافة ملعب'}</h2>
          <label className="field"><span>اسم الملعب</span><input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} minLength="2" maxLength="100" required /></label>
          <label className="field"><span>سعر الساعة (ر.ع)</span><input type="number" step="0.001" min="0.001" value={form.pricePerHour} onChange={(event) => setForm({ ...form, pricePerHour: event.target.value })} required /></label>
          <div className="field-grid">
            <label className="field"><span>يفتح</span><input type="time" value={form.openingTime} onChange={(event) => setForm({ ...form, openingTime: event.target.value })} required /></label>
            <label className="field"><span>يغلق</span><input type="time" value={form.closingTime} onChange={(event) => setForm({ ...form, closingTime: event.target.value })} required /></label>
          </div>
          <label className="switch-field"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} /><span>الملعب نشط ومتاح للحجز</span></label>
          <Feedback type="error">{error}</Feedback><Feedback type="success">{success}</Feedback>
          <div className="form-actions"><button className="button button--primary">{editingId ? 'حفظ التعديل' : 'إضافة الملعب'}</button>{editingId && <button type="button" className="button button--ghost" onClick={() => { setEditingId(null); setForm(emptyForm) }}>إلغاء</button>}</div>
        </form>

        <div className="resource-list">
          {loading ? <Loading /> : courts.map((court) => (
            <article className="resource-card card" key={court.id}>
              <div className="resource-card__top"><div><span className={`dot ${court.isActive ? 'dot--active' : ''}`} /><strong>{court.name}</strong></div><span className="resource-price">{formatCurrency(court.pricePerHour)} / ساعة</span></div>
              <p>{formatTime(court.openingTime)} — {formatTime(court.closingTime)}</p>
              <div className="row-actions"><button onClick={() => edit(court)}>تعديل</button><button className="danger-link" onClick={() => remove(court)}>حذف</button></div>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
