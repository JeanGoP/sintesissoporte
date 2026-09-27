import {test,expect} from '@playwright/test';
test.use({baseURL:'http://127.0.0.1:5187'});
test('El administrador crea categorías y módulos y configura el acceso del agente',async({page})=>{
 const categories=[{id:'c1',name:'General',enabled:true}]; const modules:{id:string;name:string;categoryId:string;enabled:boolean}[]=[]; let saved=false;
 await page.route('**/api/v1/**',async route=>{
  const path=new URL(route.request().url()).pathname,method=route.request().method();
  if(path.endsWith('/auth/me'))return route.fulfill({json:{id:'admin',displayName:'Administrador',email:'admin@test.invalid',role:'Admin',organizationId:'org',organization:'Empresa'}});
  if(path.endsWith('/auth/csrf'))return route.fulfill({json:{token:'csrf'}});
  if(path.endsWith('/directory'))return route.fulfill({json:{categories:categories.map(c=>c.name),modules:modules.map(m=>({...m,category:categories.find(c=>c.id===m.categoryId)!.name})),organizations:[{id:'org',name:'Empresa'}],teams:[],agents:[],users:[{id:'agent',displayName:'Agente prueba',email:'agent@test.invalid',role:'Agent',organizationId:'org',organizationIds:[],moduleIds:[]}]}});
  if(path.endsWith('/catalog')&&method==='GET')return route.fulfill({json:{categories,modules}});
  if(path.endsWith('/categories')&&method==='POST'){const r=route.request().postDataJSON();categories.push({id:'c2',name:r.name,enabled:true});return route.fulfill({json:categories[1]});}
  if(path.endsWith('/modules')&&method==='POST'){const r=route.request().postDataJSON();expect(r.categoryId).toBe('c2');modules.push({id:'m1',...r});return route.fulfill({json:modules[0]});}
  if(path.endsWith('/agent/scope')){expect(route.request().postDataJSON().moduleIds).toEqual(['m1']);saved=true;return route.fulfill({status:204});}
  return route.fulfill({json:{items:[],total:0,summary:{active:0,unassigned:0,overdue:0,resolved:0}}});
 });
 await page.goto('/admin');
 await page.getByRole('button',{name:'Nueva categoría',exact:true}).click();
 await page.getByRole('dialog').getByLabel(/^Nombre/).fill('Soporte técnico');await page.getByRole('dialog').getByRole('button',{name:'Guardar',exact:true}).click();
 await page.getByRole('button',{name:'Nuevo módulo',exact:true}).click();
 await page.getByRole('dialog').getByLabel(/^Nombre/).fill('Inventario');await page.getByLabel('Categoría del módulo').click();await page.getByRole('option',{name:'Soporte técnico'}).click();await page.getByRole('dialog').getByRole('button',{name:'Guardar',exact:true}).click();
 await expect(page.getByRole('button',{name:'Inventario',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'Empresas y módulos',exact:true}).click();await page.getByLabel('Módulos del agente').click();await page.getByRole('option',{name:'Soporte técnico / Inventario'}).click();await page.keyboard.press('Escape');await page.getByRole('button',{name:'Guardar acceso'}).click();await expect(page.getByRole('dialog')).toHaveCount(0);expect(saved).toBe(true);
});
test('Cambiar categoría limpia el módulo y no ofrece escoger agente',async({page})=>{
 await page.route('**/api/v1/public/config',route=>route.fulfill({json:{available:true,categories:['General','Soporte técnico'],modules:[{id:'m1',name:'General',category:'General'},{id:'m2',name:'Inventario',category:'Soporte técnico'}]}}));
 await page.goto('/solicitar');await page.getByLabel(/^Módulo/).click();await page.getByRole('option',{name:'General',exact:true}).click();await page.getByLabel(/^Categoría/).click();await page.getByRole('option',{name:'Soporte técnico'}).click();await page.getByLabel(/^Módulo/).click();await expect(page.getByRole('option',{name:'General',exact:true})).toHaveCount(0);await page.getByRole('option',{name:'Inventario'}).click();await expect(page.getByLabel('Responsable')).toHaveCount(0);await expect(page.getByLabel('Agente',{exact:true})).toHaveCount(0);
});
