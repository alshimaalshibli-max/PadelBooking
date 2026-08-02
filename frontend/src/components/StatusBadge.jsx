import { bookingStatusLabels, paymentStatusLabels } from '../utils/format'

export default function StatusBadge({ value }) {
  const label = bookingStatusLabels[value] || paymentStatusLabels[value] || value
  return <span className={`status-badge status-badge--${value?.toLowerCase()}`}>{label}</span>
}
