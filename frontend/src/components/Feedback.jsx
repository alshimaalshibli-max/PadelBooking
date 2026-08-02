export default function Feedback({ type = 'info', children }) {
  if (!children) return null
  return (
    <div className={`feedback feedback--${type}`} role="status">
      {children}
    </div>
  )
}
