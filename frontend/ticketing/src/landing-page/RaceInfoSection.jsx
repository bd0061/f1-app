import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiRequest } from "../api.js";

function formatDate(value) {
  if (!value) {
    return "Not available";
  }

  return new Intl.DateTimeFormat(undefined, { dateStyle: "long" }).format(
    new Date(`${value}T00:00:00`),
  );
}

const RaceInfoSection = () => {
  const [race, setRace] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [retryCount, setRetryCount] = useState(0);

  useEffect(() => {
    let isCurrent = true;

    async function loadRace() {
      setLoading(true);
      setError("");

      try {
        const data = await apiRequest("/api/race");
        if (isCurrent) {
          setRace(data);
        }
      } catch (requestError) {
        if (isCurrent) {
          setError(requestError.message);
        }
      } finally {
        if (isCurrent) {
          setLoading(false);
        }
      }
    }

    loadRace();
    return () => {
      isCurrent = false;
    };
  }, [retryCount]);

  return (
    <section className="page-section" id="race-information">
      <p className="eyebrow">Live race information</p>
      <h2>{race?.name ?? "Upcoming race weekend"}</h2>
      <p className="page-intro">
        Current race configuration comes directly from the ticketing service.
      </p>

      {loading && (
        <div className="status-panel">Loading race information...</div>
      )}

      {error && (
        <div className="status-panel status-error" role="alert">
          <p>Unable to load race information: {error}</p>
          <button
            className="button button-secondary"
            onClick={() => setRetryCount((count) => count + 1)}
            type="button"
          >
            Retry
          </button>
        </div>
      )}

      {!loading && !error && !race && (
        <div className="status-panel">
          No race information is available yet.
        </div>
      )}

      {!loading && !error && race && (
        <div className="info-panel">
          <div className="info-grid">
            <div className="info-item">
              <span>Location</span>
              <strong>{race.location}</strong>
            </div>
            <div className="info-item">
              <span>Start date</span>
              <strong>{formatDate(race.startDate)}</strong>
            </div>
            <div className="info-item">
              <span>End date</span>
              <strong>{formatDate(race.endDate)}</strong>
            </div>
          </div>
          <p>
            {race.additionalInformation ||
              "Race details will be announced soon."}
          </p>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Competition day</th>
                  <th>Base price</th>
                  <th>Capacity</th>
                </tr>
              </thead>
              <tbody>
                {(race.raceDays || []).map((day) => (
                  <tr key={day.id || day.date}>
                    <td>{formatDate(day.date)}</td>
                    <td>EUR {Number(day.basePrice).toFixed(2)}</td>
                    <td>{day.capacity}</td>
                  </tr>
                ))}
                {(race.raceDays || []).length === 0 && (
                  <tr>
                    <td colSpan="3">No competition days are available yet.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="form-actions">
            <Link className="button button-primary" to="/buy-ticket">
              Buy a ticket
            </Link>
          </div>
        </div>
      )}
    </section>
  );
};

export default RaceInfoSection;
