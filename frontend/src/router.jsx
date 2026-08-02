import { createContext, useContext, useEffect, useMemo, useState } from 'react'

const RouterContext = createContext(null)

function readLocation() {
  return {
    pathname: window.location.pathname,
    search: window.location.search,
    state: window.history.state,
  }
}

export function RouterProvider({ children }) {
  const [location, setLocation] = useState(readLocation)

  useEffect(() => {
    const onPopState = () => setLocation(readLocation())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = (to, options = {}) => {
    const method = options.replace ? 'replaceState' : 'pushState'
    window.history[method](options.state ?? null, '', to)
    setLocation(readLocation())
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const value = useMemo(() => ({ location, navigate }), [location])
  return <RouterContext.Provider value={value}>{children}</RouterContext.Provider>
}

export function useRouter() {
  const context = useContext(RouterContext)
  if (!context) throw new Error('useRouter must be used inside RouterProvider.')
  return context
}

export function Link({ to, children, onClick, ...props }) {
  const { navigate } = useRouter()

  const handleClick = (event) => {
    onClick?.(event)
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    ) return

    event.preventDefault()
    navigate(to)
  }

  return <a href={to} onClick={handleClick} {...props}>{children}</a>
}

export function Navigate({ to, replace = false, state = null }) {
  const { navigate } = useRouter()
  useEffect(() => navigate(to, { replace, state }), [navigate, replace, state, to])
  return null
}
