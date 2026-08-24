import { useEffect, useState } from "react";
import { apiRequest } from "../api.js";

function formatDate(value) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "long" }).format(
    new Date(`${value}T00:00:00`),
  );
}

const ManageTicket = () => {
  const [race, setRace] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [retryCount, setRetryCount] = useState(0);
  const [modifyForm, setModifyForm] = useState({
    registrationCode: "",
    email: "",
    currency: "",
  });
  const [addDays, setAddDays] = useState({});
  const [removeDays, setRemoveDays] = useState({});
  const [modifyError, setModifyError] = useState("");
  const [modifyResult, setModifyResult] = useState(null);
  const [modifying, setModifying] = useState(false);
  const [cancelForm, setCancelForm] = useState({
    registrationCode: "",
    email: "",
    confirmed: false,
  });
  const [cancelError, setCancelError] = useState("");
  const [cancelled, setCancelled] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  useEffect(() => {
    let isCurrent = true;

    async function loadRace() {
      setLoading(true);
      setLoadError("");
      try {
        const data = await apiRequest("/api/race");
        if (isCurrent) {
          setRace(data);
          setModifyForm((current) => ({
            ...current,
            currency: data.allowedCurrencies?.[0] || "",
          }));
        }
      } catch (requestError) {
        if (isCurrent) {
          setLoadError(requestError.message);
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

  function updateModify(field, value) {
    setModifyForm((current) => ({ ...current, [field]: value }));
  }

  function toggleAdd(date) {
    setAddDays((current) => {
      const next = { ...current };
      if (next[date] === undefined) {
        next[date] = "";
      } else {
        delete next[date];
      }
      return next;
    });
    setRemoveDays((current) => {
      const next = { ...current };
      delete next[date];
      return next;
    });
  }

  function toggleRemove(date) {
    setRemoveDays((current) => {
      const next = { ...current };
      if (next[date]) {
        delete next[date];
      } else {
        next[date] = true;
      }
      return next;
    });
    setAddDays((current) => {
      const next = { ...current };
      delete next[date];
      return next;
    });
  }

  async function handleModify(event) {
    event.preventDefault();
    setModifyError("");
    setModifyResult(null);

    const additions = Object.entries(addDays).map(([date, zone]) => ({
      date,
      zone,
    }));
    const removals = Object.keys(removeDays);
    if (additions.length === 0 && removals.length === 0) {
      setModifyError("Select at least one day to add or remove.");
      return;
    }
    if (additions.some((day) => !day.zone)) {
      setModifyError("Choose a seating zone for every added day.");
      return;
    }

    setModifying(true);
    try {
      const response = await apiRequest("/api/tickets", {
        method: "PUT",
        body: JSON.stringify({
          registrationCode: modifyForm.registrationCode,
          email: modifyForm.email,
          addDays: additions,
          removeDays: removals,
          currency: modifyForm.currency,
        }),
      });
      setModifyResult(response);
      setAddDays({});
      setRemoveDays({});
    } catch (requestError) {
      setModifyError(requestError.message);
    } finally {
      setModifying(false);
    }
  }

  async function handleCancel(event) {
    event.preventDefault();
    setCancelError("");
    setCancelled(false);

    if (!cancelForm.confirmed) {
      setCancelError(
        "Confirm that this cancellation is final before continuing.",
      );
      return;
    }

    setCancelling(true);
    try {
      const params = new URLSearchParams({
        registrationCode: cancelForm.registrationCode,
        email: cancelForm.email,
      });
      await apiRequest(`/api/tickets/cancel?${params.toString()}`, {
        method: "POST",
      });
      setCancelForm({ registrationCode: "", email: "", confirmed: false });
      setCancelled(true);
    } catch (requestError) {
      setCancelError(requestError.message);
    } finally {
      setCancelling(false);
    }
  }

  function readableDays() {
    return (modifyResult?.days || []).map((ticketDay) => {
      const day = race?.raceDays?.find(
        (item) => String(item.id) === String(ticketDay.raceDayId),
      );
      const zone = race?.seatingZones?.find(
        (item) => String(item.id) === String(ticketDay.seatingZoneId),
      );
      return `${day ? formatDate(day.date) : `Race day ${ticketDay.raceDayId}`} - ${zone?.name || `Zone ${ticketDay.seatingZoneId}`}`;
    });
  }

  return (
    <section className="page-section">
      <p className="eyebrow">Ticket management</p>
      <h1>Modify or cancel a ticket.</h1>
      <p className="page-intro">
        There is no account required. Use the registration code and the original
        email address to identify an active ticket.
      </p>

      {loading && (
        <div className="status-panel">Loading current race choices...</div>
      )}
      {loadError && (
        <div className="status-panel status-error" role="alert">
          {loadError}
          <div className="inline-actions">
            <button
              className="button button-secondary"
              onClick={() => setRetryCount((count) => count + 1)}
              type="button"
            >
              Retry
            </button>
          </div>
        </div>
      )}

      {!loading && race && (
        <>
          <form className="form-panel" onSubmit={handleModify}>
            <div className="form-heading">
              <div>
                <h2>Modify ticket</h2>
                <p>
                  Add a competition day, remove a day, or choose a new allowed
                  currency.
                </p>
              </div>
            </div>
            <div className="form-grid">
              <label className="field-label">
                Registration code
                <input
                  onChange={(event) =>
                    updateModify("registrationCode", event.target.value)
                  }
                  required
                  value={modifyForm.registrationCode}
                />
              </label>
              <label className="field-label">
                Original email
                <input
                  onChange={(event) =>
                    updateModify("email", event.target.value)
                  }
                  required
                  type="email"
                  value={modifyForm.email}
                />
              </label>
              <label className="field-label">
                Currency
                <select
                  onChange={(event) =>
                    updateModify("currency", event.target.value)
                  }
                  required
                  value={modifyForm.currency}
                >
                  {(race.allowedCurrencies || []).map((code) => (
                    <option key={code} value={code}>
                      {code}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <h3>Add days</h3>
            <div className="selection-list">
              {(race.raceDays || []).map((day) => {
                const isSelected = addDays[day.date] !== undefined;
                return (
                  <div
                    className="selection-row"
                    key={`add-${day.id || day.date}`}
                  >
                    <label className="check-label">
                      <input
                        checked={isSelected}
                        onChange={() => toggleAdd(day.date)}
                        type="checkbox"
                      />
                      {formatDate(day.date)}
                    </label>
                    <label className="field-label">
                      Seating zone
                      <select
                        disabled={!isSelected}
                        onChange={(event) =>
                          setAddDays((current) => ({
                            ...current,
                            [day.date]: event.target.value,
                          }))
                        }
                        value={addDays[day.date] || ""}
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

            <h3>Remove days</h3>
            <p className="field-help">
              Select dates you believe are currently on the ticket. The service
              validates the ticket&apos;s real selections.
            </p>
            <div className="selection-list">
              {(race.raceDays || []).map((day) => (
                <label
                  className="check-label"
                  key={`remove-${day.id || day.date}`}
                >
                  <input
                    checked={Boolean(removeDays[day.date])}
                    onChange={() => toggleRemove(day.date)}
                    type="checkbox"
                  />
                  {formatDate(day.date)}
                </label>
              ))}
            </div>

            {modifyError && (
              <div className="status-panel status-error" role="alert">
                {modifyError}
              </div>
            )}
            {modifyResult && (
              <div className="status-panel status-success" aria-live="polite">
                Updated total: {modifyResult.currency}{" "}
                {Number(modifyResult.totalPrice).toFixed(2)}.
                {readableDays().length > 0 && (
                  <ul>
                    {readableDays().map((day) => (
                      <li key={day}>{day}</li>
                    ))}
                  </ul>
                )}
              </div>
            )}
            <div className="form-actions">
              <button
                className="button button-primary"
                disabled={modifying}
                type="submit"
              >
                {modifying ? "Updating..." : "Update ticket"}
              </button>
            </div>
          </form>

          <form className="form-panel" onSubmit={handleCancel}>
            <div className="form-heading">
              <div>
                <h2>Cancel ticket</h2>
                <p>
                  Cancellation is permanent and invalidates the ticket&apos;s
                  promo code.
                </p>
              </div>
            </div>
            <div className="form-grid">
              <label className="field-label">
                Registration code
                <input
                  onChange={(event) =>
                    setCancelForm((current) => ({
                      ...current,
                      registrationCode: event.target.value,
                    }))
                  }
                  required
                  value={cancelForm.registrationCode}
                />
              </label>
              <label className="field-label">
                Original email
                <input
                  onChange={(event) =>
                    setCancelForm((current) => ({
                      ...current,
                      email: event.target.value,
                    }))
                  }
                  required
                  type="email"
                  value={cancelForm.email}
                />
              </label>
            </div>
            <label className="check-label">
              <input
                checked={cancelForm.confirmed}
                onChange={(event) =>
                  setCancelForm((current) => ({
                    ...current,
                    confirmed: event.target.checked,
                  }))
                }
                type="checkbox"
              />
              I understand this ticket cannot be reactivated.
            </label>
            {cancelError && (
              <div className="status-panel status-error" role="alert">
                {cancelError}
              </div>
            )}
            {cancelled && (
              <div className="status-panel status-success" aria-live="polite">
                Ticket cancelled. It cannot be reactivated and its promo code is
                now invalid.
              </div>
            )}
            <div className="form-actions">
              <button
                className="button button-danger"
                disabled={cancelling}
                type="submit"
              >
                {cancelling ? "Cancelling..." : "Cancel ticket"}
              </button>
            </div>
          </form>
        </>
      )}
    </section>
  );
};

export default ManageTicket;
