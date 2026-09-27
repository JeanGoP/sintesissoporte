import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("El chat verifica una vez y permite continuar un ticket o crear otro", async ({
  page,
}) => {
  let verified = false,
    verifications = 0,
    sends = 0;
  let draft = {
    name: "",
    email: "",
    module: "",
    subject: "",
    body: "",
    category: "General",
  };
  const chosen: (string | null)[] = [];
  await page.route("**/api/v1/chat/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (path.includes("/sites/"))
      return route.fulfill({
        json: { name: "ERP prueba", available: true, testMode: false },
      });
    if (path.endsWith("/sessions"))
      return route.fulfill({ json: { id: "session-1", token: "test-token" } });
    if (path.endsWith("/draft")) {
      draft = route.request().postDataJSON();
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/verification")) return route.fulfill({ status: 204 });
    if (path.endsWith("/verify")) {
      expect(route.request().postDataJSON().code).toBe("12345678");
      verified = true;
      verifications++;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/tickets")) {
      expect(verified).toBe(true);
      return route.fulfill({
        json: [
          {
            id: "ticket-own",
            number: "SC-00042",
            subject: "Problema de facturación",
            status: "WaitingRequester",
          },
        ],
      });
    }
    if (path.endsWith("/send")) {
      chosen.push(route.request().postDataJSON().ticketId);
      sends++;
      return route.fulfill({
        json: { number: sends === 1 ? "SC-00042" : "SC-00043" },
      });
    }
    if (path.endsWith("/next")) {
      draft = { ...draft, module: "", subject: "", body: "" };
      return route.fulfill({
        json: { id: "session-2", token: "test-token-2" },
      });
    }
    if (method === "GET")
      return route.fulfill({
        json: {
          ...draft,
          verified,
          codeSent: false,
          submitted: false,
          number: null,
          attachments: [],
        },
      });
    return route.fulfill({ status: 404 });
  });
  await page.goto("/chat-widget?site=test-site");
  await page
    .getByRole("button", { name: "Iniciar solicitud", exact: true })
    .click();
  await page.getByLabel(/^Tu nombre/).fill("Cliente de prueba");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await page.getByLabel(/^Tu correo/).fill("customer@example.invalid");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await expect(page.getByText(/Problema de facturación/)).toHaveCount(0);
  await page
    .getByRole("button", { name: "Enviar código", exact: true })
    .click();
  await page.getByLabel(/^Código de 8 dígitos/).fill("12345678");
  await page.getByRole("button", { name: "Verificar código" }).click();
  await page.getByRole("button", { name: /SC-00042/ }).click();
  await page
    .getByLabel(/^Descripción del problema/)
    .fill("Todavía no puedo generar mi factura.");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await page
    .getByRole("button", { name: "Enviar al ticket", exact: true })
    .click();
  await expect(page.getByText("SC-00042", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "Continuar o crear otra solicitud" })
    .click();
  await page
    .getByRole("button", { name: "Crear una nueva solicitud", exact: true })
    .click();
  await page.getByLabel(/^Módulo o pantalla/).fill("Inventario");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await page
    .getByLabel(/^Asunto de la solicitud/)
    .fill("Nuevo problema con inventario");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await page
    .getByLabel(/^Descripción del problema/)
    .fill("El inventario no carga correctamente.");
  await page.getByRole("button", { name: "Continuar", exact: true }).click();
  await page.getByRole("button", { name: "Confirmar y crear ticket" }).click();
  await expect(page.getByText("SC-00043", { exact: true })).toBeVisible();
  expect(verifications).toBe(1);
  expect(chosen).toEqual(["ticket-own", null]);
});
