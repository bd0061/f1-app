import { useEffect, useState } from "react";
import { apiRequest } from "../api.js";

const emptyCustomer = {
  firstName: "",
  lastName: "",
  address1: "",
  postalCode: "",
  city: "",
  country: "",
  email: "",
  emailConfirmation: "",
};

function formatDate(value) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "long" }).format(
    new Date(`${value}T00:00:00`),
  );
}

function formatMoney(amount, currency) {
  return `${currency || "EUR"} ${Number(amount).toFixed(2)}`;
}

const BuyTicket = () => {
  const [race, setRace] = useState(null);
  const [customer, setCustomer] = useState(emptyCustomer);
  const [selectedDays, setSelectedDays] = useState({});
  const [currency, setCurrency] = useState("");
  const [promoCode, setPromoCode] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);
  const [retryCount, setRetryCount] = useState(0);
  const [copied, setCopied] = useState("");

  useEffect(() => {
    let isCurrent = true;

    async function loadRace() {
      setLoading(true);
      setError("");

      try {
        const data = await apiRequest("/api/race");
        if (isCurrent) {
          setRace(data);
          setCurrency(data.allowedCurrencies?.[0] || "");
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

  function updateCustomer(field, value) {
    setCustomer((current) => ({ ...current, [field]: value }));
  }

  function toggleDay(date) {
    setSelectedDays((current) => {
      const next = { ...current };
      if (next[date] === undefined) {
        next[date] = "";
      } else {
        delete next[date];
      }
      return next;
    });
  }

  function updateZone(date, zone) {
    setSelectedDays((current) => ({ ...current, [date]: zone }));
  }

  async function copyCode(value, label) {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(`${label} copied.`);
    } catch {
      setCopied(`Select and copy the ${label.toLowerCase()}.`);
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");

    if (customer.email !== customer.emailConfirmation) {
      setError("Email and email confirmation must match.");
      return;
    }

    const days = Object.entries(selectedDays).map(([date, zone]) => ({
      date,
      zone,
    }));
    if (days.length === 0) {
      setError("Select at least one competition day.");
      return;
    }
    if (days.some((day) => !day.zone)) {
      setError("Choose a seating zone for every selected competition day.");
      return;
    }

    setSubmitting(true);
    try {
      const response = await apiRequest("/api/tickets", {
        method: "POST",
        body: JSON.stringify({
          customer,
          days,
          currency,
          promoCode: promoCode.trim() || null,
        }),
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
        <p className="eyebrow">Ticket confirmed</p>
        <h1>Your race ticket is ready.</h1>
        <p className="page-intro">
          Keep the registration code. You need it, together with your email, to
          modify, cancel, or add a paddock pass to this ticket.
        </p>
        <div className="result-panel status-success">
          <div className="result-grid">
            <div className="result-item">
              <span>Registration code</span>
              <strong className="code-value">{result.registrationCode}</strong>
              <button
                className="button button-secondary"
                onClick={() =>
                  copyCode(result.registrationCode, "Registration code")
                }
                type="button"
              >
                Copy code
              </button>
            </div>
            <div className="result-item">
              <span>New promo code</span>
              <strong className="code-value">{result.promoCode}</strong>
              <button
                className="button button-secondary"
                onClick={() => copyCode(result.promoCode, "Promo code")}
                type="button"
              >
                Copy code
              </button>
            </div>
            <div className="result-item">
              <span>Total price</span>
              <strong>{formatMoney(result.totalPrice, result.currency)}</strong>
            </div>
          </div>
          {copied && <p aria-live="polite">{copied}</p>}
        </div>
      </section>
    );
  }

  return (
    <section className="page-section">
      <p className="eyebrow">Ticket purchase</p>
      <h1>Build your race weekend.</h1>
      <p className="page-intro">
        Select competition days and a seating zone for each day. Final
        conversion and promo-code validation are calculated by the ticketing
        service.
      </p>

      {loading && (
        <div className="status-panel">Loading current race options...</div>
      )}
      {error && (
        <div className="status-panel status-error" role="alert">
          {error}
          {!race && (
            <div className="inline-actions">
              <button
                className="button button-secondary"
                onClick={() => setRetryCount((count) => count + 1)}
                type="button"
              >
                Retry
              </button>
            </div>
          )}
        </div>
      )}

      {!loading && race && (
        <form onSubmit={handleSubmit}>
          <div className="form-panel">
            <div className="form-heading">
              <h2>Customer details</h2>
            </div>
            <div className="form-grid">
              <label className="field-label">
                First name
                <input
                  onChange={(event) =>
                    updateCustomer("firstName", event.target.value)
                  }
                  required
                  value={customer.firstName}
                />
              </label>
              <label className="field-label">
                Last name
                <input
                  onChange={(event) =>
                    updateCustomer("lastName", event.target.value)
                  }
                  required
                  value={customer.lastName}
                />
              </label>
              <label className="field-label full-width">
                Address 1
                <input
                  onChange={(event) =>
                    updateCustomer("address1", event.target.value)
                  }
                  required
                  value={customer.address1}
                />
              </label>
              <label className="field-label">
                Postal code
                <input
                  onChange={(event) =>
                    updateCustomer("postalCode", event.target.value)
                  }
                  required
                  value={customer.postalCode}
                />
              </label>
              <label className="field-label">
                City
                <input
                  onChange={(event) =>
                    updateCustomer("city", event.target.value)
                  }
                  required
                  value={customer.city}
                />
              </label>
              <label className="field-label">
                Country
                <input
                  onChange={(event) =>
                    updateCustomer("country", event.target.value)
                  }
                  required
                  value={customer.country}
                />
              </label>
              <label className="field-label">
                Email
                <input
                  onChange={(event) =>
                    updateCustomer("email", event.target.value)
                  }
                  required
                  type="email"
                  value={customer.email}
                />
              </label>
              <label className="field-label full-width">
                Confirm email
                <input
                  onChange={(event) =>
                    updateCustomer("emailConfirmation", event.target.value)
                  }
                  required
                  type="email"
                  value={customer.emailConfirmation}
                />
              </label>
            </div>
          </div>

          <div className="form-panel">
            <div className="form-heading">
              <h2>Competition days and seating</h2>
              <p>One zone is required for every selected day.</p>
            </div>
            <div className="selection-list">
              {(race.raceDays || []).map((day) => {
                const isSelected = selectedDays[day.date] !== undefined;
                return (
                  <div className="selection-row" key={day.id || day.date}>
                    <div>
                      <label className="check-label">
                        <input
                          checked={isSelected}
                          onChange={() => toggleDay(day.date)}
                          type="checkbox"
                        />
                        {formatDate(day.date)}
                      </label>
                      <p>Base price: EUR {Number(day.basePrice).toFixed(2)}</p>
                    </div>
                    <label className="field-label">
                      Seating zone
                      <select
                        disabled={!isSelected}
                        onChange={(event) =>
                          updateZone(day.date, event.target.value)
                        }
                        value={selectedDays[day.date] || ""}
                      >
                        <option value="">Choose a zone</option>
                        {(race.seatingZones || []).map((zone) => (
                          <option key={zone.id || zone.name} value={zone.name}>
                            {zone.name} (+EUR{" "}
                            {Number(zone.surcharge).toFixed(2)})
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="form-panel">
            <div className="form-grid">
              <label className="field-label">
                Currency
                <select
                  onChange={(event) => setCurrency(event.target.value)}
                  required
                  value={currency}
                >
                  {(race.allowedCurrencies || []).map((code) => (
                    <option key={code} value={code}>
                      {code}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field-label">
                Promo code (optional)
                <input
                  onChange={(event) => setPromoCode(event.target.value)}
                  value={promoCode}
                />
              </label>
            </div>
            <p className="field-help">
              The backend validates the promo code and returns the final
              converted amount.{" "}
              {race.discountUntil && (
                <>
                  Early-discount eligibility ends on{" "}
                  {formatDate(race.discountUntil)}.
                </>
              )}
            </p>
            <div className="form-actions">
              <button
                className="button button-primary"
                disabled={submitting}
                type="submit"
              >
                {submitting ? "Purchasing..." : "Purchase ticket"}
              </button>
            </div>
          </div>
        </form>
      )}
    </section>
  );
};

export default BuyTicket;
