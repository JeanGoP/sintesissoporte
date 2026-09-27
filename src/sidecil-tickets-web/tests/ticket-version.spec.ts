import { test, expect } from "@playwright/test";

test.use({ baseURL: "http://127.0.0.1:5187" });
for (const header of ['W/"version-1"', "", "automatico"]) {
  test(`Respuesta con ETag ${header || "ausente"} y actualizacion`, async ({
    page,
  }) => {
    let version = '"version-1"';
    const writes: string[] = [];
    await page.route("**/api/v1/**", async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/auth/me"))
        return route.fulfill({
          json: {
            id: "admin",
            displayName: "Agente",
            email: "agent@example.test",
            role: "Admin",
            organization: "Pruebas",
            organizationId: "org",
          },
        });
      if (path.endsWith("/auth/csrf"))
        return route.fulfill({ json: { token: "test" } });
      if (path.endsWith("/directory"))
        return route.fulfill({
          json: {
            agents: [],
            teams: [],
            users: [],
            organizations: [],
            categories: ["General"],
          },
        });
      if (path.endsWith("/tickets/test/messages")) {
        const received = route.request().headers()["if-match"];
        writes.push(received);
        return received === version
          ? route.fulfill({ status: 204 })
          : route.fulfill({
              status: 412,
              json: {
                detail:
                  "El ticket cambió. Actualiza antes de enviar; conserva tu borrador.",
              },
            });
      }
      if (path.endsWith("/tickets/test"))
        return route.fulfill({
          headers: header ? { ETag: header } : {},
          json: {
            id: "test",
            version,
            number: "SC-00001",
            subject: "Ticket de prueba",
            category: "General",
            status: "New",
            priority: "Normal",
            createdAt: "2026-09-27T10:00:00Z",
            updatedAt: "2026-09-27T10:00:00Z",
            dueAt: "2026-09-28T10:00:00Z",
            requester: { displayName: "Cliente", email: "client@example.test" },
            organization: "Pruebas",
            messages:
              version === '"version-2"'
                ? [
                    {
                      id: 1,
                      body: "Nueva respuesta por correo",
                      author: "Cliente",
                      createdAt: "2026-09-27T10:01:00Z",
                      visibility: "Public",
                      source: "Email",
                      own: false,
                    },
                  ]
                : [],
            attachments: [],
            events: [],
            nextStatuses: [],
            assigneeId: null,
          },
        });
      return route.fulfill({ json: {} });
    });
    await page.goto("/tickets/test");
    const draft = page.getByLabel("Mensaje", { exact: true });
    await draft.fill("Respuesta conservada");
    // Un correo entrante cambia SQL despues de abrir la pantalla.
    version = '"version-2"';
    if (header === "automatico") {
      await expect(
        page.getByText("Nueva respuesta por correo", { exact: true }),
      ).toBeVisible({ timeout: 12000 });
      await expect(draft).toHaveValue("Respuesta conservada");
      expect(writes).toEqual([]);
      await page.getByRole("button", { name: "Enviar respuesta" }).click();
      await expect(draft).toHaveValue("");
      expect(writes).toEqual(['"version-2"']);
      return;
    }
    await page.getByRole("button", { name: "Enviar respuesta" }).click();
    await expect(
      page.getByText("El ticket cambió.", { exact: false }),
    ).toBeVisible();
    await expect(draft).toHaveValue("Respuesta conservada");
    expect(writes).toEqual(['"version-1"']);
    await page.getByRole("button", { name: "Actualizar", exact: true }).click();
    await expect(
      page.getByText("Ticket actualizado. Tu borrador se conservó."),
    ).toBeVisible();
    await expect(draft).toHaveValue("Respuesta conservada");
    await page.getByRole("button", { name: "Enviar respuesta" }).click();
    await expect(draft).toHaveValue("");
    expect(writes).toEqual(['"version-1"', '"version-2"']);
  });
}
