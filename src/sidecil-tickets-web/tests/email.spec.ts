import { test, expect, type APIRequestContext } from "@playwright/test";
import { readFileSync, readdirSync, writeFileSync, mkdirSync } from "node:fs";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { randomUUID } from "node:crypto";
import path from "node:path";
import { simpleParser } from "mailparser";
const root = path.resolve("../..");
const credentials = JSON.parse(
  readFileSync(path.join(root, ".local/demo-credentials.json"), "utf8"),
);
const out = path.join(root, ".local/mail/out"),
  inbox = path.join(root, ".local/mail/in");
const run = promisify(execFile);
async function worker() {
  await run(
    path.join(root, ".tools/dotnet/dotnet.exe"),
    ["Sidecil.Tickets.Worker.dll", "--once"],
    {
      cwd: path.join(root, "artifacts/worker"),
      env: {
        ...process.env,
        DOTNET_ENVIRONMENT: "Development",
        Mail__Mode: "Pickup",
        Mail__PickupDirectory: out,
        Mail__InboxDirectory: inbox,
        ConnectionStrings__Tickets:
          "Server=(localdb)\\MSSQLLocalDB;Database=SidecilTicketsDev;Trusted_Connection=True;TrustServerCertificate=True",
      },
      timeout: 40000,
    },
  );
}
async function login(playwright: any, email: string) {
  const request: APIRequestContext = await playwright.request.newContext({
    baseURL: "http://localhost:5080",
  });
  let token = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  expect(
    (
      await request.post("/api/v1/auth/login", {
        headers: { "X-CSRF-TOKEN": token },
        data: { email, password: credentials.password },
      })
    ).status(),
  ).toBe(204);
  token = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  return { request, headers: { "X-CSRF-TOKEN": token } };
}
async function findMail(recipient: string, subjectPart: string) {
  const messages = await Promise.all(
    readdirSync(out)
      .filter((x) => x.endsWith(".eml"))
      .map((x) => simpleParser(readFileSync(path.join(out, x)))),
  );
  const match = messages
    .filter(
      (m) =>
        (Array.isArray(m.to)
          ? m.to.flatMap((x) => x.value)
          : m.to?.value || []
        ).some((x) => x.address === recipient) &&
        (m.subject || "").includes(subjectPart),
    )
    .at(-1);
  expect(match, "Correo esperado: " + subjectPart).toBeTruthy();
  return match!;
}
function reply(
  from: string,
  ref: string | undefined,
  subject: string,
  body: string,
  extra = "",
) {
  const messageId = randomUUID() + "@test.invalid";
  const raw =
    "From: " +
    from +
    "\r\nTo: soporte@sidecil.invalid\r\nMessage-ID: <" +
    messageId +
    ">\r\nSubject: " +
    subject +
    "\r\n" +
    (ref ? "In-Reply-To: " + ref + "\r\nReferences: " + ref + "\r\n" : "") +
    extra +
    "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n" +
    body;
  mkdirSync(inbox, { recursive: true });
  writeFileSync(path.join(inbox, randomUUID() + ".eml"), raw);
  return raw;
}
test("Correo completo: registrado, invitado, respuesta, duplicados y privacidad", async ({
  playwright,
  page,
  browser,
}) => {
  test.setTimeout(180000);
  const admin = await login(playwright, "admin@sidecil.local"),
    customer = await login(playwright, "cliente@sidecil.local");
  const result = await customer.request.post("/api/v1/tickets", {
    headers: customer.headers,
    data: {
      subject: "Correo de prueba " + Date.now(),
      body: "Necesito confirmar que la conversación continúa por correo electrónico.",
      category: "General",
      priority: "Normal",
    },
  });
  expect(result.status()).toBe(201);
  const ticket = await result.json(),
    url = "/api/v1/tickets/" + ticket.id;
  await worker();
  const receipt = await findMail("cliente@sidecil.local", ticket.number);
  expect(receipt.text).toContain("Hola Mariana López");
  expect(receipt.text).toContain(ticket.number);
  const before = await (await admin.request.get("/api/v1/admin/email")).json();
  let detail = await admin.request.get(url);
  expect(
    (
      await admin.request.post(url + "/messages", {
        headers: { ...admin.headers, "If-Match": detail.headers().etag },
        data: {
          body: "PRIVADO-NO-ENVIAR-" + ticket.number,
          visibility: "Internal",
        },
      })
    ).status(),
  ).toBe(204);
  const after = await (await admin.request.get("/api/v1/admin/email")).json();
  expect(after.outbound.length).toBe(before.outbound.length);
  detail = await admin.request.get(url);
  expect(
    (
      await admin.request.post(url + "/messages", {
        headers: { ...admin.headers, "If-Match": detail.headers().etag },
        data: {
          body: "Respuesta pública del equipo: estamos revisando el caso.",
          visibility: "Public",
        },
      })
    ).status(),
  ).toBe(204);
  await worker();
  const publicReply = await findMail(
    "cliente@sidecil.local",
    "[" + ticket.number + "] Respuesta",
  );
  expect(publicReply.text).toContain("Respuesta pública del equipo");
  expect(publicReply.text).not.toContain("PRIVADO-NO-ENVIAR");
  const text = "Respuesta real por correo " + randomUUID();
  const raw = reply(
    "cliente@sidecil.local",
    publicReply.messageId,
    "Re: " + publicReply.subject,
    text,
  );
  await worker();
  let data = await (await admin.request.get(url)).json();
  expect(data.hasCustomerReply).toBe(true);
  expect(
    data.messages.filter((m: any) => m.body === text && m.source === "Email"),
  ).toHaveLength(1);
  writeFileSync(path.join(inbox, randomUUID() + ".eml"), raw);
  reply(
    "otro@test.invalid",
    publicReply.messageId,
    "Re: " + publicReply.subject,
    "Intento de remitente ajeno",
  );
  reply(
    "cliente@sidecil.local",
    undefined,
    "Re: [" + ticket.number + "]",
    "Solo el asunto no autoriza una respuesta",
  );
  reply(
    "cliente@sidecil.local",
    publicReply.messageId,
    "Respuesta automática",
    "Fuera de la oficina",
    "Auto-Submitted: auto-replied\r\n",
  );
  await worker();
  data = await (await admin.request.get(url)).json();
  expect(data.messages.filter((m: any) => m.source === "Email")).toHaveLength(
    1,
  );
  const inbound = (
    await (await admin.request.get("/api/v1/admin/email")).json()
  ).inbound;
  expect(
    inbound.some(
      (m: any) => m.state === "Review" && m.reason.includes("remitente"),
    ),
  ).toBe(true);
  expect(inbound.some((m: any) => m.state === "Ignored")).toBe(true);
  // El invitado verifica el correo antes de materializar el ticket, sin crear una cuenta.
  const guest = "invitado-" + Date.now() + "@test.invalid";
  await page.goto("/solicitar");
  await page.getByLabel("Nombre completo").fill("Cliente sin cuenta");
  await page.getByLabel("Correo electrónico").fill(guest);
  await page.getByLabel(/^Asunto/).fill("Solicitud sin cuenta " + Date.now());
  await page
    .getByLabel("Describe tu solicitud")
    .fill(
      "Deseo comunicarme con Sidecil sin registrarme y responder por correo.",
    );
  await page
    .getByRole("button", { name: "Enviar solicitud", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Revisa tu correo" }),
  ).toBeVisible();
  await worker();
  const verify = await findMail(guest, "Confirma tu solicitud");
  const link = verify.text?.match(
    /http:\/\/localhost:5080\/confirmar-solicitud#token=[A-F0-9]{64}/,
  )?.[0];
  expect(link).toBeTruthy();
  await page.goto(link!);
  await page
    .getByRole("button", { name: "Confirmar solicitud", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Solicitud registrada" }),
  ).toBeVisible();
  const number = (await page.locator(".guest-success").innerText()).match(
    /SC-\d+/,
  )![0];
  await worker();
  const guestReceipt = await findMail(guest, number);
  const guestTicket = (
    await (await admin.request.get("/api/v1/tickets?search=" + number)).json()
  ).items[0];
  expect(guestTicket.requester).toBe("Cliente sin cuenta");
  // Reutilizar la confirmación no crea otro ticket.
  const csrf = (await (await page.request.get("/api/v1/auth/csrf")).json())
    .token;
  const again = await page.request.post("/api/v1/public/confirm", {
    headers: { "X-CSRF-TOKEN": csrf },
    data: { token: link!.split("token=")[1] },
  });
  expect((await again.json()).number).toBe(number);
  expect(
    (await (await admin.request.get("/api/v1/tickets?search=" + number)).json())
      .total,
  ).toBe(1);
  reply(
    guest,
    guestReceipt.messageId,
    "Re: " + guestReceipt.subject,
    "Esta es mi respuesta como cliente sin cuenta.",
  );
  await worker();
  const guestDetail = await (
    await admin.request.get("/api/v1/tickets/" + guestTicket.id)
  ).json();
  expect(
    guestDetail.messages.some(
      (m: any) => m.source === "Email" && m.author === "Cliente sin cuenta",
    ),
  ).toBe(true);
  expect(
    (
      await await customer.request.get("/api/v1/tickets/" + guestTicket.id)
    ).status(),
  ).toBe(404);
  const adminContext = await browser.newContext({
    storageState: await admin.request.storageState(),
    viewport: { width: 1440, height: 1000 },
  });
  const adminPage = await adminContext.newPage();
  await adminPage.goto("/admin/email");
  await expect(
    adminPage.getByRole("heading", { name: "Correo de atención." }),
  ).toBeVisible();
  await expect(adminPage.locator(".mail-preview")).toContainText("SC-00125");
  mkdirSync(path.join(root, "artifacts/screenshots"), { recursive: true });
  await adminPage.screenshot({
    path: path.join(root, "artifacts/screenshots/correo-admin.png"),
    fullPage: true,
  });
  await adminPage.goto("/tickets/" + guestTicket.id);
  await expect(
    adminPage.getByText("Esta es mi respuesta como cliente sin cuenta."),
  ).toBeVisible();
  await expect(
    adminPage.getByText("Por correo", { exact: true }),
  ).toBeVisible();
  await adminPage.screenshot({
    path: path.join(root, "artifacts/screenshots/correo-conversacion.png"),
    fullPage: true,
  });
  await adminContext.close();
  await admin.request.dispose();
  await customer.request.dispose();
});
