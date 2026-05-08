import { useEffect, useState } from 'react';

type Health = {
  status: string;
  database: string;
  timestamp: string;
};

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

function App() {
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch(`${API_BASE}/api/health`)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json() as Promise<Health>;
      })
      .then(setHealth)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)));
  }, []);

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 720 }}>
      <h1>Geogrid</h1>
      <p>2D/3D land subdivision simulator.</p>
      <section>
        <h2>API health</h2>
        {error && <pre style={{ color: 'crimson' }}>Error: {error}</pre>}
        {!error && !health && <p>Checking…</p>}
        {health && (
          <pre style={{ background: '#f4f4f4', padding: '1rem', borderRadius: 6 }}>
            {JSON.stringify(health, null, 2)}
          </pre>
        )}
      </section>
    </main>
  );
}

export default App;