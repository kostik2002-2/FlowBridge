'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict');
const {prepareFlow}=require('../src/prepare.cjs');
test('closes extension management and stale FlowBridge preparation tabs only in current window',async()=>{
 const setup={id:1,index:6,windowId:7,url:'chrome-extension://ours/complete.html'};
 const tabs=[
 setup,
 {id:2,windowId:7,url:'chrome://extensions/'},
 {id:3,windowId:7,url:'opera://extensions/?id=example'},
 {id:4,windowId:7,url:'chrome-extension://ours/complete.html?old=1'},
 {id:5,windowId:7,url:'https://example.org/'},
 {id:6,windowId:7,url:'chrome-extension://other/options.html'},
 {id:7,windowId:7,url:'https://example.org/chrome://extensions'},
 {id:8,windowId:8,url:'chrome://extensions/'}
 ];const removed=[];
 const api={tabs:{
  getCurrent:async()=>setup,
  query:async({windowId})=>tabs.filter(t=>t.windowId===windowId&&!removed.includes(t.id)),
  create:async()=>({id:9}),get:async id=>tabs.find(t=>t.id===id),
  remove:async id=>removed.push(id)
 }};
 await prepareFlow(api);assert.deepEqual(removed,[2,3,4,1]);
});
