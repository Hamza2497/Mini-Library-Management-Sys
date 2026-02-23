const GOOGLE_CLIENT_ID =
  (localStorage.getItem("google_client_id") || "").trim() ||
  "308905289637-064rj6fgrqlcebmbh1gg1v2ub20gc93p.apps.googleusercontent.com";
const API_BASE = resolveApiBase();
const TOKEN_KEY = "id_token";
const THEME_KEY = "theme";

const statusBox = document.getElementById("statusBox");
const booksBody = document.getElementById("booksBody");
const googleSignInEl = document.getElementById("googleSignIn");
const userEmailEl = document.getElementById("userEmail");
const signOutBtn = document.getElementById("signOutBtn");

const navLibrary = document.getElementById("navLibrary");
const navDashboard = document.getElementById("navDashboard");
const libraryView = document.getElementById("libraryView");
const dashboardView = document.getElementById("dashboardView");

const searchInput = document.getElementById("searchInput");
const availabilitySelect = document.getElementById("availabilitySelect");
const addBookForm = document.getElementById("addBookForm");

const editDialog = document.getElementById("editDialog");
const editBookForm = document.getElementById("editBookForm");
const editTitleInput = document.getElementById("editTitleInput");
const editAuthorInput = document.getElementById("editAuthorInput");
const detailsDialog = document.getElementById("detailsDialog");
const detailsTitle = document.getElementById("detailsTitle");
const detailsCategory = document.getElementById("detailsCategory");
const detailsTags = document.getElementById("detailsTags");
const detailsDescription = document.getElementById("detailsDescription");

let currentRoles = [];
let editBookId = null;

function resolveApiBase() {
  const configured = (localStorage.getItem("api_base_url") || "").trim();
  if (configured) {
    return configured.replace(/\/+$/, "");
  }

  const isLocalHost =
    window.location.hostname === "localhost" ||
    window.location.hostname === "127.0.0.1";

  return isLocalHost ? "http://localhost:5099" : "https://mini-library-api-po6c.onrender.com";
}

function decodeJwtPayload(token) {
  try {
    const payload = token.split(".")[1];
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(base64));
  } catch {
    return null;
  }
}

function getToken() {
  return localStorage.getItem(TOKEN_KEY) || "";
}

function hasRole(role) {
  return currentRoles.includes(role);
}

function canManageBooks() {
  return hasRole("Admin") || hasRole("Librarian");
}

function canDeleteBooks() {
  return hasRole("Admin");
}

function setStatus(message, isError = false) {
  statusBox.textContent = message;
  statusBox.className = isError ? "status error" : "status ok";
}

function applyTheme(theme) {
  document.documentElement.setAttribute("data-theme", theme);
  document.getElementById("themeToggle").textContent = theme === "dark" ? "Light" : "Dark";
}

function initTheme() {
  const saved = localStorage.getItem(THEME_KEY) || "light";
  applyTheme(saved);
  document.getElementById("themeToggle").addEventListener("click", () => {
    const current = document.documentElement.getAttribute("data-theme") || "light";
    const next = current === "dark" ? "light" : "dark";
    localStorage.setItem(THEME_KEY, next);
    applyTheme(next);
  });
}

async function loadProfile() {
  const token = getToken();
  if (!token) {
    userEmailEl.textContent = "Not signed in";
    currentRoles = [];
    navDashboard.classList.add("hidden");
    addBookForm.classList.add("hidden");
    return;
  }

  try {
    const me = await apiFetch("/api/me", { method: "GET" });
    userEmailEl.textContent = me.email || "Signed in";
    currentRoles = me.roles || [];
  } catch {
    const payload = decodeJwtPayload(token);
    userEmailEl.textContent = payload?.email || "Signed in";
    currentRoles = [];
  }

  if (canManageBooks()) {
    navDashboard.classList.remove("hidden");
    addBookForm.classList.remove("hidden");
  } else {
    navDashboard.classList.add("hidden");
    addBookForm.classList.add("hidden");
    showLibrary();
  }
}

function updateAuthButtons() {
  const signedIn = Boolean(getToken());
  googleSignInEl.classList.toggle("hidden", signedIn);
  signOutBtn.classList.toggle("hidden", !signedIn);
}

function showLibrary() {
  libraryView.classList.remove("hidden");
  dashboardView.classList.add("hidden");
}

function showDashboard() {
  if (!canManageBooks()) {
    setStatus("403 Admin/Librarian only.", true);
    return;
  }

  libraryView.classList.add("hidden");
  dashboardView.classList.remove("hidden");
}

function authHeaders() {
  const token = getToken();
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  return headers;
}

async function apiFetch(path, options = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: { ...authHeaders(), ...(options.headers || {}) }
  });

  const text = await response.text();
  let json;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = text;
  }

  if (!response.ok) {
    const message =
      (json && json.message) ||
      (typeof json === "string" && json.trim().length > 0 ? json.split("\n")[0] : "") ||
      response.statusText ||
      "Request failed";
    throw new Error(`${response.status} ${message}`);
  }

  return json;
}

function renderBooks(items) {
  booksBody.innerHTML = "";

  for (const b of items) {
    const tr = document.createElement("tr");

    let actions = b.isAvailable
      ? `<button data-action="checkout" data-id="${b.id}">Checkout</button>`
      : `<button data-action="checkin" data-id="${b.id}">Checkin</button>`;

    if (canManageBooks()) {
      actions += ` <button data-action="edit" data-id="${b.id}" data-title="${encodeURIComponent(b.title)}" data-author="${encodeURIComponent(b.author)}">Edit</button>`;
      actions += ` <button data-action="enrich" data-id="${b.id}">Enrich</button>`;
    }

    if (canDeleteBooks()) {
      actions += ` <button data-action="delete" data-id="${b.id}" class="secondary">Delete</button>`;
    }

    actions += ` <button data-action="details" data-title="${encodeURIComponent(b.title)}" data-category="${encodeURIComponent(b.category || "")}" data-tags="${encodeURIComponent((b.tags || []).join(", "))}" data-description="${encodeURIComponent(b.description || "")}">Details</button>`;

    tr.innerHTML = `
      <td>${b.title}</td>
      <td>${b.author}</td>
      <td>${b.category ? `<span class="category-badge">${b.category}</span>` : "-"}</td>
      <td>${b.isAvailable ? "Yes" : "No"}</td>
      <td class="actions">${actions}</td>
    `;

    booksBody.appendChild(tr);
  }
}

async function loadBooks() {
  try {
    const q = encodeURIComponent(searchInput.value.trim());
    const selected = availabilitySelect.value;
    const availablePart = selected === "all" ? "" : `&available=${selected}`;
    const data = await apiFetch(`/api/books?q=${q}${availablePart}&page=1&pageSize=20`, { method: "GET" });
    renderBooks(data.items || []);
    setStatus(`Loaded ${data.items?.length || 0} / ${data.total ?? 0} books.`);
  } catch (err) {
    setStatus(err.message, true);
  }
}

async function checkoutBook(bookId) {
  try {
    await apiFetch(`/api/books/${bookId}/checkout`, { method: "POST", body: JSON.stringify({}) });
    setStatus("Checked out.");
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

async function checkinBook(bookId) {
  try {
    await apiFetch(`/api/books/${bookId}/checkin`, { method: "POST" });
    setStatus("Checked in.");
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

function openEditDialog(bookId, title, author) {
  editBookId = bookId;
  editTitleInput.value = title;
  editAuthorInput.value = author;
  editDialog.showModal();
}

async function saveEdit(event) {
  event.preventDefault();
  if (!editBookId) return;

  const title = editTitleInput.value.trim();
  const author = editAuthorInput.value.trim();
  if (!title || !author) {
    setStatus("Title and Author are required.", true);
    return;
  }

  try {
    await apiFetch(`/api/books/${editBookId}`, {
      method: "PUT",
      body: JSON.stringify({ title, author })
    });
    editDialog.close();
    setStatus("Updated.");
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

async function deleteBook(bookId) {
  if (!confirm("Delete this book?")) return;

  try {
    await apiFetch(`/api/books/${bookId}`, { method: "DELETE" });
    setStatus("Deleted.");
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

async function enrichBook(bookId) {
  try {
    await apiFetch(`/api/books/${bookId}/ai/enrich`, { method: "POST" });
    setStatus("Enriched.");
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

function openDetails(title, category, tags, description) {
  detailsTitle.textContent = `${title} Details`;
  detailsCategory.textContent = category || "-";
  detailsTags.textContent = tags || "-";
  detailsDescription.textContent = description || "-";
  detailsDialog.showModal();
}

async function addBook(event) {
  event.preventDefault();

  const title = document.getElementById("titleInput").value.trim();
  const author = document.getElementById("authorInput").value.trim();
  if (!title || !author) {
    setStatus("Title and Author are required.", true);
    return;
  }

  try {
    await apiFetch("/api/books", {
      method: "POST",
      body: JSON.stringify({ title, author })
    });

    addBookForm.reset();
    setStatus("Book added.");
    showLibrary();
    await loadBooks();
  } catch (err) {
    setStatus(err.message, true);
  }
}

function initGoogle(attempt = 0) {
  if (!window.google || !window.google.accounts || !window.google.accounts.id) {
    if (attempt < 40) {
      setTimeout(() => initGoogle(attempt + 1), 250);
      return;
    }

    setStatus("Google Sign-In failed to load. Refresh the page.", true);
    return;
  }

  window.google.accounts.id.initialize({
    client_id: GOOGLE_CLIENT_ID,
    callback: async (response) => {
      localStorage.setItem(TOKEN_KEY, response.credential);
      updateAuthButtons();
      await loadProfile();
      await loadBooks();
    }
  });

  googleSignInEl.innerHTML = "";
  window.google.accounts.id.renderButton(googleSignInEl, {
    theme: "outline",
    size: "large"
  });
}

navLibrary.addEventListener("click", (e) => {
  e.preventDefault();
  showLibrary();
});

navDashboard.addEventListener("click", (e) => {
  e.preventDefault();
  showDashboard();
});

document.getElementById("searchBtn").addEventListener("click", loadBooks);
addBookForm.addEventListener("submit", addBook);

booksBody.addEventListener("click", (e) => {
  const button = e.target.closest("button[data-action]");
  if (!button) return;

  const { action, id, title, author } = button.dataset;
  if (action === "checkout") checkoutBook(id);
  if (action === "checkin") checkinBook(id);
  if (action === "edit") openEditDialog(id, decodeURIComponent(title), decodeURIComponent(author));
  if (action === "enrich") enrichBook(id);
  if (action === "delete") deleteBook(id);
  if (action === "details") openDetails(
    decodeURIComponent(button.dataset.title || ""),
    decodeURIComponent(button.dataset.category || ""),
    decodeURIComponent(button.dataset.tags || ""),
    decodeURIComponent(button.dataset.description || "")
  );
});

editBookForm.addEventListener("submit", saveEdit);
document.getElementById("cancelEditBtn").addEventListener("click", () => editDialog.close());
document.getElementById("closeDetailsBtn").addEventListener("click", () => detailsDialog.close());

signOutBtn.addEventListener("click", async () => {
  localStorage.removeItem(TOKEN_KEY);
  currentRoles = [];
  updateAuthButtons();
  await loadProfile();
  setStatus("Signed out.");
});

initTheme();
updateAuthButtons();
initGoogle();
showLibrary();
loadProfile().then(loadBooks);
