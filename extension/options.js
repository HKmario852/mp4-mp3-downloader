document.getElementById("id").textContent = chrome.runtime.id;
const consent = document.getElementById("consent");
chrome.storage.local.get("forwardCookies").then(v => consent.checked = !!v.forwardCookies);
consent.addEventListener("change", async () => { await chrome.storage.local.set({ forwardCookies: consent.checked }); document.getElementById("status").textContent = "已儲存"; });
