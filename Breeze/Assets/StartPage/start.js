"use strict";

const SVG_NS = "http://www.w3.org/2000/svg";
let searchUrl = "https://duckduckgo.com/?q=";
const ICON_URL = "https://breeze.icons/";
const SLOT_COUNT = 8;

const host = window.chrome && window.chrome.webview;

const form = document.getElementById("search");
const query = document.getElementById("query");
const grid = document.getElementById("shortcuts");

const editor = document.getElementById("editor");
const editorForm = document.getElementById("editor-form");
const editorTitle = document.getElementById("editor-title");
const editorName = document.getElementById("editor-name");
const editorUrl = document.getElementById("editor-url");
const editorError = document.getElementById("editor-error");

const confirmDialog = document.getElementById("confirm");
const confirmText = document.getElementById("confirm-text");

const menu = document.getElementById("menu");

let shortcuts = [];
let editIndex = -1;
let menuIndex = -1;
let dragIndex = -1;

form.addEventListener("submit", event => {
  event.preventDefault();
  const text = query.value.trim();
  if (text) {
    location.href = resolve(text);
  }
});

function resolve(text) {
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(text)) {
    return text;
  }
  if (/^localhost(:\d+)?([/?#].*)?$/i.test(text) || /^[^\s/?#]+\.[^\s/?#.]{2,}([/?#]\S*)?$/.test(text)) {
    return "https://" + text;
  }
  return searchUrl + encodeURIComponent(text);
}

function send(message) {
  if (host) {
    host.postMessage(message);
  }
}

if (host) {
  host.addEventListener("message", event => {
    const data = event.data;
    if (data && data.type === "shortcuts") {
      // Defence in depth: only web URLs are ever put into a tile's href.
      shortcuts = Array.isArray(data.items) ? data.items.filter(item => /^https?:\/\//i.test(item.url)) : [];
      if (typeof data.searchUrl === "string" && data.searchUrl) {
        searchUrl = data.searchUrl;
      }
      render();
    }
  });
  send({ type: "list" });
}

function plusIcon() {
  const svg = document.createElementNS(SVG_NS, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("class", "plus");
  svg.setAttribute("aria-hidden", "true");

  const path = document.createElementNS(SVG_NS, "path");
  path.setAttribute("d", "M12 6v12M6 12h12");
  svg.append(path);
  return svg;
}

function tile(shortcut, index) {
  const item = document.createElement("li");
  const link = document.createElement("a");
  link.href = shortcut.url;
  link.title = shortcut.url;
  link.draggable = true;

  const icon = document.createElement("span");
  icon.className = "icon";

  const image = document.createElement("img");
  image.alt = "";
  image.src = shortcut.icon ? ICON_URL + encodeURIComponent(shortcut.icon) : "globe.svg";
  image.addEventListener("error", () => {
    image.src = "globe.svg";
  });
  icon.append(image);

  const name = document.createElement("span");
  name.className = "name";
  name.textContent = shortcut.name;

  link.append(icon, name);

  link.addEventListener("contextmenu", event => {
    event.preventDefault();
    openMenu(event.clientX, event.clientY, index);
  });

  link.addEventListener("dragstart", event => {
    dragIndex = index;
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", String(index));
  });

  link.addEventListener("dragend", () => {
    dragIndex = -1;
  });

  item.addEventListener("dragover", event => {
    if (dragIndex >= 0 && dragIndex !== index) {
      event.preventDefault();
      item.classList.add("over");
    }
  });

  item.addEventListener("dragleave", () => item.classList.remove("over"));

  item.addEventListener("drop", event => {
    event.preventDefault();
    item.classList.remove("over");
    if (dragIndex >= 0 && dragIndex !== index) {
      send({ type: "move", from: dragIndex, to: index });
    }
    dragIndex = -1;
  });

  item.append(link);
  return item;
}

function slot() {
  const item = document.createElement("li");
  const button = document.createElement("button");
  button.type = "button";
  button.className = "slot";
  button.title = "Add shortcut";
  button.setAttribute("aria-label", "Add shortcut");
  button.append(plusIcon());
  button.addEventListener("click", () => openEditor(-1));
  item.append(button);
  return item;
}

function render() {
  const count = Math.max(SLOT_COUNT, shortcuts.length);
  grid.replaceChildren(...Array.from({ length: count }, (_, i) => (shortcuts[i] ? tile(shortcuts[i], i) : slot())));
}

function openEditor(index) {
  closeMenu();
  editIndex = index;
  const shortcut = index >= 0 ? shortcuts[index] : null;
  editorTitle.textContent = shortcut ? "Edit shortcut" : "Add shortcut";
  editorName.value = shortcut ? shortcut.name : "";
  editorUrl.value = shortcut ? shortcut.url : "";
  editorError.textContent = "";
  editor.showModal();
  editorName.focus();
}

editorForm.addEventListener("submit", event => {
  event.preventDefault();

  const name = editorName.value.trim();
  const raw = editorUrl.value.trim();

  if (!name || !raw) {
    editorError.textContent = "Name and URL are required.";
    return;
  }

  const url = normalize(raw);
  if (!url) {
    editorError.textContent = "Enter a valid URL, for example github.com.";
    return;
  }

  send({ type: "save", index: editIndex, name, url });
  editor.close();
});

document.getElementById("editor-cancel").addEventListener("click", () => editor.close());

function normalize(text) {
  const candidate = /^[a-z][a-z0-9+.-]*:\/\//i.test(text) ? text : "https://" + text;
  try {
    const url = new URL(candidate);
    if (url.protocol !== "http:" && url.protocol !== "https:") {
      return null;
    }
    return url.hostname.includes(".") || url.hostname === "localhost" ? url.href : null;
  } catch {
    return null;
  }
}

function openMenu(x, y, index) {
  menuIndex = index;
  menu.hidden = false;
  const bounds = menu.getBoundingClientRect();
  menu.style.left = Math.min(x, window.innerWidth - bounds.width - 8) + "px";
  menu.style.top = Math.min(y, window.innerHeight - bounds.height - 8) + "px";
}

function closeMenu() {
  menu.hidden = true;
  menuIndex = -1;
}

document.getElementById("menu-edit").addEventListener("click", () => openEditor(menuIndex));

document.getElementById("menu-delete").addEventListener("click", () => {
  const shortcut = shortcuts[menuIndex];
  if (!shortcut) {
    closeMenu();
    return;
  }
  editIndex = menuIndex;
  confirmText.textContent = `Remove "${shortcut.name}" from your shortcuts?`;
  closeMenu();
  confirmDialog.showModal();
});

document.getElementById("confirm-cancel").addEventListener("click", () => confirmDialog.close());

document.getElementById("confirm-delete").addEventListener("click", () => {
  send({ type: "delete", index: editIndex });
  confirmDialog.close();
});

document.addEventListener("pointerdown", event => {
  if (!menu.hidden && !menu.contains(event.target)) {
    closeMenu();
  }
});

document.addEventListener("keydown", event => {
  if (event.key === "Escape") {
    closeMenu();
  }
});

document.addEventListener("contextmenu", event => {
  if (!event.target.closest(".shortcuts a")) {
    closeMenu();
  }
});

render();
