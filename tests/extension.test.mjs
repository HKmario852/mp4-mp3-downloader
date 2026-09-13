import { test } from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source=fs.readFileSync(new URL('../extension/worker.js',import.meta.url),'utf8');
function harness({consent=true,ack=true}={}) {
  let listener, sent;
  const chrome={runtime:{id:'a'.repeat(32),onMessage:{addListener(fn){listener=fn}},async sendNativeMessage(host,request){sent=request;return {ok:ack,requestId:request.requestId,state:'PendingChoice'}}},storage:{local:{async get(){return {forwardCookies:consent}}}},cookies:{async getAllCookieStores(){return [{id:'profile-a',tabIds:[3]}]},async getAll(arg){assert.equal(arg.storeId,'profile-a');return [{name:'SID',value:'test-only',domain:'.youtube.com',path:'/',secure:true,httpOnly:true,hostOnly:false},{name:'SID',value:'partitioned',domain:'.youtube.com',partitionKey:{topLevelSite:'https://example.org'}}]}}};
  vm.runInNewContext(source,{chrome,URL,crypto:{randomUUID:()=> 'b282baff-e026-4fb0-8c0e-2dd53eb65055'},Error});
  return {send(message,sender){return new Promise(resolve=>listener(message,sender,resolve))},get sent(){return sent}};
}
const url='https://www.youtube.com/watch?v=test&list=playlist';
const sender={id:'a'.repeat(32),tab:{id:3},frameId:0,url};
test('consented payload stays in native messaging and filters partitioned cookies',async()=>{const h=harness();const r=await h.send({type:'download',mode:'mp3',url},sender);assert.equal(r.ok,true);assert.equal(h.sent.cookies.length,1);assert.equal(h.sent.url,url)});
test('does not send credentials without local consent',async()=>{const h=harness({consent:false});const r=await h.send({type:'download',mode:'mp3',url},sender);assert.equal(r.ok,false);assert.equal(h.sent,undefined)});
test('rejects stale SPA target and subframes',async()=>{for(const s of [{...sender,url:url+'x'},{...sender,frameId:1}]){const h=harness();assert.equal((await h.send({type:'download',mode:'mp3',url},s)).ok,false);assert.equal(h.sent,undefined)}});
test('a native rejection never becomes success',async()=>{const h=harness({ack:false});assert.equal((await h.send({type:'download',mode:'mp3',url},sender)).ok,false)});
