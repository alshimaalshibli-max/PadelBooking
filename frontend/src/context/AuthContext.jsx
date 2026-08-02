import { createContext, useContext, useMemo, useState } from 'react'
import { apiRequest } from '../api/client'

const AuthContext = createContext(null)
const storageKey = 'padel_admin_token'

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => sessionStorage.getItem(storageKey))

  const login = async (username, password) => {
    const result = await apiRequest('/auth/admin/login', {
      method: 'POST',
      body: { username, password },
    })
    sessionStorage.setItem(storageKey, result.accessToken)
    setToken(result.accessToken)
  }

  const logout = () => {
    sessionStorage.removeItem(storageKey)
    setToken(null)
  }

  const value = useMemo(
    () => ({ token, isAuthenticated: Boolean(token), login, logout }),
    [token],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider.')
  return context
}
