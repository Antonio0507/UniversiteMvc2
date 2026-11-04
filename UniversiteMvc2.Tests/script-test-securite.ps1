# Script de test du groupe 5 : sécurité, erreurs et journalisation, sur l'application en marche.
#
#   .\script-test-securite.ps1 -AdminEmail admin@universite.local -AdminMotDePasse "..."
#
# À lancer sur une base de développement ou de démonstration, jamais en production : le script crée deux comptes
# de test (désactivés ou verrouillés à la fin) et une dizaine de lignes dans le journal d'audit.
# L'administrateur doit déjà avoir changé son mot de passe provisoire.
# Attendre une minute depuis les dernières tentatives de connexion : le test T17 consomme la limite de 10 essais par minute.
param(
    [string]$Url = "http://localhost:5267",
    [Parameter(Mandatory = $true)][string]$AdminEmail,
    [Parameter(Mandatory = $true)][string]$AdminMotDePasse
)

Add-Type -AssemblyName System.Net.Http
$script:echecs = 0
$script:numero = 0

function Nouveau-Navigateur {
    $gestionnaire = New-Object System.Net.Http.HttpClientHandler
    $gestionnaire.AllowAutoRedirect = $false
    $gestionnaire.CookieContainer = New-Object System.Net.CookieContainer
    $client = New-Object System.Net.Http.HttpClient($gestionnaire)
    $client.BaseAddress = [Uri]$Url
    return $client
}

function Lire($client, [string]$chemin) {
    $reponse = $client.GetAsync($chemin).GetAwaiter().GetResult()
    return [pscustomobject]@{
        Code     = [int]$reponse.StatusCode
        Texte    = [System.Net.WebUtility]::HtmlDecode($reponse.Content.ReadAsStringAsync().GetAwaiter().GetResult())
        Location = if ($reponse.Headers.Location) { $reponse.Headers.Location.ToString() } else { "" }
        Entetes  = $reponse.Headers
    }
}

function Poster($client, [string]$chemin, [hashtable]$champs, [string]$formulaire) {
    $valeurs = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    foreach ($cle in $champs.Keys) { $valeurs[$cle] = $champs[$cle] }
    if ($formulaire) {
        $page = $client.GetStringAsync($formulaire).GetAwaiter().GetResult()
        $jeton = [regex]::Match($page, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
        $valeurs["__RequestVerificationToken"] = $jeton
    }
    $contenu = New-Object System.Net.Http.FormUrlEncodedContent($valeurs)
    $reponse = $client.PostAsync($chemin, $contenu).GetAwaiter().GetResult()
    return [pscustomobject]@{
        Code     = [int]$reponse.StatusCode
        Texte    = [System.Net.WebUtility]::HtmlDecode($reponse.Content.ReadAsStringAsync().GetAwaiter().GetResult())
        Location = if ($reponse.Headers.Location) { $reponse.Headers.Location.ToString() } else { "" }
        Entetes  = $reponse.Headers
    }
}

function Connecter($client, [string]$email, [string]$motDePasse, [string]$chemin = "/Connexion") {
    return Poster $client $chemin @{ email = $email; motDePasse = $motDePasse } "/Connexion"
}

function Verifier([string]$titre, [bool]$condition, [string]$detail = "") {
    $script:numero++
    $id = "T{0:00}" -f $script:numero
    if ($condition) {
        Write-Host ("[ OK ] {0}  {1}" -f $id, $titre) -ForegroundColor Green
    } else {
        $script:echecs++
        Write-Host ("[ECHEC] {0}  {1}  {2}" -f $id, $titre, $detail) -ForegroundColor Red
    }
}

$suffixe = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$lecteurEmail = "lecteur-$suffixe@securite.local"
$cibleEmail = "cible-$suffixe@securite.local"
$definitif = "Definitif-Test-456"

Write-Host "Application testée : $Url`n"

# --- Authentification ---------------------------------------------------------
$anonyme = Nouveau-Navigateur
$r = Lire $anonyme "/Etudiants"
Verifier "Sans connexion, une page protégée redirige vers /Connexion" ($r.Code -eq 302 -and $r.Location -match "/Connexion") "code $($r.Code)"

$r = Lire $anonyme "/sante"
Verifier "La page de supervision /sante reste publique et répond 200" ($r.Code -eq 200) "code $($r.Code)"

$r = Lire $anonyme "/Connexion"
$csp = if ($r.Entetes.Contains("Content-Security-Policy")) { ($r.Entetes.GetValues("Content-Security-Policy") -join " ") } else { "" }
$frame = if ($r.Entetes.Contains("X-Frame-Options")) { ($r.Entetes.GetValues("X-Frame-Options") -join " ") } else { "" }
Verifier "Les en-têtes de sécurité sont présents (CSP, X-Frame-Options)" ($csp -match "script-src 'self'" -and $frame -eq "DENY")

$r = Connecter (Nouveau-Navigateur) $AdminEmail "mauvais-Mot-de-passe-1"
Verifier "Un mauvais mot de passe est refusé avec un message générique" ($r.Code -eq 200 -and $r.Texte -match "E-mail ou mot de passe incorrect") "code $($r.Code)"

$admin = Nouveau-Navigateur
$r = Connecter $admin $AdminEmail $AdminMotDePasse
if ($r.Code -ne 302 -or $r.Location -match "MotDePasse") {
    Write-Host "`nArrêt : connexion administrateur impossible (code $($r.Code), redirection « $($r.Location) »)." -ForegroundColor Red
    Write-Host "Vérifiez le mot de passe, changez le mot de passe provisoire, ou attendez une minute si le code est 429." -ForegroundColor Red
    exit 1
}
$cookie = ($r.Entetes.GetValues("Set-Cookie") | Where-Object { $_ -match "universite.session" }) -join " "
Verifier "Le cookie de session est HttpOnly et SameSite=Lax" ($cookie -match "(?i)httponly" -and $cookie -match "(?i)samesite=lax") $cookie

# --- Comptes de test : créés par l'interface, comme le ferait un administrateur ---
$creation = Poster $admin "/Comptes/Create" @{ Email = $lecteurEmail; Nom = "Lecteur de test"; Role = "Consultation" } "/Comptes/Create"
$page = (Lire $admin "/Comptes").Texte
$provisoire = [regex]::Match($page, [regex]::Escape($lecteurEmail) + " : ([A-Za-z0-9]+)").Groups[1].Value
Verifier "L'administrateur crée un compte et reçoit un mot de passe provisoire" ($creation.Code -eq 302 -and $provisoire.Length -ge 10)
Poster $admin "/Comptes/Create" @{ Email = $cibleEmail; Nom = "Cible de verrouillage"; Role = "Consultation" } "/Comptes/Create" | Out-Null

$lecteur = Nouveau-Navigateur
$r = Connecter $lecteur $lecteurEmail $provisoire
$bloque = Lire $lecteur "/Etudiants"
Verifier "Mot de passe provisoire : rien n'est accessible avant de l'avoir changé" ($r.Location -match "MotDePasse" -and $bloque.Location -match "MotDePasse") "redirection « $($bloque.Location) »"

$r = Poster $lecteur "/Connexion/MotDePasse" @{ actuel = $provisoire; nouveau = "court"; confirmation = "court" } "/Connexion/MotDePasse"
Verifier "Un mot de passe trop faible est refusé" ($r.Code -eq 200 -and $r.Texte -match "au moins")

$r = Poster $lecteur "/Connexion/MotDePasse" @{ actuel = $provisoire; nouveau = $definitif; confirmation = $definitif } "/Connexion/MotDePasse"
$ok = Lire $lecteur "/Etudiants"
Verifier "Après le changement, la consultation est autorisée" ($r.Code -eq 302 -and $ok.Code -eq 200) "code $($ok.Code)"

$externe = Nouveau-Navigateur
$r = Connecter $externe $lecteurEmail $definitif "/Connexion?retour=https://malveillant.example"
Verifier "Une redirection vers un site externe après connexion est ignorée" ($r.Code -eq 302 -and $r.Location -eq "/") "redirection « $($r.Location) »"

# --- Autorisation -------------------------------------------------------------
$r = Lire $lecteur "/Comptes"
Verifier "Profil Consultation : l'administration renvoie 403 et une page lisible" ($r.Code -eq 403 -and $r.Texte -match "Accès non autorisé") "code $($r.Code)"

$creer = Lire $lecteur "/Etudiants/Create"
$exporter = Lire $lecteur "/Etudiants/Exporter"
Verifier "Profil Consultation : création et export CSV renvoient 403" ($creer.Code -eq 403 -and $exporter.Code -eq 403) "codes $($creer.Code), $($exporter.Code)"

$r = Poster $lecteur "/Etudiants/Delete/1" @{} "/Documents"
Verifier "Un POST forgé est refusé côté serveur, même avec un jeton valide" ($r.Code -eq 403) "code $($r.Code)"

# --- Gestion des erreurs ------------------------------------------------------
$r = Poster $admin "/Filieres/Create" @{ Code = "CSRF"; Nom = "Forge" } $null
Verifier "Un formulaire sans jeton anti-CSRF renvoie 400 et une page lisible" ($r.Code -eq 400 -and $r.Texte -match "Demande invalide") "code $($r.Code)"

$r = Lire $admin "/Etudiants/Details/99999999"
Verifier "Un élément inexistant renvoie 404 et une page lisible" ($r.Code -eq 404 -and $r.Texte -match "Cette page n'existe pas") "code $($r.Code)"

# --- Sessions, verrouillage et journalisation -----------------------------------
$comptes = (Lire $admin "/Comptes").Texte
$idLecteur = [regex]::Match($comptes, '/Comptes/Edit/(\d+)"[^>]*>[^<]*</a></td>\s*<td[^>]*>' + [regex]::Escape($lecteurEmail)).Groups[1].Value
$r = Poster $admin "/Comptes/Edit/$idLecteur" @{ Nom = "Lecteur de test"; Role = "Consultation"; Actif = "false" } "/Comptes/Edit/$idLecteur"
$coupe = Lire $lecteur "/Etudiants"
Verifier "Désactiver un compte coupe sa session immédiatement" ($r.Code -eq 302 -and $coupe.Code -eq 302 -and $coupe.Location -match "/Connexion") "code $($coupe.Code)"

$cible = Nouveau-Navigateur
1..5 | ForEach-Object { Connecter $cible $cibleEmail "mauvais-Mot-de-passe-1" | Out-Null }
$r = Connecter $cible $cibleEmail "mauvais-Mot-de-passe-1"
Verifier "Cinq échecs verrouillent le compte" ($r.Texte -match "verrouillé") "code $($r.Code)"

$journal = (Lire $admin ("/Journal?q=" + [Uri]::EscapeDataString($cibleEmail))).Texte
Verifier "Le journal d'audit trace les refus et le verrouillage" ($journal -match "Connexion refusée" -and $journal -match "verrouillé")
$tout = (Lire $admin "/Journal").Texte
Verifier "Le journal d'audit ne contient aucune empreinte de mot de passe" ($tout -notmatch "AQAAAA")

$limite = Nouveau-Navigateur
$code429 = $false
for ($i = 0; $i -lt 15 -and -not $code429; $i++) {
    $code429 = (Connecter $limite "inconnu@securite.local" "x").Code -eq 429
}
Verifier "Au-delà de la limite, les essais de connexion renvoient 429" $code429

Write-Host ""
if ($script:echecs -eq 0) {
    Write-Host "Résultat : $($script:numero) tests réussis sur $($script:numero)." -ForegroundColor Green
} else {
    Write-Host "Résultat : $($script:echecs) échec(s) sur $($script:numero) tests." -ForegroundColor Red
}
Write-Host "Vérifiez aussi dans la console de l'application les lignes « Connexion refusée », « Compte verrouillé » et « Accès refusé »."
exit $script:echecs
