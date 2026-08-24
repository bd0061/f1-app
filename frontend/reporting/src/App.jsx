import { useEffect, useState } from "react";

async function request(path) {
  const response = await fetch(path, { headers: { Accept: "application/json" } });
  if (!response.ok) {
    let message = `Request failed with status ${response.status}.`;
    try {
      const data = await response.json();
      if (typeof data.error === "string") {
        message = data.error;
      }
    } catch {
      message = `Request failed with status ${response.status}.`;
    }
    throw new Error(message);
  }
  return response.json();
}

function formatDate(value) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
    new Date(`${value}T00:00:00`),
  );
}

function CountBar({ maximum, value }) {
  const width = maximum > 0 ? `${Math.max(4, (value / maximum) * 100)}%` : "0%";
  return (
    <div aria-label={`${value} purchases`} className="count-bar">
      <span style={{ width }} />
    </div>
  );
}

function EmptyRows({ colSpan, message }) {
  return (
    <tr>
      <td className="empty-cell" colSpan={colSpan}>
        {message}
      </td>
    </tr>
  );
}

const App = () => {
  const [reports, setReports] = useState(null);
  const [fromInput, setFromInput] = useState("");
  const [appliedFrom, setAppliedFrom] = useState("");
  const [error, setError] = useState("");
  const [refreshing, setRefreshing] = useState(true);
  const [lastRefreshed, setLastRefreshed] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    let isCurrent = true;

    async function loadReports() {
      setRefreshing(true);
      setError("");

      try {
        const query = appliedFrom
          ? `?${new URLSearchParams({ from: appliedFrom }).toString()}`
          : "";
        const [ticketsByRaceDay, purchasesByDate, paddockPasses] = await Promise.all([
          request("/api/reports/tickets-by-race-day"),
          request(`/api/reports/purchases-by-date${query}`),
          request("/api/reports/paddock-passes"),
        ]);

        if (isCurrent) {
          setReports({ ticketsByRaceDay, purchasesByDate, paddockPasses });
          setLastRefreshed(new Date());
        }
      } catch (requestError) {
        if (isCurrent) {
          setError(requestError.message);
        }
      } finally {
        if (isCurrent) {
          setRefreshing(false);
        }
      }
    }

    loadReports();
    const intervalId = window.setInterval(loadReports, 10_000);
    return () => {
      isCurrent = false;
      window.clearInterval(intervalId);
    };
  }, [appliedFrom, refreshKey]);

  function applyFilter(event) {
    event.preventDefault();
    setAppliedFrom(fromInput);
  }

  function clearFilter() {
    setFromInput("");
    setAppliedFrom("");
  }

  const ticketMaximum = Math.max(
    0,
    ...(reports?.ticketsByRaceDay || []).map((item) => item.tickets),
  );
  const purchaseMaximum = Math.max(
    0,
    ...(reports?.purchasesByDate || []).map((item) => item.purchases),
  );
  const paddock = reports?.paddockPasses || {
    totalPasses: 0,
    pitLane: 0,
    food: 0,
    drinks: 0,
  };

  return (
    <main className="dashboard-shell">
      <header className="dashboard-header">
        <div>
          <p className="eyebrow">Organizer portal</p>
          <h1>Race operations reporting</h1>
          <p className="header-copy">
            Local reporting data is refreshed every 10 seconds as ticketing events reach
            the reporting service.
          </p>
        </div>
        <div className="header-actions">
          <span className="refresh-status" aria-live="polite">
            {lastRefreshed
              ? `Updated ${lastRefreshed.toLocaleTimeString()}`
              : "Awaiting first update"}
          </span>
          <button className="button button-secondary" disabled={refreshing} onClick={() => setRefreshKey((key) => key + 1)} type="button">
            {refreshing ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </header>

      {error && (
        <div className="status-panel status-error" role="alert">
          {error}
        </div>
      )}
      {!reports && refreshing && (
        <div className="status-panel">Loading reporting data...</div>
      )}

      <section className="report-section" aria-labelledby="race-day-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Ticket distribution</p>
            <h2 id="race-day-title">Tickets by competition day</h2>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Competition date</th>
                <th>Purchased tickets</th>
                <th aria-label="Relative purchase volume" />
              </tr>
            </thead>
            <tbody>
              {(reports?.ticketsByRaceDay || []).map((item) => (
                <tr key={item.raceDay}>
                  <td>{formatDate(item.raceDay)}</td>
                  <td>{item.tickets}</td>
                  <td><CountBar maximum={ticketMaximum} value={item.tickets} /></td>
                </tr>
              ))}
              {reports && reports.ticketsByRaceDay.length === 0 && (
                <EmptyRows colSpan={3} message="No ticket events have reached the reporting database yet." />
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="report-section" aria-labelledby="purchases-title">
        <div className="section-heading section-heading-filter">
          <div>
            <p className="eyebrow">Purchase timing</p>
            <h2 id="purchases-title">Purchases by date</h2>
          </div>
          <form className="filter-form" onSubmit={applyFilter}>
            <label>
              Start date
              <input onChange={(event) => setFromInput(event.target.value)} type="date" value={fromInput} />
            </label>
            <button className="button button-primary" type="submit">Apply</button>
            <button className="button button-secondary" onClick={clearFilter} type="button">Clear</button>
          </form>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Purchase date</th>
                <th>Purchases</th>
                <th aria-label="Relative purchase volume" />
              </tr>
            </thead>
            <tbody>
              {(reports?.purchasesByDate || []).map((item) => (
                <tr key={item.purchaseDate}>
                  <td>{formatDate(item.purchaseDate)}</td>
                  <td>{item.purchases}</td>
                  <td><CountBar maximum={purchaseMaximum} value={item.purchases} /></td>
                </tr>
              ))}
              {reports && reports.purchasesByDate.length === 0 && (
                <EmptyRows colSpan={3} message="No purchases match this start-date filter." />
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="report-section" aria-labelledby="paddock-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Paddock uptake</p>
            <h2 id="paddock-title">Paddock pass options</h2>
          </div>
        </div>
        <div className="stat-grid">
          <article className="stat-item"><span>Total passes</span><strong>{paddock.totalPasses}</strong></article>
          <article className="stat-item"><span>Pit Lane</span><strong>{paddock.pitLane}</strong></article>
          <article className="stat-item"><span>Food</span><strong>{paddock.food}</strong></article>
          <article className="stat-item"><span>Drinks</span><strong>{paddock.drinks}</strong></article>
        </div>
      </section>
    </main>
  );
};

export default App;