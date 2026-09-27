import { test, expect } from "@playwright/test";

test.use({ baseURL: "http://127.0.0.1:5187" });

for (const provider of ["Microsoft", "Google"]) {
  test(`Volver desde ${provider} permite un nuevo intento`, async ({
    page,
  }) => {
    let attempts = 0;
    await page.route("**/api/v1/**", async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/auth/providers"))
        return route.fulfill({
          json: [
            { name: "Microsoft", enabled: true },
            { name: "Google", enabled: true },
          ],
        });
      if (path.endsWith("/auth/csrf"))
        return route.fulfill({ json: { token: "prueba" } });
      if (path.endsWith(`/auth/external/${provider}`)) {
        attempts++;
        // Conserva el documento para simular la restauración de su estado en caché.
        return route.fulfill({ json: { url: "/#proveedor" } });
      }
      return route.fulfill({ status: 401, json: {} });
    });
    await page.goto("/");
    const microsoft = page.getByRole("button", {
      name: "Continuar con Microsoft",
    });
    const google = page.getByRole("button", { name: "Continuar con Google" });
    const selected = provider === "Microsoft" ? microsoft : google;
    await selected.click();
    await expect(page).toHaveURL(/#proveedor$/);
    await expect(microsoft).toBeDisabled();
    await expect(google).toBeDisabled();
    await page.evaluate(() =>
      window.dispatchEvent(
        new PageTransitionEvent("pageshow", { persisted: true }),
      ),
    );
    await expect(microsoft).toBeEnabled();
    await expect(google).toBeEnabled();
    await selected.click();
    await expect.poll(() => attempts).toBe(2);
  });
}

test("Restaurar la página no activa proveedores sin configurar", async ({
  page,
}) => {
  await page.route("**/api/v1/**", async (route) => {
    if (route.request().url().endsWith("/auth/providers"))
      return route.fulfill({
        json: [
          { name: "Microsoft", enabled: true },
          { name: "Google", enabled: false },
        ],
      });
    return route.fulfill({ status: 401, json: {} });
  });
  await page.goto("/");
  await expect(
    page.getByRole("button", { name: "Continuar con Microsoft" }),
  ).toBeEnabled();
  await page.evaluate(() =>
    window.dispatchEvent(
      new PageTransitionEvent("pageshow", { persisted: true }),
    ),
  );
  await expect(
    page.getByRole("button", { name: "Continuar con Google" }),
  ).toBeDisabled();
});
