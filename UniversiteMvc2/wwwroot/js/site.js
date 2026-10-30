// Menu complet sur petit écran
document.querySelectorAll("[data-bascule-menu]").forEach(bouton => {
    bouton.addEventListener("click", () => {
        const ouvert = document.getElementById("tete").classList.toggle("est-ouvert");
        bouton.setAttribute("aria-expanded", String(ouvert));
    });
});

// Menus déroulants : un seul ouvert à la fois, fermeture au clic extérieur ou avec Échap
const menus = [...document.querySelectorAll("[data-menu]")];
menus.forEach(menu => {
    menu.addEventListener("toggle", () => {
        if (menu.open) menus.filter(m => m !== menu).forEach(m => m.open = false);
    });
});
document.addEventListener("click", e => {
    if (!e.target.closest("[data-menu]")) menus.forEach(m => m.open = false);
});
document.addEventListener("keydown", e => {
    if (e.key === "Escape") menus.forEach(m => m.open = false);
});

// Thème clair / sombre, mémorisé dans le navigateur
document.querySelectorAll("[data-bascule-theme]").forEach(bouton => {
    bouton.addEventListener("click", () => {
        const racine = document.documentElement;
        const sombre = racine.dataset.theme
            ? racine.dataset.theme === "dark"
            : matchMedia("(prefers-color-scheme: dark)").matches;
        racine.dataset.theme = sombre ? "light" : "dark";
        try { localStorage.setItem("theme", racine.dataset.theme); } catch { /* stockage indisponible */ }
    });
});

// Notifications : fermeture manuelle, et automatique pour les confirmations
document.querySelectorAll(".message").forEach(message => {
    const fermer = () => {
        message.classList.add("est-ferme");
        setTimeout(() => message.remove(), 300);
    };
    message.querySelector("[data-fermer]")?.addEventListener("click", fermer);
    if (message.hasAttribute("data-auto-fermer")) setTimeout(fermer, 6000);
});

// Filtres : une liste modifiée relance la recherche
document.querySelectorAll("form.filtres select").forEach(liste => {
    liste.addEventListener("change", () => liste.form.requestSubmit());
});

// Raccourci « / » : aller au champ de recherche
document.addEventListener("keydown", e => {
    if (e.key !== "/" || e.ctrlKey || e.metaKey || e.target.closest("input, textarea, select")) return;
    const recherche = document.querySelector("[data-recherche]");
    if (recherche) {
        e.preventDefault();
        recherche.focus();
    }
});

// Formulaire des documents : n'affiche que les rubriques utiles au type choisi.
// Les champs masqués sont désactivés, donc ni validés ni envoyés.
document.querySelectorAll("[data-documents]").forEach(formulaire => {
    const mettreAJour = () => {
        const type = formulaire.querySelector("input[name=Type]:checked")?.value;
        formulaire.querySelectorAll("[data-pour]").forEach(rubrique => {
            const visible = rubrique.dataset.pour.split(" ").includes(type);
            rubrique.hidden = !visible;
            rubrique.querySelectorAll("input, select").forEach(champ => champ.disabled = !visible);
        });
    };
    formulaire.addEventListener("change", e => { if (e.target.name === "Type") mettreAJour(); });
    mettreAJour();
});

document.querySelectorAll("[data-imprimer]").forEach(bouton => {
    bouton.addEventListener("click", () => window.print());
});
