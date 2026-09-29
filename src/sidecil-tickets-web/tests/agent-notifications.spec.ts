import { test, expect } from "@playwright/test";

test.use({ baseURL: "http://127.0.0.1:5187" });

test("Agente configura sonido y abre un aviso de ticket nuevo", async ({ page }) => {
  let polls = 0;
  let sound = "Off";
  const ticketId = "12345678-1234-1234-1234-123456789abc";
  await page.route("**/api/v1/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/me")) return route.fulfill({ json: {
      id: "agent-1", displayName: "Agente prueba", email: "agente@test.invalid", role: "Agent",
      organization: "Sidecil", organizationId: "91563faf-00a3-49ac-b2d0-0e8c702f4d41", teamId: null,
      hasPassword: true, notificationSound: sound,
    } });
    if (path.endsWith("/auth/csrf")) return route.fulfill({ json: { token: "csrf" } });
    if (path.endsWith("/auth/notification-sound")) {
      sound = route.request().postDataJSON().sound;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/notifications")) {
      polls++;
      return route.fulfill({ json: polls === 1 ? { cursor: 0, items: [] } : {
        cursor: 1, items: polls === 2 ? [{ eventId: 1, ticketId, number: "SC-00001", subject: "Problema de ERP", kind: "ticket" }] : [],
      } });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto("/account");
  await expect(page.getByRole("combobox", { name: "Sonido de las alertas" })).toBeVisible();
  await page.getByRole("combobox", { name: "Sonido de las alertas" }).click();
  await page.getByRole("option", { name: "Campanilla" }).click();
  await page.getByRole("button", { name: "Guardar sonido" }).click();
  await expect(page.getByText("Preferencia guardada para tu cuenta.")).toBeVisible();
  await expect(page.getByText("Nuevo ticket · SC-00001")).toBeVisible({ timeout: 12000 });
  await expect(page.getByText("Problema de ERP")).toBeVisible();
  await page.getByRole("button", { name: "Abrir" }).click();
  await expect(page).toHaveURL(new RegExp("/tickets/" + ticketId));
});
