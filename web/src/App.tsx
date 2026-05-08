import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth';
import Login from './pages/Login';
import Register from './pages/Register';
import ProjectsList from './pages/ProjectsList';
import NewProject from './pages/NewProject';
import ProjectView from './pages/ProjectView';

function NavBar() {
  const { user, logout } = useAuth();
  return (
    <nav style={{
      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      padding: '0.75rem 2rem', borderBottom: '1px solid #ddd', fontFamily: 'system-ui, sans-serif',
    }}>
      <Link to="/" style={{ fontWeight: 600, textDecoration: 'none', color: 'inherit' }}>Geogrid</Link>
      <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
        {user ? (
          <>
            <Link to="/projects">Projects</Link>
            <span style={{ color: '#666' }}>{user.email}</span>
            <button onClick={logout}>Sign out</button>
          </>
        ) : (
          <>
            <Link to="/login">Sign in</Link>
            <Link to="/register">Register</Link>
          </>
        )}
      </div>
    </nav>
  );
}

function RequireAuth() {
  const { user } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}

function Home() {
  const { user } = useAuth();
  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 720, margin: '0 auto' }}>
      <h1>Geogrid</h1>
      <p>2D/3D land subdivision simulator.</p>
      {user ? (
        <p><Link to="/projects">Go to your projects →</Link></p>
      ) : (
        <p><Link to="/login">Sign in</Link> or <Link to="/register">create an account</Link> to get started.</p>
      )}
    </div>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <NavBar />
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route element={<RequireAuth />}>
          <Route path="/projects" element={<ProjectsList />} />
          <Route path="/projects/new" element={<NewProject />} />
          <Route path="/projects/:id" element={<ProjectView />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  );
}