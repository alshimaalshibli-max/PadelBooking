export function toInputDate(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function formatArabicDate(value) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('ar-OM', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(`${value.slice(0, 10)}T12:00:00`))
}

export function formatTime(value) {
  return value ? value.slice(0, 5) : '—'
}

export function formatCurrency(value) {
  return new Intl.NumberFormat('ar-OM', {
    style: 'currency',
    currency: 'OMR',
    minimumFractionDigits: 3,
  }).format(Number(value || 0))
}

export const bookingStatusLabels = {
  Confirmed: 'مؤكد',
  Cancelled: 'ملغي',
  Completed: 'مكتمل',
}

export const paymentStatusLabels = {
  Pending: 'بانتظار الدفع',
  Paid: 'مدفوع',
  Failed: 'فشل الدفع',
}

export const paymentMethodLabels = {
  Cash: 'الدفع عند الوصول',
  Thawani: 'الدفع الإلكتروني عبر ثواني',
  Card: 'بطاقة (سجل سابق)',
}
