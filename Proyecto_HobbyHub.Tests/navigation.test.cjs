const test=require('node:test');const assert=require('node:assert/strict');
const initialize=require('../Proyecto_HobbyHub/wwwroot/js/navigation.js');
function fixture(){
 const events=new Map(),attributes={},sidebar={dataset:{}},root={dataset:{}};let focused=false;
 const button={setAttribute:(name,value)=>attributes[name]=value,addEventListener:(name,fn)=>events.set('button:'+name,fn),focus:()=>focused=true};
 const panel={addEventListener:(name,fn)=>events.set('panel:'+name,fn)};
 const status={hidden:true},form={checkValidity:()=>true,setAttribute:(n,v)=>attributes[n]=v,removeAttribute:n=>delete attributes[n]};
 const document={documentElement:root,getElementById:id=>id==='navigation-toggle'?button:panel,querySelector:s=>s==='.app-sidebar'?sidebar:status,querySelectorAll:()=>[form],addEventListener:(name,fn)=>events.set('document:'+name,fn)};
 const media={matches:true,addEventListener:(name,fn)=>events.set('media:'+name,fn)},window={matchMedia:()=>media,addEventListener:(name,fn)=>events.set('window:'+name,fn)};
 initialize(document,window);return{events,attributes,sidebar,status,form,focused:()=>focused,media};
}
test('menu starts closed, opens on button and updates ARIA',()=>{const f=fixture();assert.equal(f.attributes['aria-expanded'],'false');f.events.get('button:click')();assert.equal(f.attributes['aria-expanded'],'true');assert.equal(f.attributes['aria-label'],'Cerrar menú');});
test('Escape closes menu and returns keyboard focus',()=>{const f=fixture();f.events.get('button:click')();let prevented=false;f.events.get('document:keydown')({key:'Escape',preventDefault:()=>prevented=true});assert.equal(f.attributes['aria-expanded'],'false');assert.equal(f.focused(),true);assert.equal(prevented,true);});
test('navigation and viewport changes close menu',()=>{const f=fixture();f.events.get('button:click')();f.events.get('panel:click')({target:{closest:()=>({})}});assert.equal(f.sidebar.dataset.menuOpen,'false');f.events.get('button:click')();f.events.get('media:change')();assert.equal(f.sidebar.dataset.menuOpen,'false');});
test('cancelled or invalid form has no false loading state',()=>{const f=fixture();f.events.get('document:submit')({target:f.form,defaultPrevented:true});assert.equal(f.status.hidden,true);f.form.checkValidity=()=>false;f.events.get('document:submit')({target:f.form,defaultPrevented:false});assert.equal(f.status.hidden,true);});
test('valid submit announces loading and back navigation restores state',()=>{const f=fixture();f.events.get('document:submit')({target:f.form,defaultPrevented:false});assert.equal(f.status.hidden,false);assert.equal(f.attributes['aria-busy'],'true');f.events.get('window:pageshow')();assert.equal(f.status.hidden,true);assert.equal(f.attributes['aria-busy'],undefined);});
