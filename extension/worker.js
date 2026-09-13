const HOST = "com.omni.downloader";
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  (async () => {
    if (sender.id !== chrome.runtime.id || !sender.tab || sender.frameId !== 0) throw new Error("無效來源");
    const page = new URL(sender.url);
    if (page.protocol !== "https:" || !["www.youtube.com", "m.youtube.com"].includes(page.hostname)) throw new Error("無效來源");
    if (message.type !== "download" || !["mp3", "mp4"].includes(message.mode)) throw new Error("無效請求");
    const target = new URL(message.url);
    if (target.origin !== page.origin || target.href !== page.href) throw new Error("頁面已切換，請重新點擊");
    const { forwardCookies = false } = await chrome.storage.local.get("forwardCookies");
    if (!forwardCookies) throw new Error("請先在擴充功能設定同意將 YouTube 登入憑證傳送至本機下載器");
    const stores = await chrome.cookies.getAllCookieStores();
    const store = stores.find(s => s.tabIds.includes(sender.tab.id));
    if (!store) throw new Error("無法識別目前瀏覽器登入工作階段");
    const all = await chrome.cookies.getAll({ domain: ".youtube.com", storeId: store.id });
    // Netscape has no partition-key representation: never merge partitioned cookies.
    const cookies = all.filter(c => !c.partitionKey && ["youtube.com", "www.youtube.com", "m.youtube.com"].includes(c.domain.replace(/^\./, "")))
      .map(({ name, value, domain, path, secure, httpOnly, hostOnly, expirationDate }) => ({ name, value, domain, path, secure, httpOnly, hostOnly, expirationDate }));
    if (!cookies.some(c => /^(SID|__Secure-[13]P[AS]ID|SAPISID|LOGIN_INFO)$/.test(c.name))) throw new Error("未找到登入憑證，請先登入 YouTube");
    const requestId = crypto.randomUUID();
    const result = await chrome.runtime.sendNativeMessage(HOST, { requestId, url: target.href, mode: message.mode, cookies });
    if (!result?.ok || result.requestId !== requestId) throw new Error(result?.error || "主程式未確認接收");
    return result;
  })().then(reply).catch(e => reply({ ok: false, error: e.message }));
  return true;
});
