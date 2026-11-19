# New server deployment

The single workflow `deployapi.yml` uses the GitHub environment `Master`.
It is manually triggered and restricted to the `master` branch during migration.
It replaces the former deployment that overwrote configuration from GitHub secrets.
Do not change DNS until the new API and HTTPS have been checked.

## Server preparation

Run the uploaded script interactively:

```bash
sudo bash ~/setup-actions.sh root1
```

This requires completed database provisioning and an empty application directory.
It creates release directories and grants the SSH user permission to run commands
as the application user and restart only `anemiascan-api` as root. It does not
grant general root sudo access. Keep `/etc/anemiascan/api.env` root-only; systemd
reads it before dropping privileges. Complete SMTP and approved legal settings
there before deployment. Never paste its contents into logs or issues.

## GitHub environment

Use **Settings → Environments → Master**. Set these environment secrets:

| Name | Value |
|---|---|
| `VPS_HOST` | New server address |
| `VPS_USERNAME` | SSH deployment username |
| `VPS_SSH_KEY` | Dedicated CI private key, without a passphrase |
| `VPS_KNOWN_HOSTS` | Verified OpenSSH known_hosts entry for the server |
| `SIXLABORS_LICENSE_KEY` | Valid build license key |

Optional environment variable: `VPS_PORT` (defaults to `22`). Add only the CI
public key to the deployment user's `authorized_keys`. Do not reuse the personal
encrypted key. Verify the host fingerprint through the existing trusted session
before storing its known_hosts entry; the workflow never trusts ssh-keyscan blindly.

## Release

After committing and pushing the reviewed changes, run **Actions → Deploy
AnemiaScanApi → Run workflow**, selecting `master`. Tests run in Debug, then publishing
uses Release and the license key. Mongo-backed tests still require their separate
test database configuration; production credentials are not supplied to tests.

If direct GitHub-to-VPS transfer is slow, select `build_only` when dispatching.
The job saves `api.tar.gz` and its SHA-256 checksum as a seven-day Actions artifact,
without connecting to the server or deploying. Download that artifact through a
trusted workstation and relay the unchanged archive over SSH. Verify its checksum
on the server before extraction. A successful build-only job is not a deployment.
Normal runs also save the artifact before transfer; upload has a ten-minute limit.

Artifacts are extracted as the application user into separate release directories.
The workflow switches the current symlink, restarts the service and checks
`/health/ready`. If readiness times out, it restores the prior symlink when one
exists. First deployment has no previous release to restore. Old releases remain
on disk; monitor space and retain needed releases deliberately.

Readiness checks do not exercise actual inference or SMTP. Verify these separately
before switching DNS. The current workflow does not roll back database changes
or course imports, and an SSH interruption can require manual recovery.
