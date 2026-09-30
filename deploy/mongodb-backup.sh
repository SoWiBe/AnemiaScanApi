#!/usr/bin/env bash
#
# Ночной бэкап MongoDB (P0 №14 в docs/plans/MVP_PLAN.md).
#
# Что делает:
#   1. mongodump в сжатый архив
#   2. проверяет, что архив читается (mongorestore --dryRun) — дамп, который
#      не разворачивается, это не бэкап, а файл
#   3. отправляет копию за пределы сервера
#   4. чистит старые локальные архивы
#
# ВАЖНО про локализацию: если сервер выбран в Казахстане ради требований
# закона о персональных данных, то дампы тоже обязаны остаться в РК.
# S3/R2/Backblaze за пределами РК здесь не подходят — см. REMOTE_TARGET ниже.
#
# Установка:
#   sudo install -m 0750 -o root -g root mongodb-backup.sh /usr/local/bin/
#   sudo install -m 0600 /dev/null /etc/anemiascan-backup.env   # заполнить руками
#
set -euo pipefail

CONFIG_FILE="${BACKUP_CONFIG:-/etc/anemiascan-backup.env}"
# shellcheck source=/dev/null
[ -r "$CONFIG_FILE" ] && source "$CONFIG_FILE"

: "${MONGO_URI:?MONGO_URI не задан (см. $CONFIG_FILE)}"
: "${BACKUP_DIR:=/var/backups/anemiascan}"
: "${RETENTION_DAYS:=14}"
# Куда уезжает копия. Поддерживаются две формы:
#   rsync://user@host:/path   — второй сервер (желательно у другого провайдера)
#   s3://bucket/prefix        — S3-совместимое хранилище, настроенное в rclone
#                               под именем remote (см. RCLONE_REMOTE)
: "${REMOTE_TARGET:?REMOTE_TARGET не задан — бэкап на том же диске не является бэкапом}"
: "${RCLONE_REMOTE:=kzstorage}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
ARCHIVE="${BACKUP_DIR}/anemiascan-${STAMP}.archive.gz"

log() { printf '%s [backup] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
fail() { log "ОШИБКА: $*"; exit 1; }

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

log "дамп в $ARCHIVE"
mongodump --uri="$MONGO_URI" --archive="$ARCHIVE" --gzip --quiet \
    || fail "mongodump упал"

SIZE_BYTES="$(stat -c %s "$ARCHIVE")"
[ "$SIZE_BYTES" -gt 1024 ] || fail "архив подозрительно мал (${SIZE_BYTES} байт)"
log "готово, размер $(numfmt --to=iec "$SIZE_BYTES")"

# Проверка читаемости. Это не полноценный restore-тест (его делает
# mongodb-restore-check.sh раз в месяц), но ловит битый или обрезанный архив
# в ту же ночь, а не через полгода, когда он понадобится.
log "проверка архива"
mongorestore --uri="$MONGO_URI" --archive="$ARCHIVE" --gzip --dryRun --quiet \
    || fail "архив не читается mongorestore"

log "выгрузка за пределы сервера: $REMOTE_TARGET"
case "$REMOTE_TARGET" in
    rsync://*)
        DEST="${REMOTE_TARGET#rsync://}"
        rsync -a --chmod=600 "$ARCHIVE" "$DEST" || fail "rsync не отработал"
        ;;
    s3://*)
        rclone copy "$ARCHIVE" "${RCLONE_REMOTE}:${REMOTE_TARGET#s3://}" \
            || fail "rclone не отработал"
        ;;
    *)
        fail "непонятный REMOTE_TARGET: $REMOTE_TARGET"
        ;;
esac

log "чистка архивов старше ${RETENTION_DAYS} дней"
find "$BACKUP_DIR" -name 'anemiascan-*.archive.gz' -mtime "+${RETENTION_DAYS}" -delete

log "бэкап завершён"
