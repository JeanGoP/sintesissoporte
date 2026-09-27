(() => {
  "use strict";
  const script = document.currentScript;
  if (
    !script ||
    !script.dataset.site ||
    document.getElementById("sidecil-support-widget")
  )
    return;
  const source = new URL(script.src);
  const host = document.createElement("div");
  host.id = "sidecil-support-widget";
  const root = host.attachShadow({ mode: "open" });
  const css = document.createElement("link");
  css.rel = "stylesheet";
  css.href = new URL("sidecil-chat.css", source).href;
  const frame = document.createElement("iframe");
  frame.title = "Chat de soporte de Sidecil";
  frame.id = "sidecil-chat-panel";
  frame.hidden = true;
  frame.referrerPolicy = "no-referrer";
  frame.setAttribute("sandbox", "allow-scripts allow-same-origin allow-forms");
  const button = document.createElement("button");
  button.type = "button";
  button.className = "launcher";
  button.textContent = "¿Necesitas ayuda?";
  button.setAttribute("aria-expanded", "false");
  button.setAttribute("aria-controls", frame.id);
  let loaded = false;
  function toggle(open) {
    if (open && !loaded) {
      const url = new URL("chat-widget", source);
      url.searchParams.set("site", script.dataset.site);
      frame.src = url.href;
      loaded = true;
    }
    frame.hidden = !open;
    button.textContent = open ? "Cerrar chat ×" : "¿Necesitas ayuda?";
    button.setAttribute("aria-expanded", String(open));
    if (!open) button.focus();
  }
  button.addEventListener("click", () => toggle(frame.hidden));
  host.addEventListener("keydown", (event) => {
    if (event.key === "Escape") toggle(false);
  });
  root.append(css, frame, button);
  document.body.append(host);
})();
