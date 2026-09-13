import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir:'./tests/browser', outputDir:'./artifacts/browser-test-results', reporter:[['list'],['json',{outputFile:'artifacts/browser-results.json'}]], use:{browserName:'chromium',headless:true,viewport:{width:1280,height:800}}, workers:1 });
