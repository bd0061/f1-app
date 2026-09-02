import { useEffect, useState } from "react";
import { apiRequest } from "../api.js";

const blankRaceDay = () => ({ date: "", basePrice: "", capacity: "" });
const blankZone = () => ({
  name: "",
  characteristics: "",
  capacity: "",
  surcharge: "",
});

function toForm(race) {
  return {
    name: race.name || "",
    location: race.location || "",
    startDate: race.startDate || "",
    endDate: race.endDate || "",
    additionalInformation: race.additionalInformation || "",
    discountUntil: race.discountUntil || "",
    raceDays: (race.raceDays || []).map((day) => ({
      date: day.date,
      basePrice: String(day.basePrice),
      capacity: String(day.capacity),
    })),
    seatingZones: (race.seatingZones || []).map((zone) => ({
      name: zone.name,
      characteristics: zone.characteristics,
      capacity: String(zone.capacity),
      surcharge: String(zone.surcharge),
    })),
    allowedCurrencies: (race.allowedCurrencies || []).join(", "),
  };
}

function validateForm(form) {
  if (
    !form.name.trim() ||
    !form.location.trim() ||
    !form.startDate ||
    !form.endDate ||
    !form.discountUntil
  ) {
    return "Complete the race name, location, event dates, and discount expiration date.";
  }
  if (form.startDate > form.endDate) {
    return "The start date must be on or before the end date.";
  }
  if (form.raceDays.length === 0) {
    return "At least one race day is required.";
  }

  const dates = new Set();
  for (const day of form.raceDays) {
    if (
      !day.date ||
      dates.has(day.date) ||
      day.date < form.startDate ||
      day.date > form.endDate
    ) {
      return "Race days need unique dates within the event date range.";
    }
    if (
      day.basePrice === "" ||
      Number(day.basePrice) < 0 ||
      !Number.isFinite(Number(day.basePrice))
    ) {
      return "Every race day needs a nonnegative base price.";
    }
    if (Number(day.capacity) <= 0 || !Number.isInteger(Number(day.capacity))) {
      return "Every race day needs a positive whole-number capacity.";
    }
    dates.add(day.date);
  }

  if (form.seatingZones.length === 0) {
    return "At least one seating zone is required.";
  }
  const names = new Set();
  for (const zone of form.seatingZones) {
    const name = zone.name.trim().toLowerCase();
    if (!name || names.has(name)) {
      return "Seating zones need unique, nonblank names.";
    }
    if (
      Number(zone.capacity) <= 0 ||
      !Number.isInteger(Number(zone.capacity))
    ) {
      return "Every seating zone needs a positive whole-number capacity.";
    }
    if (
      zone.surcharge === "" ||
      Number(zone.surcharge) < 0 ||
      !Number.isFinite(Number(zone.surcharge))
    ) {
      return "Every seating zone needs a nonnegative surcharge.";
    }
    names.add(name);
  }

  const codes = form.allowedCurrencies
    .split(",")
    .map((code) => code.trim().toUpperCase())
    .filter(Boolean);
  if (codes.length === 0 || codes.some((code) => !/^[A-Z]{3}$/.test(code))) {
    return "Enter at least one three-letter uppercase currency code.";
  }

  return "";
}

const Admin = () => {
  const [form, setForm] = useState({
    name: "",
    location: "",
    startDate: "",
    endDate: "",
    additionalInformation: "",
    discountUntil: "",
    raceDays: [blankRaceDay()],
    seatingZones: [blankZone()],
    allowedCurrencies: "",
  });
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [retryCount, setRetryCount] = useState(0);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let isCurrent = true;
    async function loadRace() {
      setLoading(true);
      setLoadError("");
      try {
        const race = await apiRequest("/api/race");
        if (isCurrent) {
          setForm(toForm(race));
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

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function updateRow(collection, index, field, value) {
    setForm((current) => ({
      ...current,
      [collection]: current[collection].map((item, itemIndex) =>
        itemIndex === index ? { ...item, [field]: value } : item,
      ),
    }));
  }

  function addRow(collection, value) {
    setForm((current) => ({
      ...current,
      [collection]: [...current[collection], value],
    }));
  }

  function removeRow(collection, index) {
    setForm((current) => ({
      ...current,
      [collection]: current[collection].filter(
        (_, itemIndex) => itemIndex !== index,
      ),
    }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");
    setSuccess("");

    const validationError = validateForm(form);
    if (validationError) {
      setError(validationError);
      return;
    }

    const allowedCurrencies = [
      ...new Set(
        form.allowedCurrencies
          .split(",")
          .map((code) => code.trim().toUpperCase())
          .filter(Boolean),
      ),
    ];
    const payload = {
      name: form.name.trim(),
      location: form.location.trim(),
      startDate: form.startDate,
      endDate: form.endDate,
      additionalInformation: form.additionalInformation,
      discountUntil: form.discountUntil,
      raceDays: form.raceDays.map((day) => ({
        date: day.date,
        basePrice: Number(day.basePrice),
        capacity: Number(day.capacity),
      })),
      seatingZones: form.seatingZones.map((zone) => ({
        name: zone.name.trim(),
        characteristics: zone.characteristics.trim(),
        capacity: Number(zone.capacity),
        surcharge: Number(zone.surcharge),
      })),
      allowedCurrencies,
    };

    setSubmitting(true);
    try {
      const updated = await apiRequest("/api/admin/race", {
        method: "PUT",
        body: JSON.stringify(payload),
      });
      setForm(toForm(updated));
      setSuccess("Race configuration saved.");
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="page-section">
      <p className="eyebrow">Administration</p>
      <h1>Configure the race weekend.</h1>
      <p className="page-intro">
        This page replaces the active race, its days, seating zones, and allowed
        currencies. No login layer is required for this project.
      </p>

      {loading && (
        <div className="status-panel">Loading current configuration...</div>
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

      {!loading && !loadError && (
        <form onSubmit={handleSubmit}>
          <div className="form-panel">
            <h2>Race details</h2>
            <div className="form-grid">
              <label className="field-label">
                Name
                <input
                  onChange={(event) => updateField("name", event.target.value)}
                  required
                  value={form.name}
                />
              </label>
              <label className="field-label">
                Location
                <input
                  onChange={(event) =>
                    updateField("location", event.target.value)
                  }
                  required
                  value={form.location}
                />
              </label>
              <label className="field-label">
                Start date
                <input
                  onChange={(event) =>
                    updateField("startDate", event.target.value)
                  }
                  required
                  type="date"
                  value={form.startDate}
                />
              </label>
              <label className="field-label">
                End date
                <input
                  onChange={(event) =>
                    updateField("endDate", event.target.value)
                  }
                  required
                  type="date"
                  value={form.endDate}
                />
              </label>
              <label className="field-label">
                Discount expiration date
                <input
                  onChange={(event) =>
                    updateField("discountUntil", event.target.value)
                  }
                  required
                  type="date"
                  value={form.discountUntil}
                />
              </label>
              <label className="field-label full-width">
                Additional information
                <textarea
                  onChange={(event) =>
                    updateField("additionalInformation", event.target.value)
                  }
                  value={form.additionalInformation}
                />
              </label>
            </div>
          </div>

          <div className="form-panel">
            <div className="form-heading">
              <h2>Race days</h2>
              <button
                className="button button-secondary"
                onClick={() => addRow("raceDays", blankRaceDay())}
                type="button"
              >
                Add day
              </button>
            </div>
            {form.raceDays.map((day, index) => (
              <div className="row-editor" key={`day-${index}`}>
                <label className="field-label">
                  Date
                  <input
                    onChange={(event) =>
                      updateRow("raceDays", index, "date", event.target.value)
                    }
                    required
                    type="date"
                    value={day.date}
                  />
                </label>
                <label className="field-label">
                  Base price (EUR)
                  <input
                    min="0"
                    onChange={(event) =>
                      updateRow(
                        "raceDays",
                        index,
                        "basePrice",
                        event.target.value,
                      )
                    }
                    required
                    step="0.01"
                    type="number"
                    value={day.basePrice}
                  />
                </label>
                <label className="field-label">
                  Capacity
                  <input
                    min="1"
                    onChange={(event) =>
                      updateRow(
                        "raceDays",
                        index,
                        "capacity",
                        event.target.value,
                      )
                    }
                    required
                    step="1"
                    type="number"
                    value={day.capacity}
                  />
                </label>
                <button
                  className="remove-button"
                  disabled={form.raceDays.length === 1}
                  onClick={() => removeRow("raceDays", index)}
                  type="button"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>

          <div className="form-panel">
            <div className="form-heading">
              <h2>Seating zones</h2>
              <button
                className="button button-secondary"
                onClick={() => addRow("seatingZones", blankZone())}
                type="button"
              >
                Add zone
              </button>
            </div>
            {form.seatingZones.map((zone, index) => (
              <div className="row-editor" key={`zone-${index}`}>
                <label className="field-label">
                  Name
                  <input
                    onChange={(event) =>
                      updateRow(
                        "seatingZones",
                        index,
                        "name",
                        event.target.value,
                      )
                    }
                    required
                    value={zone.name}
                  />
                </label>
                <label className="field-label">
                  Characteristics
                  <input
                    onChange={(event) =>
                      updateRow(
                        "seatingZones",
                        index,
                        "characteristics",
                        event.target.value,
                      )
                    }
                    required
                    value={zone.characteristics}
                  />
                </label>
                <label className="field-label">
                  Capacity
                  <input
                    min="1"
                    onChange={(event) =>
                      updateRow(
                        "seatingZones",
                        index,
                        "capacity",
                        event.target.value,
                      )
                    }
                    required
                    step="1"
                    type="number"
                    value={zone.capacity}
                  />
                </label>
                <label className="field-label">
                  Surcharge (EUR)
                  <input
                    min="0"
                    onChange={(event) =>
                      updateRow(
                        "seatingZones",
                        index,
                        "surcharge",
                        event.target.value,
                      )
                    }
                    required
                    step="0.01"
                    type="number"
                    value={zone.surcharge}
                  />
                </label>
                <button
                  className="remove-button"
                  disabled={form.seatingZones.length === 1}
                  onClick={() => removeRow("seatingZones", index)}
                  type="button"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>

          <div className="form-panel">
            <label className="field-label">
              Allowed currencies (comma separated, three-letter codes)
              <input
                onChange={(event) =>
                  updateField(
                    "allowedCurrencies",
                    event.target.value.toUpperCase(),
                  )
                }
                required
                value={form.allowedCurrencies}
              />
            </label>
          </div>

          {error && (
            <div className="status-panel status-error" role="alert">
              {error}
            </div>
          )}
          {success && (
            <div className="status-panel status-success" aria-live="polite">
              {success}
            </div>
          )}
          <div className="form-actions">
            <button
              className="button button-primary"
              disabled={submitting}
              type="submit"
            >
              {submitting ? "Saving..." : "Save race configuration"}
            </button>
          </div>
        </form>
      )}
    </section>
  );
};

export default Admin;
