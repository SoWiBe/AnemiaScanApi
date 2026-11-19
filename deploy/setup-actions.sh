#!/usr/bin/env bash
# Prepare release directories and narrowly scoped service control for CI.
set -euo pipefail
[[ $EUID -eq 0 ]] || exit 1
deploy_user=${1:?Pass the SSH deployment username}
[[ "$deploy_user" =~ ^[a-z_][a-z0-9_-]*$ ]] || exit 1
id "$deploy_user" >/dev/null
test -f /etc/anemiascan/database-setup.complete
install -d -m 755 -o anemiascan -g anemiascan /var/lib/anemiascan /var/lib/anemiascan/releases
if [[ ! -L /var/www/anemiascan-api ]]; then
    # Refuse to move or delete an existing deployment.
    rmdir /var/www/anemiascan-api
    ln -s /var/lib/anemiascan/current /var/www/anemiascan-api
fi
sudoers_file=$(mktemp)
trap 'rm -f "$sudoers_file"' EXIT
cat > "$sudoers_file" <<EOF
$deploy_user ALL=(anemiascan) NOPASSWD: /usr/bin/bash
$deploy_user ALL=(root) NOPASSWD: /usr/bin/systemctl restart anemiascan-api
EOF
visudo -cf "$sudoers_file"
install -m 440 -o root -g root "$sudoers_file" /etc/sudoers.d/anemiascan-actions
echo 'Release directories and CI service permissions configured.'
