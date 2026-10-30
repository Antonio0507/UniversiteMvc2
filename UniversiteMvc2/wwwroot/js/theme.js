// Thème appliqué avant l'affichage pour éviter un flash (chargé de façon synchrone dans le <head>)
try { const t = localStorage.getItem("theme"); if (t) document.documentElement.dataset.theme = t; } catch { }
