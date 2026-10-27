const {chromium}=require('playwright'),axe=require('axe-core');
const fs=require('node:fs'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {login}=require('./browser-helpers.cjs');
const base='http://127.0.0.1:5088',read=f=>JSON.parse(fs.readFileSync(f,'utf8').replace(/^\uFEFF/,''));
const fixtures=read('.local/visual-browser-accounts.json'),privileged=read('.local/visual-privileged-accounts.json');
assert.equal(fixtures.Database,privileged.Database);assert.match(fixtures.Database,/^HobbyHub_Development_Visual_20261008_[a-f0-9]{8}$/);
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});const result={roles:[],pages:0,links:0,forms:0,buttons:0,accessibility:[],performance:[],consoleErrors:[],permissionChecks:0};
 try{
 const ordinary={Role:'Usuario',Email:'delivery-user-'+crypto.randomUUID()+'@example.test',Password:crypto.randomBytes(18).toString('hex')};
 const registration=await browser.newPage();await registration.goto(base+'/Auth/Registro');await registration.locator('[name=Nombre]').fill('Demostración sintética usuario');await registration.locator('[name=Correo]').fill(ordinary.Email);await registration.locator('[name=Password]').fill(ordinary.Password);await registration.locator('[name=ConfirmarPassword]').fill(ordinary.Password);await registration.locator('[name=RolId]').selectOption({label:'Usuario'});await Promise.all([registration.waitForNavigation(),registration.locator('button[type=submit]').click()]);await registration.locator('#loginButton').waitFor();await registration.close();
 const accounts=[ordinary,{...fixtures.Owner,Role:'Creador'},...privileged.Accounts];
 for(const account of accounts){
  const context=await browser.newContext({viewport:{width:1366,height:900}}),page=await context.newPage();page.on('pageerror',e=>result.consoleErrors.push(e.message));
  await login(page,account);result.roles.push(account.Role);
  const moderator=['Administrador','Moderador'].includes(account.Role),admin=account.Role==='Administrador';
  assert.equal(await page.locator('#main-navigation a[href^="/Administracion"]').count()>0,admin);assert.equal(await page.locator('#main-navigation a[href^="/Moderacion"]').count()>0,moderator);
  const routes=['/Social/Inicio','/Social/Explorar','/Comunidades/Index','/Perfil','/Perfil/Editar','/Social/Nueva','/Social/Mensajes','/Social/Notificaciones','/Social/Reportes','/Social/Seguidores','/Social/Guardados','/Social/Ajustes','/Cuenta/Seguridad','/Social/Actividad','/Social/Reacciones','/Social/MisComentarios','/Social/Bloqueados','/Home/Privacy'];
  if(moderator)routes.push('/Moderacion','/Moderacion?state=Resuelto');if(admin)routes.push('/Administracion','/Administracion/Auditoria','/Administracion/Comunidades');
  const checked=new Set();
  for(const route of routes){
   const response=await page.goto(base+route,{waitUntil:'domcontentloaded'});assert.equal(response.status(),200,account.Role+' '+route);assert.equal(await page.locator('.app-shell').count(),1);assert.equal(response.headers()['x-content-type-options'],'nosniff');assert.equal(response.headers()['x-frame-options'],'DENY');
   const timing=response.request().timing();result.performance.push({role:account.Role,route,ttfbMs:Math.round(timing.responseStart-timing.requestStart)});result.pages++;
   const controls=await page.evaluate(()=>{
    const visible=el=>!!el.getClientRects().length;
    const fields=[...document.querySelectorAll('input:not([type=hidden]):not([type=submit]):not([type=button]),textarea,select')].filter(visible);
    const unlabeled=fields.filter(el=>!el.labels?.length&&!el.getAttribute('aria-label')&&!el.getAttribute('aria-labelledby')).map(el=>el.name||el.tagName);
    const images=[...document.querySelectorAll('img')].filter(el=>!el.hasAttribute('alt')).length;
    const forms=[...document.querySelectorAll('form')];const unsafe=forms.filter(f=>f.method.toLowerCase()==='post'&&!f.querySelector('[name=__RequestVerificationToken]')).length;
    const buttons=[...document.querySelectorAll('button')].filter(visible);const decorative=buttons.filter(b=>!b.form&&b.id!=='navigation-toggle'&&!b.getAttribute('onclick')&&!b.getAttribute('data-bs-toggle')).length;
    const ids=[...document.querySelectorAll('[id]')].map(el=>el.id);const duplicateIds=ids.filter((id,i)=>ids.indexOf(id)!==i);
    const links=[...document.querySelectorAll('a[href]')].filter(visible).map(a=>({href:a.getAttribute('href'),text:a.textContent.trim()}));
    return{unlabeled,images,unsafe,forms:forms.length,buttons:buttons.length,decorative,duplicateIds,links};
   });
   assert.deepEqual(controls.unlabeled,[],account.Role+' '+route+' labels');assert.equal(controls.images,0,route+' alt');assert.equal(controls.unsafe,0,route+' csrf');assert.equal(controls.decorative,0,route+' decorative buttons');assert.deepEqual(controls.duplicateIds,[],route+' duplicate IDs');result.forms+=controls.forms;result.buttons+=controls.buttons;
   for(const link of controls.links){
    if(link.href.startsWith('#')){if(link.href.length>1)assert.ok(await page.locator(link.href).count(),route+' missing anchor');continue;}
    const target=new URL(link.href,base);if(target.origin!==base||checked.has(target.href))continue;checked.add(target.href);
    const linked=await context.request.get(target.href);assert.ok(linked.status()<400,`${account.Role}: ${route}: broken rendered link ${target.pathname} (${linked.status()})`);result.links++;
   }
   if(['/Social/Inicio','/Perfil','/Social/Nueva','/Social/Ajustes','/Cuenta/Seguridad','/Administracion','/Administracion/Auditoria','/Moderacion'].includes(route)){
    await page.addScriptTag({content:axe.source});const analysis=await page.evaluate(async()=>{const a=await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']}});return a.violations.map(v=>({id:v.id,impact:v.impact,count:v.nodes.length,targets:v.nodes.slice(0,3).map(n=>n.target)}));});result.accessibility.push({role:account.Role,route,violations:analysis});
   }
  }
  for(const [route,allowed] of [['/Administracion',admin],['/Moderacion',moderator]]){const r=await page.goto(base+route);assert.equal(r.status(),allowed?200:403,account.Role+' permissions '+route);result.permissionChecks++;}
  await page.goto(base+'/does-not-exist');assert.match(await page.locator('body').innerText(),/404/);assert.equal(await page.locator('.app-shell').count(),1);
  await page.goto(base+'/Social/Inicio?page=-5');await page.getByText('Página 1',{exact:true}).waitFor();
  await page.goto(base+'/Social/Explorar?q='+encodeURIComponent('query-that-does-not-exist-xyz'));await page.locator('[name=q]').waitFor();
  await page.goto(base+'/Social/Ajustes');await page.evaluate(()=>{const input=document.querySelector('[name=Name]');const validator=window.jQuery(input.form).validate();window.jQuery.validator.addMethod('deliveryFalse',()=>false);window.jQuery(input).rules('add',{deliveryFalse:true,messages:{deliveryFalse:'<img src=x onerror="window.deliveryXss=true">'}});validator.element(input);});assert.equal(await page.evaluate(()=>window.deliveryXss===true),false);assert.equal(await page.locator('label.error img, .field-validation-error img').count(),0);result.permissionChecks++;
  await context.close();
 }
 assert.deepEqual(result.consoleErrors,[]);
 const times=result.performance.map(p=>p.ttfbMs).filter(n=>n>=0).sort((a,b)=>a-b);result.performanceSummary={samples:times.length,p50Ms:times[Math.floor(times.length*.5)],p95Ms:times[Math.floor(times.length*.95)],maxMs:times.at(-1),scope:'Local HTML TTFB; no load test'};
 fs.mkdirSync('.local/final-review',{recursive:true});fs.writeFileSync('.local/final-review/browser-review.json',JSON.stringify(result,null,2));
 const issues=result.accessibility.flatMap(p=>p.violations.map(v=>({route:p.route,role:p.role,id:v.id,impact:v.impact,count:v.count})));
 console.log(JSON.stringify({roles:result.roles,pages:result.pages,links:result.links,forms:result.forms,buttons:result.buttons,permissionChecks:result.permissionChecks,performance:result.performanceSummary,accessibilityIssues:issues},null,2));
 }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
