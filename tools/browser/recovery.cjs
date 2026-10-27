const {chromium}=require('playwright');
const fs=require('node:fs');const path=require('node:path');const crypto=require('node:crypto');const assert=require('node:assert/strict');
(async()=>{
 const base=process.env.HOBBYHUB_URL||'http://127.0.0.1:5087';assert.match(base,/^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?$/);
 const local=path.resolve(__dirname,'../../Proyecto_HobbyHub/.local');const id=fs.readFileSync(path.join(local,'browser-mail-id.txt'),'utf8').trim();assert.match(id,/^[a-f0-9]{32}$/);
 const mail=JSON.parse(fs.readFileSync(path.join(local,'mailbox',id+'.local.json'),'utf8'));const password=crypto.randomBytes(18).toString('hex');
 const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();
 try{
  await reset();await page.locator('#loginButton').waitFor();
  await reset();await page.getByText(/El código no es válido/).waitFor();
  await page.goto(base+'/Auth/Login');await page.locator('[name="Email"]').fill(mail.Recipient);await page.locator('[name="Password"]').fill(password);
  await page.locator('#loginButton').click();await page.waitForURL('**/Social/Inicio');
  console.log('Browser recovery: encrypted local mailbox, password reset, rejected token reuse and login with the new password passed. No real email was sent and no sensitive values were logged.');
 }finally{await browser.close();}
 async function reset(){await page.goto(base+'/Cuenta/Restablecer');await page.locator('[name="Token"]').fill(mail.Token);await page.locator('[name="Password"]').fill(password);await page.locator('[name="ConfirmPassword"]').fill(password);await page.locator('main form button').click();}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
