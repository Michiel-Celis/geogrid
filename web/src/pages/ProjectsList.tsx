import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { projects, type Project } from '../api';

export default function ProjectsList() {
  const [items, setItems] = useState<Project[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    try {
      setItems(await projects.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  useEffect(() => {
    let cancelled = false;
    projects
      .list()
      .then((rows) => {
        if (!cancelled) setItems(rows);
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onDelete(id: string) {
    if (!confirm('Delete this project?')) return;
    await projects.remove(id);
    void refresh();
  }

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', padding: '2rem', maxWidth: 960, margin: '0 auto' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h1>Projects</h1>
        <Link to="/projects/new"><button>+ New project</button></Link>
      </header>

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {!items && !error && <p>Loading…</p>}
      {items && items.length === 0 && <p>No projects yet. Create one to get started.</p>}

      {items && items.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>
              <th style={{ padding: '0.5rem' }}>Name</th>
              <th style={{ padding: '0.5rem' }}>SRID</th>
              <th style={{ padding: '0.5rem' }}>Center</th>
              <th style={{ padding: '0.5rem' }}>Updated</th>
              <th style={{ padding: '0.5rem' }}></th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id} style={{ borderBottom: '1px solid #eee' }}>
                <td style={{ padding: '0.5rem' }}>
                  <Link to={`/projects/${p.id}`}>{p.name}</Link>
                </td>
                <td style={{ padding: '0.5rem' }}>{p.srid}</td>
                <td style={{ padding: '0.5rem' }}>
                  {p.centerLat.toFixed(4)}, {p.centerLon.toFixed(4)}
                </td>
                <td style={{ padding: '0.5rem' }}>{new Date(p.updatedAt).toLocaleString()}</td>
                <td style={{ padding: '0.5rem' }}>
                  <button onClick={() => void onDelete(p.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
