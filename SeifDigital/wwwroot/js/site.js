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
