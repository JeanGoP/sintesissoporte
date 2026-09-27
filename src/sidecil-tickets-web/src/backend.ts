const configured = (import.meta.env.VITE_API_URL || "").trim();
function resolveBackend() {
  if (!configured) return import.meta.env.BASE_URL.replace(/\/$/, "");
  const url = new URL(configured);
  if (
    !["https:", "http:"].includes(url.protocol) ||
    url.search ||
    url.hash ||
    url.username ||
    url.password
  )
    throw new Error(
      "VITE_API_URL debe ser la URL base del backend, sin consulta ni credenciales.",
    );
  return url.origin + url.pathname.replace(/\/$/, "");
}
export const backendBaseUrl = resolveBackend();
export function backendUrl(path: string) {
  if (!path.startsWith("/") || path.startsWith("//"))
    throw new Error("Ruta de API inválida.");
  return backendBaseUrl + path;
}
