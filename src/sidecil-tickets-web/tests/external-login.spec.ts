import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";
const demo = JSON.parse(
  readFileSync(path.resolve("../../.local/chat-demo.json"), "utf8"),
);
const credentials = JSON.parse(
  readFileSync(path.resolve("../../.local/demo-credentials.json"), "utf8"),
);
test("Microsoft y Google en el acceso principal y Mi cuenta; ausentes del chat", async ({
  page,
  request,
}) => {
  const providers = await (await request.get("/api/v1/auth/providers")).json();
  expect(providers.map((p: { name: string }) => p.name).sort()).toEqual([
    "Google",
    "Microsoft",
  ]);
  expect(providers.every((p: { enabled: boolean }) => !p.enabled)).toBe(true);
  expect(
    (await request.post("/api/v1/auth/external/Google", { data: {} })).status(),
  ).toBe(400);
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/");
  await expect(
    page.getByRole("button", { name: "Continuar con Microsoft" }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Continuar con Google" }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Ingresar a mi espacio" }),
  ).toBeEnabled();
  await page.screenshot({
    path: path.resolve(
      "../../artifacts/screenshots/acceso-principal-proveedores.png",
    ),
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: path.resolve(
      "../../artifacts/screenshots/acceso-principal-mobile.png",
    ),
    fullPage: true,
  });
  await page.getByLabel("Correo electrónico").fill("admin@sidecil.local");
  await page.getByLabel(/^Contraseña/).fill(credentials.password);
  await page.getByRole("button", { name: "Ingresar a mi espacio" }).click();
  await expect(
    page.getByRole("button", { name: "Nuevo ticket", exact: true }),
  ).toBeVisible();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/account");
  await expect(
    page.getByRole("heading", { name: "Acceso con Microsoft y Google" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Vincular", exact: true }),
  ).toHaveCount(2);
  await page.screenshot({
    path: path.resolve("../../artifacts/screenshots/cuentas-vinculadas.png"),
    fullPage: true,
  });
  await page.goto(demo.url);
  await page.getByRole("button", { name: "¿Necesitas ayuda?" }).click();
  const widget = page.frameLocator("#sidecil-support-widget iframe");
  await expect(
    widget.getByRole("button", { name: "Iniciar solicitud" }),
  ).toBeEnabled();
  await expect(
    widget.getByRole("button", { name: /Continuar con (Microsoft|Google)/ }),
  ).toHaveCount(0);
  await page.screenshot({
    path: path.resolve(
      "../../artifacts/screenshots/chat-sin-acceso-externo.png",
    ),
    fullPage: true,
  });
  expect(errors).toEqual([]);
});
