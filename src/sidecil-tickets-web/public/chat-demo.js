(() => {
  const site = new URLSearchParams(location.search).get("site");
  if (!site || !/^[0-9a-f-]{36}$/i.test(site)) {
    document.getElementById("demo-note").textContent =
      "Abre la demostración desde «Chat en tus sistemas» después de registrar una integración.";
    return;
  }
  const script = document.createElement("script");
  script.src = "/sidecil-chat.js";
  script.dataset.site = site;
  document.body.append(script);
})();
