const THEME_KEY = "dackel-theme";
const THEME_COLORS: Record<Theme, string> = {
  dark: "#070b12",
  light: "#f7fafc",
};

type Theme = "dark" | "light";

const root = document.documentElement;

function query<T extends Element>(selector: string, scope: ParentNode = document): T | null {
  return scope.querySelector<T>(selector);
}

function queryAll<T extends Element>(selector: string, scope: ParentNode = document): T[] {
  return Array.from(scope.querySelectorAll<T>(selector));
}

/* --------------------------------------------------------------------------
   Tema
   -------------------------------------------------------------------------- */

function getStoredTheme(): Theme | null {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    return stored === "light" || stored === "dark" ? stored : null;
  } catch {
    return null;
  }
}

function preferredTheme(): Theme {
  return getStoredTheme() ??
    (window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark");
}

function applyTheme(theme: Theme): void {
  root.dataset.theme = theme;
  root.style.colorScheme = theme;

  const meta = query<HTMLMetaElement>('meta[name="theme-color"]');
  meta?.setAttribute("content", THEME_COLORS[theme]);

  const toggle = query<HTMLButtonElement>("[data-theme-toggle]");
  if (toggle) {
    const next = theme === "dark" ? "claro" : "escuro";
    toggle.setAttribute("aria-label", `Ativar tema ${next}`);
    toggle.setAttribute("title", `Ativar tema ${next}`);
  }
}

applyTheme(preferredTheme());

for (const toggle of queryAll<HTMLButtonElement>("[data-theme-toggle]")) {
  toggle.addEventListener("click", () => {
    const next: Theme = root.dataset.theme === "light" ? "dark" : "light";
    applyTheme(next);

    try {
      localStorage.setItem(THEME_KEY, next);
    } catch {
      // localStorage indisponível (modo privado): mantém apenas em memória.
    }
  });
}

/* --------------------------------------------------------------------------
   Copiar para a área de transferência
   -------------------------------------------------------------------------- */

async function copyText(text: string): Promise<void> {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
}

for (const button of queryAll<HTMLButtonElement>("[data-copy]")) {
  button.addEventListener("click", async () => {
    const targetId = button.dataset.copy;
    const target = targetId ? document.getElementById(targetId) : null;

    if (!target) {
      return;
    }

    const original = button.textContent ?? "Copiar";

    try {
      await copyText(target.textContent ?? "");
      button.textContent = "Copiado!";
      button.classList.add("is-copied");
    } catch {
      button.textContent = "Falhou";
    }

    window.setTimeout(() => {
      button.textContent = original;
      button.classList.remove("is-copied");
    }, 1600);
  });
}

/* --------------------------------------------------------------------------
   Tabs de código
   -------------------------------------------------------------------------- */

for (const group of queryAll<HTMLElement>("[data-tabs]")) {
  const tabs = queryAll<HTMLButtonElement>('[role="tab"]', group);
  const panels = queryAll<HTMLElement>("[data-panel]", group);

  const activate = (index: number, moveFocus = false): void => {
    tabs.forEach((tab, tabIndex) => {
      const selected = tabIndex === index;
      tab.classList.toggle("is-active", selected);
      tab.setAttribute("aria-selected", String(selected));
      tab.tabIndex = selected ? 0 : -1;

      if (selected && moveFocus) {
        tab.focus();
      }
    });

    panels.forEach((panel, panelIndex) => {
      const selected = panelIndex === index;
      panel.classList.toggle("is-active", selected);
      panel.hidden = !selected;
    });
  };

  tabs.forEach((tab, index) => {
    tab.addEventListener("click", () => activate(index));

    tab.addEventListener("keydown", (event) => {
      const keys = ["ArrowRight", "ArrowLeft", "Home", "End"];

      if (!keys.includes(event.key)) {
        return;
      }

      event.preventDefault();

      let next = index;

      if (event.key === "ArrowRight") {
        next = (index + 1) % tabs.length;
      } else if (event.key === "ArrowLeft") {
        next = (index - 1 + tabs.length) % tabs.length;
      } else if (event.key === "Home") {
        next = 0;
      } else if (event.key === "End") {
        next = tabs.length - 1;
      }

      activate(next, true);
    });
  });

  activate(0);
}

/* --------------------------------------------------------------------------
   Menu mobile
   -------------------------------------------------------------------------- */

const nav = query<HTMLElement>("[data-nav]");
const menuButton = query<HTMLButtonElement>("[data-menu-toggle]");

function closeMenu(): void {
  nav?.classList.remove("is-open");
  menuButton?.setAttribute("aria-expanded", "false");
}

menuButton?.addEventListener("click", () => {
  const open = nav?.classList.toggle("is-open") ?? false;
  menuButton.setAttribute("aria-expanded", String(open));
});

for (const link of queryAll<HTMLAnchorElement>("a", nav ?? document)) {
  link.addEventListener("click", closeMenu);
}

document.addEventListener("click", (event) => {
  if (!nav?.classList.contains("is-open")) {
    return;
  }

  const target = event.target as Node;

  if (nav.contains(target) || menuButton?.contains(target)) {
    return;
  }

  closeMenu();
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    closeMenu();
  }
});

/* --------------------------------------------------------------------------
   Header / seção ativa
   -------------------------------------------------------------------------- */

const header = query<HTMLElement>("[data-header]");

function onScroll(): void {
  header?.classList.toggle("is-scrolled", window.scrollY > 8);
}

onScroll();
window.addEventListener("scroll", onScroll, { passive: true });

const navLinks = queryAll<HTMLAnchorElement>("[data-nav-link]");
const sections = navLinks
  .map((link) => {
    const href = link.getAttribute("href");
    return href ? document.querySelector<HTMLElement>(href) : null;
  })
  .filter((section): section is HTMLElement => section !== null);

function setActiveSection(id: string): void {
  for (const link of navLinks) {
    const active = link.getAttribute("href") === `#${id}`;

    if (active) {
      link.setAttribute("aria-current", "true");
    } else {
      link.removeAttribute("aria-current");
    }
  }
}

if ("IntersectionObserver" in window && sections.length > 0) {
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          setActiveSection(entry.target.id);
        }
      }
    },
    { rootMargin: "-42% 0px -52% 0px", threshold: 0 },
  );

  for (const section of sections) {
    observer.observe(section);
  }
}

/* --------------------------------------------------------------------------
   Reveal on scroll
   -------------------------------------------------------------------------- */

const revealItems = queryAll<HTMLElement>("[data-reveal]");
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

if (reduceMotion || !("IntersectionObserver" in window)) {
  for (const item of revealItems) {
    item.classList.add("is-visible");
  }
} else {
  const revealObserver = new IntersectionObserver(
    (entries, observer) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) {
          continue;
        }

        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      }
    },
    { threshold: 0.12, rootMargin: "0px 0px -8% 0px" },
  );

  for (const item of revealItems) {
    revealObserver.observe(item);
  }
}

/* --------------------------------------------------------------------------
   Versão publicada no NuGet
   -------------------------------------------------------------------------- */

async function loadNuGetVersion(): Promise<void> {
  const targets = queryAll<HTMLElement>("[data-nuget-version]");

  if (targets.length === 0) {
    return;
  }

  try {
    const response = await fetch("https://api.nuget.org/v3-flatcontainer/dackel.log/index.json", {
      headers: { Accept: "application/json" },
    });

    if (!response.ok) {
      return;
    }

    const payload: unknown = await response.json();

    if (typeof payload !== "object" || payload === null || !("versions" in payload)) {
      return;
    }

    const versions = (payload as { versions?: unknown }).versions;

    if (!Array.isArray(versions)) {
      return;
    }

    const stable = versions.filter(
      (version): version is string => typeof version === "string" && !version.includes("-"),
    );

    const latest = stable.at(-1);

    if (latest) {
      for (const target of targets) {
        target.textContent = latest;
      }
    }
  } catch {
    // Mantém a versão de fallback do HTML quando offline ou ainda não publicado.
  }
}

void loadNuGetVersion();

/* --------------------------------------------------------------------------
   Ano do rodapé
   -------------------------------------------------------------------------- */

const currentYear = String(new Date().getFullYear());

for (const element of queryAll<HTMLElement>("[data-year]")) {
  element.textContent = currentYear;
}
