import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("Vista previa conserva texto literal y maneja permisos", async ({ page }) => {
  let denied = false;
  await page.route("**/api/v1/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/me")) return route.fulfill({ json: { id: "u", displayName: "Cliente", role: "Requester", email: "cliente@test.invalid" } });
    if (path.endsWith("/directory")) return route.fulfill({ json: { categories: [], modules: [], organizations: [], agents: [] } });
    if (path.endsWith("/attachments/img")) return route.fulfill({body:Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=","base64"),contentType:"application/octet-stream"});
    if (path.endsWith("/attachments/pdf")) return route.fulfill({body:"%PDF-1.4\n%%EOF",contentType:"application/octet-stream"});
    if (path.includes("/attachments/")) return denied ? route.fulfill({status:403}) : route.fulfill({ body: "Evidencia <script>alert(1)</script>", contentType: "application/octet-stream" });
    if (path.endsWith("/tickets/t1")) return route.fulfill({ json: { id: "t1", number: "SC-1", subject: "Prueba adjuntos", category: "General", status: "New", priority: "Normal", createdAt: new Date().toISOString(), dueAt: new Date().toISOString(), requester: { displayName: "Cliente", email: "cliente@test.invalid" }, messages: [], events: [], attachments: [{id:"f1",fileName:"evidencia.txt",length:100},{id:"img",fileName:"captura.png",length:100},{id:"pdf",fileName:"manual.pdf",length:100}], nextStatuses: [] } });
    return route.fulfill({ json: {} });
  });
  await page.goto("/tickets/t1");
  await page.getByRole("button", { name: "Ver evidencia.txt", exact: true }).click();
  await expect(page.getByRole("dialog").locator("pre")).toHaveText("Evidencia <script>alert(1)</script>");
  await expect(page.getByRole("dialog").getByRole("link", {name:"Descargar"})).toHaveAttribute("href", /attachments\/f1$/);
  await page.getByRole("button", { name: "Cerrar", exact: true }).click();
  await page.getByRole("button", {name:"Ver captura.png",exact:true}).click();
  await expect.poll(() => page.getByRole("dialog").getByRole("img", {name:"captura.png",exact:true}).evaluate((el: HTMLImageElement) => el.naturalWidth)).toBe(1);
  await page.getByRole("button", {name:"Cerrar",exact:true}).click();
  await page.getByRole("button", {name:"Ver manual.pdf",exact:true}).click();
  await expect(page.getByRole("dialog").locator("iframe")).toHaveAttribute("src", /^blob:/);
  await page.getByRole("button", {name:"Cerrar",exact:true}).click();
  denied = true;
  await page.getByRole("button", { name: "Ver evidencia.txt", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText("no tienes permiso");
});
