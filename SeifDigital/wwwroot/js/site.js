(function () {
    // ===== Navbar shrink on scroll =====
    const nav = document.getElementById("mainNavbar");
    function onScroll() {
        if (!nav) return;
        if (window.scrollY > 10) nav.classList.add("navbar-scrolled");
        else nav.classList.remove("navbar-scrolled");
    }
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();

    // ===== Theme toggle (dark mode) =====
    const btn = document.getElementById("btnTheme");
    const KEY = "wizvault_theme"; // "light" | "dark"

    function setTheme(mode) {
        // punem tema pe <html> ca să prindă tot
        const root = document.documentElement;
        if (mode === "dark") root.classList.add("theme-dark");
        else root.classList.remove("theme-dark");

        localStorage.setItem(KEY, mode);
        if (btn) btn.textContent = (mode === "dark" ? "☀️" : "🌙");
    }

    // init
    const saved = localStorage.getItem(KEY);
    if (saved === "dark" || saved === "light") setTheme(saved);
    else setTheme("light");

    if (btn) {
        btn.addEventListener("click", () => {
            const isDark = document.documentElement.classList.contains("theme-dark");
            setTheme(isDark ? "light" : "dark");
        });
    }
})();

// Hold-to-reveal password (used on Login / ResetPassword)
window._wv_showPwd = function (inputId) {
    var el = document.getElementById(inputId);
    if (!el) return;
    el.dataset._wvPrevType = el.type;
    el.type = "text";
};

window._wv_hidePwd = function (inputId) {
    var el = document.getElementById(inputId);
    if (!el) return;
    // restore to password (or previous)
    el.type = (el.dataset._wvPrevType || "password");
};

// ---- appended: wire "hold-to-reveal" buttons that use data-target (ResetPassword) ----
(function () {
    function attachPwEyeHandlers() {
        var buttons = document.querySelectorAll('.pw-eye-btn');
        if (!buttons || buttons.length === 0) return;

        buttons.forEach(function (btn) {
            var targetId = btn.getAttribute('data-target');
            if (!targetId) return;

            var show = function () { window._wv_showPwd && window._wv_showPwd(targetId); };
            var hide = function () { window._wv_hidePwd && window._wv_hidePwd(targetId); };

            btn.addEventListener('mousedown', function (e) { e.preventDefault(); show(); });
            btn.addEventListener('mouseup', function (e) { e.preventDefault(); hide(); });
            btn.addEventListener('mouseleave', function () { hide(); });

            // touch
            btn.addEventListener('touchstart', function (e) { e.preventDefault(); show(); }, { passive: false });
            btn.addEventListener('touchend', function (e) { e.preventDefault(); hide(); }, { passive: false });
        });
    }

    // Attach on DOM ready (site.js load is at bottom of layout so DOM is available, but be safe)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', attachPwEyeHandlers);
    } else {
        attachPwEyeHandlers();
    }
})();
