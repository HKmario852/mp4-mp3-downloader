import { test, expect } from '@playwright/test';
import fs from 'node:fs';
const script=fs.readFileSync('extension/content.js','utf8');
const css=fs.readFileSync('extension/content.css','utf8');
async function fixture(page,{reject=false,login=false,fallback=false}={}) {
  await page.route('https://www.youtube.com/**',route=>route.fulfill({contentType:'text/html',body:`<!doctype html><style>body{background:#111923;color:white;margin:40px;font:16px Arial}#owner,#menu{height:38px;overflow:hidden;contain:paint;transform:translateZ(0)}button{font-size:0!important}ytd-watch-metadata{display:block;margin-top:120px;width:500px}</style><ytd-app><ytd-masthead>${login?'<a href="https://accounts.google.com/login">Sign in</a>':'<button id="avatar-btn">Account</button>'}</ytd-masthead><ytd-watch-metadata>${fallback?'<div id="actions"><div id="menu"></div></div>':'<div id="top-row"><div id="owner"></div></div>'}</ytd-watch-metadata></ytd-app>`}));
  await page.goto('https://www.youtube.com/watch?v=fixture');
  await page.evaluate(({reject})=>{window.sent=[];window.chrome={runtime:{id:'fixture',getURL:p=>'chrome-extension://fixture/'+p,sendMessage:async m=>{window.sent.push(m);return reject?{ok:false,error:'Native host missing'}:{ok:true,state:'Queued'}}}}},{reject});
  await page.addStyleTag({content:css});await page.addScriptTag({content:script});
}
test('menu remains clickable outside clipped containers and page CSS; exactly one ACK',async({page})=>{
  await fixture(page);await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();
  await expect(page.getByRole('menuitem',{name:'下載為 MP3（音訊）',exact:true})).toBeVisible();
  const menuBox=await page.getByRole('menu').boundingBox(),owner=await page.locator('#owner').boundingBox();expect(menuBox.y).toBeGreaterThan(owner.y+owner.height);
  await page.screenshot({path:'artifacts/extension-menu.png'});
  await page.getByRole('menuitem',{name:'下載為 MP3（音訊）',exact:true}).click();await expect(page.getByRole('button',{name:'✓ 已接收',exact:true})).toBeVisible();
  expect(await page.evaluate(()=>window.sent)).toEqual([{type:'download',url:'https://www.youtube.com/watch?v=fixture',mode:'mp3'}]);
  await expect(page.getByRole('button',{name:'↓ 快速下載',exact:true})).toBeEnabled();
});
test('fallback mount and repeated SPA navigation have one functioning button',async({page})=>{
  await fixture(page,{fallback:true});await page.evaluate(()=>{history.pushState({},'', '/watch?v=second');window.dispatchEvent(new Event('yt-navigate-finish'));window.dispatchEvent(new Event('spfdone'));});
  await expect(page.locator('#omni-download-control')).toHaveCount(1);await expect(page.locator('#omni-download-overlay')).toHaveCount(1);
  await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();await page.getByRole('menuitem',{name:'下載為 MP4（視訊）',exact:true}).click();expect((await page.evaluate(()=>window.sent))[0].url).toContain('v=second');
});
test('native error stays visible and offers settings, without false success',async({page})=>{
  await fixture(page,{reject:true});await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();await page.getByRole('menuitem',{name:'下載為 MP4（視訊）',exact:true}).click();
  await expect(page.getByRole('status')).toContainText('Native host missing');await expect(page.getByRole('link',{name:'開啟擴充功能設定'})).toBeVisible();expect(await page.locator('button[data-state=success]').count()).toBe(0);
});
test('visible logged-out state never sends a download',async({page})=>{
  await fixture(page,{login:true});await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();await page.getByRole('menuitem',{name:'下載為 MP4（視訊）',exact:true}).click();
  await expect(page.getByRole('status')).toContainText('請先登入 YouTube');expect(await page.evaluate(()=>window.sent.length)).toBe(0);
});
test('replaced owner node remounts instead of leaving a dead cloned control',async({page})=>{
  await fixture(page);await page.evaluate(()=>{const owner=document.querySelector('#owner');owner.replaceWith(owner.cloneNode(true));});await expect(page.getByRole('button',{name:'↓ 快速下載',exact:true})).toBeVisible();await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();await expect(page.getByRole('menu')).toBeVisible();await expect(page.locator('#omni-download-control')).toHaveCount(1);
});
test('invalidated extension context shows refresh guidance and restores the button',async({page})=>{
  await fixture(page);await page.evaluate(()=>{window.chrome.runtime.id=undefined;window.chrome.runtime.getURL=()=>{throw new Error('Extension context invalidated');};});
  await page.getByRole('button',{name:'↓ 快速下載',exact:true}).click();await page.getByRole('menuitem',{name:'下載為 MP4（視訊）',exact:true}).click();
  await expect(page.getByRole('status')).toContainText('重新整理 YouTube');await expect(page.getByRole('button',{name:'↓ 快速下載',exact:true})).toBeEnabled();expect(await page.evaluate(()=>window.sent.length)).toBe(0);
});
