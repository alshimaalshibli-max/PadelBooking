import Brand from './components/Brand'
import ProtectedRoute from './components/ProtectedRoute'
import BookingPage from './pages/BookingPage'
import PaymentResultPage from './pages/PaymentResultPage'
import AdminLoginPage from './pages/AdminLoginPage'
import AdminDashboardPage from './pages/AdminDashboardPage'
import { Link, useRouter } from './router'

function PublicHeader() {
  return (
    <header className="public-header">
      <div className="shell public-header__inner">
        <Link to="/" className="brand-link" aria-label="الصفحة الرئيسية"><Brand /></Link>
        <Link to="/admin/login" className="button button--ghost button--small">دخول الإدارة</Link>
      </div>
    </header>
  )
}

function PublicLayout({ children }) {
  return (
    <div className="public-app">
      <PublicHeader />
      {children}
      <footer className="public-footer">
        <div className="shell"><Brand compact /><p>حجز واضح، توزيع عادل، ووقت أكثر للعب.</p></div>
      </footer>
    </div>
  )
}

export default function App() {
  const { location } = useRouter()
  const path = location.pathname.replace(/\/+$/, '') || '/'

  if (path === '/') return <PublicLayout><BookingPage /></PublicLayout>
  if (path === '/payment/success') return <PublicLayout><PaymentResultPage mode="success" /></PublicLayout>
  if (path === '/payment/cancel') return <PublicLayout><PaymentResultPage mode="cancel" /></PublicLayout>
  if (path === '/admin/login') return <AdminLoginPage />
  if (path === '/admin' || path.startsWith('/admin/')) {
    return <ProtectedRoute><AdminDashboardPage /></ProtectedRoute>
  }

  return <main className="result-page shell"><div className="result-card card"><h1>الصفحة غير موجودة</h1><Link to="/" className="button button--primary">العودة للرئيسية</Link></div></main>
}
