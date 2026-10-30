#!/bin/sh
# Sauvegarde de la base, à lancer chaque nuit depuis le dossier de compose.yaml (cron) :
#   0 2 * * * cd /opt/universite && ./deploy/sauvegarde.sh >> sauvegardes/journal.log 2>&1
# Conserve 30 jours. Copiez aussi ce dossier hors du serveur : une sauvegarde locale ne protège pas d'une panne de disque.
set -eu

dossier="sauvegardes"
fichier="$dossier/universite-$(date +%Y%m%d-%H%M%S).sql.gz"
mkdir -p "$dossier"

docker compose exec -T base sh -c 'exec mariadb-dump --single-transaction --routines -uroot -p"$MARIADB_ROOT_PASSWORD" "$MARIADB_DATABASE"' \
    | gzip > "$fichier.tmp"
gzip -t "$fichier.tmp"
mv "$fichier.tmp" "$fichier"

find "$dossier" -name 'universite-*.sql.gz' -mtime +30 -delete
echo "$(date '+%F %T') sauvegarde : $fichier ($(du -h "$fichier" | cut -f1))"
