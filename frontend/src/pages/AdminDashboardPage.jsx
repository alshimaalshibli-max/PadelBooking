import { useState } from 'react'
import Brand from '../components/Brand'
import { useAuth } from '../context/AuthContext'
import BookingsAdmin from '../admin/BookingsAdmin'
import CourtsAdmin from '../admin/CourtsAdmin'
import OffersAdmin from '../admin/OffersAdmin'
import ClosuresAdmin from '../admin/ClosuresAdmin'
import StatisticsAdmin from '../admin/StatisticsAdmin'
import { useRouter } from '../router'

const tabs = [
  { id: 'bookings', label: 'الحجوزات', icon: 'ح' },
  { id: 'statistics', label: 'الإحصائيات', icon: 'إ' },
  { id: 'courts', label: 'الملاعب', icon: 'م' },
  { id: 'offers', label: 'العروض', icon: '%' },
  { id: 'closures', label: 'الإغلاقات', icon: '×' },
]

export default function AdminDashboardPage() {
  const [activeTab, setActiveTab] = useState('bookings')
  const [menuOpen, setMenuOpen] = useState(false)
  const { token, logout } = useAuth()
  const { navigate } = useRouter()

  const signOut = () => {
    logout()
    navigate('/admin/login', { replace: true })
  }

  const selectTab = (tab) => {
    setActiveTab(tab)
    setMenuOpen(false)
  }

  const contentProps = { token, onUnauthorized: signOut }

  return (
    <div className="admin-app">
      <aside className={`admin-sidebar ${menuOpen ? 'admin-sidebar--open' : ''}`}>
        <div className="admin-sidebar__brand"><Brand /><button className="sidebar-close" onClick={() => setMenuOpen(false)}>×</button></div>
        <nav>
          {tabs.map((tab) => (
            <button key={tab.id} className={activeTab === tab.id ? 'active' : ''} onClick={() => selectTab(tab.id)}><span>{tab.icon}</span>{tab.label}</button>
          ))}
        </nav>
        <div className="admin-sidebar__footer"><a href="/" target="_blank" rel="noreferrer">فتح واجهة العميل</a><button onClick={signOut}>تسجيل الخروج</button></div>
      </aside>
      {menuOpen && <button className="sidebar-backdrop" onClick={() => setMenuOpen(false)} aria-label="إغلاق القائمة" />}
      <div className="admin-main">
        <header className="admin-topbar"><button className="menu-button" onClick={() => setMenuOpen(true)}>☰</button><div><strong>{tabs.find((tab) => tab.id === activeTab)?.label}</strong><small>لوحة إدارة ملعبك</small></div><span className="admin-avatar">إ</span></header>
        <div className="admin-content">
          {activeTab === 'bookings' && <BookingsAdmin {...contentProps} />}
          {activeTab === 'statistics' && <StatisticsAdmin {...contentProps} />}
          {activeTab === 'courts' && <CourtsAdmin {...contentProps} />}
          {activeTab === 'offers' && <OffersAdmin {...contentProps} />}
          {activeTab === 'closures' && <ClosuresAdmin {...contentProps} />}
        </div>
      </div>
    </div>
  )
}
