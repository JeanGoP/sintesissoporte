import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";

test("frontend separado: cookies, CSRF, ETag y cierre de sesión", async ({
  page,
}) => {
  test.skip(
    !process.env.SIDECIL_SPLIT_TEST,
    "Requiere frontend 5180 y API 5099; ver docs/COOLIFY.md",
  );
  const credentials = JSON.parse(
    readFileSync(path.resolve("../../.local/demo-credentials.json"), "utf8"),
  );
  await page.goto("http://localhost:5180/");
  await page.getByLabel("Correo electrónico").fill("admin@sidecil.local");
  await page.getByLabel(/^Contraseña/).fill(credentials.password);
  await page.getByRole("button", { name: "Ingresar a mi espacio" }).click();
  await expect(
    page.getByRole("button", { name: "Nuevo ticket", exact: true }),
  ).toBeVisible();
  const result = await page.evaluate(async () => {
    const base = "http://localhost:5099/soporte/api/v1";
    const options = { credentials: "include" as const };
    const me = await fetch(base + "/auth/me", options);
    const tickets = await (await fetch(base + "/tickets", options)).json();
    const detail = await fetch(
      base + "/tickets/" + tickets.items[0].id,
      options,
    );
    const withoutCsrf = await fetch(base + "/auth/logout", {
      ...options,
      method: "POST",
    });
    const { token } = await (await fetch(base + "/auth/csrf", options)).json();
    const logout = await fetch(base + "/auth/logout", {
      ...options,
      method: "POST",
      headers: { "X-CSRF-TOKEN": token },
    });
    const after = await fetch(base + "/auth/me", options);
    return {
      me: me.status,
      etag: detail.headers.get("ETag"),
      withoutCsrf: withoutCsrf.status,
      logout: logout.status,
      after: after.status,
    };
  });
  expect(result.me).toBe(200);
  expect(result.etag).toMatch(/^".+"$/);
  expect(result.withoutCsrf).toBe(400);
  expect(result.logout).toBe(204);
  expect(result.after).toBe(401);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Ingresar a mi espacio" }),
  ).toBeVisible();
});

test("widget y recursos publicados bajo /soporte", async ({ page }) => {
  test.skip(
    !process.env.SIDECIL_SPLIT_TEST,
    "Requiere recursos IIS compilados con -ApiPath /soporte",
  );
  const demo = JSON.parse(
    readFileSync(path.resolve("../../.local/chat-demo.json"), "utf8"),
  );
  const site = new URL(demo.url).searchParams.get("site");
  await page.goto("http://localhost:5099/soporte/chat-demo.html?site=" + site);
  await page.getByRole("button", { name: "¿Necesitas ayuda?" }).click();
  const widget = page.frameLocator("#sidecil-support-widget iframe");
  await expect(
    widget.getByRole("button", { name: "Iniciar solicitud" }),
  ).toBeEnabled();
  const logo = widget.getByRole("img", { name: "Sidecil · Cloud technology" });
  await expect(logo).toBeVisible();
  expect(
    await logo.evaluate((img: HTMLImageElement) => img.naturalWidth),
  ).toBeGreaterThan(0);
});
