import { test, expect } from "@playwright/test";
test.use({baseURL:"http://127.0.0.1:5187"});
for (const persisted of [true,false]) test("Cambiar rol verifica persistencia: "+persisted,async({page})=>{
 let role="Requester",writes=0;
 await page.route("**/api/v1/**",async route=>{
   const path=new URL(route.request().url()).pathname;
   if(path.endsWith("/auth/me"))return route.fulfill({json:{id:"admin",displayName:"Administrador",role:"Admin",email:"admin@test.invalid"}});
   if(path.endsWith("/auth/csrf"))return route.fulfill({json:{token:"csrf"}});
   if(path.endsWith("/directory"))return route.fulfill({json:{categories:[],modules:[],agents:[],teams:[],organizations:[],users:[{id:"target",displayName:"Usuario prueba",email:"prueba@test.invalid",role,moduleIds:[],organizationIds:[]}]}});
   if(path.endsWith("/catalog"))return route.fulfill({json:{categories:[],modules:[]}});
   if(path.endsWith("/target/role")){
     expect(route.request().method()).toBe("PUT");
     expect(route.request().postDataJSON().role).toBe("Admin"); writes++;
     if(persisted)role="Admin";
     return route.fulfill({status:204});
   }
   return route.fulfill({json:{}});
 });
 await page.goto("/admin");
 await page.getByRole("button",{name:"Cambiar rol",exact:true}).click();
 await page.getByRole("dialog").getByRole("combobox").click();
 await page.getByRole("option",{name:"Administrador",exact:true}).click();
 await page.getByRole("button",{name:"Guardar rol",exact:true}).click();
 if(persisted){
   await expect(page.getByRole("dialog")).toHaveCount(0);
   await expect(page.getByRole("alert")).toContainText("Rol confirmado: Administrador");
   await expect(page.getByRole("row").filter({hasText:"prueba@test.invalid"})).toContainText("Administrador");
   await page.reload();
   await expect(page.getByRole("row").filter({hasText:"prueba@test.invalid"})).toContainText("Administrador");
 }else{
   await expect(page.getByRole("dialog")).toBeVisible();
   await expect(page.getByRole("dialog")).toContainText("El servidor no confirmó el nuevo rol");
   await page.getByRole("dialog").getByRole("button",{name:"Cancelar",exact:true}).click();
   await expect(page.getByRole("row").filter({hasText:"prueba@test.invalid"})).toContainText("Solicitante");
 }
 expect(writes).toBe(1);
});
