import { test, expect } from '@playwright/test';
test.use({baseURL:'http://127.0.0.1:5187'});
test('Administración confirma bajas y explica por qué no se puede eliminar',async({page})=>{
 let active=true,exists=true,deletedCategory=false;
 await page.route('**/api/v1/**',async route=>{
 const path=new URL(route.request().url()).pathname;const method=route.request().method();
 if(path.endsWith('/auth/me'))return route.fulfill({json:{id:'admin',displayName:'Administrador',email:'admin@test.invalid',role:'Admin',organization:'Empresa'}});
 if(path.endsWith('/auth/csrf'))return route.fulfill({json:{token:'csrf'}});
 if(path.endsWith('/directory'))return route.fulfill({json:{categories:[],modules:[],agents:[],organizations:[],teams:[],users:exists?[{id:'agent',displayName:'Agente prueba',email:'agent@test.invalid',role:'Agent',isActive:active,moduleIds:[],organizationIds:[]}]:[]}});
 if(path.endsWith('/catalog'))return route.fulfill({json:{categories:deletedCategory?[]:[{id:'cat',name:'Categoría de prueba',enabled:true}],modules:[]}});
 if(path.endsWith('/agent/enabled')){active=route.request().postDataJSON().enabled;return route.fulfill({status:204});}
 if(path.endsWith('/users/agent')&&method==='DELETE'){return route.fulfill({status:409,json:{detail:'Este agente tiene historial. Desactívalo para conservarlo.'}});}
 if(path.endsWith('/categories/cat')&&method==='DELETE'){deletedCategory=true;return route.fulfill({status:204});}
 return route.fulfill({json:{items:[],total:0,summary:{active:0,unassigned:0,overdue:0,resolved:0}}});
 });
 await page.goto('/admin');await page.getByRole('button',{name:'Desactivar',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Cancelar',exact:true}).click();expect(active).toBe(true);
 await page.getByRole('button',{name:'Desactivar',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Confirmar',exact:true}).click();await expect(page.getByRole('button',{name:'Reactivar',exact:true})).toBeVisible();expect(active).toBe(false);
 await page.getByRole('button',{name:'Reactivar',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Confirmar',exact:true}).click();await expect(page.getByRole('button',{name:'Desactivar',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'Eliminar agente',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Confirmar',exact:true}).click();await expect(page.getByRole('dialog').getByText('Este agente tiene historial. Desactívalo para conservarlo.')).toBeVisible();await page.getByRole('dialog').getByRole('button',{name:'Cancelar',exact:true}).click();
 await page.getByRole('button',{name:'Editar categoría',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Eliminar',exact:true}).click();await page.getByRole('button',{name:'Eliminar definitivamente',exact:true}).click();await expect(page.getByText('Categoría de prueba',{exact:true})).toHaveCount(0);expect(deletedCategory).toBe(true);
});
