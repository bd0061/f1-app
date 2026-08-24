function normalizeJsonKeys(value) {
  if (Array.isArray(value)) {
    return value.map(normalizeJsonKeys);
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, childValue]) => [
        `${key.charAt(0).toLowerCase()}${key.slice(1)}`,
        normalizeJsonKeys(childValue),
      ]),
    );
  }

  return value;
}

export async function apiRequest(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      Accept: "application/json",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...options.headers,
    },
  });

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

  if (response.status === 204) {
    return null;
  }

  if (!response.headers.get("content-type")?.includes("application/json")) {
    return null;
  }

  return normalizeJsonKeys(await response.json());
}
