/* eslint-disable react-refresh/only-export-components */
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { auth as authApi, clearSession, getUser, setSession, type AuthUser } from './api';

type AuthContextValue = {
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(getUser());

  const login = useCallback(async (email: string, password: string) => {
    const r = await authApi.login(email, password);
    setSession(r.token, { userId: r.userId, email: r.email });
    setUser({ userId: r.userId, email: r.email });
  }, []);

  const register = useCallback(async (email: string, password: string) => {
    const r = await authApi.register(email, password);
    setSession(r.token, { userId: r.userId, email: r.email });
    setUser({ userId: r.userId, email: r.email });
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setUser(null);
  }, []);

  const value = useMemo(() => ({ user, login, register, logout }), [user, login, register, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
