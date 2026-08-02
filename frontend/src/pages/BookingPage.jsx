import { useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api/client'
import Feedback from '../components/Feedback'
import Loading from '../components/Loading'
import {
  formatArabicDate,
  formatCurrency,
  formatTime,
  toInputDate,
} from '../utils/format'
import { useRouter } from '../router'

const initialCustomer = {
  phone: '',
  customerName: '',
  email: '',
  paymentMethod: 'Cash',
}

function toBookingSlots(appointments) {
  return appointments.map((item) => ({
    bookingDate: item.date,
    startTime: item.startTime,
    hours: item.hours,
  }))
}

async function requestPricePreview(appointments, signal) {
  return apiRequest('/bookings/preview', {
    method: 'POST',
    signal,
    body: { slots: toBookingSlots(appointments) },
  })
}

function timeToMinutes(value) {
  const [hour, minute] = value.split(':').map(Number)
  return (hour * 60) + minute
}

function minutesToTime(value) {
  const normalized = ((value % 1440) + 1440) % 1440
  const hour = String(Math.floor(normalized / 60)).padStart(2, '0')
  const minute = String(normalized % 60).padStart(2, '0')
  return `${hour}:${minute}:00`
}

function addHoursToTime(value, hoursToAdd) {
  return minutesToTime(timeToMinutes(value) + (hoursToAdd * 60))
}

export default function BookingPage() {
  const { navigate } = useRouter()
  const today = useMemo(() => toInputDate(), [])
  const [date, setDate] = useState(today)
  const [slots, setSlots] = useState([])
  const [selectedTime, setSelectedTime] = useState('')
  const [hours, setHours] = useState(1)
  const [offers, setOffers] = useState([])
  const [selectedOfferId, setSelectedOfferId] = useState(null)
  const [appointments, setAppointments] = useState([])
  const [customer, setCustomer] = useState(initialCustomer)
  const [draftPreview, setDraftPreview] = useState(null)
  const [pricePreview, setPricePreview] = useState(null)
  const [loadingSlots, setLoadingSlots] = useState(false)
  const [loadingDraftPrice, setLoadingDraftPrice] = useState(false)
  const [loadingPrice, setLoadingPrice] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [offersError, setOffersError] = useState('')
  const [draftPriceError, setDraftPriceError] = useState('')
  const [priceError, setPriceError] = useState('')
  const [error, setError] = useState('')

  const totalHours = useMemo(
    () => appointments.reduce((total, item) => total + item.hours, 0),
    [appointments],
  )

  const draftAppointments = useMemo(() => {
    return selectedTime
      ? [{ date, startTime: selectedTime, hours: Number(hours) }]
      : []
  }, [date, hours, selectedTime])

  const selectedHourBlocks = useMemo(
    () => draftAppointments.flatMap((appointment) =>
      Array.from({ length: appointment.hours }, (_, index) => {
        const startTime = addHoursToTime(appointment.startTime, index)
        return {
          key: `${appointment.date}-${startTime}`,
          startTime,
          endTime: addHoursToTime(startTime, 1),
        }
      })),
    [draftAppointments],
  )

  useEffect(() => {
    const controller = new AbortController()
    apiRequest('/offers/public', { signal: controller.signal })
      .then((result) => setOffers(result || []))
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') {
          setOffersError(requestError.message)
        }
      })

    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setLoadingSlots(true)
    setError('')

    apiRequest(`/bookings/available?date=${date}&hours=${Number(hours)}`, {
      signal: controller.signal,
    })
      .then((result) => {
        setSlots(result || [])
        setSelectedTime(result?.[0]?.startTime || '')
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') {
          setSlots([])
          setSelectedTime('')
          setError(requestError.message)
        }
      })
      .finally(() => setLoadingSlots(false))

    return () => controller.abort()
  }, [date, hours])

  useEffect(() => {
    if (draftAppointments.length === 0) {
      setDraftPreview(null)
      setDraftPriceError('')
      return undefined
    }

    const controller = new AbortController()
    setLoadingDraftPrice(true)
    setDraftPriceError('')

    requestPricePreview(draftAppointments, controller.signal)
      .then(setDraftPreview)
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') {
          setDraftPreview(null)
          setDraftPriceError(requestError.message)
        }
      })
      .finally(() => setLoadingDraftPrice(false))

    return () => controller.abort()
  }, [draftAppointments])

  useEffect(() => {
    if (appointments.length === 0) {
      setPricePreview(null)
      setPriceError('')
      return undefined
    }

    const controller = new AbortController()
    setLoadingPrice(true)
    setPriceError('')

    requestPricePreview(appointments, controller.signal)
      .then(setPricePreview)
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') {
          setPricePreview(null)
          setPriceError(requestError.message)
        }
      })
      .finally(() => setLoadingPrice(false))

    return () => controller.abort()
  }, [appointments])

  const selectOffer = (offer) => {
    setHours(offer.minimumHours)
    setSelectedOfferId(offer.id)
    document.querySelector('#booking')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  const changeHours = (event) => {
    setHours(Number(event.target.value))
    setSelectedOfferId(null)
  }

  const addAppointment = () => {
    setError('')
    if (draftAppointments.length === 0) {
      setError('اختر وقت بداية متاحًا أولًا.')
      return
    }

    if (!draftPreview || draftPriceError) {
      setError('راجع توفر الموعد وسعره قبل إضافته.')
      return
    }

    const newAppointments = draftAppointments.filter((candidate) =>
      !appointments.some(
        (item) => item.date === candidate.date && item.startTime === candidate.startTime,
      ))
    if (newAppointments.length === 0) {
      setError('الساعات المحددة مضافة بالفعل إلى الحجز.')
      return
    }

    setAppointments((current) => [
      ...current,
      ...newAppointments.map((appointment) => ({
        id: crypto.randomUUID(),
        ...appointment,
      })),
    ])
    setSelectedTime('')
  }

  const removeAppointment = (id) => {
    setAppointments((current) => current.filter((item) => item.id !== id))
  }

  const updateCustomer = (event) => {
    const { name, value } = event.target
    setCustomer((current) => ({ ...current, [name]: value }))
  }

  const submitBooking = async (event) => {
    event.preventDefault()
    setError('')

    if (appointments.length === 0) {
      setError('أضف موعدًا واحدًا على الأقل.')
      return
    }

    if (!pricePreview || priceError || loadingPrice) {
      setError('انتظر حتى يكتمل حساب السعر النهائي ثم راجعه.')
      return
    }

    setSubmitting(true)
    try {
      const latestPrice = await requestPricePreview(appointments)
      setPricePreview(latestPrice)

      if (latestPrice.totalPrice !== pricePreview.totalPrice) {
        setError('تغيّر السعر أو العرض. راجع السعر النهائي المحدّث ثم اضغط التأكيد مرة أخرى.')
        return
      }

      const result = await apiRequest('/bookings/batch', {
        method: 'POST',
        body: {
          customerName: customer.customerName.trim() || null,
          phone: customer.phone.trim(),
          email: customer.email.trim() || null,
          paymentMethod: customer.paymentMethod,
          expectedTotalPrice: latestPrice.totalPrice,
          slots: toBookingSlots(appointments),
        },
      })

      if (customer.paymentMethod === 'Thawani') {
        const bookingIds = result.bookings.map((booking) => booking.id)
        const payment = await apiRequest('/payments/thawani/sessions', {
          method: 'POST',
          body: { phone: customer.phone.trim(), bookingIds },
        })
        sessionStorage.setItem(
          'pending_thawani_payment',
          JSON.stringify({ phone: customer.phone.trim(), bookingIds }),
        )
        window.location.assign(payment.paymentUrl)
        return
      }

      navigate('/payment/success', {
        state: {
          cashBooking: true,
          bookings: result.bookings,
          totalPrice: result.totalPrice,
        },
      })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setSubmitting(false)
    }
  }

  const draftPrice = draftPreview?.slots?.[0]

  return (
    <main>
      <section className="hero">
        <div className="shell hero__grid">
          <div className="hero__content">
            <span className="eyebrow">احجز. العب. كرر.</span>
            <h1>
              وقتك في الملعب،
              <span> بخطوات أبسط.</span>
            </h1>
            <p>
              اختر الوقت المناسب وسنخصص لك ملعبًا متاحًا تلقائيًا. شاهد العروض والسعر
              النهائي قبل أن تؤكد الحجز أو تنتقل إلى الدفع.
            </p>
            <div className="hero__facts">
              <span>أفضل سعر مؤهل يطبّق تلقائيًا</span>
              <span>أكثر من موعد في عملية واحدة</span>
              <span>دفع آمن عبر ثواني</span>
            </div>
          </div>
          <div className="court-visual" aria-hidden="true">
            <div className="court-visual__net" />
            <span className="court-visual__ball" />
            <strong>جاهز للمباراة؟</strong>
          </div>
        </div>
      </section>

      <section className="offers-showcase shell" aria-labelledby="offers-title">
        <div className="offers-showcase__heading">
          <div>
            <span className="eyebrow">وفّر أكثر</span>
            <h2 id="offers-title">العروض المتاحة</h2>
          </div>
          <p>اختر عرضًا لضبط مدة الموعد، وسيؤكد النظام تلقائيًا أفضل سعر تستحقه.</p>
        </div>

        {offers.length > 0 ? (
          <div className="customer-offers">
            {offers.map((offer) => (
              <button
                type="button"
                className={`customer-offer ${selectedOfferId === offer.id ? 'customer-offer--selected' : ''}`}
                key={offer.id}
                onClick={() => selectOffer(offer)}
              >
                <span className="customer-offer__badge">عرض {offer.minimumHours} ساعات</span>
                <div className="customer-offer__prices">
                  {offer.standardPricePerHour > offer.pricePerHour && (
                    <del>{formatCurrency(offer.standardPricePerHour)}</del>
                  )}
                  <strong>{formatCurrency(offer.pricePerHour)}</strong>
                  <small>لكل ساعة</small>
                </div>
                {offer.originalTotalPrice > offer.offerTotalPrice ? (
                  <span className="customer-offer__total">
                    إجمالي {offer.minimumHours} ساعات:
                    <del>{formatCurrency(offer.originalTotalPrice)}</del>
                    <strong>{formatCurrency(offer.offerTotalPrice)}</strong>
                  </span>
                ) : (
                  <small>عند حجز {offer.minimumHours} ساعات أو أكثر</small>
                )}
                <span className="customer-offer__action">
                  {selectedOfferId === offer.id ? 'تم اختيار العرض' : 'اختيار العرض'}
                </span>
              </button>
            ))}
          </div>
        ) : (
          <div className="empty-state">لا توجد عروض نشطة حاليًا، وسيظهر السعر الأساسي للموعد.</div>
        )}
        <Feedback type="error">{offersError}</Feedback>
      </section>

      <section className="booking-section" id="booking">
        <div className="shell booking-layout">
          <div className="booking-builder card">
            <div className="section-heading">
              <span className="step-number">01</span>
              <div>
                <h2>كوّن مواعيدك</h2>
                <p>اختر وقت البداية والمدة، وسنعرض وقت النهاية وكل الساعات المشمولة.</p>
              </div>
            </div>

            <div className="field-grid field-grid--booking">
              <label className="field">
                <span>التاريخ</span>
                <input
                  type="date"
                  value={date}
                  min={today}
                  onChange={(event) => setDate(event.target.value)}
                />
              </label>
              <label className="field">
                <span>المدة المتتالية</span>
                <select value={hours} onChange={changeHours}>
                  {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => (
                    <option key={value} value={value}>
                      {value} {value === 1 ? 'ساعة' : 'ساعات'}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div className="slot-area">
              <div className="slot-area__header">
                <strong>اختر وقت بداية الفترة</strong>
                <span>{formatArabicDate(date)}</span>
              </div>
              {loadingSlots ? (
                <Loading label="نبحث عن الأوقات المتاحة..." />
              ) : slots.length > 0 ? (
                <div className="slot-grid">
                  {slots.map((slot) => (
                    <button
                      type="button"
                      key={slot.startTime}
                      className={`slot ${selectedTime === slot.startTime ? 'slot--selected' : ''}`}
                      onClick={() => setSelectedTime(slot.startTime)}
                    >
                      <strong>{formatTime(slot.startTime)}</strong>
                      {Number(hours) > 1 && (
                        <small>حتى {formatTime(slot.endTime)}</small>
                      )}
                    </button>
                  ))}
                </div>
              ) : (
                <div className="empty-state">لا توجد أوقات متاحة في هذا اليوم.</div>
              )}
            </div>

            {selectedHourBlocks.length > 0 && (
              <div className="selected-hours" aria-live="polite">
                <div className="selected-hours__heading">
                  <strong>الساعات التي اخترتها</strong>
                  <span>
                    {selectedHourBlocks.length === 1
                      ? 'ساعة واحدة'
                      : `${selectedHourBlocks.length} ساعات`}
                  </span>
                </div>
                <div className="selected-hours__list">
                  {selectedHourBlocks.map((block) => (
                    <span key={block.key}>
                      {formatTime(block.startTime)} – {formatTime(block.endTime)}
                    </span>
                  ))}
                </div>
              </div>
            )}

            <div className="draft-price" aria-live="polite">
              {loadingDraftPrice ? (
                <Loading label="نحسب سعر الموعد..." />
              ) : draftPreview ? (
                <>
                  <div>
                    <span>
                      {draftPreview.totalSavings > 0
                        ? 'السعر بعد الخصم'
                        : 'سعر الموعد المختار'}
                    </span>
                    {draftPreview.totalSavings > 0 && (
                      <del className="original-price">
                        السعر الأصلي {formatCurrency(draftPreview.totalPrice + draftPreview.totalSavings)}
                      </del>
                    )}
                    <strong>{formatCurrency(draftPreview.totalPrice)}</strong>
                    <small>
                      {draftPrice && `${formatCurrency(draftPrice.finalPricePerHour)} لكل ساعة · ${draftPrice.hours} ${draftPrice.hours === 1 ? 'ساعة' : 'ساعات متتالية'}`}
                    </small>
                  </div>
                  {draftPreview.totalSavings > 0 ? (
                    <div className="applied-offer">
                      <span>العرض مطبّق</span>
                      <strong>وفّرت {formatCurrency(draftPreview.totalSavings)}</strong>
                    </div>
                  ) : (
                    <span className="base-price-note">السعر الأساسي</span>
                  )}
                </>
              ) : (
                <span>اختر وقت البداية والمدة لعرض السعر.</span>
              )}
            </div>
            <Feedback type="error">{draftPriceError}</Feedback>

            <button
              type="button"
              className="button button--secondary button--full"
              onClick={addAppointment}
              disabled={draftAppointments.length === 0 || loadingDraftPrice || !draftPreview || Boolean(draftPriceError)}
            >
              إضافة الفترة إلى الحجز
            </button>

            <div className="appointment-list">
              {appointments.map((item, index) => {
                const itemPrice = pricePreview?.slots?.[index]
                return (
                  <article className="appointment" key={item.id}>
                    <span className="appointment__index">{index + 1}</span>
                    <div>
                      <strong>{formatArabicDate(item.date)}</strong>
                      <small>
                        {formatTime(item.startTime)} – {formatTime(addHoursToTime(item.startTime, item.hours))}
                        {' · '}{item.hours} {item.hours === 1 ? 'ساعة' : 'ساعات متتالية'}
                      </small>
                      {itemPrice && (
                        <span className="appointment__price">
                          {itemPrice.savings > 0 && (
                            <del>{formatCurrency(itemPrice.totalPrice + itemPrice.savings)}</del>
                          )}
                          <strong>{formatCurrency(itemPrice.totalPrice)}</strong>
                          {itemPrice.appliedOfferId && ' · بعد الخصم'}
                        </span>
                      )}
                    </div>
                    <button
                      type="button"
                      className="icon-button"
                      onClick={() => removeAppointment(item.id)}
                      aria-label="حذف الموعد"
                    >
                      ×
                    </button>
                  </article>
                )
              })}
            </div>
          </div>

          <form className="customer-form card" onSubmit={submitBooking}>
            <div className="section-heading">
              <span className="step-number">02</span>
              <div>
                <h2>بيانات التأكيد</h2>
                <p>لن تحتاج إلى إنشاء حساب.</p>
              </div>
            </div>

            <label className="field">
              <span>رقم الهاتف *</span>
              <input
                name="phone"
                value={customer.phone}
                onChange={updateCustomer}
                inputMode="tel"
                placeholder="96891234567"
                pattern="\+?[0-9]{8,15}"
                required
              />
            </label>
            <label className="field">
              <span>الاسم <small>اختياري</small></span>
              <input
                name="customerName"
                value={customer.customerName}
                onChange={updateCustomer}
                minLength="2"
                maxLength="100"
                placeholder="كيف نناديك؟"
              />
            </label>
            <label className="field">
              <span>البريد الإلكتروني <small>اختياري</small></span>
              <input
                name="email"
                value={customer.email}
                onChange={updateCustomer}
                type="email"
                maxLength="150"
                placeholder="name@example.com"
              />
            </label>

            <fieldset className="payment-options">
              <legend>طريقة الدفع</legend>
              <label className={customer.paymentMethod === 'Cash' ? 'payment-option payment-option--active' : 'payment-option'}>
                <input
                  type="radio"
                  name="paymentMethod"
                  value="Cash"
                  checked={customer.paymentMethod === 'Cash'}
                  onChange={updateCustomer}
                />
                <span className="payment-option__icon">ر.ع</span>
                <span>
                  <strong>عند الوصول</strong>
                  <small>ادفع في موقع الملعب</small>
                </span>
              </label>
              <label className={customer.paymentMethod === 'Thawani' ? 'payment-option payment-option--active' : 'payment-option'}>
                <input
                  type="radio"
                  name="paymentMethod"
                  value="Thawani"
                  checked={customer.paymentMethod === 'Thawani'}
                  onChange={updateCustomer}
                />
                <span className="payment-option__icon">ث</span>
                <span>
                  <strong>ثواني</strong>
                  <small>دفع إلكتروني آمن</small>
                </span>
              </label>
            </fieldset>

            <div className="price-summary" aria-live="polite">
              <div className="price-summary__title">
                <strong>ملخص السعر</strong>
                {loadingPrice && <span>جارٍ التحديث...</span>}
              </div>
              <div>
                <span>عدد المواعيد</span>
                <strong>{appointments.length}</strong>
              </div>
              <div>
                <span>إجمالي الساعات</span>
                <strong>{totalHours}</strong>
              </div>
              <div>
                <span>السعر الأصلي</span>
                <strong>
                  {pricePreview && !priceError
                    ? formatCurrency(pricePreview.totalPrice + pricePreview.totalSavings)
                    : '—'}
                </strong>
              </div>
              <div className="price-summary__saving">
                <span>خصم العروض</span>
                <strong>
                  {pricePreview && !priceError
                    ? `- ${formatCurrency(pricePreview.totalSavings)}`
                    : '—'}
                </strong>
              </div>
              <div className="price-summary__total">
                <span>السعر بعد الخصم</span>
                <strong>
                  {pricePreview && !priceError ? formatCurrency(pricePreview.totalPrice) : '—'}
                </strong>
              </div>
              <small>هذا هو المبلغ الذي سيُعتمد عند تأكيد الحجز.</small>
            </div>

            <Feedback type="error">{priceError}</Feedback>
            <Feedback type="error">{error}</Feedback>
            <button
              className="button button--primary button--full button--large"
              disabled={
                submitting ||
                loadingPrice ||
                appointments.length === 0 ||
                !pricePreview ||
                Boolean(priceError)
              }
            >
              {submitting
                ? 'جارٍ تأكيد الحجز...'
                : customer.paymentMethod === 'Thawani'
                  ? 'تأكيد الحجز والانتقال للدفع'
                  : 'تأكيد الحجز بالسعر النهائي'}
            </button>
            <p className="privacy-note">
              لا يمكن للمتصفح تغيير السعر؛ يعيد الخادم التحقق منه قبل حفظ الحجز.
            </p>
          </form>
        </div>
      </section>
    </main>
  )
}
