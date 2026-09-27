import { test, expect } from "@playwright/test";
test.use({ baseURL: "http://127.0.0.1:5187" });
test("Reiniciar olvida la sesión restaurada y permite empezar con otra identidad", async ({
  page,
}) => {
  let restored = 0;
  const mutations: string[] = [];
  await page.addInitScript(() => {
    if (!sessionStorage.getItem("seeded")) {
      sessionStorage.setItem("seeded", "1");
      sessionStorage.setItem(
        "sidecil-chat:test",
        JSON.stringify({ id: "old", token: "old-token" }),
      );
      sessionStorage.setItem("sidecil-chat:other", "keep");
    }
  });
  await page.route("**/api/v1/chat/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() !== "GET") mutations.push(path);
    if (path.endsWith("/sites/test"))
      return route.fulfill({
        json: {
          name: "ERP",
          available: true,
          testMode: false,
          categories: ["General"],
          modules: [],
        },
      });
    if (path.endsWith("/sessions/old")) {
      restored++;
      return route.fulfill({
        json: {
          name: "Cliente anterior",
          email: "old@test.invalid",
          module: "",
          category: "General",
          subject: "",
          body: "",
          verified: true,
          submitted: false,
          codeSent: false,
          number: null,
          attachments: [],
        },
      });
    }
    if (path.endsWith("/tickets"))
      return route.fulfill({
        json: [
          {
            id: "ticket",
            number: "SC-00001",
            subject: "Prueba anterior",
            status: "New",
          },
        ],
      });
    if (path.endsWith("/sessions"))
      return route.fulfill({ json: { id: "new", token: "new-token" } });
    return route.fulfill({ status: 404 });
  });
  await page.goto("/chat-widget?site=test");
  await expect(page.getByRole("button", { name: /SC-00001/ })).toBeVisible();
  await page
    .getByRole("button", { name: "Reiniciar chat", exact: true })
    .click();
  await page.getByRole("button", { name: "Cancelar", exact: true }).click();
  await expect(page.getByRole("button", { name: /SC-00001/ })).toBeVisible();
  await page
    .getByRole("button", { name: "Reiniciar chat", exact: true })
    .click();
  await page.getByRole("button", { name: "Reiniciar", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Estamos para ayudarte." }),
  ).toBeVisible();
  await expect(page.getByText(/SC-00001/)).toHaveCount(0);
  expect(
    await page.evaluate(() => sessionStorage.getItem("sidecil-chat:test")),
  ).toBeNull();
  expect(
    await page.evaluate(() => sessionStorage.getItem("sidecil-chat:other")),
  ).toBe("keep");
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Iniciar solicitud", exact: true }),
  ).toBeEnabled();
  expect(restored).toBe(1);
  await page
    .getByRole("button", { name: "Iniciar solicitud", exact: true })
    .click();
  await expect(page.getByLabel(/^Tu nombre/)).toHaveValue("");
  expect(mutations).toEqual(["/api/v1/chat/sessions"]);
});
