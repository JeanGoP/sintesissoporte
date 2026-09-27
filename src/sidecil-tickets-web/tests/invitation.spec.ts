import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("La invitación confirma contraseña y elimina el enlace al activar", async ({ page }) => {
  let attempts = 0;
  await page.route("**/api/v1/**", async route => {
    if (route.request().url().endsWith("/auth/csrf")) return route.fulfill({ json: { token: "csrf-test" } });
    if (route.request().url().endsWith("/auth/invitation")) {
      attempts++;
      expect(route.request().postDataJSON()).toEqual({ userId: "test-user", token: "test-token", password: "Strong!Password123" });
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 401, json: {} });
  });
  await page.goto("/activar-cuenta#user=test-user&token=test-token");
  await page.getByLabel(/^Nueva contraseña/).fill("Strong!Password123");
  await page.getByLabel(/^Confirmar contraseña/).fill("Different!Password123");
  await page.getByRole("button", { name: "Crear mi contraseña" }).click();
  await expect(page.getByText("Las contraseñas no coinciden.")).toBeVisible();
  expect(attempts).toBe(0);
  await page.getByLabel(/^Confirmar contraseña/).fill("Strong!Password123");
  await page.getByRole("button", { name: "Crear mi contraseña" }).click();
  await expect(page.getByText(/Tu contraseña quedó creada/)).toBeVisible();
  await expect(page).toHaveURL(/\/activar-cuenta$/);
  expect(attempts).toBe(1);
});
test("Un enlace incompleto no permite crear contraseña", async ({ page }) => {
  await page.goto("/activar-cuenta");
  await expect(page.getByText(/Abre el enlace completo/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Crear mi contraseña" })).toHaveCount(0);
});
