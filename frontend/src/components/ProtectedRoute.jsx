import { useAuth } from '../context/AuthContext'
import { Navigate, useRouter } from '../router'

export default function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  const { location } = useRouter()

  if (!isAuthenticated) {
    return <Navigate to="/admin/login" replace state={{ from: location }} />
  }

  return children
}
