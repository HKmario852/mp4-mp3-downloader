(() => {
  const ID = 'omni-download-control';
  const SELECTORS = ['ytd-watch-metadata #top-row #owner', 'ytd-watch-metadata #actions #menu', 'ytd-watch-metadata ytd-menu-renderer', '.html5-video-player .ytp-right-controls', 'ytd-playlist-header-renderer #top-level-buttons-computed', 'ytd-playlist-header-renderer #actions', 'ytd-playlist-header-renderer', 'ytd-playlist-sidebar-primary-info-renderer'];
  let mountedRoot, dispose, observer, mountTimer;
  const validPage = () => (location.pathname === '/watch' && new URL(location.href).searchParams.has('v')) || (location.pathname === '/playlist' && new URL(location.href).searchParams.has('list')) || /^\/shorts\/[\w-]+/.test(location.pathname);
  function mount() {
    if (!validPage()) { dispose?.(); return; }
    if (mountedRoot?.isConnected && document.getElementById(ID) === mountedRoot) return;
    dispose?.(); document.getElementById(ID)?.remove();
    const target = SELECTORS.map(s => document.querySelector(s)).find(n => n?.getClientRects().length)
      || (location.pathname.startsWith('/shorts/') ? document.querySelector('ytd-reel-video-renderer[is-active] #actions') : null);
    if (!target) return;
    const root = document.createElement('div'); root.id = ID; mountedRoot = root;
    const shadow = root.attachShadow({ mode: 'open' });
    shadow.innerHTML = `<style>:host{display:inline-flex;vertical-align:middle;margin:0 8px}button{all:unset;box-sizing:border-box;display:inline-flex;align-items:center;justify-content:center;gap:8px;height:36px;padding:0 16px;border-radius:18px;background:var(--yt-spec-badge-chip-background,#303030);color:var(--yt-spec-text-primary,#f1f1f1);font:500 14px/1.2 Roboto,"Noto Sans TC",sans-serif;white-space:nowrap;cursor:pointer}button:hover{filter:brightness(1.2)}button:focus-visible{outline:2px solid #8c9eff;outline-offset:2px}button:disabled{cursor:wait;opacity:.8}button[data-state=success]{background:#168245;color:white}button[data-state=warning]{background:#8d5b0d;color:white}</style>`;
    const button = document.createElement('button'); button.type = 'button'; button.textContent = '↓ 快速下載'; button.setAttribute('aria-haspopup', 'menu'); button.setAttribute('aria-expanded', 'false'); shadow.append(button); target.append(root);
    // Top-layer portal escapes clipped YouTube containers and page styles.
    const portal = document.createElement('div'); portal.id = 'omni-download-overlay'; portal.setAttribute('popover', 'manual');
    const panel = portal.attachShadow({ mode: 'open' });
    panel.innerHTML = `<style>:host{all:initial;position:fixed!important;margin:0!important;padding:0!important;border:0!important;background:transparent!important;inset:auto;z-index:2147483647!important;width:min(300px,calc(100vw - 24px))!important;color-scheme:dark}:host::backdrop{background:transparent;pointer-events:none}.box{box-sizing:border-box;padding:10px;border:1px solid #40516b;border-radius:12px;background:#18222d;color:#edf2ff;box-shadow:0 8px 32px #0008;font:14px/1.6 Roboto,"Noto Sans TC",sans-serif}button,a{font:inherit}button{display:block;box-sizing:border-box;border:0;border-radius:7px;width:100%;padding:11px 12px;text-align:left;background:transparent;color:inherit;cursor:pointer}button:hover,button:focus-visible{background:#2b3b5b;outline:0}p{margin:4px 8px;overflow-wrap:anywhere}a{display:block;color:#7fa5ff;margin:8px}[hidden]{display:none!important}</style><div class="box"><div role="menu"></div><p role="status" hidden></p><div class="links"></div></div>`;
    document.documentElement.append(portal);
    const menu = panel.querySelector('[role=menu]'), status = panel.querySelector('[role=status]'), links = panel.querySelector('.links');
    let busy = false, alive = true, visible = false, restoreTimer, requestTimer;
    const events = new AbortController();
    const position = () => {
      if (!visible) return;
      const r = button.getBoundingClientRect(), width = Math.min(300, innerWidth - 24), height = portal.getBoundingClientRect().height || 110;
      portal.style.setProperty('left', `${Math.max(12, Math.min(r.left, innerWidth - width - 12))}px`, 'important');
      portal.style.setProperty('top', `${Math.max(12, r.bottom + 8 + height <= innerHeight - 12 ? r.bottom + 8 : r.top - height - 8)}px`, 'important');
    };
    const hide = () => { if (portal.matches(':popover-open')) portal.hidePopover(); portal.style.display = 'none'; visible = false; button.setAttribute('aria-expanded', 'false'); };
    const open = () => { portal.style.display = 'block'; if (typeof portal.showPopover === 'function' && !portal.matches(':popover-open')) portal.showPopover(); visible = true; button.setAttribute('aria-expanded', 'true'); position(); };
    const show = (text, state = '') => { button.textContent = text; button.dataset.state = state; };
    const reset = () => { if (!alive) return; busy = false; button.disabled = false; show('↓ 快速下載'); };
    const note = text => { menu.hidden = true; status.hidden = false; status.textContent = text; links.replaceChildren(); open(); };
    button.addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); if (busy) return; if (visible && !menu.hidden) { hide(); return; } menu.hidden = false; status.hidden = true; links.replaceChildren(); open(); });
    for (const [mode, label] of [['mp4', '下載為 MP4（視訊）'], ['mp3', '下載為 MP3（音訊）']]) {
      const item = document.createElement('button'); item.type = 'button'; item.textContent = label; item.setAttribute('role', 'menuitem');
      item.addEventListener('click', async e => {
        e.preventDefault(); e.stopPropagation(); if (busy) return;
        const avatar = document.querySelector('ytd-masthead #avatar-btn, ytd-topbar-menu-button-renderer #avatar-btn');
        const login = [...document.querySelectorAll('ytd-masthead a[href*="accounts.google.com"]')].some(n => n.getClientRects().length);
        if (!avatar && login) { show('⚠ 請先登入', 'warning'); note('請先登入 YouTube 帳號以啟用下載'); clearTimeout(restoreTimer); restoreTimer = setTimeout(reset, 2500); return; }
        // Worker cookie validation remains authoritative if avatar selectors change.
        busy = true; button.disabled = true; const url = location.href; show('◌ 正在傳送'); note('正在等待本機下載器確認接收…');
        const onBlur = () => {}; window.addEventListener('blur', onBlur, { once: true }); const blurTimer = setTimeout(() => window.removeEventListener('blur', onBlur), 2000);
        try {
          if (!chrome.runtime?.id) throw new Error('擴充功能已更新，請重新整理 YouTube 頁面');
          const result = await Promise.race([chrome.runtime.sendMessage({ type: 'download', url, mode }), new Promise((_, reject) => { requestTimer = setTimeout(() => reject(new Error('接收確認逾時；請先查看 App，避免重複加入')), 20000); })]);
          if (!alive) return;
          if (!result?.ok) throw new Error(result?.error || '未收到回覆');
          show('✓ 已接收', 'success'); note(result.state === 'PendingChoice' ? '請到下載器選擇單片或整個播放清單' : '已交予本機下載器');
          clearTimeout(restoreTimer); restoreTimer = setTimeout(() => { reset(); hide(); }, 1500);
        } catch (error) {
          if (!alive) return;
          show('⚠ 未確認接收', 'warning'); note(error.message);
          if (chrome.runtime?.id) { const settings = document.createElement('a'); settings.href = 'chrome-extension://' + chrome.runtime.id + '/options.html'; settings.target = '_blank'; settings.rel = 'noopener'; settings.textContent = '開啟擴充功能設定'; links.append(settings); }
          const fallback = document.createElement('a'); fallback.href = `ytdl://download?url=${encodeURIComponent(url)}&mode=${encodeURIComponent(mode)}`; fallback.textContent = '開啟下載器（不傳登入憑證）'; links.append(fallback); position();
          clearTimeout(restoreTimer); restoreTimer = setTimeout(reset, 2500);
        } finally { clearTimeout(requestTimer); clearTimeout(blurTimer); window.removeEventListener('blur', onBlur); }
      }); menu.append(item);
    }
    hide();
    document.addEventListener('pointerdown', e => { if (!e.composedPath().includes(root) && !e.composedPath().includes(portal)) hide(); }, { capture: true, signal: events.signal });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') { hide(); button.focus(); } }, { signal: events.signal });
    window.addEventListener('resize', position, { signal: events.signal }); document.addEventListener('scroll', position, { capture: true, passive: true, signal: events.signal });
    dispose = () => { alive = false; clearTimeout(restoreTimer); clearTimeout(requestTimer); events.abort(); root.remove(); portal.remove(); mountedRoot = null; dispose = null; };
  }
  function navigate() {
    clearTimeout(mountTimer); observer?.disconnect(); dispose?.(); mount();
    if (location.pathname === '/watch' || location.pathname === '/playlist') {
      observer = new MutationObserver(() => { if (!mountedRoot?.isConnected || document.getElementById(ID) !== mountedRoot) { clearTimeout(mountTimer); mountTimer = setTimeout(mount, 100); } });
      observer.observe(document.querySelector('ytd-app') || document.body, { childList: true, subtree: true });
    }
  }
  window.addEventListener('yt-navigate-finish', navigate); window.addEventListener('spfdone', navigate); window.addEventListener('popstate', navigate); navigate();
})();
