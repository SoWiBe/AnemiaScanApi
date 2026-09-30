# Deployment

Guide for deploying AnemiaScanApi to an Ubuntu 24.04 LTS VPS via GitHub Actions.

## Architecture

```
GitHub push (master)
  → GitHub Actions build (Ubuntu runner)
    → dotnet publish -c Release -r linux-x64 (framework-dependent)
      → tar.gz artifact
        → scp to VPS /tmp
          → ssh: extract to /var/www/anemiascan-api, write .env + systemd unit, restart service
            → systemd runs Kestrel on 127.0.0.1:5000 as user `anemiascan`
              → nginx (443) proxies to Kestrel with Let's Encrypt TLS
```

MongoDB работает **на том же VPS**, слушая только loopback (вариант B, решение от 29.09.2026 — см. «MongoDB на том же VPS» ниже). Это отменяет прежний план с Atlas: бесплатный M0 упирался в 512 МБ и не даёт бэкапов, а выделенный тариф стоил дороже всей машины.

**Требования к VPS:** 4 vCPU x86-64 с AVX2, **6 ГБ RAM минимум** (8 ГБ комфортно), **80 ГБ NVMe**, Ubuntu 24.04 LTS.
На 6 ГБ обязателен swap — см. «MongoDB на том же VPS».
Выбрано: GoodHost NVMe-VPS-E (6 vCPU / 6 ГБ / 120 ГБ) — решение от 29.09.2026.
ARM (Ampere, Graviton) не подойдёт — в `.csproj` жёстко `PlatformTarget x64`.
Прежние «2 vCPU / 4 ГБ / 40 ГБ» из `MVP_PLAN.md` §3 считались под «на сервере только API»; с локальной базой этого не хватает.

**Юрисдикция:** если аудитория — граждане РК, база с персональными данными должна физически находиться в Казахстане. Это касается и дампов бэкапов.

## One-time VPS setup

Run everything below **on the VPS as a sudo-capable user** (via SSH from your machine).

### 1. Install runtime and nginx

```bash
sudo add-apt-repository ppa:dotnet/backports -y   # .NET 10 isn't in the default 24.04 feeds yet
sudo apt update
sudo apt install -y aspnetcore-runtime-10.0 nginx
dotnet --list-runtimes                            # confirm Microsoft.AspNetCore.App 10.0.x is present
```

### 2. Create the service user and directory

```bash
sudo useradd -r -s /usr/sbin/nologin anemiascan
sudo mkdir -p /var/www/anemiascan-api
sudo chown anemiascan:anemiascan /var/www/anemiascan-api
```

### 3. Configure passwordless sudo for the deploy user

The CI script runs `sudo` non-interactively. Without this, the SSH step will hang waiting for a password. Assume your CI SSH user is `deploy`:

```bash
sudo visudo -f /etc/sudoers.d/anemiascan-deploy
```

Paste (adjust `deploy` to your actual username):

```
deploy ALL=(root) NOPASSWD: /usr/bin/systemctl, /usr/bin/tee, /usr/bin/mkdir, /usr/bin/rm, /usr/bin/tar, /usr/sbin/useradd, /usr/bin/chown, /usr/bin/chmod, /usr/bin/id
```

For a personal server you can broaden to `NOPASSWD: ALL`. Save and exit.

### 4. Add the deploy SSH key

Generate a keypair locally (**do not reuse** a personal key):

```bash
ssh-keygen -t ed25519 -f ~/.ssh/anemiascan_deploy -C "github-actions"
```

On the VPS, add the public key to `deploy`'s `~/.ssh/authorized_keys`. The private key goes into GitHub Secrets (see below).

### 5. Nginx reverse proxy + TLS

Create `/etc/nginx/sites-available/anemiascan`:

```nginx
server {
    listen 80;
    server_name api.example.com;   # your domain

    client_max_body_size 15M;      # allow image uploads (validator caps at 10MB)

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        keep-alive;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 60s;
    }
}
```

Enable and issue a cert:

```bash
sudo ln -s /etc/nginx/sites-available/anemiascan /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d api.example.com
```

Certbot rewrites the file to serve on 443 with auto-renewal.

## GitHub configuration

### Secrets to add

Repository → **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `VPS_HOST` | Server IP or hostname |
| `VPS_USERNAME` | Deploy user (e.g. `deploy`) |
| `VPS_SSH_KEY` | Contents of `~/.ssh/anemiascan_deploy` (private key, full PEM including headers) |
| `JWT_SECRET` | 64+ random chars |
| `JWT_ISSUER` | e.g. `anemiascan` |
| `JWT_AUDIENCE` | e.g. `anemiascan-clients` |
| `JWT_ACCESS_MIN` | e.g. `60` |
| `JWT_REFRESH_DAYS` | e.g. `30` |
| `EMAIL_SENDER` | SMTP sender address |
| `EMAIL_PASSWORD` | SMTP app password |
| `SMTP_SERVER` | e.g. `smtp.gmail.com` |
| `SMTP_PORT` | e.g. `587` |
| `MONGODB_CONNECTION` | Локальная база: `mongodb://anemiascan:ПАРОЛЬ@127.0.0.1:27017/SmartAnemiaScan?authSource=SmartAnemiaScan` |
| `MONGODB_DB_NAME` | Имя базы, напр. `SmartAnemiaScan` |
| `CODE_GEN_LENGTH` | e.g. `6` |
| `CODE_GEN_CHARS` | e.g. `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` |
| `CODE_GEN_CACHE_KEY` | e.g. `verify_code` |
| `LEGAL_POLICY_VERSION` | Версия политики конфиденциальности, e.g. `1.0`. Пишется в согласие пользователя — поднимать при каждой правке текста политики. Пусто — берётся `1.0` |
| `LEGAL_POLICY_URL` | Ссылка на опубликованный текст политики, отдаётся клиенту в `GET /legal` |

### Workflow

`.github/workflows/deployapi.yml` already exists in the repo. Triggers:
- Push to `master`
- Manual dispatch from the Actions tab

## First deploy

```bash
git push origin master
```

Watch the Action run. On success, verify from your machine:

```bash
curl -i https://api.example.com/openapi/v1.json
```

If you want to test the pipeline without a real domain yet, `curl -i http://<VPS_IP>:5000/openapi/v1.json` **from the VPS itself** (Kestrel binds to loopback; not reachable externally until nginx is in front).

## Daily operations

All run on the VPS via SSH.

```bash
sudo systemctl status anemiascan-api               # current state
sudo systemctl restart anemiascan-api              # manual restart
sudo systemctl stop anemiascan-api                 # stop
sudo journalctl -u anemiascan-api -f               # live logs
sudo journalctl -u anemiascan-api -n 200 --no-pager
sudo systemctl cat anemiascan-api                  # effective unit config
sudo systemctl edit anemiascan-api                 # add overrides without touching the unit
```

App-level logs also written to `/var/www/anemiascan-api/logs/` (Serilog: `myapp.txt` daily, `errors.txt` daily).

To update env vars without a full redeploy:

```bash
sudo nano /var/www/anemiascan-api/.env
sudo systemctl restart anemiascan-api
```

Note: the next `git push` to master **will overwrite** `.env` from GitHub secrets. Keep secrets in sync there.

## MongoDB на том же VPS

Все файлы из этого раздела лежат в репозитории в `deploy/`.

### 1. Установка

```bash
curl -fsSL https://www.mongodb.org/static/pgp/server-8.0.asc \
  | sudo gpg -o /usr/share/keyrings/mongodb-server-8.0.gpg --dearmor
echo "deb [signed-by=/usr/share/keyrings/mongodb-server-8.0.gpg] https://repo.mongodb.org/apt/ubuntu noble/mongodb-org/8.0 multiverse" \
  | sudo tee /etc/apt/sources.list.d/mongodb-org-8.0.list
sudo apt update
sudo apt install -y mongodb-org
```

Сверь версию мажора со страницей MongoDB перед установкой — репозиторий привязан к конкретной ветке.

### 2. Отключить transparent huge pages

С включёнными THP WiredTiger даёт рваную производительность и завышенный RSS. Настройка не переживает перезагрузку, поэтому юнитом:

```bash
sudo install -m 0644 deploy/mongodb-thp.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mongodb-thp
cat /sys/kernel/mm/transparent_hugepage/enabled   # должно быть [never]
```

### 3. Конфиг

```bash
sudo cp deploy/mongod.conf /etc/mongod.conf
sudo systemctl restart mongod
```

Ключевое в нём — `cacheSizeGB: 1`. По умолчанию WiredTiger забирает 50% от (RAM − 1 ГБ), то есть на 8 ГБ машине ~3.5 ГБ, и дерётся за память с API, которому в systemd-юните отдано `MemoryMax=2500M` под TensorFlow. **Если берёшь машину с другим объёмом RAM — пересчитай это значение.**

Бюджет памяти на 6 ГБ:

| Компонент | Оценка при планировании | **Факт (замер 30.09.2026)** |
|---|---|---|
| API (TF + ONNX + ImageSharp) | ~2.5 ГБ | **624 МБ** |
| mongod (кэш 1 ГБ + оверхед) | ~1.5 ГБ | **103 МБ** |
| ОС, nginx, логи | ~0.5 ГБ | ~270 МБ |
| **Занято всего** | ~4.5 ГБ | **1.0 ГБ из 5.8** |
| **Swap использован** | — | **0** |

Замер сделан на GoodHost NVMe-VPS-E (Xeon Gold 6154) после серии реальных сканов
через `POST /analysis/anemia/prediction/`. **Оценка при планировании была завышена
втрое** — фактически хватило бы и 4 ГБ. `MemoryMax=2500M` оставлен как есть: он
ничего не стоит и ловит утечки, если появятся.

Почему 6 ГБ достаточно: `MemoryMax=2500M` у API — это cgroup-лимит, то есть ядро душит и убивает процесс **внутри его группы**. Всплеск TensorFlow не может утянуть за собой mongod: умрёт только API, и `Restart=always` поднимет его. Худший сценарий — рестарт приложения, а не падение всей машины.

### 3.1. Swap (обязательно на 6 ГБ)

Подушка на случай, когда свободной памяти не осталось: ядро вытеснит холодные страницы вместо того, чтобы звать OOM-killer. MongoDB прямо рекомендует держать swap доступным.

```bash
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# swappiness=1, а не 0: база не должна уезжать в swap при нормальной работе,
# но возможность вытеснения нужна сохранить.
echo 'vm.swappiness=1' | sudo tee /etc/sysctl.d/99-mongodb.conf
sudo sysctl -p /etc/sysctl.d/99-mongodb.conf

free -h    # проверить, что swap виден
```

Если в мониторинге swap начнёт активно использоваться (а не просто числиться) — это сигнал, что пора на тариф с 8 ГБ, а не что всё хорошо работает.

### 4. Пользователи базы

Конфиг включает `authorization: enabled`, так что пользователей надо завести сразу — иначе после рестарта ты сам в базу не попадёшь. Пока авторизация ещё не применилась (localhost exception), создай админа:

```bash
mongosh --eval '
  db.getSiblingDB("admin").createUser({
    user: "admin", pwd: passwordPrompt(),
    roles: [{role: "userAdminAnyDatabase", db: "admin"}, {role: "root", db: "admin"}]
  })'
```

Затем пользователя приложения — **только на свою базу**, без прав на остальные:

```bash
mongosh -u admin -p --authenticationDatabase admin --eval '
  db.getSiblingDB("SmartAnemiaScan").createUser({
    user: "anemiascan", pwd: passwordPrompt(),
    roles: [{role: "readWrite", db: "SmartAnemiaScan"}]
  })'
```

И отдельного для бэкапов, с правами только на чтение:

```bash
mongosh -u admin -p --authenticationDatabase admin --eval '
  db.getSiblingDB("SmartAnemiaScan").createUser({
    user: "backup", pwd: passwordPrompt(),
    roles: [{role: "read", db: "SmartAnemiaScan"}]
  })'
```

### 5. Строка подключения

Обнови секрет `MONGODB_CONNECTION` в GitHub на локальный адрес:

```
mongodb://anemiascan:ПАРОЛЬ@127.0.0.1:27017/SmartAnemiaScan?authSource=SmartAnemiaScan
```

Проверка после деплоя:

```bash
curl -s http://127.0.0.1:5000/health/ready | jq
```

`mongodb` в ответе должен стать `Healthy`.

### 6. Проверка, что порт закрыт

```bash
sudo ss -tlnp | grep 27017    # должен быть только 127.0.0.1:27017
```

Если видишь `0.0.0.0:27017` — `bindIp` не применился, база торчит в интернет.

## Бэкапы (P0 №14)

Раз база своя, бэкапы — твоя ответственность. Дамп на тот же диск бэкапом не является.

### Установка

```bash
sudo install -m 0750 deploy/mongodb-backup.sh /usr/local/bin/
sudo install -m 0750 deploy/mongodb-restore-check.sh /usr/local/bin/
sudo install -m 0644 deploy/anemiascan-backup.service /etc/systemd/system/
sudo install -m 0644 deploy/anemiascan-backup.timer /etc/systemd/system/

sudo cp deploy/anemiascan-backup.env.example /etc/anemiascan-backup.env
sudo chmod 600 /etc/anemiascan-backup.env
sudo nano /etc/anemiascan-backup.env          # заполнить MONGO_URI и REMOTE_TARGET

sudo systemctl daemon-reload
sudo systemctl enable --now anemiascan-backup.timer
```

### Что делает ночной прогон

1. `mongodump` в сжатый архив
2. проверяет архив через `mongorestore --dryRun` — битый или обрезанный дамп ловится в ту же ночь, а не когда понадобится
3. выгружает копию за пределы сервера
4. удаляет локальные архивы старше `RETENTION_DAYS`

Скрипт откажется работать без `REMOTE_TARGET`: бэкап, лежащий рядом с базой, не переживёт смерть машины.

**Про локализацию:** если сервер выбран в РК ради требований закона о персданных, приёмник бэкапов тоже должен быть в РК. S3/R2/Backblaze за пределами Казахстана не подходят. Разумные варианты — S3-совместимое хранилище казахстанского провайдера или второй дешёвый VPS **у другого хостера** (бэкап у того же провайдера не спасёт от проблем самого провайдера).

### Проверка восстановления

«Бэкапы включены» без единого успешного восстановления — это надежда, а не бэкап. Вторая половина №14:

```bash
sudo /usr/local/bin/mongodb-restore-check.sh
```

Скрипт разворачивает последний архив в отдельную базу `SmartAnemiaScan_restorecheck`, сверяет наличие и наполненность коллекций, удаляет временную базу за собой. Боевую не трогает. Расхождение в количестве документов ожидаемо — архив ночной, в боевую с тех пор дописали.

Прогнать **обязательно один раз перед релизом** и дальше раз в месяц.

### Контроль

```bash
systemctl list-timers anemiascan-backup.timer     # когда следующий запуск
journalctl -u anemiascan-backup.service -n 50     # как прошёл последний
ls -lh /var/backups/anemiascan/                   # что лежит локально
```

## Замеры на боевом сервере (30.09.2026)

Первый полный прогон на GoodHost NVMe-VPS-E, Алматы. Закрывает пункт «замер RSS и
времени ответа под нагрузкой» из Фазы 4 плана — сделан заранее.

**Железо:** Intel Xeon Gold 6154 @ 3.00 GHz, 6 vCPU, 5.8 ГБ RAM, 115 ГБ NVMe,
Ubuntu 24.04.5 LTS, KVM. AVX2 и AVX-512 присутствуют.

> **Важно при выборе ноды:** изначально провайдер выдал generic-модель CPU
> (`QEMU Virtual CPU version 2.5+`) вообще без AVX. Официальные сборки TensorFlow
> требуют AVX и падают с `Illegal instruction` на таком процессоре. Решается
> заявкой в поддержку с просьбой включить **host-passthrough**. Проверять сразу
> после получения доступа: `grep -o avx2 /proc/cpuinfo`.

**Время ответа `POST /analysis/anemia/prediction/`:**

| Запрос | Время |
|---|---|
| Первый после рестарта (загрузка графа TensorFlow) | 2.75 с |
| Прогретые, 5 подряд | 83–113 мс |

Обе модели отрабатывают: TF-классификатор даёт вердикт с confidence, CIELab-регрессия
возвращает Hb и severity.

**Downscale перед GridFS (P0 №9)** подтверждён в бою: тестовый PNG 24 346 → 5 946 байт.

**Rate-limit (P0 №6)** подтверждён: 10 сканов проходят, 11-й отдаёт `429`.

## Health checks

Две пробы, обе без авторизации:

| Эндпоинт | Смысл | Когда 200 |
|---|---|---|
| `GET /health` | liveness — процесс отвечает | всегда, пока приложение живо |
| `GET /health/ready` | readiness — Mongo пингуется, файлы моделей на месте | когда обе проверки прошли, иначе `503` |

```bash
curl -s http://127.0.0.1:5000/health
curl -s http://127.0.0.1:5000/health/ready | jq
```

`/health` сознательно не проверяет Mongo: упавшая база не должна провоцировать
бесконечный рестарт сервиса. Для «жив ли процесс» — `/health`, для
«можно ли пускать трафик» — `/health/ready`.

После деплоя проверить фактическую память под нагрузкой (P0 №4 — лимит подняли
с 700M, но реальный RSS ещё не замерен):

```bash
systemctl show anemiascan-api -p MemoryCurrent
```

## Настройки без передеплоя

Живут в `.env`, меняются рестартом сервиса. Помнить: следующий push в master
перезапишет `.env` из GitHub secrets.

| Переменная | По умолчанию | Смысл |
|---|---|---|
| `RateLimiting__PredictionPermitLimit` | `10` | Сканов на пользователя за окно |
| `RateLimiting__PredictionWindowMinutes` | `1` | Длина окна для сканов |
| `RateLimiting__EmailCodePermitLimit` | `5` | Писем с кодом на IP за окно |
| `RateLimiting__EmailCodeWindowMinutes` | `10` | Длина окна для писем |
| `ImageStorage__MaxDimension` | `1024` | Максимальная сторона снимка в GridFS, px |
| `ImageStorage__JpegQuality` | `85` | Качество JPEG при сохранении |
| `Legal__PolicyVersion` | `1.0` | Версия политики, пишется в согласие пользователя |
| `Legal__PolicyUrl` | — | Ссылка на текст политики |
| `Legal__DisclaimerText` | встроенный текст | Переопределение медицинского дисклеймера |
| `ApiDocs__Enabled` | `false` | Открыть OpenAPI/Scalar в проде (по умолчанию закрыты) |

## Manual deployment (skip CI)

For debugging or bootstrapping without CI:

```bash
# On your local machine
dotnet publish AnemiaScanApi/AnemiaScanApi.csproj -c Release -r linux-x64 --self-contained false -o ./publish
tar -czf anemiascan-publish.tar.gz -C ./publish .
scp anemiascan-publish.tar.gz deploy@<VPS_IP>:/tmp/

# On the VPS
sudo systemctl stop anemiascan-api
sudo rm -rf /var/www/anemiascan-api/*
sudo tar -xzf /tmp/anemiascan-publish.tar.gz -C /var/www/anemiascan-api
sudo chown -R anemiascan:anemiascan /var/www/anemiascan-api
sudo systemctl start anemiascan-api
```

The `.env` and systemd unit persist across manual deploys; only the app binaries change.

## First-time systemd unit + env file (if not using CI to provision)

The workflow creates these automatically on every deploy. If you want to hand-craft them once (e.g. to test before wiring CI), on the VPS:

```bash
# Env file
sudo tee /var/www/anemiascan-api/.env >/dev/null <<'EOF'
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
MongoDB__ConnectionString=mongodb://anemiascan:ПАРОЛЬ@127.0.0.1:27017/SmartAnemiaScan?authSource=SmartAnemiaScan
MongoDB__DatabaseName=SmartAnemiaScan
JwtSettings__Secret=<64+ random chars>
JwtSettings__Issuer=anemiascan
JwtSettings__Audience=anemiascan-clients
JwtSettings__AccessTokenExpirationMinutes=60
JwtSettings__RefreshTokenExpirationDays=30
EmailSender__Email=...
EmailSender__Password=...
Smtp__Server=smtp.gmail.com
Smtp__Port=587
CodeGenerator__Length=6
CodeGenerator__Chars=ABCDEFGHJKLMNPQRSTUVWXYZ23456789
CodeGenerator__CacheKey=verify_code
Legal__PolicyVersion=1.0
Legal__PolicyUrl=https://example.com/privacy
EOF
sudo chown anemiascan:anemiascan /var/www/anemiascan-api/.env
sudo chmod 600 /var/www/anemiascan-api/.env

# systemd unit
sudo tee /etc/systemd/system/anemiascan-api.service >/dev/null <<'EOF'
[Unit]
Description=Anemia Scan ML API
After=network.target

[Service]
WorkingDirectory=/var/www/anemiascan-api
ExecStart=/usr/bin/dotnet /var/www/anemiascan-api/AnemiaScanApi.dll
Restart=always
RestartSec=10
EnvironmentFile=/var/www/anemiascan-api/.env
User=anemiascan
# Потолок памяти: TensorFlow-рантайм держит граф модели в RAM, рабочий RSS ~1-1.5 ГБ.
# Замерить фактический после деплоя: systemctl show anemiascan-api -p MemoryCurrent
MemoryHigh=2000M
MemoryMax=2500M
MemoryAccounting=true

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable anemiascan-api
sudo systemctl start anemiascan-api
```

Do **not** put comments (`#`) on the same line as `EnvironmentFile=` in the unit — systemd treats them as part of the path.

## Troubleshooting

**Service won't start — `Unit ... failed to load`.**
`sudo systemctl status anemiascan-api` and `sudo journalctl -u anemiascan-api -n 100`. Usually a typo in the unit file or missing `EnvironmentFile`.

**`dotnet: command not found` in journal.**
`/usr/bin/dotnet` not installed. Re-run step 1.

**`Unable to load shared library 'tensorflow'` or similar on prediction.**
`SciSharp.TensorFlow.Redist` should bundle the Linux native lib. Check `/var/www/anemiascan-api/runtimes/linux-x64/native/` exists after deploy; if not, the publish RID mismatch is the cause — the workflow uses `-r linux-x64` which is correct.

**`FileNotFoundException: ...anemia_v10_more_aug.zip` on first prediction.**
Model wasn't copied into publish output. Check `AnemiaScanApi.csproj` contains the `<Content Include="LLM\anemia_v10_more_aug.zip">` block, and that `LLMExtensions.cs` reads from `"LLM"` (not `"../LLM"`).

**502 Bad Gateway from nginx.**
Kestrel isn't listening. `sudo ss -tlnp | grep 5000` — if empty, the service died. Check `journalctl`.

**CORS errors from the frontend.**
`Program.cs` only defines `DevCorsPolicy` for `http://localhost:8081`. Add a production policy with your real frontend origin and reference it in `app.UseCors(...)`.

**GitHub Action hangs at "Extract and Restart Service".**
Deploy user is being prompted for a sudo password. Re-do step 3 (`/etc/sudoers.d/anemiascan-deploy`).

**Deploy succeeds but `curl` returns connection refused.**
Kestrel binds to `127.0.0.1:5000` — only reachable via nginx from the internet. Verify nginx is running (`sudo systemctl status nginx`) and the site is enabled.

## Rollback

There's no built-in rollback. The safe pattern:

```bash
# Before deploying, on the VPS:
sudo cp -a /var/www/anemiascan-api /var/www/anemiascan-api.prev

# If new deploy is bad:
sudo systemctl stop anemiascan-api
sudo rm -rf /var/www/anemiascan-api
sudo mv /var/www/anemiascan-api.prev /var/www/anemiascan-api
sudo systemctl start anemiascan-api
```

For a proper zero-downtime setup, use symlinked releases (`/var/www/anemiascan-api/current` → `releases/<timestamp>/`) — worth adding once you have real traffic.
