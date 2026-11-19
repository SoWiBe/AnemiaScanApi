#!/usr/bin/env bash
# One-time package setup on a fresh Ubuntu 24.04 x86-64 VPS.
# Run interactively with sudo; does not create database credentials or deploy API.
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive

[[ $EUID -eq 0 ]] || { echo 'Run with sudo bash.' >&2; exit 1; }
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 24.04 && "$(uname -m)" == x86_64 ]] || {
    echo 'Requires Ubuntu 24.04 x86-64.' >&2; exit 1;
}
# Do not replace an existing database installation or configuration.
if [[ -e /etc/mongod.conf ]] || command -v mongod >/dev/null; then
    echo 'Existing MongoDB detected; review manually before provisioning.' >&2
    exit 1
fi

apt-get update
apt-get install -y ca-certificates curl gnupg software-properties-common
add-apt-repository -y ppa:dotnet/backports

key_file=$(mktemp)
trap 'rm -f "$key_file"' EXIT
curl --fail --silent --show-error --location https://www.mongodb.org/static/pgp/server-8.0.asc -o "$key_file"
gpg --batch --yes --dearmor -o /usr/share/keyrings/mongodb-server-8.0.gpg "$key_file"
chmod 644 /usr/share/keyrings/mongodb-server-8.0.gpg
echo 'deb [arch=amd64 signed-by=/usr/share/keyrings/mongodb-server-8.0.gpg] https://repo.mongodb.org/apt/ubuntu noble/mongodb-org/8.0 multiverse' > /etc/apt/sources.list.d/mongodb-org-8.0.list

apt-get update
apt-get install -y aspnetcore-runtime-10.0 nginx libgomp1 mongodb-org jq certbot python3-certbot-nginx

systemctl stop mongod
cp -a /etc/mongod.conf /etc/mongod.conf.before-anemiascan
cat > /etc/mongod.conf <<'EOF'
storage:
  dbPath: /var/lib/mongodb
  wiredTiger:
    engineConfig:
      cacheSizeGB: 0.5
systemLog:
  destination: file
  logAppend: true
  path: /var/log/mongodb/mongod.log
net:
  port: 27017
  bindIp: 127.0.0.1
security:
  authorization: enabled
EOF
chmod 644 /etc/mongod.conf

id anemiascan >/dev/null 2>&1 || useradd --system --shell /usr/sbin/nologin anemiascan
install -d -o anemiascan -g anemiascan -m 750 /var/www/anemiascan-api
systemctl enable --now mongod nginx

echo '=== Installed runtimes ==='
dotnet --list-runtimes
echo '=== Services ==='
systemctl is-active mongod nginx
echo '=== MongoDB listener (must be loopback only) ==='
ss -ltnp 'sport = :27017'
echo '=== CPU instructions ==='
grep -m1 -oE '\b(avx|avx2|fma)\b' /proc/cpuinfo || true
echo 'Packages installed. Next: database users, deployment access, DNS/TLS and GitHub Actions.'
