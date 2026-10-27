// Repeatable suite: all writes stay in the separate visual database configured on port 5088.
const {spawnSync}=require('node:child_process');const path=require('node:path');
const root=path.resolve(__dirname,'../..');
for(const script of ['visual-functional.cjs','visual-public.cjs','visual-accessibility.cjs','visual-secondary.cjs']){
 const result=spawnSync(process.execPath,[path.join(__dirname,script)],{cwd:root,stdio:'inherit'});
 if(result.error){console.error(result.error.message);process.exit(1);}
 if(result.status!==0)process.exit(result.status||1);
}
