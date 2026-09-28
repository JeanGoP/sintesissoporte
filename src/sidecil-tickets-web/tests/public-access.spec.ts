import { test, expect } from "@playwright/test";
test.use({baseURL:"http://127.0.0.1:5187"});
test("Sin cuenta: verifica y responde al ticket existente",async({page})=>{
 const messages:any[]=[];
 await page.route("**/api/v1/**",async route=>{
   const path=new URL(route.request().url()).pathname;
   if(path.endsWith("/auth/csrf"))return route.fulfill({json:{token:"csrf"}});
   if(path.endsWith("/public/config"))return route.fulfill({json:{available:true,testMode:false,categories:["General"],modules:[{id:"d1000000-0000-0000-0000-000000000001",name:"General",category:"General"}]}});
   if(path.endsWith("/public/access/start")){messages.push(route.request().postDataJSON());return route.fulfill({status:202,json:{id:"11111111-1111-1111-1111-111111111111"}});}
   if(path.endsWith("/public/access/verify"))return route.fulfill({json:{token:"A".repeat(64)}});
   if(path.endsWith("/public/access/tickets"))return route.fulfill({json:{name:"Cliente prueba",email:"cliente@test.invalid",tickets:[{id:"t1",number:"SC-00001",subject:"Problema anterior",status:"New"}]}});
   if(path.endsWith("/public/access/send")){const form=route.request().postData()||"";messages.push(form);return route.fulfill({json:{number:form.includes("t1")?"SC-00001":"SC-00002",existing:form.includes("t1")}});}
   return route.fulfill({status:404});
 });
 await page.goto("/solicitar");
 await page.getByRole("textbox",{name:"Nombre completo"}).fill("Cliente prueba");
 await page.getByRole("textbox",{name:"Correo electrónico"}).fill("cliente@test.invalid");
 await page.getByRole("button",{name:"Enviar código"}).click();
 await expect(page.getByRole("heading",{name:"Escribe el código que te enviamos."})).toBeVisible();
 expect(messages[0].email).toBe("cliente@test.invalid");
 await page.getByRole("textbox",{name:"Código de 8 dígitos"}).fill("12345678");
 await page.getByRole("button",{name:"Verificar código"}).click();
 await expect(page.getByRole("button",{name:/SC-00001/})).toBeVisible();
 await page.getByRole("button",{name:/SC-00001/}).click();
 await expect(page.getByRole("textbox",{name:"Asunto"})).toHaveCount(0);
 await page.getByRole("textbox",{name:"Describe tu solicitud"}).fill("Añado más información al problema anterior");
 await page.getByRole("button",{name:"Enviar al ticket"}).click();
 await expect(page.getByText(/Tu mensaje se agregó al ticket SC-00001/)).toBeVisible();
 expect(messages[1]).toContain('name="ticketId"');
 expect(messages[1]).toContain("t1");
 await page.goto("/solicitar");
 await page.getByRole("textbox",{name:"Nombre completo"}).fill("Cliente prueba");
 await page.getByRole("textbox",{name:"Correo electrónico"}).fill("cliente@test.invalid");
 await page.getByRole("button",{name:"Enviar código"}).click();
 await page.getByRole("textbox",{name:"Código de 8 dígitos"}).fill("87654321");
 await page.getByRole("button",{name:"Verificar código"}).click();
 await page.getByRole("button",{name:"Crear una nueva solicitud"}).click();
 await page.getByRole("textbox",{name:"Asunto"}).fill("Nuevo problema de prueba");
 await page.getByRole("textbox",{name:"Nombre de la empresa"}).fill("Empresa de prueba");
 await page.getByRole("combobox",{name:"Categoría"}).click();
 await page.getByRole("option",{name:"General"}).click();
 await page.getByRole("combobox",{name:"Módulo"}).click();
 await page.getByRole("option",{name:"General"}).click();
 await page.getByRole("textbox",{name:"Describe tu solicitud"}).fill("Una solicitud completamente nueva");
 await page.getByRole("button",{name:"Crear ticket",exact:true}).click();
 await expect(page.getByText(/Tu ticket SC-00002 está registrado/)).toBeVisible();
});
