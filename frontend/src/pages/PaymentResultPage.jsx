import { useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api/client'
import Loading from '../components/Loading'
import { formatCurrency } from '../utils/format'
import { Link, useRouter } from '../router'

export default function PaymentResultPage({ mode }) {
  const { location } = useRouter()
  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search])
  const [status, setStatus] = useState(
    location.state?.cashBooking ? 'success' : 'loading',
  )
  const [message, setMessage] = useState('')

  useEffect(() => {
    if (mode === 'cancel') {
      const storedPayment = sessionStorage.getItem('pending_thawani_payment')
      if (!storedPayment) {
        setStatus('cancelled')
        setMessage(location.state?.paymentError || '')
        return
      }

      let pendingPayment
      try {
        pendingPayment = JSON.parse(storedPayment)
      } catch {
        sessionStorage.removeItem('pending_thawani_payment')
        setStatus('error')
        setMessage('تعذر قراءة معلومات جلسة الدفع.')
        return
      }

      apiRequest('/payments/thawani/cancel', {
        method: 'POST',
        body: pendingPayment,
      })
        .then((result) => {
          setStatus(result.paid ? 'success' : 'cancelled')
          setMessage(result.message)
          sessionStorage.removeItem('pending_thawani_payment')
        })
        .catch((requestError) => {
          setStatus('error')
          setMessage(requestError.message)
        })
      return
    }
    if (location.state?.cashBooking) return

    const storedPayment = sessionStorage.getItem('pending_thawani_payment')

    if (!storedPayment) {
      setStatus('error')
      setMessage('تعذر العثور على معلومات جلسة الدفع.')
      return
    }

    let pendingPayment
    try {
      pendingPayment = JSON.parse(storedPayment)
    } catch {
      sessionStorage.removeItem('pending_thawani_payment')
      setStatus('error')
      setMessage('تعذر قراءة معلومات جلسة الدفع.')
      return
    }

    const sessionId = searchParams.get('session_id')
      || searchParams.get('sessionId')
      || pendingPayment.sessionId
    if (!sessionId) {
      setStatus('error')
      setMessage('تعذر العثور على رقم جلسة الدفع.')
      return
    }

    apiRequest('/payments/thawani/verify', {
      method: 'POST',
      body: {
        sessionId,
        phone: pendingPayment.phone,
        bookingIds: pendingPayment.bookingIds,
      },
    })
      .then((result) => {
        if (result.paid === false) {
          setStatus('pending')
          setMessage(result.message)
          return
        }
        sessionStorage.removeItem('pending_thawani_payment')
        setStatus('success')
      })
      .catch((requestError) => {
        setStatus('error')
        setMessage(requestError.message)
      })
  }, [location.state, mode, searchParams])

  if (status === 'loading') {
    return (
      <main className="result-page shell">
        <div className="result-card card">
          <Loading label="نتحقق من نتيجة الدفع مع ثواني..." />
        </div>
      </main>
    )
  }

  const isSuccess = status === 'success'
  return (
    <main className="result-page shell">
      <div className={`result-card card result-card--${isSuccess ? 'success' : 'error'}`}>
        <div className="result-icon">{isSuccess ? '✓' : '!'}</div>
        <span className="eyebrow">
          {isSuccess ? 'تمت العملية' : status === 'cancelled' ? 'أُلغيت العملية' : 'لم يكتمل الدفع'}
        </span>
        <h1>{isSuccess ? 'حجزك مؤكد!' : 'لم يتم تأكيد الدفع'}</h1>
        <p>
          {isSuccess
            ? 'احتفظ برقم هاتفك؛ يمكنك استخدامه عند الوصول إلى الملعب.'
            : message || 'لم تُخصم أي مبالغ. يمكنك العودة والمحاولة مرة أخرى.'}
        </p>
        {location.state?.totalPrice != null && (
          <div className="result-total">
            <span>الإجمالي عند الوصول</span>
            <strong>{formatCurrency(location.state.totalPrice)}</strong>
          </div>
        )}
        <Link to="/" className="button button--primary">
          العودة إلى الحجز
        </Link>
      </div>
    </main>
  )
}
