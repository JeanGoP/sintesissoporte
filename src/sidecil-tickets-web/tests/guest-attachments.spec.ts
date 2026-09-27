import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("Solicitud sin cuenta permite adjuntar archivos y conserva el formulario si falla", async ({ page }) => {
  let sends = 0;
  await page.route("**/api/v1/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/public/config")) return route.fulfill({ json: { available: true, testMode: false, categories: ["General"] } });
    if (path.endsWith("/auth/csrf")) return route.fulfill({ json: { token: "csrf" } });
    if (path.endsWith("/public/tickets/with-attachments")) {
      sends++;
      expect(route.request().headers()["content-type"]).toContain("multipart/form-data; boundary=");
      expect(route.request().postData()).toContain('filename="captura.txt"');
      expect(route.request().postData()).not.toContain('filename="quitar.txt"');
      return sends === 1 ? route.fulfill({ status: 400, json: { detail: "No se pudo guardar. Intenta de nuevo." } }) : route.fulfill({ status: 202, json: { message: "Revisa tu correo y confirma la solicitud." } });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto("/solicitar");
  await page.getByLabel(/^Nombre completo/).fill("Cliente prueba");
  await page.getByLabel(/^Correo electrónico/).fill("client@example.invalid");
  await page.getByLabel(/^Asunto/).fill("Problema de inventario");
  await page.getByLabel(/^Describe tu solicitud/).fill("El inventario muestra cantidades incorrectas.");
  await page.locator('input[type="file"]').setInputFiles([
    { name: "captura.txt", mimeType: "text/plain", buffer: Buffer.from("Detalle del problema") },
    { name: "quitar.txt", mimeType: "text/plain", buffer: Buffer.from("No enviar") },
  ]);
  await page.getByRole("button", { name: "Eliminar quitar.txt" }).click();
  await page.getByRole("button", { name: "Enviar solicitud" }).click();
  await expect(page.getByText("No se pudo guardar. Intenta de nuevo.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Eliminar captura.txt" })).toBeVisible();
  await expect(page.getByLabel(/^Asunto/)).toHaveValue("Problema de inventario");
  await page.getByRole("button", { name: "Enviar solicitud" }).click();
  await expect(page.getByRole("heading", { name: "Revisa tu correo" })).toBeVisible();
  expect(sends).toBe(2);
});
