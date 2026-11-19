#!/usr/bin/env bash
# Fresh-server database setup. Run interactively with sudo.
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Run with sudo bash.' >&2; exit 1; }
command -v mongosh >/dev/null
command -v openssl >/dev/null
systemctl is-active --quiet mongod
grep -q 'authorization: enabled' /etc/mongod.conf

install -d -m 700 -o root -g root /etc/anemiascan
credentials=/etc/anemiascan/database-secrets.env
if [[ -e "$credentials" ]]; then
    echo 'Saved credentials already exist; refusing to replace them. Review provisioning state.' >&2
    exit 1
fi
umask 077
admin_password=$(openssl rand -hex 32)
app_password=$(openssl rand -hex 32)
jwt_secret=$(openssl rand -hex 48)
printf 'MONGO_ADMIN_PASSWORD=%s\nMONGO_APP_PASSWORD=%s\n' "$admin_password" "$app_password" > "$credentials"
export MONGO_ADMIN_PASSWORD="$admin_password" MONGO_APP_PASSWORD="$app_password"

# Passwords are passed through the environment, never command arguments or logs.
mongosh --quiet --norc --file /dev/stdin <<'JS'
const admin = db.getSiblingDB('admin');
admin.createUser({user: 'anemiascan_admin', pwd: process.env.MONGO_ADMIN_PASSWORD,
    roles: [{role: 'root', db: 'admin'}]});
if (!admin.auth('anemiascan_admin', process.env.MONGO_ADMIN_PASSWORD)) {
    throw new Error('Administrator authentication failed');
}
db.getSiblingDB('SmartAnemiaScan').createUser({user: 'anemiascan',
    pwd: process.env.MONGO_APP_PASSWORD,
    roles: [{role: 'readWrite', db: 'SmartAnemiaScan'}]});
JS

# Authenticate as the application and verify permission to write and clean up.
mongosh --quiet --norc --file /dev/stdin <<'JS'
const app = db.getSiblingDB('SmartAnemiaScan');
if (!app.auth('anemiascan', process.env.MONGO_APP_PASSWORD)) {
    throw new Error('Application authentication failed');
}
app.getCollection('_provisioning_check').insertOne({_id: 'setup'});
app.getCollection('_provisioning_check').drop();
JS

if [[ -e /etc/anemiascan/api.env ]]; then
    echo 'api.env already exists; refusing to overwrite it.' >&2
    exit 1
fi
cat > /etc/anemiascan/api.env <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
MongoDB__DatabaseName=SmartAnemiaScan
MongoDB__ConnectionString=mongodb://anemiascan:${app_password}@127.0.0.1:27017/SmartAnemiaScan?authSource=SmartAnemiaScan
JwtSettings__Secret=${jwt_secret}
JwtSettings__Issuer=anemiascan
JwtSettings__Audience=anemiascan-clients
JwtSettings__AccessTokenExpirationMinutes=60
JwtSettings__RefreshTokenExpirationDays=30
CodeGenerator__Length=6
CodeGenerator__Chars=ABCDEFGHJKLMNPQRSTUVWXYZ23456789
EOF
unset MONGO_ADMIN_PASSWORD MONGO_APP_PASSWORD admin_password app_password jwt_secret
touch /etc/anemiascan/database-setup.complete
echo 'Database users created; application read/write check passed.'
echo 'Credentials: /etc/anemiascan/database-secrets.env (root only).'
echo 'Application configuration: /etc/anemiascan/api.env (root only).'
echo 'Next: SMTP/legal settings, systemd service and regional GitHub Actions deployment.'
