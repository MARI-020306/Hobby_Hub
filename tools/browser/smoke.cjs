const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

function syntheticPng(){
 function chunk(type,data){const length=Buffer.alloc(4);length.writeUInt32BE(data.length);const body=Buffer.concat([Buffer.from(type),data]);let crc=0xffffffff;for(const byte of body){crc^=byte;for(let bit=0;bit<8;bit++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}const checksum=Buffer.alloc(4);checksum.writeUInt32BE((crc^0xffffffff)>>>0);return Buffer.concat([length,body,checksum]);}
 const header=Buffer.alloc(13);header.writeUInt32BE(1,0);header.writeUInt32BE(1,4);header[8]=8;header[9]=6;
 return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',zlib.deflateSync(Buffer.from([0,150,80,200,255]))),chunk('IEND',Buffer.alloc(0))]);
}

(async () => {
 const base=process.env.HOBBYHUB_URL||'http://127.0.0.1:5087';
 assert.match(base,/^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/);
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const context=await browser.newContext({viewport:{width:1366,height:900}});
 const page=await context.newPage();
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const email=`browser-${crypto.randomUUID()}@example.test`,password=crypto.randomBytes(18).toString('hex');
 const out=path.resolve(__dirname,'../../docs/artifacts');fs.mkdirSync(out,{recursive:true});
 let screenshots=0,mobileChecks=0;
 try {
  await page.goto(base+'/Auth/Registro');
  await page.locator('[name="Nombre"]').fill('Synthetic browser creator');
  await page.locator('[name="Correo"]').fill(email);
  await page.locator('[name="Password"]').fill(password);
  await page.locator('[name="ConfirmarPassword"]').fill(password);
  await page.locator('[name="RolId"]').selectOption({label:'Creador'});
  const [registered]=await Promise.all([page.waitForResponse(r=>r.request().method()==='POST'&&r.url().includes('/Auth/Registro')),page.locator('button[type="submit"]').click()]);
  assert.equal(registered.status(),302,'Browser registration did not redirect');await page.locator('#loginButton').waitFor();
  await page.locator('[name="Email"]').fill(email);await page.locator('[name="Password"]').fill(password);
  await page.locator('#loginButton').click();await page.waitForURL('**/Social/Inicio');
  await assertPage('/Social/Inicio','feed-desktop');
  await assertPage('/Social/Explorar','explore-desktop');
  await assertPage('/Comunidades','communities-desktop');
  await assertPage('/Perfil','profile-desktop');
  await assertPage('/Social/Mensajes','messages-desktop');
  await assertPage('/Cuenta/Seguridad','security-desktop');
  await assertPage('/Social/Ajustes','settings-desktop');
  await page.locator('[name="Bio"]').fill('');await page.locator('[name="Interests"]').fill('');
  await page.locator('form[action="/Social/Ajustes"] button').click();await page.getByText('Preferencias guardadas.').waitFor();
  await assertPage('/Social/Actividad','activity-desktop');
  await page.goto(base+'/Perfil');const publicProfile=await page.locator('a[href^="/Social/Persona/"]').first().getAttribute('href');
  await assertPage(publicProfile,'public-profile-desktop');
  await assertPage('/Social/Reacciones','reactions-desktop');
  await assertPage('/Social/MisComentarios','comments-desktop');
  await assertPage('/Social/Bloqueados','blocked-people-desktop');
  await assertPage('/Social/Notificaciones','notifications-desktop');
  await assertPage('/Social/Reportes','reports-desktop');
  await assertPage('/Social/Seguidores','followers-desktop');
  await page.goto(base+'/Comunidades?mostrarFormulario=true');
  const communityName='Synthetic browser photography '+crypto.randomUUID();
  await page.locator('[name="Formulario.Nombre"]').fill(communityName);
  await page.locator('[name="Formulario.Descripcion"]').fill('Comunidad sintética para comprobar fotografías y navegación.');
  await page.locator('[name="Formulario.Categoria"]').selectOption({label:'Fotografía'});
  await Promise.all([page.waitForResponse(r=>r.request().method()==='POST'&&r.url().includes('/Comunidades/Guardar')),page.locator('form.community-form button[type="submit"]').click()]);
  const communityLink=page.locator('h3 a').filter({hasText:communityName});await communityLink.waitFor();
  const communityId=(await communityLink.getAttribute('href')).split('/').pop();
  await assertPage('/Comunidades/Detalle/'+communityId,'community-detail-desktop');
  await assertPage('/Comunidades/Administrar/'+communityId,'community-management-desktop');
  await page.goto(base+'/Social/Nueva');
  await page.locator('[name="CommunityId"]').selectOption(communityId);
  await page.locator('[name="Content"]').fill('Fotografía sintética creada mediante el formulario real.');
  await page.locator('[name="Images"]').setInputFiles({name:'synthetic.png',mimeType:'image/png',buffer:syntheticPng()});
  await page.locator('main form button').click();await page.waitForURL('**/Social/Publicacion/*');
  await page.locator('.gallery img').waitFor();assert.equal(await page.locator('.gallery img').evaluate(img=>img.complete&&img.naturalWidth>0),true);
  await page.screenshot({path:path.join(out,'publication-desktop.png'),fullPage:true});
  screenshots++;
  await page.locator('[name="content"]').first().fill('Comentario sintético del navegador.');
  await page.locator('form[action="/Social/Comentar"] button').first().click();
  await page.getByText('Comentario sintético del navegador.',{exact:true}).waitFor();
  await page.locator('form[action="/Social/Guardar"] button').first().click();
  await assertPage('/Social/Guardados','saved-desktop');
  await page.setViewportSize({width:390,height:844});
  for(const [url,name] of [['/Social/Inicio','feed-mobile'],['/Comunidades','communities-mobile'],['/Perfil','profile-mobile'],['/Cuenta/Seguridad','security-mobile'],[publicProfile,'public-profile-mobile']]){
   await assertPage(url,name);
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+2),true,`Horizontal overflow: ${name}`);
   mobileChecks++;
  }
  await page.setViewportSize({width:1366,height:900});
  await page.locator('form[action="/Auth/Logout"] button').click();await page.locator('#loginButton').waitFor();
  const mailbox=path.resolve(__dirname,'../../Proyecto_HobbyHub/.local/mailbox');
  const before=new Set(fs.existsSync(mailbox)?fs.readdirSync(mailbox):[]);
  await page.goto(base+'/Cuenta/Recuperar');await page.locator('[name="Email"]').fill(email);
  await page.locator('main form button').click();await page.getByText(/Si la cuenta puede recuperarse/).waitFor();
  const message=fs.readdirSync(mailbox).find(file=>file.endsWith('.mail')&&!before.has(file));assert.ok(message,'Local encrypted mailbox was not written');
  fs.writeFileSync(path.resolve(mailbox,'../browser-mail-id.txt'),path.parse(message).name);
  await page.goto(base+'/Auth/Login');
  const adminFile=path.resolve(__dirname,'../../Proyecto_HobbyHub/.local/synthetic-administrator.json');
  assert.equal(fs.existsSync(adminFile),true,'Prepare the isolated synthetic administrator first.');
  const administrator=JSON.parse(fs.readFileSync(adminFile,'utf8'));
  await page.locator('[name="Email"]').fill(administrator.Email);await page.locator('[name="Password"]').fill(administrator.Password);
  await page.locator('#loginButton').click();await page.waitForURL('**/Social/Inicio');
  await assertPage('/Administracion','administration-desktop');
  await assertPage('/Administracion/Auditoria','audit-desktop');
  await assertPage('/Administracion/Comunidades','administration-communities-desktop');
  await assertPage('/Moderacion','moderation-desktop');
  await page.setViewportSize({width:390,height:844});await assertPage('/Administracion','administration-mobile');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+2),true,'Administrative mobile overflow');
  mobileChecks++;
  assert.deepEqual(errors,[]);console.log(`Browser: registration, two role logins, community creation, PNG publication, comment, saved content, POST logout and local recovery request passed; ${screenshots} screenshots and ${mobileChecks} mobile overflow checks. Credentials are not logged.`);
 } finally { await browser.close(); }
 async function assertPage(url,name){const response=await page.goto(base+url);assert.equal(response.status(),200,`${url}: unexpected status`);assert.equal(await page.locator('nav[aria-label="Navegación principal"], nav.hh-nav').count()>0,true);await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});screenshots++;}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
