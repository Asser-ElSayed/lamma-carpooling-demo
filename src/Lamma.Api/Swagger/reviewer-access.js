(() => {
  "use strict";
  const accountNames = ["Host", "Rider1", "Rider2", "Rider3", "Rider4", "UnapprovedHost", "InactiveRider"];
  const tokens = new Map();
  const pending = new Map();
  let selectedAccount = "";
  let status;

  function show(message, failed = false) {
    if (!status) return;
    status.textContent = message;
    status.style.color = failed ? "#a32424" : "#155b50";
  }

  async function tokenFor(account) {
    const cached = tokens.get(account);
    if (cached && Date.parse(cached.expiresAtUtc) - Date.now() > 60000) return cached;
    if (!pending.has(account)) {
      const task = (async () => {
        const response = await fetch("/swagger/reviewer-token", {
          method: "POST", credentials: "omit", cache: "no-store",
          headers: { "Content-Type": "application/json" }, body: JSON.stringify({ account })
        });
        const data = await response.json();
        if (!response.ok) throw new Error(data.detail || "Reviewer authorization is unavailable.");
        tokens.set(account, data);
        return data;
      })();
      pending.set(account, task);
    }
    try { return await pending.get(account); }
    finally { pending.delete(account); }
  }

  async function authorize(account) {
    selectedAccount = account;
    if (!account) {
      window.ui.authActions.logout(["Bearer"]);
      show("Choose a demo account to try the endpoints.");
      return;
    }
    show("Authorizing " + account + "…");
    try {
      const data = await tokenFor(account);
      if (selectedAccount !== account) return;
      window.ui.preauthorizeApiKey("Bearer", data.accessToken);
      show("Authorized as " + account + ". Tokens renew automatically when you execute a request.");
    } catch (error) {
      if (selectedAccount !== account) return;
      window.ui.authActions.logout(["Bearer"]);
      show(error.message, true);
    }
  }

  // Only attach credentials to the three business operations on this same origin.
  window.lammaReviewerRequest = async request => {
    const url = new URL(request.url, window.location.href);
    if (url.origin !== window.location.origin) {
      if (request.headers) {
        delete request.headers.Authorization;
        delete request.headers.authorization;
      }
      return request;
    }
    if (!["/rides", "/rides/search", "/bookings"].includes(url.pathname) || !selectedAccount)
      return request;
    const account = selectedAccount;
    const data = await tokenFor(account);
    request.headers = request.headers || {};
    request.headers.Authorization = "Bearer " + data.accessToken;
    if (selectedAccount === account) window.ui.preauthorizeApiKey("Bearer", data.accessToken);
    return request;
  };

  function mount(attempt = 0) {
    const root = document.getElementById("swagger-ui");
    if (!window.ui || !root || !root.querySelector(".information-container")) {
      if (attempt < 200) window.setTimeout(() => mount(attempt + 1), 50);
      return;
    }
    if (document.getElementById("reviewer-access")) return;
    const panel = document.createElement("section");
    panel.id = "reviewer-access";
    panel.style.cssText = "box-sizing:border-box;max-width:1460px;margin:24px auto;padding:20px;border:1px solid #b8d4cc;border-radius:8px;background:#f0faf6;font:15px/1.5 system-ui,sans-serif;color:#172b39";
    const title = document.createElement("h2");
    title.textContent = "Try the demo — choose an account";
    title.style.cssText = "margin:0 0 8px;font-size:22px";
    const description = document.createElement("p");
    description.textContent = "Host publishes a ride; Rider1–4 search and book. UnapprovedHost and InactiveRider demonstrate permission failures. These are shared synthetic demo accounts.";
    const label = document.createElement("label");
    label.htmlFor = "reviewer-account";
    label.textContent = "Demo account: ";
    const select = document.createElement("select");
    select.id = "reviewer-account";
    select.style.cssText = "padding:9px;min-width:180px;margin:0 12px 8px 0;border:1px solid #8aaba2;border-radius:5px;background:white";
    select.add(new Option("Select an account", ""));
    accountNames.forEach(name => select.add(new Option(name, name)));
    select.addEventListener("change", () => authorize(select.value));
    const refresh = document.createElement("button");
    refresh.type = "button";
    refresh.textContent = "Get fresh token / authorize";
    refresh.style.cssText = "padding:10px 14px;margin:0 8px 8px 0;border:0;border-radius:5px;background:#146c5e;color:white;cursor:pointer";
    refresh.addEventListener("click", () => {
      if (!selectedAccount) return show("Select an account first.", true);
      tokens.delete(selectedAccount);
      authorize(selectedAccount);
    });
    const copy = document.createElement("button");
    copy.type = "button";
    copy.textContent = "Copy JWT";
    copy.style.cssText = "padding:9px 14px;margin-bottom:8px;border:1px solid #146c5e;border-radius:5px;background:white;color:#146c5e;cursor:pointer";
    copy.addEventListener("click", async () => {
      if (!selectedAccount) return show("Select an account first.", true);
      try {
        const data = await tokenFor(selectedAccount);
        if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(data.accessToken);
        else {
          const field = document.createElement("textarea");
          field.value = data.accessToken;
          panel.append(field); field.select();
          const copied = document.execCommand("copy"); field.remove();
          if (!copied) throw new Error("Select the account in Swagger; automatic authorization does not need clipboard access.");
        }
        show(selectedAccount + " JWT copied. Swagger is already authorized for this account.");
      } catch (error) { show(error.message, true); }
    });
    status = document.createElement("p");
    status.id = "reviewer-access-status";
    status.setAttribute("role", "status");
    panel.append(title, description, label, select, refresh, copy, status);
    root.prepend(panel);
    show("Choose a demo account to try the endpoints. No downloads or token sheet needed.");
  }
  if (document.readyState === "complete") mount();
  else window.addEventListener("load", () => mount(), { once: true });
})();
