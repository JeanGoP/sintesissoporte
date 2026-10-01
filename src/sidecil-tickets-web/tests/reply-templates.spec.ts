import { test, expect } from "@playwright/test";

test.use({ baseURL: "http://127.0.0.1:5188" });

test("Una plantilla se inserta editable y no se envía por elegirla", async ({ page }) => {
  const id = "12345678-1234-1234-1234-123456789abc";
  let sent = 0;
  let sentBody = "";
  await page.route("**/api/v1/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/me")) return route.fulfill({ json: {
      id: "agent-1", displayName: "Agente", email: "agent@test.invalid", role: "Agent",
      organization: "Sidecil", organizationId: "91563faf-00a3-49ac-b2d0-0e8c702f4d41", teamId: null,
      notificationSound: "Off",
    } });
    if (path.endsWith("/auth/csrf")) return route.fulfill({ json: { token: "csrf" } });
    if (path.endsWith("/notifications")) return route.fulfill({ json: { cursor: 0, items: [] } });
    if (path.endsWith("/directory")) return route.fulfill({ json: {
      agents: [], organizations: [], users: null, teams: null, categories: ["General"], modules: [],
    } });
    if (path.endsWith("/reply-templates")) return route.fulfill({ json: [
      { id: "template-1", title: "Confirmar revisión", body: "Hola {nombre}, revisamos {numero}: {asunto}.", enabled: true },
    ] });
    if (path.endsWith("/tickets/" + id + "/seen")) return route.fulfill({ status: 204 });
    if (path.endsWith("/tickets/" + id + "/messages")) {
      sent++;
      sentBody = route.request().postDataJSON().body;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/tickets/" + id)) return route.fulfill({ json: {
      id, version: '"test-version"', number: "SC-00001", subject: "Error en ERP", category: "General",
      module: "General", moduleId: null, status: "New", priority: "Normal", assigneeId: "agent-1",
      createdAt: "2026-10-01T12:00:00Z", updatedAt: "2026-10-01T12:00:00Z", dueAt: "2026-10-02T12:00:00Z",
      requester: { displayName: "Ana Pérez", email: "ana@test.invalid" }, organization: "Cliente",
      attachments: [], messages: [], events: [], nextStatuses: [],
    } });
    return route.fulfill({ status: 404 });
  });
  await page.goto("/tickets/" + id);
  await page.getByRole("combobox", { name: "Respuesta rápida" }).selectOption("template-1");
  await page.getByRole("button", { name: "Insertar" }).click();
  const reply = page.getByRole("textbox", { name: "Mensaje" });
  await expect(reply).toHaveValue("Hola Ana Pérez, revisamos SC-00001: Error en ERP.");
  expect(sent).toBe(0);
  await reply.fill("Hola Ana Pérez, ya revisamos el error y te contactaremos.");
  await page.getByRole("button", { name: "Enviar respuesta" }).click();
  await expect.poll(() => sent).toBe(1);
  expect(sentBody).toContain("ya revisamos el error");
});
