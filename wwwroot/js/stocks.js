"use strict";

// ---- Configuration & state -------------------------------------------------
const API_BASE = "/api/stocks";
const pageSize = 20;

let currentPage = 1;
let totalPages = 1;
let isLoading = false;

// Map the Sort dropdown to the API's sortBy / sortDirection parameters.
const SORT_OPTIONS = {
    "newest":      { sortBy: "priceDate", sortDirection: "desc" },
    "oldest":      { sortBy: "priceDate", sortDirection: "asc" },
    "close-high":  { sortBy: "close",     sortDirection: "desc" },
    "close-low":   { sortBy: "close",     sortDirection: "asc" },
    "volume-high": { sortBy: "volume",    sortDirection: "desc" }
};

// ---- Element references ----------------------------------------------------
const el = {
    refreshBtn: document.getElementById("refreshBtn"),
    banner: document.getElementById("banner"),

    latestCard: document.getElementById("latestCard"),
    latestClose: document.getElementById("latestClose"),
    latestDate: document.getElementById("latestDate"),
    latestOpen: document.getElementById("latestOpen"),
    latestHigh: document.getElementById("latestHigh"),
    latestLow: document.getElementById("latestLow"),
    latestVolume: document.getElementById("latestVolume"),

    search: document.getElementById("searchInput"),
    fromDate: document.getElementById("fromDate"),
    toDate: document.getElementById("toDate"),
    sort: document.getElementById("sortSelect"),
    clearBtn: document.getElementById("clearBtn"),

    loading: document.getElementById("loading"),
    error: document.getElementById("error"),
    empty: document.getElementById("empty"),
    importBtn: document.getElementById("importBtn"),
    tableWrap: document.getElementById("tableWrap"),
    tableBody: document.getElementById("stockTableBody"),

    resultSummary: document.getElementById("resultSummary"),
    pagination: document.getElementById("pagination")
};

// ---- Formatting helpers ----------------------------------------------------
const currencyFmt = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const numberFmt = new Intl.NumberFormat("en-US");

function formatCurrency(value) {
    return typeof value === "number" ? currencyFmt.format(value) : "—";
}

function formatVolume(value) {
    return typeof value === "number" ? numberFmt.format(value) : "—";
}

// Turn "2026-08-18" into "18 August 2026" without timezone surprises.
function formatLongDate(isoDate) {
    if (!isoDate) return "";
    const [y, m, d] = isoDate.split("-").map(Number);
    const date = new Date(Date.UTC(y, m - 1, d));
    return new Intl.DateTimeFormat("en-GB", {
        day: "numeric", month: "long", year: "numeric", timeZone: "UTC"
    }).format(date);
}

// ---- Query string ----------------------------------------------------------
function buildQueryString() {
    const params = new URLSearchParams();
    params.set("page", String(currentPage));
    params.set("pageSize", String(pageSize));

    const search = el.search.value.trim();
    if (search) params.set("search", search);

    if (el.fromDate.value) params.set("fromDate", el.fromDate.value);
    if (el.toDate.value) params.set("toDate", el.toDate.value);

    const sort = SORT_OPTIONS[el.sort.value] || SORT_OPTIONS.newest;
    params.set("sortBy", sort.sortBy);
    params.set("sortDirection", sort.sortDirection);

    return params.toString();
}

// ---- Rendering -------------------------------------------------------------
function renderStocks(result) {
    el.tableBody.replaceChildren();

    for (const item of result.items) {
        const tr = document.createElement("tr");
        tr.append(
            cell(item.priceDate),
            cell(item.symbol),
            cell(formatCurrency(item.open), "num"),
            cell(formatCurrency(item.high), "num"),
            cell(formatCurrency(item.low), "num"),
            cell(formatCurrency(item.close), "num close-cell"),
            cell(formatVolume(item.volume), "num"),
            cell(item.source || "—")
        );
        el.tableBody.append(tr);
    }

    const { totalCount, page } = result;
    const first = totalCount === 0 ? 0 : (page - 1) * result.pageSize + 1;
    const last = Math.min(page * result.pageSize, totalCount);
    el.resultSummary.textContent =
        `Showing ${first}–${last} of ${numberFmt.format(totalCount)} record${totalCount === 1 ? "" : "s"}`;
}

function cell(text, className) {
    const td = document.createElement("td");
    td.textContent = text;
    if (className) td.className = className;
    return td;
}

function renderPagination() {
    el.pagination.replaceChildren();
    if (totalPages <= 1) return;

    el.pagination.append(pageButton("Previous", currentPage - 1, {
        disabled: currentPage === 1, label: "Previous page"
    }));

    for (const p of pageWindow(currentPage, totalPages)) {
        if (p === "…") {
            const span = document.createElement("span");
            span.className = "page-ellipsis";
            span.textContent = "…";
            el.pagination.append(span);
        } else {
            el.pagination.append(pageButton(String(p), p, {
                active: p === currentPage, label: `Page ${p}`
            }));
        }
    }

    el.pagination.append(pageButton("Next", currentPage + 1, {
        disabled: currentPage === totalPages, label: "Next page"
    }));
}

function pageButton(text, targetPage, opts = {}) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "page-btn" + (opts.active ? " is-active" : "");
    btn.textContent = text;
    if (opts.label) btn.setAttribute("aria-label", opts.label);
    if (opts.active) btn.setAttribute("aria-current", "page");
    btn.disabled = Boolean(opts.disabled) || isLoading;
    btn.addEventListener("click", () => {
        if (targetPage >= 1 && targetPage <= totalPages && targetPage !== currentPage) {
            currentPage = targetPage;
            loadStocks();
        }
    });
    return btn;
}

// Build a compact page list, e.g. 1 … 4 5 [6] 7 8 … 20
function pageWindow(current, total) {
    const pages = [];
    const add = (p) => { if (!pages.includes(p)) pages.push(p); };
    const window = [];

    window.push(1);
    for (let p = current - 2; p <= current + 2; p++) {
        if (p > 1 && p < total) window.push(p);
    }
    window.push(total);
    window.sort((a, b) => a - b);

    let prev = 0;
    for (const p of window) {
        if (p - prev > 1) pages.push("…");
        add(p);
        prev = p;
    }
    return pages;
}

// ---- State toggles ---------------------------------------------------------
function showLoading() {
    isLoading = true;
    el.loading.hidden = false;
    el.error.hidden = true;
    el.empty.hidden = true;
    el.tableWrap.hidden = true;
    setControlsDisabled(true);
}

function showTable() {
    isLoading = false;
    el.loading.hidden = true;
    el.error.hidden = true;
    el.empty.hidden = true;
    el.tableWrap.hidden = false;
    setControlsDisabled(false);
}

function showEmpty() {
    isLoading = false;
    el.loading.hidden = true;
    el.error.hidden = true;
    el.tableWrap.hidden = true;
    el.empty.hidden = false;
    el.pagination.replaceChildren();
    el.resultSummary.textContent = "Showing 0 of 0 records";
    setControlsDisabled(false);
}

function showError(message) {
    isLoading = false;
    el.loading.hidden = true;
    el.tableWrap.hidden = true;
    el.empty.hidden = true;
    el.error.hidden = false;
    if (message) {
        el.error.querySelector(".state-detail").textContent = message;
    }
    setControlsDisabled(false);
}

function setControlsDisabled(disabled) {
    el.search.disabled = disabled;
    el.fromDate.disabled = disabled;
    el.toDate.disabled = disabled;
    el.sort.disabled = disabled;
    el.clearBtn.disabled = disabled;
    // Disable any currently-rendered pagination buttons while a request is running.
    // A successful load rebuilds them with the correct enabled/disabled state.
    el.pagination.querySelectorAll("button").forEach((b) => { b.disabled = disabled; });
}

function showBanner(message, isError) {
    el.banner.textContent = message;
    el.banner.classList.toggle("is-error", Boolean(isError));
    el.banner.hidden = false;
}

// ---- Data loading ----------------------------------------------------------
async function loadStocks() {
    showLoading();
    try {
        const response = await fetch(`${API_BASE}?${buildQueryString()}`);
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`);

        const result = await response.json();
        totalPages = result.totalPages || 0;

        if (result.totalCount === 0) {
            showEmpty();
            return;
        }

        // If the current page fell out of range (e.g. after filtering), retreat.
        if (currentPage > totalPages) {
            currentPage = totalPages;
            return loadStocks();
        }

        // showTable() first so isLoading is false before we build the pagination
        // buttons — otherwise they would render permanently disabled.
        showTable();
        renderStocks(result);
        renderPagination();
    } catch (err) {
        console.error("Failed to load stock data", err);
        showError("Please try again.");
    }
}

async function loadLatestStock() {
    el.latestCard.setAttribute("aria-busy", "true");
    try {
        const response = await fetch(`${API_BASE}/latest`);
        if (response.status === 404) {
            setLatest(null);
            return;
        }
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`);
        setLatest(await response.json());
    } catch (err) {
        console.error("Failed to load latest record", err);
        setLatest(null);
    } finally {
        el.latestCard.setAttribute("aria-busy", "false");
    }
}

function setLatest(record) {
    if (!record) {
        el.latestClose.textContent = "—";
        el.latestDate.textContent = "";
        el.latestOpen.textContent = "—";
        el.latestHigh.textContent = "—";
        el.latestLow.textContent = "—";
        el.latestVolume.textContent = "—";
        return;
    }
    el.latestClose.textContent = formatCurrency(record.close);
    el.latestDate.textContent = formatLongDate(record.priceDate);
    el.latestOpen.textContent = formatCurrency(record.open);
    el.latestHigh.textContent = formatCurrency(record.high);
    el.latestLow.textContent = formatCurrency(record.low);
    el.latestVolume.textContent = formatVolume(record.volume);
}

async function refreshStockData() {
    el.refreshBtn.disabled = true;
    const originalText = el.refreshBtn.textContent;
    el.refreshBtn.textContent = "Refreshing…";
    showBanner("Contacting Alpha Vantage…", false);

    try {
        const response = await fetch(`${API_BASE}/ingest`, { method: "POST" });
        const body = await response.json().catch(() => null);

        if (!response.ok) {
            const detail = (body && (body.detail || body.title)) || "Ingestion failed. Please try again.";
            showBanner(detail, true);
            return;
        }

        showBanner(
            `Stock data updated\n` +
            `${numberFmt.format(body.recordsReceived)} records received\n` +
            `${numberFmt.format(body.recordsInserted)} new records added\n` +
            `${numberFmt.format(body.recordsSkipped)} existing records skipped`,
            false
        );

        // Refresh both the latest card and the table.
        currentPage = 1;
        await Promise.all([loadLatestStock(), loadStocks()]);
    } catch (err) {
        console.error("Ingestion request failed", err);
        showBanner("Unable to reach the server. Please try again.", true);
    } finally {
        el.refreshBtn.disabled = false;
        el.refreshBtn.textContent = originalText;
    }
}

// ---- Filters ---------------------------------------------------------------
function applyFilters() {
    currentPage = 1;
    loadStocks();
}

function clearFilters() {
    el.search.value = "";
    el.fromDate.value = "";
    el.toDate.value = "";
    el.sort.value = "newest";
    applyFilters();
}

// Debounce so we don't fire a request on every keystroke.
function debounce(fn, delay) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), delay);
    };
}

// ---- Wire up events --------------------------------------------------------
el.refreshBtn.addEventListener("click", refreshStockData);
el.importBtn.addEventListener("click", refreshStockData);
el.clearBtn.addEventListener("click", clearFilters);
el.sort.addEventListener("change", applyFilters);
el.fromDate.addEventListener("change", applyFilters);
el.toDate.addEventListener("change", applyFilters);
el.search.addEventListener("input", debounce(applyFilters, 350));

// ---- Initial load ----------------------------------------------------------
loadLatestStock();
loadStocks();
