#!/usr/bin/env bash
# Configure systemd and HTTP proxy before deploying application binaries.
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Run with sudo bash.' >&2; exit 1; }
domain=${1:?Pass the API hostname without https://}
[[ "$domain" =~ ^[a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?$ ]] || exit 1
test -f /etc/anemiascan/database-setup.complete
test -s /etc/anemiascan/api.env
test -x /usr/bin/dotnet
id anemiascan >/dev/null

cat > /etc/systemd/system/anemiascan-api.service <<'EOF'
[Unit]
Description=AnemiaScan API
After=network.target mongod.service
Wants=mongod.service

[Service]
User=anemiascan
Group=anemiascan
WorkingDirectory=/var/www/anemiascan-api
ExecStart=/usr/bin/dotnet /var/www/anemiascan-api/AnemiaScanApi.dll
EnvironmentFile=/etc/anemiascan/api.env
Restart=always
RestartSec=10
MemoryAccounting=true
MemoryHigh=2000M
MemoryMax=2500M

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable anemiascan-api

install -d -m 755 /var/www/letsencrypt
nginx_config=/etc/nginx/sites-available/anemiascan
if [[ -e "$nginx_config" ]]; then
    echo 'Existing nginx site detected; refusing to replace it.' >&2
    exit 1
fi
cat > "$nginx_config" <<'EOF'
server {
    listen 80;
    server_name __API_DOMAIN__;
    client_max_body_size 12m;

    location /.well-known/acme-challenge/ {
        root /var/www/letsencrypt;
    }

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }
}
EOF
sed -i "s/__API_DOMAIN__/$domain/" "$nginx_config"
ln -s "$nginx_config" /etc/nginx/sites-enabled/anemiascan
nginx -t
systemctl reload nginx
echo 'Database provisioning marker verified; systemd and HTTP proxy configured.'
echo 'API is not started: deploy binaries and complete SMTP/legal settings first.'
echo 'HTTPS certificate and GitHub Actions deployment still need configuration.'
