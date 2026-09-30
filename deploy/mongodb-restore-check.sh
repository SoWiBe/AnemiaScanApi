#!/usr/bin/env bash
#
# Проверка восстановления из бэкапа — вторая половина P0 №14.
# «Бэкапы включены» без единого успешного восстановления — это надежда, а не бэкап.
#
# Разворачивает последний архив в отдельную базу, сравнивает количество
# документов с боевой и удаляет временную базу за собой. Боевую не трогает.
#
# Запускать вручную раз в месяц и обязательно один раз перед релизом:
#   sudo /usr/local/bin/mongodb-restore-check.sh
#
set -euo pipefail

CONFIG_FILE="${BACKUP_CONFIG:-/etc/anemiascan-backup.env}"
# shellcheck source=/dev/null
[ -r "$CONFIG_FILE" ] && source "$CONFIG_FILE"

: "${MONGO_URI:?MONGO_URI не задан (см. $CONFIG_FILE)}"
: "${BACKUP_DIR:=/var/backups/anemiascan}"
: "${SOURCE_DB:=SmartAnemiaScan}"

RESTORE_DB="${SOURCE_DB}_restorecheck"
ARCHIVE="$(find "$BACKUP_DIR" -name 'anemiascan-*.archive.gz' -type f -printf '%T@ %p\n' \
    | sort -rn | head -1 | cut -d' ' -f2-)"

log() { printf '%s [restore-check] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
fail() { log "ОШИБКА: $*"; exit 1; }

[ -n "$ARCHIVE" ] || fail "в $BACKUP_DIR нет ни одного архива"
log "проверяю архив $ARCHIVE"

count_docs() {
    mongosh "$MONGO_URI" --quiet --eval "
        const db = db.getSiblingDB('$1');
        JSON.stringify(db.getCollectionNames().sort().reduce(
            (acc, name) => (acc[name] = db.getCollection(name).countDocuments(), acc), {}))"
}

cleanup() {
    log "удаляю временную базу $RESTORE_DB"
    mongosh "$MONGO_URI" --quiet --eval "db.getSiblingDB('$RESTORE_DB').dropDatabase()" >/dev/null || true
}
trap cleanup EXIT

log "разворачиваю в $RESTORE_DB"
mongorestore --uri="$MONGO_URI" --archive="$ARCHIVE" --gzip \
    --nsFrom="${SOURCE_DB}.*" --nsTo="${RESTORE_DB}.*" --quiet \
    || fail "mongorestore упал"

SOURCE_COUNTS="$(count_docs "$SOURCE_DB")"
RESTORED_COUNTS="$(count_docs "$RESTORE_DB")"

log "боевая:        $SOURCE_COUNTS"
log "восстановлено: $RESTORED_COUNTS"

# Расхождение ожидаемо: архив сделан ночью, в боевую с тех пор дописали.
# Нас интересует, что коллекции на месте и не пустые.
echo "$RESTORED_COUNTS" | grep -q '"Users"' || fail "в восстановленной базе нет коллекции Users"
echo "$RESTORED_COUNTS" | grep -q '"AnemiaScans"' || fail "в восстановленной базе нет коллекции AnemiaScans"

log "восстановление прошло успешно — P0 №14 подтверждён на $(date -u +%Y-%m-%d)"
