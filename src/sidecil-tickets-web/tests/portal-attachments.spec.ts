import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("Crear ticket permite seleccionar, quitar y conservar archivos ante errores", async ({ page }) => {
  let sends = 0;
  await page.route("**/api/v1/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/me")) return route.fulfill({ json: { id: "client", displayName: "Cliente", email: "client@example.invalid", role: "Requester", organization: "Pruebas", organizationId: "org" } });
    if (path.endsWith("/auth/csrf")) return route.fulfill({ json: { token: "csrf" } });
    if (path.endsWith("/directory")) return route.fulfill({ json: { categories: ["General"], agents: [], teams: [], users: [], organizations: [] } });
    if (path.endsWith("/tickets/with-attachments")) {
      sends++;
      expect(route.request().headers()["content-type"]).toContain("multipart/form-data; boundary=");
      expect(route.request().headers()["x-csrf-token"]).toBe("csrf");
      const payload = route.request().postData()!;
      expect(payload).toContain('filename="soporte.txt"');
      expect(payload).not.toContain('filename="quitar.txt"');
      expect(payload).toContain("Evidencia del error");
      return sends === 1 ? route.fulfill({ status: 400, json: { detail: "No se pudo guardar. Intenta nuevamente." } }) : route.fulfill({ status: 201, json: { id: "created" } });
    }
    if (path.endsWith("/tickets/created")) return route.fulfill({ status: 404, json: { detail: "Ticket creado en prueba" } });
    return route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 25, summary: { active: 0, unassigned: 0, overdue: 0, resolved: 0 } } });
  });
  await page.goto("/");
  await page.getByRole("button", { name: /Crear ticket|Nueva solicitud|Nuevo ticket/ }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/^Asunto/).fill("Problema de inventario");
  await dialog.getByLabel(/^Describe tu solicitud/).fill("El inventario muestra una cantidad incorrecta.");
  const picker = dialog.locator('input[type="file"]');
  await picker.setInputFiles([
    { name: "soporte.txt", mimeType: "text/plain", buffer: Buffer.from("Evidencia del error") },
    { name: "quitar.txt", mimeType: "text/plain", buffer: Buffer.from("No enviar") },
  ]);
  await dialog.getByRole("button", { name: "Eliminar quitar.txt" }).click();
  await expect(dialog.getByText("quitar.txt", { exact: true })).toHaveCount(0);
  await dialog.getByRole("button", { name: "Crear ticket", exact: true }).click();
  await expect(dialog.getByText("No se pudo guardar. Intenta nuevamente.")).toBeVisible();
  await expect(dialog.getByLabel(/^Asunto/)).toHaveValue("Problema de inventario");
  await expect(dialog.getByRole("button", { name: "Eliminar soporte.txt" })).toBeVisible();
  await dialog.getByRole("button", { name: "Crear ticket", exact: true }).click();
  await expect(page).toHaveURL(/\/tickets\/created$/);
  expect(sends).toBe(2);
});
