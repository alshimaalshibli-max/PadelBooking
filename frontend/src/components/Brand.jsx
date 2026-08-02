export default function Brand({ compact = false }) {
  return (
    <div className={`brand ${compact ? 'brand--compact' : ''}`}>
      <span className="brand__mark" aria-hidden="true">
        م
      </span>
      <span>
        <strong>ملعبك</strong>
        {!compact && <small>بادل، بالوقت الذي يناسبك</small>}
      </span>
    </div>
  )
}
