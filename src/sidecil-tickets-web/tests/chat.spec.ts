import { test, expect, type APIRequestContext } from "@playwright/test";
import { createServer, type Server } from "node:http";
import { readFileSync, readdirSync, mkdirSync } from "node:fs";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import path from "node:path";
import { simpleParser } from "mailparser";
const root = path.resolve("../..");
const credentials = JSON.parse(
  readFileSync(path.join(root, ".local/demo-credentials.json"), "utf8"),
);
const run = promisify(execFile);
async function login(
  request: APIRequestContext,
  email = "admin@sidecil.local",
) {
  let csrf = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  expect(
    (
      await request.post("/api/v1/auth/login", {
        headers: { "X-CSRF-TOKEN": csrf },
        data: { email, password: credentials.password },
      })
    ).status(),
  ).toBe(204);
  csrf = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  return { "X-CSRF-TOKEN": csrf };
}
async function dispatch() {
  await run(
    path.join(root, ".tools/dotnet/dotnet.exe"),
    ["Sidecil.Tickets.Worker.dll", "--once"],
    {
      cwd: path.join(root, ".local/runtime/mail"),
      timeout: 40000,
      env: {
        ...process.env,
        DOTNET_ENVIRONMENT: "Development",
        Mail__Mode: "Pickup",
        Mail__PickupDirectory: path.join(root, ".local/mail/out"),
        Mail__InboxDirectory: path.join(root, ".local/mail/in"),
        ConnectionStrings__Tickets:
          "Server=(localdb)\\MSSQLLocalDB;Database=SidecilTicketsDev;Trusted_Connection=True;TrustServerCertificate=True",
      },
    },
  );
}
test("Chat externo: adjuntos, confirmación, idempotencia y acceso del agente", async ({
  page,
  playwright,
}) => {
  test.setTimeout(120000);
  page.setDefaultTimeout(15000);
  const headers = await login(page.request);
  const created = await page.request.post("/api/v1/admin/chat/sites", {
    headers,
    data: {
      name: "ERP de prueba " + Date.now(),
      origin: "http://localhost:5091",
    },
  });
  expect(created.status()).toBe(201);
  const site = await created.json();
  const server: Server = createServer((req, res) => {
    res.setHeader("Content-Type", "text/html; charset=utf-8");
    res.end(
      `<!doctype html><html lang="es"><head><title>ERP · Prueba de integración</title><style>body{font:16px Arial;background:#f3f6fa;padding:40px;color:#24354c}section{padding:35px;border-radius:18px;background:white;max-width:700px}</style></head><body><section><h1>ERP de Sidecil</h1><p>Módulo de facturación</p><p>El soporte está disponible desde el botón de la esquina.</p></section><script src="http://localhost:5080/sidecil-chat.js" data-site="${site.id}" defer></script></body></html>`,
    );
  });
  await new Promise<void>((resolve) => server.listen(5091, resolve));
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  try {
    await page.goto("http://localhost:5091");
    await page.getByRole("button", { name: "¿Necesitas ayuda?" }).click();
    const frame = page.frameLocator("#sidecil-support-widget iframe");
    await frame.getByRole("button", { name: "Iniciar solicitud" }).click();
    const recipient = `chat-${Date.now()}@example.com`;
    for (const [label, value] of [
      ["Tu nombre", "Cliente del ERP"],
      ["Tu correo", recipient],
      ["Módulo o pantalla", "Facturación / Nueva factura"],
      ["Asunto de la solicitud", "No puedo guardar mi factura"],
      [
        "Descripción del problema",
        "Al guardar la factura aparece un error. Esperaba registrar el documento y obtener su número.",
      ],
    ]) {
      await frame.getByLabel(label).fill(value);
      await frame
        .getByRole("button", { name: "Continuar", exact: true })
        .click();
    }
    await expect(
      frame.getByRole("heading", { name: "Revisa tu solicitud" }),
    ).toBeVisible();
    await frame.locator('input[type="file"]').setInputFiles({
      name: "evidencia.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("Error al registrar factura. Código FACT-102."),
    });
    await expect(
      frame.locator(".chat-file").filter({ hasText: "evidencia.txt" }),
    ).toBeVisible();
    const session = await page
      .frames()
      .find((f) => f.url().includes("/chat-widget"))!
      .evaluate(
        (siteId) =>
          JSON.parse(sessionStorage.getItem("sidecil-chat:" + siteId)!),
        site.id,
      );
    const chatHeaders = {
      "X-Sidecil-Widget": "1",
      Authorization: "Bearer " + session.token,
    };
    const url = "/api/v1/chat/sessions/" + session.id;
    const anon = await playwright.request.newContext({
      baseURL: "http://localhost:5080",
    });
    expect(
      (await anon.get(url, { headers: { "X-Sidecil-Widget": "1" } })).status(),
    ).toBe(401);
    expect(
      (
        await anon.get(url, {
          headers: {
            "X-Sidecil-Widget": "1",
            Authorization: "Bearer " + "0".repeat(64),
          },
        })
      ).status(),
    ).toBe(401);
    expect(
      (
        await anon.post(url + "/attachments", {
          headers: {
            ...chatHeaders,
            "Content-Type": "application/octet-stream",
            "X-File-Name": "fake.png",
          },
          data: Buffer.from("<script>alert(1)</script>"),
        })
      ).status(),
    ).toBe(400);
    expect(
      (
        await anon.post(url + "/attachments", {
          headers: {
            ...chatHeaders,
            "Content-Type": "application/octet-stream",
            "X-File-Name": "big.txt",
          },
          data: Buffer.alloc(5 * 1024 * 1024 + 1, 65),
        })
      ).status(),
    ).toBe(413);
    const second = await (
      await anon.post("/api/v1/chat/sessions", {
        headers: { "X-Sidecil-Widget": "1" },
        data: { siteId: site.id },
      })
    ).json();
    expect(
      (
        await anon.get(url, {
          headers: {
            "X-Sidecil-Widget": "1",
            Authorization: "Bearer " + second.token,
          },
        })
      ).status(),
    ).toBe(401);
    const files = (await (await anon.get(url, { headers: chatHeaders })).json())
      .attachments;
    const wrongDelete = await anon.delete(
      `/api/v1/chat/sessions/${second.id}/attachments/${files[0].id}`,
      {
        headers: {
          "X-Sidecil-Widget": "1",
          Authorization: "Bearer " + second.token,
        },
      },
    );
    expect(wrongDelete.status()).toBe(404);
    await page.reload();
    await page.getByRole("button", { name: "¿Necesitas ayuda?" }).click();
    await expect(
      frame.getByRole("heading", { name: "Revisa tu solicitud" }),
    ).toBeVisible();
    await expect(
      frame.locator(".chat-file").filter({ hasText: "evidencia.txt" }),
    ).toBeVisible();
    mkdirSync(path.join(root, "artifacts/screenshots"), { recursive: true });
    await page.screenshot({
      path: path.join(root, "artifacts/screenshots/chat-erp.png"),
      fullPage: true,
    });
    await frame
      .getByRole("button", { name: "Confirmar y enviar solicitud" })
      .click();
    await expect(
      frame.getByRole("heading", { name: "Revisa tu correo." }),
    ).toBeVisible();
    const retries = await Promise.all([
      anon.post(url + "/submit", { headers: chatHeaders, data: {} }),
      anon.post(url + "/submit", { headers: chatHeaders, data: {} }),
    ]);
    expect(retries.map((r) => r.status())).toEqual([202, 202]);
    expect(
      (
        await anon.delete(url + "/attachments/" + files[0].id, {
          headers: chatHeaders,
        })
      ).status(),
    ).toBe(409);
    await dispatch();
    const messages = await Promise.all(
      readdirSync(path.join(root, ".local/mail/out"))
        .filter((f) => f.endsWith(".eml"))
        .map((f) =>
          simpleParser(readFileSync(path.join(root, ".local/mail/out", f))),
        ),
    );
    const verification = messages.filter((m) =>
      (Array.isArray(m.to)
        ? m.to.flatMap((x) => x.value)
        : m.to?.value || []
      ).some((x) => x.address === recipient),
    );
    expect(verification).toHaveLength(1);
    const token = verification[0].text!.match(/#token=([A-F0-9]+)/)![1];
    const csrf = (await (await anon.get("/api/v1/auth/csrf")).json()).token;
    const confirmed = await anon.post("/api/v1/public/confirm", {
      headers: { "X-CSRF-TOKEN": csrf },
      data: { token },
    });
    expect(confirmed.status()).toBe(200);
    const number = (await confirmed.json()).number;
    expect(
      (
        await (
          await anon.post("/api/v1/public/confirm", {
            headers: { "X-CSRF-TOKEN": csrf },
            data: { token },
          })
        ).json()
      ).number,
    ).toBe(number);
    await frame.getByRole("button", { name: "Ya confirmé mi correo" }).click();
    await expect(frame.getByText(number, { exact: true })).toBeVisible();
    const ticket = (
      await (await page.request.get("/api/v1/tickets?search=" + number)).json()
    ).items[0];
    const detail = await (
      await page.request.get("/api/v1/tickets/" + ticket.id)
    ).json();
    expect(detail.messages[0].source).toBe("Chat");
    expect(detail.messages[0].body).toContain("Facturación / Nueva factura");
    expect(detail.attachments).toHaveLength(1);
    const download = `/api/v1/tickets/${ticket.id}/attachments/${files[0].id}`;
    const file = await page.request.get(download);
    expect(file.status()).toBe(200);
    expect(file.headers()["content-disposition"]).toContain("attachment");
    expect(await file.text()).toContain("FACT-102");
    expect((await anon.get(download)).status()).toBe(401);
    const customer = await playwright.request.newContext({
      baseURL: "http://localhost:5080",
    });
    await login(customer, "cliente@sidecil.local");
    expect((await customer.get(download)).status()).toBe(404);
    expect((await customer.get("/api/v1/admin/chat/sites")).status()).toBe(403);
    await page.goto("http://localhost:5080/tickets/" + ticket.id);
    await expect(
      page.getByRole("heading", { name: "Archivos recibidos desde el chat" }),
    ).toBeVisible();
    await page.screenshot({
      path: path.join(root, "artifacts/screenshots/chat-ticket.png"),
      fullPage: true,
    });
    await page.goto("http://localhost:5080/admin/chat");
    await expect(
      page.getByRole("heading", { name: "Chat en tus sistemas." }),
    ).toBeVisible();
    await page.screenshot({
      path: path.join(root, "artifacts/screenshots/chat-admin.png"),
      fullPage: true,
    });
    const csp = (await anon.get("/chat-widget?site=" + site.id)).headers()[
      "content-security-policy"
    ];
    expect(csp).toContain("frame-ancestors 'self' http://localhost:5091");
    expect(
      (await anon.get("/")).headers()["content-security-policy"],
    ).toContain("frame-ancestors 'none'");
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("http://localhost:5091");
    await page.getByRole("button", { name: "¿Necesitas ayuda?" }).click();
    await expect(frame.getByText(number, { exact: true })).toBeVisible();
    await page.screenshot({
      path: path.join(root, "artifacts/screenshots/chat-mobile.png"),
      fullPage: true,
    });
    expect(
      await page
        .frames()
        .find((f) => f.url().includes("/chat-widget"))!
        .evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    ).toBe(true);
    expect(errors).toEqual([]);
    expect(
      (
        await page.request.put("/api/v1/admin/chat/sites/" + site.id, {
          headers,
          data: { enabled: false },
        })
      ).status(),
    ).toBe(204);
    expect((await anon.get(url, { headers: chatHeaders })).status()).toBe(401);
    expect(
      (await anon.get("/chat-widget?site=" + site.id)).headers()[
        "content-security-policy"
      ],
    ).toContain("frame-ancestors 'none'");
    await customer.dispose();
    await anon.dispose();
  } finally {
    server.closeAllConnections();
    await new Promise<void>((resolve) => server.close(() => resolve()));
  }
});
