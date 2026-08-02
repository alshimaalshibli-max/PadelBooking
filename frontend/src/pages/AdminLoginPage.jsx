import { useState } from 'react'
import Brand from '../components/Brand'
import Feedback from '../components/Feedback'
import { useAuth } from '../context/AuthContext'
import { Navigate, useRouter } from '../router'

export default function AdminLoginPage() {
  const { isAuthenticated, login } = useAuth()
  const { navigate, location } = useRouter()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  if (isAuthenticated) return <Navigate to="/admin" replace />

  const submit = async (event) => {
    event.preventDefault()
    setLoading(true)
    setError('')
    try {
      await login(username, password)
      navigate(location.state?.from?.pathname || '/admin', { replace: true })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="login-page">
      <div className="login-page__brand">
        <Brand />
        <div>
          <span className="eyebrow">لوحة الإدارة</span>
          <h1>كل تفاصيل الملاعب، في مكان واحد.</h1>
          <p>راقب الحجوزات وأدر الأسعار والمواعيد والإغلاقات بأمان.</p>
        </div>
      </div>
      <form className="login-card card" onSubmit={submit}>
        <div className="section-heading">
          <div>
            <h2>تسجيل الدخول</h2>
            <p>أدخل البيانات التي ضُبطت في متغيرات بيئة الخادم.</p>
          </div>
        </div>
        <label className="field">
          <span>اسم المستخدم</span>
          <input
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            autoComplete="username"
            required
          />
        </label>
        <label className="field">
          <span>كلمة المرور</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </label>
        <Feedback type="error">{error}</Feedback>
        <button className="button button--primary button--large button--full" disabled={loading}>
          {loading ? 'جارٍ التحقق...' : 'دخول آمن'}
        </button>
        <a href="/" className="text-link">العودة إلى واجهة الحجز</a>
      </form>
    </main>
  )
}
