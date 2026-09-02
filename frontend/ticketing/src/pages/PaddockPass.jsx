import { useState } from "react";
import { apiRequest } from "../api.js";

const PaddockPass = () => {
  const [registrationCode, setRegistrationCode] = useState("");
  const [options, setOptions] = useState({
    pitLane: false,
    food: false,
    drinks: false,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);

  const estimate =
    100 +
    (options.pitLane ? 50 : 0) +
    (options.food ? 25 : 0) +
    (options.drinks ? 25 : 0);

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");
    setSubmitting(true);

    try {
      const response = await apiRequest("/api/paddock-passes", {
        method: "POST",
        body: JSON.stringify({ registrationCode, ...options }),
      });
      setResult(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <section className="page-section">
        <p className="eyebrow">Paddock pass confirmed</p>
        <h1>Access options saved.</h1>
        <div className="result-panel status-success">
          <div className="result-grid">
            <div className="result-item">
              <span>Pass ID</span>
              <strong className="code-value">{result.id}</strong>
            </div>
            <div className="result-item">
              <span>Final total</span>
              <strong>{Number(result.totalPrice).toFixed(2)}</strong>
            </div>
            <div className="result-item">
              <span>Selected options</span>
              <strong>
                Garage Access{result.pitLane ? ", Pit Lane" : ""}
                {result.food ? ", Food" : ""}
                {result.drinks ? ", Drinks" : ""}
              </strong>
            </div>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="page-section">
      <p className="eyebrow">Paddock pass</p>
      <h1>Add access to an active ticket.</h1>
      <p className="page-intro">
        Garage Access is required. This is a EUR estimate; the service applies
        any early discount and uses the currency attached to the existing
        ticket.
      </p>
      <form className="form-panel" onSubmit={handleSubmit}>
        <label className="field-label">
          Registration code
          <input
            onChange={(event) => setRegistrationCode(event.target.value)}
            required
            value={registrationCode}
          />
        </label>
        <div className="selection-list">
          <label className="check-label">
            <input checked disabled type="checkbox" />
            Garage Access - EUR 100
          </label>
          <label className="check-label">
            <input
              checked={options.pitLane}
              onChange={(event) =>
                setOptions((current) => ({
                  ...current,
                  pitLane: event.target.checked,
                }))
              }
              type="checkbox"
            />
            Pit Lane - EUR 50
          </label>
          <label className="check-label">
            <input
              checked={options.food}
              onChange={(event) =>
                setOptions((current) => ({
                  ...current,
                  food: event.target.checked,
                }))
              }
              type="checkbox"
            />
            Food - EUR 25
          </label>
          <label className="check-label">
            <input
              checked={options.drinks}
              onChange={(event) =>
                setOptions((current) => ({
                  ...current,
                  drinks: event.target.checked,
                }))
              }
              type="checkbox"
            />
            Drinks / Beverages - EUR 25
          </label>
        </div>
        <div className="status-panel">
          Estimated EUR subtotal: <strong>EUR {estimate.toFixed(2)}</strong>
        </div>
        {error && (
          <div className="status-panel status-error" role="alert">
            {error}
          </div>
        )}
        <div className="form-actions">
          <button
            className="button button-primary"
            disabled={submitting}
            type="submit"
          >
            {submitting ? "Purchasing..." : "Purchase paddock pass"}
          </button>
        </div>
      </form>
    </section>
  );
};

export default PaddockPass;
