const assert=require('node:assert/strict');
const base='http://127.0.0.1:5088';
async function login(page,account){
 for(let attempt=0;attempt<3;attempt++){
  await page.context().clearCookies();await page.goto(base+'/Auth/Login');
  await page.locator('[name=Email]').fill(account.Email);await page.locator('[name=Password]').fill(account.Password);
  const action=new URL(await page.locator('#loginForm').getAttribute('action'),base).pathname;
  const [response]=await Promise.all([page.waitForResponse(r=>r.request().method()==='POST'&&new URL(r.url()).pathname===action),page.locator('#loginButton').click()]);
  if(response.status()===429){
   const seconds=Math.max(1,Math.min(60,Number(response.headers()['retry-after'])||60));
   console.log('Límite de login activo: respetando Retry-After antes de repetir una cuenta sintética.');
   await page.waitForTimeout(seconds*1000);continue;
  }
  assert.equal(response.status(),302,'Synthetic login must redirect; credentials are never logged.');
  await page.waitForURL('**/Social/Inicio',{waitUntil:'domcontentloaded'});return;
 }
 throw new Error('Login rate limit persisted; no security mechanism was disabled.');
}
module.exports={login};
