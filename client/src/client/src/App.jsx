import { useEffect, useMemo, useState } from "react";
import "./App.css";

const API = "http://localhost:5000/api";

const STATUS_LABEL = { 0: "Pending", 1: "Approved", 2: "Rejected" };
const ACTION_LABEL = { 0: "Submitted", 1: "Auto-approved", 2: "Approved", 3: "Rejected" };

export default function App() {
  const [users, setUsers] = useState([]);
  // ?user=<name> pre-selects the acting user (used with ?expand= for deep links).
  const [currentUser, setCurrentUser] = useState(
    () => new URLSearchParams(window.location.search).get("user") || "");
  const [expenses, setExpenses] = useState([]);
  const [form, setForm] = useState({ title: "", category: "Travel", amount: "" });
  const [error, setError] = useState("");
  // ?expand=<id> deep-links straight to an expanded decision history.
  const [expanded, setExpanded] = useState(() => {
    const v = new URLSearchParams(window.location.search).get("expand");
    return v ? Number(v) : null;
  });

  const me = users.find((u) => u.Name === currentUser || u.name === currentUser);
  const isManager = (me?.Role ?? me?.role) === "Manager";

  async function api(path, options = {}) {
    const res = await fetch(`${API}${path}`, {
      ...options,
      headers: { "Content-Type": "application/json", "X-Demo-User": currentUser, ...(options.headers || {}) },
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new Error(body.error || `Request failed (${res.status})`);
    }
    return res.json();
  }

  async function load() {
    try {
      setExpenses(await api("/expenses"));
      setError("");
    } catch (e) {
      setError(e.message === "Failed to fetch" ? "Cannot reach the API. Is it running on port 5000?" : e.message);
    }
  }

  useEffect(() => {
    fetch(`${API}/users`)
      .then((r) => r.json())
      .then((list) => {
        setUsers(list);
        setCurrentUser((prev) => prev || (list.length ? list[0].Name ?? list[0].name : ""));
      })
      .catch(() => setError("Cannot reach the API. Is it running on port 5000?"));
  }, []);

  useEffect(() => { if (currentUser) load(); }, [currentUser]);

  const counts = useMemo(() => ({
    pending: expenses.filter((x) => x.status === 0).length,
    approved: expenses.filter((x) => x.status === 1).length,
    rejected: expenses.filter((x) => x.status === 2).length,
  }), [expenses]);

  const pending = expenses.filter((x) => x.status === 0);
  const decided = expenses.filter((x) => x.status !== 0);

  async function submit(e) {
    e.preventDefault();
    try {
      await api("/expenses", { method: "POST", body: JSON.stringify({ ...form, amount: Number(form.amount) }) });
      setForm({ title: "", category: "Travel", amount: "" });
      setError("");
      load();
    } catch (err) { setError(err.message); }
  }

  async function decide(id, approve) {
    let comment = null;
    if (!approve) {
      comment = window.prompt("Reason for rejection (required):");
      if (comment === null) return;
    }
    try {
      await api(`/expenses/${id}/decide`, { method: "POST", body: JSON.stringify({ approve, comment }) });
      setError("");
      load();
    } catch (err) { setError(err.message); }
  }

  function ExpenseCard({ x }) {
    return (
      <div className="expense">
        <div className="row main-row">
          <strong>{x.title}</strong>
          <span className="muted">{x.category}</span>
          <span>₹{x.amount.toLocaleString("en-IN")}</span>
          <span className="muted">by {x.submittedBy}</span>
          <span className={`badge s${x.status}`}>{STATUS_LABEL[x.status]}</span>
        </div>
        <div className="row actions">
          <button className="ghost" onClick={() => setExpanded(expanded === x.id ? null : x.id)}>
            {expanded === x.id ? "Hide history" : "History"}
          </button>
          {x.status === 0 && isManager && (
            <>
              <button onClick={() => decide(x.id, true)}>Approve</button>
              <button className="danger" onClick={() => decide(x.id, false)}>Reject</button>
            </>
          )}
        </div>
        {expanded === x.id && (
          <ul className="history">
            {x.history.map((h) => (
              <li key={h.id}>
                {new Date(h.timestamp).toLocaleString("en-IN")} - {ACTION_LABEL[h.action]} by {h.actor}
                {h.comment ? `: ${h.comment}` : ""}
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }

  return (
    <main>
      <header className="top">
        <h1>Expense Approval</h1>
        <label className="switcher">
          Acting as
          <select value={currentUser} onChange={(e) => setCurrentUser(e.target.value)}>
            {users.map((u) => {
              const name = u.Name ?? u.name, role = u.Role ?? u.role;
              return <option key={name} value={name}>{name} ({role})</option>;
            })}
          </select>
        </label>
      </header>

      <section className="summary">
        <span className="chip s0">{counts.pending} pending</span>
        <span className="chip s1">{counts.approved} approved</span>
        <span className="chip s2">{counts.rejected} rejected</span>
      </section>

      <section className="card">
        <h2>Submit an expense</h2>
        <form onSubmit={submit}>
          <input className="grow" placeholder="Title" value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })} />
          <select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
            <option>Travel</option><option>Food</option><option>Equipment</option><option>Other</option>
          </select>
          <input className="amount" placeholder="Amount (₹)" type="number" min="0" step="0.01" value={form.amount}
            onChange={(e) => setForm({ ...form, amount: e.target.value })} />
          <button type="submit">Submit</button>
        </form>
        <p className="hint">Amounts above ₹5,000 wait for a manager; smaller ones are auto-approved.</p>
      </section>

      {error && <p className="error">{error}</p>}

      <section>
        <h2>Waiting for a manager ({pending.length})</h2>
        {pending.length === 0 && <p className="muted">Nothing pending.</p>}
        {pending.map((x) => <ExpenseCard x={x} key={x.id} />)}
      </section>

      <section>
        <h2>Decided ({decided.length})</h2>
        {decided.length === 0 && <p className="muted">No decided expenses yet.</p>}
        {decided.map((x) => <ExpenseCard x={x} key={x.id} />)}
      </section>
    </main>
  );
                     }
