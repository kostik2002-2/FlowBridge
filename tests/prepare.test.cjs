'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict');
const {prepareFlow,isFlow}=require('../src/prepare.cjs');
function fixture({old=true,cookieError=false,createError=false,moved=false}={}){
  const calls=[],state=new Map([
    [1,{id:1,windowId:7,index:0,url:'chrome-extension://example/complete.html'}],
    [3,{id:3,windowId:7,url:'https://mail.google.com/mail/'}],
    [4,{id:4,windowId:8,url:'https://flow.google.com/project/other-window'}],
    [5,{id:5,windowId:7,url:'https://example.org/flow.google.com/'}],
    ...(old?[[2,{id:2,windowId:7,url:'https://flow.google.com/project/old'}]]:[])
  ]);
  const cookie=(domain,name,extra={})=>({domain,name,path:'/',storeId:'0',secure:true,httpOnly:true,sameSite:'lax',hostOnly:!domain.startsWith('.'),...extra});
  const api={
    tabs:{
      getCurrent:async()=>state.get(1),
      query:async({windowId})=>[...state.values()].filter(t=>t.windowId===windowId),
      get:async id=>moved&&id===2?{...state.get(id),url:'https://example.net/'}:state.get(id),
      create:async options=>{calls.push(['create',options]);if(createError)throw Error('create');const tab={id:9,...options};state.set(9,tab);return tab;},
      remove:async id=>{calls.push(['remove',id]);state.delete(id);}
    },
    cookies:{
      getAll:async query=>{calls.push(['cookies',query]);if(cookieError)throw Error('cookies');return query.partitionKey?
        [cookie('flow.google.com','partitioned',{partitionKey:{topLevelSite:'https://flow.google.com'}})]:
        [cookie('flow.google.com','local'),cookie('.flow.google.com','domain'),cookie('.google.com','SID'),cookie('evil-flow.google.com','foreign')];},
      set:async options=>{calls.push(['expire',options]);}
    }
  };return{api,calls,state};
}
test('limits URL matching to HTTPS Flow origin',()=>{
 for(const url of ['https://flow.google.com/','https://flow.google.com/project'])assert.ok(isFlow(url));
 for(const url of ['http://flow.google.com/','https://flow.google.com.evil.org/','https://google.com/','invalid'])assert.equal(isFlow(url),false);
});
test('expires only exact Flow cookies, keeps Google authentication and partition identity, preserves unrelated tabs/windows',async()=>{
 const f=fixture();await prepareFlow(f.api);
 const expired=f.calls.filter(c=>c[0]==='expire').map(c=>c[1]);
 assert.deepEqual(expired.map(c=>c.name),['local','domain','partitioned']);
 assert.equal(expired[0].domain,undefined);assert.equal(expired[1].domain,'.flow.google.com');
 assert.deepEqual(expired[2].partitionKey,{topLevelSite:'https://flow.google.com'});
 assert.ok(expired.every(c=>c.expirationDate===0));
 assert.deepEqual(f.calls.filter(c=>c[0]==='remove').map(c=>c[1]),[2,1]);
 assert.deepEqual([...f.state.keys()],[3,4,5,9]);
 const created=f.calls.find(c=>c[0]==='create')[1];assert.equal(created.windowId,7);assert.equal(created.active,true);
 assert.ok(f.calls.findIndex(c=>c[0]==='expire')<f.calls.findIndex(c=>c[0]==='create'));
 assert.ok(f.calls.findIndex(c=>c[0]==='create')<f.calls.findIndex(c=>c[0]==='remove'));
});
test('does not read or change cookies when Flow was not open',async()=>{
 const f=fixture({old:false});await prepareFlow(f.api);assert.equal(f.calls.some(c=>c[0]==='cookies'||c[0]==='expire'),false);
});
test('does not close any tabs if cookie cleanup or creating Flow fails',async()=>{
 for(const options of [{cookieError:true},{createError:true}]){
  const f=fixture(options);await assert.rejects(prepareFlow(f.api));assert.equal(f.calls.some(c=>c[0]==='remove'),false);assert.ok(f.state.has(2));
 }
});
test('does not close a tab navigated away from Flow during installation',async()=>{
 const f=fixture({moved:true});await prepareFlow(f.api);assert.ok(f.state.has(2));
});
