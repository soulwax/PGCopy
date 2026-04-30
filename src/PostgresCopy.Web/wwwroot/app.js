const form = document.querySelector("#copy-form");
const runButton = document.querySelector("#run");
const cancelButton = document.querySelector("#cancel");
const clearButton = document.querySelector("#clear");
const log = document.querySelector("#log");
const statusBadge = document.querySelector("#status");
const summary = document.querySelector("#summary");
const truncateDestination = document.querySelector("#truncate-destination");
const truncateConfirmWrap = document.querySelector("#truncate-confirm-wrap");
const truncateConfirmation = document.querySelector("#truncate-confirmation");
const dryRun = document.querySelector("#dry-run");
const verify = document.querySelector("#verify");
const schema = document.querySelector("#schema");
const tables = document.querySelector("#tables");
const readyMode = document.querySelector("#ready-mode");
const readySchema = document.querySelector("#ready-schema");
const readyTables = document.querySelector("#ready-tables");
const readyDestination = document.querySelector("#ready-destination");

let runStartedAt = 0;
let activeController = null;

form.addEventListener("input", updateReadiness);

truncateDestination.addEventListener("change", () => {
  truncateConfirmWrap.classList.toggle("hidden", !truncateDestination.checked);
  truncateConfirmation.required = truncateDestination.checked;

  if (!truncateDestination.checked) {
    truncateConfirmation.value = "";
  }

  updateReadiness();
});

clearButton.addEventListener("click", () => {
  log.replaceChildren();
  summary.textContent = "No run yet";
  setStatus("Idle", "idle");
});

cancelButton.addEventListener("click", () => {
  if (!activeController) {
    return;
  }

  setStatus("Cancelling", "cancelling");
  summary.textContent = `Cancelling after ${elapsedSeconds()}s`;
  cancelButton.disabled = true;
  activeController.abort();
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  log.replaceChildren();
  runStartedAt = Date.now();
  setStatus("Running", "running");
  summary.textContent = "Starting migration";
  runButton.disabled = true;
  clearButton.disabled = true;
  cancelButton.disabled = false;
  activeController = new AbortController();

  const payload = {
    origin: form.origin.value.trim(),
    destination: form.destination.value.trim(),
    schema: form.schema.value.trim() || "public",
    tables: form.tables.value.trim(),
    dryRun: form.dryRun.checked,
    verify: form.verify.checked,
    truncateDestination: form.truncateDestination.checked,
    truncateConfirmation: form.truncateConfirmation.value.trim()
  };

  try {
    const response = await fetch("/api/migrations", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
      signal: activeController.signal
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
    if (error.name === "AbortError") {
      addLogItem({ kind: "error", message: "Migration cancelled." });
      setStatus("Failed", "failed");
      summary.textContent = `Cancelled after ${elapsedSeconds()}s`;
    } else {
      addLogItem({ kind: "error", message: error.message || "Migration failed." });
      setStatus("Failed", "failed");
      summary.textContent = `Failed after ${elapsedSeconds()}s`;
    }
  } finally {
    runButton.disabled = false;
    cancelButton.disabled = true;
    clearButton.disabled = false;
    activeController = null;
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

function updateReadiness() {
  const isDryRun = dryRun.checked;
  const tableText = tables.value
    .split(",")
    .map((value) => value.trim())
    .filter(Boolean)
    .join(", ");

  readyMode.textContent = isDryRun ? "Dry run" : "Copy";
  readySchema.textContent = schema.value.trim() || "public";
  readyTables.textContent = tableText || "All";
  readyDestination.textContent = truncateDestination.checked ? "Truncate" : "Preserve";
  runButton.textContent = isDryRun ? "Run dry run" : "Run copy";
  verify.disabled = isDryRun;
}

updateReadiness();
