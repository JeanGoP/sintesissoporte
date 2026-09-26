import {
  test,
  expect,
  type APIRequestContext,
  type Page,
} from "@playwright/test";
import { readFileSync, mkdirSync } from "node:fs";
import path from "node:path";
const credentials = JSON.parse(
  readFileSync(path.resolve("../../.local/demo-credentials.json"), "utf8"),
);
const artifacts = path.resolve("../../artifacts/screenshots");
test.describe.configure({ mode: "serial" });
async function session(playwright: any, email: string) {
  const request: APIRequestContext = await playwright.request.newContext({
    baseURL: "http://localhost:5080",
  });
  let csrf = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  const login = await request.post("/api/v1/auth/login", {
    headers: { "X-CSRF-TOKEN": csrf },
    data: { email, password: credentials.password },
  });
  expect(login.status()).toBe(204);
  csrf = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  return { request, headers: { "X-CSRF-TOKEN": csrf } };
}
async function login(page: Page, email: string) {
  await page.goto("/");
  await page.getByLabel("Correo electrónico").fill(email);
  await page.getByLabel(/^Contraseña/).fill(credentials.password);
  await page.getByRole("button", { name: "Ingresar a mi espacio" }).click();
  await expect(
    page.getByRole("button", { name: "Nuevo ticket", exact: true }),
  ).toBeVisible();
}
test("API real: permisos, notas privadas, CSRF, concurrencia y persistencia", async ({
  playwright,
}) => {
  const admin = await session(playwright, "admin@sidecil.local");
  const customer = await session(playwright, "cliente@sidecil.local");
  const employee = await session(playwright, "empleado@sidecil.local");
  const anon = await playwright.request.newContext({
    baseURL: "http://localhost:5080",
  });
  expect((await anon.get("/api/v1/tickets")).status()).toBe(401);
  expect(
    (await customer.request.post("/api/v1/tickets", { data: {} })).status(),
  ).toBe(400);
  const created = await customer.request.post("/api/v1/tickets", {
    headers: customer.headers,
    data: {
      subject: "Prueba de acceso " + Date.now(),
      body: "Solicitud persistente creada por un cliente externo para validar autorización.",
      category: "General",
      priority: "High",
    },
  });
  expect(created.status()).toBe(201);
  const ticket = await created.json(),
    url = "/api/v1/tickets/" + ticket.id;
  expect((await employee.request.get(url)).status()).toBe(404);
  expect(
    (
      await employee.request.post(url + "/messages", {
        headers: employee.headers,
        data: { body: "Acceso no autorizado", visibility: "Public" },
      })
    ).status(),
  ).toBe(404);
  const customerDetail = await customer.request.get(url);
  expect(
    (
      await customer.request.post(url + "/messages", {
        headers: {
          ...customer.headers,
          "If-Match": customerDetail.headers().etag,
        },
        data: { body: "Intento de nota interna", visibility: "Internal" },
      })
    ).status(),
  ).toBe(403);
  expect(
    (
      await customer.request.post("/api/v1/admin/organizations", {
        headers: customer.headers,
        data: { name: "No permitido" },
      })
    ).status(),
  ).toBe(403);
  expect(
    (
      await customer.request.post(url + "/transitions", {
        headers: customer.headers,
        data: { status: "InProgress", reason: "Intento no autorizado" },
      })
    ).status(),
  ).toBe(403);
  const before = await admin.request.get(url),
    etag = before.headers().etag;
  const secret = "NOTA-INTERNA-" + Date.now();
  expect(
    (
      await admin.request.post(url + "/messages", {
        headers: { ...admin.headers, "If-Match": etag },
        data: { body: secret, visibility: "Internal" },
      })
    ).status(),
  ).toBe(204);
  // La versión anterior ya no puede mutar el agregado.
  expect(
    (
      await admin.request.put(url + "/assignment", {
        headers: { ...admin.headers, "If-Match": etag },
        data: { assigneeId: null },
      })
    ).status(),
  ).toBe(412);
  const external = await (await customer.request.get(url)).json();
  expect(
    external.messages.every((m: any) => m.visibility === "Public"),
  ).toBeTruthy();
  expect(JSON.stringify(external)).not.toContain(secret);
  expect(external.events).toBeNull();
  expect(
    (
      await (
        await customer.request.get("/api/v1/tickets?search=" + secret)
      ).json()
    ).total,
  ).toBe(0);
  const current = await admin.request.get(url);
  expect(
    (
      await admin.request.post(url + "/transitions", {
        headers: { ...admin.headers, "If-Match": current.headers().etag },
        data: { status: "InProgress", reason: "El equipo comienza a atender" },
      })
    ).status(),
  ).toBe(204);
  const fresh = await customer.request.get(url);
  expect(
    (
      await customer.request.post(url + "/messages", {
        headers: { ...customer.headers, "If-Match": fresh.headers().etag },
        data: {
          body: "Confirmo los datos y adjunto el contexto en este mensaje.",
          visibility: "Public",
        },
      })
    ).status(),
  ).toBe(204);
  expect((await (await admin.request.get(url)).json()).messages).toHaveLength(
    3,
  );
  const invalid = await customer.request.post("/api/v1/tickets", {
    headers: customer.headers,
    data: { subject: "x", body: "", category: "General", priority: "Normal" },
  });
  expect(invalid.status()).toBe(400);
  for (const context of [admin, customer, employee])
    await context.request.dispose();
  await anon.dispose();
});
test("Interfaz agente: bandeja, búsqueda, ticket, respuesta, nota y cambio de estado", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.setViewportSize({ width: 1440, height: 1000 });
  await login(page, "agente@sidecil.local");
  await expect(
    page.getByRole("heading", { name: "Cada ticket cuenta." }),
  ).toBeVisible();
  mkdirSync(artifacts, { recursive: true });
  await page.screenshot({
    path: path.join(artifacts, "bandeja-desktop.png"),
    fullPage: true,
  });
  await page.getByLabel("Buscar tickets").fill("sin-resultados-" + Date.now());
  await expect(page.getByText("No hay tickets en esta vista")).toBeVisible();
  await page.getByLabel("Buscar tickets").fill("");
  await page.getByRole("button", { name: "Nuevo ticket", exact: true }).click();
  const subject = "Verificación de interfaz " + Date.now();
  await page.getByLabel(/^Asunto/).fill(subject);
  await page
    .getByLabel("Describe tu solicitud")
    .fill(
      "Necesito ayuda para verificar el flujo completo de atención desde el navegador.",
    );
  await page.getByRole("button", { name: "Crear ticket", exact: true }).click();
  await expect(page.getByRole("heading", { name: subject })).toBeVisible();
  await page
    .getByLabel("Mensaje", { exact: true })
    .fill("Respuesta pública enviada desde la interfaz.");
  await page
    .getByRole("button", { name: "Enviar respuesta", exact: true })
    .click();
  await expect(
    page.getByText("Respuesta pública enviada desde la interfaz."),
  ).toBeVisible();
  await page.getByRole("button", { name: "Nota interna", exact: true }).click();
  await page
    .getByLabel("Mensaje", { exact: true })
    .fill("Nota privada de comprobación desde el navegador.");
  await page.getByRole("button", { name: "Guardar nota", exact: true }).click();
  await expect(
    page.getByText("Nota privada de comprobación desde el navegador."),
  ).toBeVisible();
  await page
    .getByLabel("Cambiar estado", { exact: true })
    .selectOption("InProgress");
  await page
    .getByLabel("Motivo o solución (interno)")
    .fill("Comenzamos el diagnóstico del caso.");
  await page.getByRole("button", { name: "Guardar cambio" }).click();
  await expect(page.locator(".detail-badges .badge")).toHaveText("En curso");
  await page.reload();
  await expect(
    page.getByText("Nota privada de comprobación desde el navegador."),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(artifacts, "ticket-desktop.png"),
    fullPage: true,
  });
  expect(errors).toEqual([]);
});
test("Portal móvil: creación y aislamiento visual del solicitante", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, "empleado@sidecil.local");
  await expect(
    page.getByRole("heading", { name: "Tus solicitudes, en un solo lugar." }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: path.join(artifacts, "portal-mobile.png"),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Nuevo ticket", exact: true }).click();
  await page.getByLabel(/^Asunto/).fill("Solicitud móvil " + Date.now());
  await page
    .getByLabel("Describe tu solicitud")
    .fill(
      "Esta solicitud fue registrada desde la vista móvil del portal de empleados.",
    );
  await page.getByRole("button", { name: "Crear ticket", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Conversación" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Nota interna", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Cambiar estado", { exact: true })).toHaveCount(
    0,
  );
  await page.screenshot({
    path: path.join(artifacts, "ticket-mobile.png"),
    fullPage: true,
  });
});

test("Administración y sesión: alta de usuarios y aislamiento dentro de la misma organización", async ({
  playwright,
  page,
}) => {
  const admin = await session(playwright, "admin@sidecil.local");
  const directory = await (await admin.request.get("/api/v1/directory")).json();
  const sidecil = directory.organizations.find(
    (o: any) => o.name === "Sidecil",
  );
  const email = "prueba." + Date.now() + "@sidecil.local";
  const response = await admin.request.post("/api/v1/admin/users", {
    headers: admin.headers,
    data: {
      displayName: "Usuario de prueba aislado",
      email,
      password: credentials.password,
      organizationId: sidecil.id,
      teamId: null,
      role: "Requester",
    },
  });
  expect(response.status()).toBe(201);
  const newUser = await session(playwright, email);
  expect(
    (await (await newUser.request.get("/api/v1/tickets")).json()).total,
  ).toBe(0);
  const existing = await (await admin.request.get("/api/v1/tickets")).json();
  expect(
    (
      await newUser.request.get("/api/v1/tickets/" + existing.items[0].id)
    ).status(),
  ).toBe(404);
  await login(page, email);
  await expect(page.getByText("No hay tickets en esta vista")).toBeVisible();
  await page.context().clearCookies();
  await page.getByRole("button", { name: "Actualizar bandeja" }).click();
  await expect(
    page.getByRole("button", { name: "Ingresar a mi espacio" }),
  ).toBeVisible();
  await admin.request.dispose();
  await newUser.request.dispose();
});
