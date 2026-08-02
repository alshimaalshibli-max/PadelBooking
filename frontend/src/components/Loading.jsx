export default function Loading({ label = 'جارٍ التحميل...' }) {
  return (
    <div className="loading" role="status">
      <span className="spinner" />
      <span>{label}</span>
    </div>
  )
}
