const form = document.querySelector("#copy-form");
const runButton = document.querySelector("#run");
const clearButton = document.querySelector("#clear");
const log = document.querySelector("#log");
const statusBadge = document.querySelector("#status");
const summary = document.querySelector("#summary");

let runStartedAt = 0;

clearButton.addEventListener("click", () => {
  log.replaceChildren();
  summary.textContent = "No run yet";
  setStatus("Idle", "idle");
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  log.replaceChildren();
  runStartedAt = Date.now();
  setStatus("Running", "running");
  summary.textContent = "Starting migration";
  runButton.disabled = true;

  const payload = {
    origin: form.origin.value.trim(),
    destination: form.destination.value.trim(),
    schema: form.schema.value.trim() || "public",
    tables: form.tables.value.trim(),
    dryRun: form.dryRun.checked
  };

  try {
    const response = await fetch("/api/migrations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!response.ok || !response.body) {
      throw new Error(`Request failed with HTTP ${response.status}`);
    }

    await readEvents(response.body);

    if (!log.querySelector(".error")) {
      setStatus("Done", "done");
      summary.textContent = `Finished in ${elapsedSeconds()}s`;
    }
  } catch (error) {
    addLogItem({ kind: "error", message: error.message || "Migration failed." });
    setStatus("Failed", "failed");
    summary.textContent = `Failed after ${elapsedSeconds()}s`;
  } finally {
    runButton.disabled = false;
  }
});

async function readEvents(body) {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split("\n");
    buffer = lines.pop() || "";

    for (const line of lines) {
      if (!line.trim()) {
        continue;
      }

      const migrationEvent = JSON.parse(line);
      addLogItem(migrationEvent);

      if (migrationEvent.kind === "error") {
        setStatus("Failed", "failed");
        summary.textContent = `Failed after ${elapsedSeconds()}s`;
      } else {
        summary.textContent = `${log.children.length} operation(s), ${elapsedSeconds()}s`;
      }
    }
  }
}

function addLogItem(migrationEvent) {
  const item = document.createElement("li");
  item.className = migrationEvent.kind;

  const kind = document.createElement("span");
  kind.className = "kind";
  kind.textContent = migrationEvent.kind;

  const message = document.createElement("span");
  message.className = "message";
  message.textContent = migrationEvent.message;

  const rows = document.createElement("span");
  rows.className = "rows";
  rows.textContent = typeof migrationEvent.rows === "number"
    ? `${migrationEvent.rows.toLocaleString()} rows`
    : "";

  item.append(kind, message, rows);
  log.append(item);
  item.scrollIntoView({ block: "nearest" });
}

function setStatus(text, className) {
  statusBadge.textContent = text;
  statusBadge.className = `status ${className}`;
}

function elapsedSeconds() {
  return ((Date.now() - runStartedAt) / 1000).toFixed(1);
}
