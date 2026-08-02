import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import { formatCurrency } from '../utils/format'

const emptyForm = { minimumHours: 2, pricePerHour: 8, isActive: true }

export default function OffersAdmin({ token, onUnauthorized }) {
  const [offers, setOffers] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const handleError = useCallback((requestError) => {
    if (requestError.status === 401) onUnauthorized()
    setError(requestError.message)
  }, [onUnauthorized])

  const load = useCallback(async () => {
    setLoading(true)
    try { setOffers(await apiRequest('/offers', { token })) }
    catch (requestError) { handleError(requestError) }
    finally { setLoading(false) }
  }, [handleError, token])

  useEffect(() => { load() }, [load])

  const submit = async (event) => {
    event.preventDefault(); setError('')
    try {
      await apiRequest(editingId ? `/offers/${editingId}` : '/offers', {
        method: editingId ? 'PUT' : 'POST', token,
        body: { minimumHours: Number(form.minimumHours), pricePerHour: Number(form.pricePerHour), isActive: form.isActive },
      })
      setEditingId(null); setForm(emptyForm); await load()
    } catch (requestError) { handleError(requestError) }
  }

  const remove = async (offer) => {
    if (!window.confirm('هل تريد حذف هذا العرض؟')) return
    try { await apiRequest(`/offers/${offer.id}`, { method: 'DELETE', token }); await load() }
    catch (requestError) { handleError(requestError) }
  }

  return (
    <section className="admin-section">
      <div className="admin-section__heading"><div><span className="eyebrow">التسعير المرن</span><h1>العروض</h1><p>كلما زادت مدة الحجز، طبّق سعرًا أفضل تلقائيًا.</p></div><div className="metric-card"><span>العروض</span><strong>{offers.length}</strong></div></div>
      <div className="admin-split">
        <form className="resource-form card" onSubmit={submit}>
          <h2>{editingId ? 'تعديل العرض' : 'عرض جديد'}</h2>
          <label className="field"><span>الحد الأدنى للساعات</span><input type="number" min="1" max="24" value={form.minimumHours} onChange={(event) => setForm({ ...form, minimumHours: event.target.value })} required /></label>
          <label className="field"><span>سعر الساعة (ر.ع)</span><input type="number" step="0.001" min="0.001" value={form.pricePerHour} onChange={(event) => setForm({ ...form, pricePerHour: event.target.value })} required /></label>
          <label className="switch-field"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} /><span>العرض نشط</span></label>
          <Feedback type="error">{error}</Feedback>
          <div className="form-actions"><button className="button button--primary">{editingId ? 'حفظ التعديل' : 'إضافة العرض'}</button>{editingId && <button type="button" className="button button--ghost" onClick={() => { setEditingId(null); setForm(emptyForm) }}>إلغاء</button>}</div>
        </form>
        <div className="resource-list">
          {loading ? <Loading /> : offers.map((offer) => (
            <article className="offer-card card" key={offer.id}>
              <span className={offer.isActive ? 'status-badge status-badge--paid' : 'status-badge'}>{offer.isActive ? 'نشط' : 'متوقف'}</span>
              <strong>{formatCurrency(offer.pricePerHour)}</strong><p>للساعة عند حجز {offer.minimumHours} ساعات أو أكثر</p>
              <div className="row-actions"><button onClick={() => { setEditingId(offer.id); setForm({ minimumHours: offer.minimumHours, pricePerHour: offer.pricePerHour, isActive: offer.isActive }) }}>تعديل</button><button className="danger-link" onClick={() => remove(offer)}>حذف</button></div>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
